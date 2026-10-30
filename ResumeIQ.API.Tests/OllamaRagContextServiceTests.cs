using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using ResumeIQ.API.Models;
using ResumeIQ.API.Services;

namespace ResumeIQ.API.Tests;

public class OllamaRagContextServiceTests
{
    [Fact]
    public async Task RetrievesMostRelevantLocalGuidanceUsingOllamaEmbeddings()
    {
        var contentRootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var knowledgeBasePath = Path.Combine(contentRootPath, "KnowledgeBase");
        Directory.CreateDirectory(knowledgeBasePath);
        await File.WriteAllTextAsync(
            Path.Combine(knowledgeBasePath, "guidance.md"),
            """
            # Test guidance

            ## Collaboration
            Describe teamwork, collaboration, and mentoring.

            ## Measurable outcomes
            Explain measurable outcomes and verified impact.
            """);

        try
        {
            var handler = new EmbeddingHandler();
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost:11434")
            };
            var service = new OllamaRagContextService(
                client,
                CreateConfiguration(),
                CreateEnvironment(contentRootPath));

            var context = await service.GetRelevantContextAsync(
                "I collaborated with the engineering team and mentored new teammates.");

            Assert.Contains("Collaboration", context);
            Assert.Contains("Describe teamwork", context);
            Assert.DoesNotContain("Measurable outcomes", context);
            Assert.Equal(2, handler.RequestCount);
        }
        finally
        {
            Directory.Delete(contentRootPath, recursive: true);
        }
    }

    [Fact]
    public async Task CanDisableRetrievalWithoutCallingOllama()
    {
        var handler = new EmbeddingHandler();
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Rag:Enabled"] = "false"
            })
            .Build();
        var service = new OllamaRagContextService(
            client,
            configuration,
            CreateEnvironment(Path.GetTempPath()));

        var context = await service.GetRelevantContextAsync("Resume text");

        Assert.Empty(context);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task AddsRetrievedGuidanceToTheGemmaAnalysisPrompt()
    {
        const string retrievedGuidance = "[Source: guidance.md — Skills]\nSupport important skills with resume evidence.";
        var handler = new GenerationHandler();
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        var service = new OllamaAiService(
            client,
            CreateConfiguration(),
            new FixedRagContextService(retrievedGuidance));

        var response = await service.AnalyzeResumeAsync("Resume contains engineering experience.");

        Assert.Empty(response.Skills);
        Assert.Contains(retrievedGuidance, handler.Prompt);
        Assert.Contains("Resume contains engineering experience.", handler.Prompt);
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Rag:Enabled"] = "true",
                ["Rag:EmbeddingModel"] = "test-embeddings",
                ["Rag:TopK"] = "1",
                ["Groq:ApiKey"] = "test-api-key",
                ["Groq:Model"] = "openai/gpt-oss-20b"
            })
            .Build();

    private static IHostEnvironment CreateEnvironment(string contentRootPath) =>
        new TestHostEnvironment(contentRootPath);

    private sealed class EmbeddingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            Assert.Equal("/api/embed", request.RequestUri?.AbsolutePath);

            using var body = await JsonDocument.ParseAsync(
                await request.Content!.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            var inputs = body.RootElement
                .GetProperty("input")
                .EnumerateArray()
                .Select(element => element.GetString() ?? string.Empty);
            var embeddings = inputs
                .Select(text => text.Contains("team", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("collaborat", StringComparison.OrdinalIgnoreCase)
                    ? new[] { 1f, 0f }
                    : new[] { 0f, 1f })
                .ToArray();

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { embeddings })
            };
        }
    }

    private sealed class GenerationHandler : HttpMessageHandler
    {
        public string Prompt { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            using var body = await JsonDocument.ParseAsync(
                await request.Content!.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            Prompt = body.RootElement.GetProperty("prompt").GetString() ?? string.Empty;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    response = """{"skills":[],"strengths":[],"weaknesses":[],"recommendations":[]}"""
                })
            };
        }
    }

    private sealed class FixedRagContextService(string context) : IRagContextService
    {
        public Task<string> GetRelevantContextAsync(
            string resumeText,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(context);
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "CareerIQ.Tests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
