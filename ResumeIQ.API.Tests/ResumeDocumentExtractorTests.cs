using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using ResumeIQ.API.Controllers;
using ResumeIQ.API.Models;
using ResumeIQ.API.Services;

namespace ResumeIQ.API.Tests;

public class ResumeDocumentExtractorTests
{
    private readonly ResumeDocumentExtractor _extractor = new();

    [Fact]
    public async Task ExtractsTextFromEveryPdfPage()
    {
        await using var stream = new MemoryStream(CreatePdf("First page", "Second page"));

        var text = await _extractor.ExtractTextAsync("resume.pdf", stream);

        Assert.Contains("First page", text);
        Assert.Contains("Second page", text);
    }

    [Fact]
    public async Task RejectsPdfWithoutExtractableText()
    {
        await using var stream = new MemoryStream(CreatePdf(string.Empty));

        var exception = await Assert.ThrowsAsync<ResumeDocumentException>(
            () => _extractor.ExtractTextAsync("resume.pdf", stream));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, exception.StatusCode);
    }

    [Fact]
    public async Task ExtractsParagraphAndTableTextFromDocx()
    {
        await using var stream = new MemoryStream(CreateDocx());

        var text = await _extractor.ExtractTextAsync("resume.docx", stream);

        Assert.Contains("Resume paragraph", text);
        Assert.Contains("Table cell", text);
    }

    [Fact]
    public async Task ParsesQuotedCsvValuesAndCommas()
    {
        var csv = "Name,Summary\r\nTaylor,\"Built search, ranking, and AI tools\"\r\n";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var text = await _extractor.ExtractTextAsync("resume.csv", stream);

        Assert.Contains("Column 2: Built search, ranking, and AI tools", text);
        Assert.Contains("Row 2:", text);
    }

    [Fact]
    public async Task RejectsMalformedCsv()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Name,Summary\nTaylor,\"unclosed field"));

        var exception = await Assert.ThrowsAsync<ResumeDocumentException>(
            () => _extractor.ExtractTextAsync("resume.csv", stream));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, exception.StatusCode);
    }

    [Fact]
    public async Task RejectsEmptyCsv()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(" \r\n"));

        var exception = await Assert.ThrowsAsync<ResumeDocumentException>(
            () => _extractor.ExtractTextAsync("resume.csv", stream));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, exception.StatusCode);
    }

    [Fact]
    public async Task RejectsMismatchedFileSignature()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("not a PDF"));

        var exception = await Assert.ThrowsAsync<ResumeDocumentException>(
            () => _extractor.ExtractTextAsync("resume.pdf", stream));

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
    }

    [Fact]
    public async Task RejectsMalformedLegacyWordDocument()
    {
        var invalidOleDocument = new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0, 0, 0, 0 };
        await using var stream = new MemoryStream(invalidOleDocument);

        var exception = await Assert.ThrowsAsync<ResumeDocumentException>(
            () => _extractor.ExtractTextAsync("resume.doc", stream));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, exception.StatusCode);
    }

    [Fact]
    public async Task RejectsUnsupportedExtension()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("anything"));

        var exception = await Assert.ThrowsAsync<ResumeDocumentException>(
            () => _extractor.ExtractTextAsync("resume.exe", stream));

        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, exception.StatusCode);
    }

    [Fact]
    public async Task RejectsMalformedDocxArchive()
    {
        await using var stream = new MemoryStream([0x50, 0x4B, 0x03, 0x04, 0, 0, 0, 0]);

        var exception = await Assert.ThrowsAsync<ResumeDocumentException>(
            () => _extractor.ExtractTextAsync("resume.docx", stream));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, exception.StatusCode);
    }

    [Fact]
    public async Task PassesExtractedResumeTextToTheExistingAiService()
    {
        var aiService = new CapturingAiService();
        var controller = new ResumeController(
            aiService,
            _extractor,
            NullLogger<ResumeController>.Instance);
        var bytes = Encoding.UTF8.GetBytes("Name,Skills\nTaylor,\"SQL, Python\"");
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "resume.csv");

        var response = await controller.AnalyzeFile(file, CancellationToken.None);

        Assert.IsType<OkObjectResult>(response.Result);
        Assert.Contains("Column 2: SQL, Python", aiService.ReceivedText);
    }

    [Fact]
    public async Task KeepsTextAnalysisEndpointContract()
    {
        var aiService = new CapturingAiService();
        var controller = new ResumeController(
            aiService,
            new StubDocumentExtractor(string.Empty),
            NullLogger<ResumeController>.Instance);

        var response = await controller.Analyze(
            new ResumeAnalysisRequest { ResumeText = "Existing text request" },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(response.Result);
        Assert.Equal("Existing text request", aiService.ReceivedText);
    }

    [Fact]
    public async Task RejectsFilesLargerThanTenMegabytesBeforeExtraction()
    {
        var aiService = new CapturingAiService();
        var extractor = new StubDocumentExtractor("Should not be extracted");
        var controller = new ResumeController(
            aiService,
            extractor,
            NullLogger<ResumeController>.Instance);
        var size = 10 * 1024 * 1024 + 1;
        var file = new FormFile(new MemoryStream(new byte[size]), 0, size, "file", "resume.pdf");

        var response = await controller.AnalyzeFile(file, CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, result.StatusCode);
        Assert.False(extractor.WasCalled);
    }

    [Fact]
    public async Task RejectsEmptyUploadedFile()
    {
        var extractor = new StubDocumentExtractor("Should not be extracted");
        var controller = new ResumeController(
            new CapturingAiService(),
            extractor,
            NullLogger<ResumeController>.Instance);
        var file = new FormFile(new MemoryStream(), 0, 0, "file", "resume.pdf");

        var response = await controller.AnalyzeFile(file, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.False(extractor.WasCalled);
    }

    private static byte[] CreateDocx()
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(
                new Body(
                    new Paragraph(new Run(new Text("Resume paragraph"))),
                    new Table(
                        new TableRow(
                            new TableCell(
                                new Paragraph(new Run(new Text("Table cell"))))))));
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static byte[] CreatePdf(params string[] pageTexts)
    {
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{string.Join(" ", pageTexts.Select((_, index) => $"{3 + index} 0 R"))}] /Count {pageTexts.Length} >>"
        };

        for (var index = 0; index < pageTexts.Length; index++)
        {
            objects.Add(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 {3 + pageTexts.Length} 0 R >> >> /Contents {4 + pageTexts.Length + index} 0 R >>");
        }

        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

        foreach (var pageText in pageTexts)
        {
            var content = $"BT /F1 12 Tf 72 720 Td ({EscapePdfString(pageText)}) Tj ET";
            objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream");
        }

        using var output = new MemoryStream();
        WriteAscii(output, "%PDF-1.4\n");
        var offsets = new List<long> { 0 };

        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(output.Position);
            WriteAscii(output, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        var xrefOffset = output.Position;
        WriteAscii(output, $"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            WriteAscii(output, $"{offset:0000000000} 00000 n \n");
        }

        WriteAscii(output, $"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");
        return output.ToArray();
    }

    private static string EscapePdfString(string value)
    {
        return value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }

    private static void WriteAscii(Stream stream, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    private sealed class CapturingAiService : IAiService
    {
        public string? ReceivedText { get; private set; }

        public Task<ResumeAnalysisResponse> AnalyzeResumeAsync(
            string resumeText,
            CancellationToken cancellationToken = default)
        {
            ReceivedText = resumeText;
            return Task.FromResult(new ResumeAnalysisResponse());
        }
    }

    private sealed class StubDocumentExtractor(string extractedText) : IResumeDocumentExtractor
    {
        public bool WasCalled { get; private set; }

        public Task<string> ExtractTextAsync(
            string fileName,
            Stream fileStream,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(extractedText);
        }
    }
}
