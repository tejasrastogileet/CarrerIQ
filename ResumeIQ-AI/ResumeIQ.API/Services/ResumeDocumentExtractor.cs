using System.IO.Compression;
using System.Text;
using Microsoft.VisualBasic.FileIO;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using NPOI.HWPF;
using Microsoft.AspNetCore.Http;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Exceptions;

namespace ResumeIQ.API.Services;

public interface IResumeDocumentExtractor
{
    Task<string> ExtractTextAsync(
        string fileName,
        Stream fileStream,
        CancellationToken cancellationToken = default);
}

public sealed class ResumeDocumentException(
    string message,
    int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed class ResumeDocumentExtractor : IResumeDocumentExtractor
{
    private const int MaxZipEntries = 2_000;
    private const long MaxUncompressedDocxBytes = 50 * 1024 * 1024;
    private static readonly byte[] LegacyWordSignature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    public async Task<string> ExtractTextAsync(
        string fileName,
        Stream fileStream,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!fileStream.CanSeek)
        {
            throw new InvalidOperationException("The uploaded file stream must support seeking.");
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var prefix = await ReadPrefixAsync(fileStream, 1024, cancellationToken);
        fileStream.Position = 0;

        var text = extension switch
        {
            ".pdf" => ExtractPdf(prefix, fileStream, cancellationToken),
            ".docx" => ExtractDocx(prefix, fileStream),
            ".doc" => ExtractDoc(prefix, fileStream),
            ".csv" => await ExtractCsvAsync(prefix, fileStream, cancellationToken),
            _ => throw new ResumeDocumentException(
                "Unsupported file format. Upload a PDF, DOCX, DOC, or CSV file.",
                StatusCodes.Status415UnsupportedMediaType)
        };

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ResumeDocumentException(
                "No readable text was found in the document.",
                StatusCodes.Status422UnprocessableEntity);
        }

        return text.Trim();
    }

    private static string ExtractPdf(
        byte[] prefix,
        Stream stream,
        CancellationToken cancellationToken)
    {
        if (!ContainsSignature(prefix, "%PDF-"u8))
        {
            throw InvalidContent("The file does not contain a valid PDF signature.");
        }

        try
        {
            using var document = PdfDocument.Open(stream);
            var pages = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                pages.AppendLine(page.Text);

                if (pages.Length > 5 * 1024 * 1024)
                {
                    throw InvalidDocument("The PDF contains too much text to analyze safely.");
                }
            }

            return pages.ToString();
        }
        catch (ResumeDocumentException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or
            InvalidOperationException or
            ArgumentException or
            PdfDocumentFormatException or
            PdfDocumentEncryptedException)
        {
            throw InvalidDocument("The PDF could not be read. It may be damaged or password-protected.");
        }
    }

    private static string ExtractDocx(byte[] prefix, Stream stream)
    {
        if (!HasZipSignature(prefix))
        {
            throw InvalidContent("The file does not contain a valid DOCX document.");
        }

        try
        {
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
            {
                if (archive.Entries.Count > MaxZipEntries ||
                    archive.GetEntry("[Content_Types].xml") is null ||
                    archive.GetEntry("word/document.xml") is null)
                {
                    throw InvalidDocument("The DOCX document is malformed or exceeds safe extraction limits.");
                }

                long uncompressedBytes = 0;
                foreach (var entry in archive.Entries)
                {
                    if (entry.Length > MaxUncompressedDocxBytes - uncompressedBytes)
                    {
                        throw InvalidDocument("The DOCX document is malformed or exceeds safe extraction limits.");
                    }

                    uncompressedBytes += entry.Length;
                }
            }

            stream.Position = 0;
            using var document = WordprocessingDocument.Open(stream, false);
            var body = document.MainDocumentPart?.Document?.Body;

            if (body is null)
            {
                throw InvalidDocument("The DOCX document does not contain readable body text.");
            }

            return string.Join(
                Environment.NewLine,
                body.Descendants<Paragraph>()
                    .Select(paragraph => string.Concat(
                        paragraph.Descendants<Text>().Select(text => text.Text)))
                    .Where(paragraphText => !string.IsNullOrWhiteSpace(paragraphText)));
        }
        catch (ResumeDocumentException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or
            InvalidDataException or
            ArgumentException or
            InvalidOperationException)
        {
            throw InvalidDocument("The DOCX document could not be read. It may be damaged or unsupported.");
        }
    }

    private static string ExtractDoc(byte[] prefix, Stream stream)
    {
        if (!prefix.AsSpan().StartsWith(LegacyWordSignature))
        {
            throw InvalidContent("The file does not contain a valid legacy Word DOC document.");
        }

        try
        {
            var document = new HWPFDocument(stream);
            return document.GetRange().Text;
        }
        catch (Exception exception) when (
            exception is IOException or
            InvalidDataException or
            ArgumentException or
            InvalidOperationException)
        {
            throw InvalidDocument("The DOC document could not be read. It may be damaged or unsupported.");
        }
    }

    private static async Task<string> ExtractCsvAsync(
        byte[] prefix,
        Stream stream,
        CancellationToken cancellationToken)
    {
        if (prefix.Length == 0 || prefix.Contains((byte)0))
        {
            throw InvalidContent("The CSV file does not contain readable text.");
        }

        try
        {
            using var reader = new StreamReader(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 1024,
                leaveOpen: true);
            var csvText = await reader.ReadToEndAsync(cancellationToken);

            if (csvText.Any(character =>
                    char.IsControl(character) &&
                    character is not '\r' and not '\n' and not '\t'))
            {
                throw InvalidContent("The CSV file contains binary or unsupported text content.");
            }

            using var parser = new TextFieldParser(new StringReader(csvText))
            {
                TextFieldType = FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = false
            };
            parser.SetDelimiters(",");

            var rows = new List<string>();
            var rowNumber = 0;

            while (!parser.EndOfData)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fields = parser.ReadFields() ?? [];
                rowNumber++;

                if (fields.Length == 0 || fields.All(string.IsNullOrWhiteSpace))
                {
                    continue;
                }

                rows.Add($"Row {rowNumber}: {string.Join(" | ", fields.Select((field, index) => $"Column {index + 1}: {field}"))}");
            }

            if (rows.Count == 0)
            {
                throw new ResumeDocumentException(
                    "The CSV file is empty or contains no readable rows.",
                    StatusCodes.Status422UnprocessableEntity);
            }

            return string.Join(Environment.NewLine, rows);
        }
        catch (ResumeDocumentException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is MalformedLineException or
            DecoderFallbackException or
            IOException or
            ArgumentException)
        {
            throw InvalidDocument("The CSV file is malformed or is not valid UTF-8 text.");
        }
    }

    private static async Task<byte[]> ReadPrefixAsync(
        Stream stream,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        var prefix = new byte[maxBytes];
        var totalRead = 0;

        while (totalRead < prefix.Length)
        {
            var bytesRead = await stream.ReadAsync(
                prefix.AsMemory(totalRead),
                cancellationToken);

            if (bytesRead == 0)
            {
                break;
            }

            totalRead += bytesRead;
        }

        return prefix[..totalRead];
    }

    private static bool ContainsSignature(ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> signature)
    {
        return prefix.IndexOf(signature) >= 0;
    }

    private static bool HasZipSignature(ReadOnlySpan<byte> prefix)
    {
        return prefix.Length >= 4 &&
               prefix[0] == 0x50 &&
               prefix[1] == 0x4B &&
               prefix[2] is 0x03 or 0x05 or 0x07 &&
               prefix[3] is 0x04 or 0x06 or 0x08;
    }

    private static ResumeDocumentException InvalidContent(string message)
    {
        return new ResumeDocumentException(message, StatusCodes.Status400BadRequest);
    }

    private static ResumeDocumentException InvalidDocument(string message)
    {
        return new ResumeDocumentException(message, StatusCodes.Status422UnprocessableEntity);
    }
}
