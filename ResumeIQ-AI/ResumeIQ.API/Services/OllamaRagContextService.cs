using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResumeIQ.API.Services;

public sealed class OllamaRagContextService : IRagContextService
{
    private const int MaximumQueryCharacters = 8_000;
    private const int MaximumChunkCharacters = 1_200;

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private IReadOnlyList<KnowledgeChunk>? _index;

    public OllamaRagContextService(
        HttpClient httpClient,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _environment = environment;
    }

    public async Task<string> GetRelevantContextAsync(
        string resumeText,
        CancellationToken cancellationToken = default)
    {
        if (!_configuration.GetValue("Rag:Enabled", true))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(resumeText))
        {
            throw new ArgumentException("Resume text cannot be empty.", nameof(resumeText));
        }

        var index = await GetIndexAsync(cancellationToken);
        var query = resumeText[..Math.Min(resumeText.Length, MaximumQueryCharacters)];
        var queryEmbedding = (await CreateEmbeddingsAsync([query], cancellationToken))[0];

        return string.Join(
            $"{Environment.NewLine}{Environment.NewLine}",
            index
                .Select(chunk => new
                {
                    Chunk = chunk,
                    Score = CosineSimilarity(queryEmbedding, chunk.Embedding)
                })
                .OrderByDescending(item => item.Score)
                .Take(Math.Clamp(_configuration.GetValue("Rag:TopK", 3), 1, 5))
                .Select(item => $"[Source: {item.Chunk.Source} — {item.Chunk.Title}]{Environment.NewLine}{item.Chunk.Text}"));
    }

    private async Task<IReadOnlyList<KnowledgeChunk>> GetIndexAsync(
        CancellationToken cancellationToken)
    {
        if (_index is not null)
        {
            return _index;
        }

        await _indexLock.WaitAsync(cancellationToken);
        try
        {
            if (_index is not null)
            {
                return _index;
            }

            var knowledgeBasePath = Path.Combine(_environment.ContentRootPath, "KnowledgeBase");
            if (!Directory.Exists(knowledgeBasePath))
            {
                throw new InvalidOperationException(
                    $"The RAG knowledge base directory was not found: {knowledgeBasePath}");
            }

            var chunks = Directory
                .EnumerateFiles(knowledgeBasePath, "*.md", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .SelectMany(ParseDocument)
                .ToArray();

            if (chunks.Length == 0)
            {
                throw new InvalidOperationException(
                    "The RAG knowledge base does not contain any readable Markdown sections.");
            }

            var embeddings = await CreateEmbeddingsAsync(
                chunks.Select(chunk => chunk.Text).ToArray(),
                cancellationToken);

            if (embeddings.Length != chunks.Length)
            {
                throw new InvalidOperationException(
                    "Ollama returned a different number of embeddings than knowledge chunks.");
            }

            _index = chunks
                .Select((chunk, index) => chunk with { Embedding = embeddings[index] })
                .ToArray();

            return _index;
        }
        finally
        {
            _indexLock.Release();
        }
    }

    private async Task<float[][]> CreateEmbeddingsAsync(
        string[] input,
        CancellationToken cancellationToken)
    {
        var model = _configuration["Rag:EmbeddingModel"] ?? "nomic-embed-text";

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(
                "/api/embed",
                new { model, input },
                cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException(
                $"Unable to connect to Ollama for RAG embeddings. Pull the '{model}' model and ensure Ollama is running.",
                exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                "Ollama did not return RAG embeddings before the request timed out.",
                exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Ollama embedding request failed with HTTP {(int)response.StatusCode}. Ensure the '{model}' model is available.");
            }

            EmbeddingResponse? result;
            try
            {
                result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(
                    cancellationToken: cancellationToken);
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException(
                    "Ollama returned an invalid embedding response.",
                    exception);
            }

            if (result?.Embeddings is not { Length: > 0 } embeddings
                || embeddings.Length != input.Length
                || embeddings.Any(embedding => embedding.Length == 0))
            {
                throw new InvalidOperationException(
                    "Ollama returned an empty or incomplete embedding response.");
            }

            var dimensions = embeddings[0].Length;
            if (embeddings.Any(embedding =>
                    embedding.Length != dimensions
                    || embedding.Any(value => !float.IsFinite(value))))
            {
                throw new InvalidOperationException(
                    "Ollama returned embeddings with inconsistent or invalid dimensions.");
            }

            return embeddings;
        }
    }

    private static IEnumerable<KnowledgeChunk> ParseDocument(string path)
    {
        var markdown = File.ReadAllText(path);
        var sections = markdown.Split(
            ["\n## ", "\r\n## "],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var section in sections)
        {
            var lines = section.Split('\n', 2, StringSplitOptions.TrimEntries);
            if (lines.Length != 2)
            {
                continue;
            }

            var title = lines[0].TrimStart('#', ' ').Trim();
            var paragraphs = lines[1]
                .Replace("\r", string.Empty, StringComparison.Ordinal)
                .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var current = new List<string>();
            var currentLength = 0;

            foreach (var paragraph in paragraphs)
            {
                var normalized = string.Join(' ', paragraph.Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries));
                if (normalized.Length == 0)
                {
                    continue;
                }

                if (currentLength > 0 && currentLength + normalized.Length + 1 > MaximumChunkCharacters)
                {
                    yield return CreateChunk(path, title, current);
                    current.Clear();
                    currentLength = 0;
                }

                foreach (var part in SplitOversizedParagraph(normalized))
                {
                    if (currentLength > 0 && currentLength + part.Length + 1 > MaximumChunkCharacters)
                    {
                        yield return CreateChunk(path, title, current);
                        current.Clear();
                        currentLength = 0;
                    }

                    current.Add(part);
                    currentLength += part.Length + 1;
                }
            }

            if (current.Count > 0)
            {
                yield return CreateChunk(path, title, current);
            }
        }
    }

    private static IEnumerable<string> SplitOversizedParagraph(string paragraph)
    {
        for (var start = 0; start < paragraph.Length; start += MaximumChunkCharacters)
        {
            var length = Math.Min(MaximumChunkCharacters, paragraph.Length - start);
            yield return paragraph.Substring(start, length);
        }
    }

    private static KnowledgeChunk CreateChunk(
        string path,
        string title,
        IEnumerable<string> paragraphs) =>
        new(Path.GetFileName(path), title, string.Join(' ', paragraphs), []);

    private static float CosineSimilarity(float[] left, float[] right)
    {
        if (left.Length != right.Length)
        {
            throw new InvalidOperationException(
                "The resume and knowledge base embeddings have different dimensions.");
        }

        double dotProduct = 0;
        double leftMagnitude = 0;
        double rightMagnitude = 0;

        for (var index = 0; index < left.Length; index++)
        {
            dotProduct += left[index] * right[index];
            leftMagnitude += left[index] * left[index];
            rightMagnitude += right[index] * right[index];
        }

        if (leftMagnitude == 0 || rightMagnitude == 0)
        {
            return 0;
        }

        return (float)(dotProduct / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude)));
    }

    private sealed record KnowledgeChunk(
        string Source,
        string Title,
        string Text,
        float[] Embedding);

    private sealed class EmbeddingResponse
    {
        [JsonPropertyName("embeddings")]
        public float[][] Embeddings { get; set; } = [];
    }
}
