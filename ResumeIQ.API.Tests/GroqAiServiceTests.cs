using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using ResumeIQ.API.Services;

namespace ResumeIQ.API.Tests;

public class GroqAiServiceTests
{
    [Fact]
    public async Task SendsResumeAndRetrievedContextToGroqAndParsesAnalysis()
    {
        const string apiKey = "test-api-key";
        const string guidance = "Use concise bullets with evidence.";
        var handler = new GroqHandler();
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.groq.com")
        };
        var configuration = CreateConfiguration(apiKey);
        var service = new GroqAiService(
            client,
            configuration,
            new FixedRagContextService(guidance));

        var result = await service.AnalyzeResumeAsync("Resume evidence and skills.");

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("/openai/v1/chat/completions", handler.Path);
        Assert.Equal($"Bearer {apiKey}", handler.Authorization);
        Assert.Equal("openai/gpt-oss-20b", handler.Model);
        Assert.Equal("json_schema", handler.ResponseFormat);
        Assert.True(handler.StrictSchema);
        Assert.Equal("low", handler.ReasoningEffort);
        Assert.Contains(guidance, handler.Prompt);
        Assert.Contains("Resume evidence and skills.", handler.Prompt);
        Assert.DoesNotContain(apiKey, handler.Prompt);
        Assert.Equal(["C#"], result.Skills);
        Assert.Equal(["Built reliable services"], result.Strengths);
    }

    [Fact]
    public async Task FailsClearlyWhenGroqApiKeyIsNotConfigured()
    {
        using var client = new HttpClient(new GroqHandler())
        {
            BaseAddress = new Uri("https://api.groq.com")
        };
        var service = new GroqAiService(
            client,
            CreateConfiguration(null),
            new FixedRagContextService(string.Empty));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AnalyzeResumeAsync("Resume text"));

        Assert.Contains("Groq__ApiKey", exception.Message);
    }

    [Fact]
    public async Task IncludesSafeGroqErrorDiagnosticsWhenRequestIsRejected()
    {
        using var client = new HttpClient(new GroqHandler
        {
            StatusCode = HttpStatusCode.BadRequest,
            ErrorContent = """
                {
                  "error": {
                    "message": "The requested model does not support this parameter.",
                    "type": "invalid_request_error",
                    "code": "unsupported_parameter"
                  }
                }
                """
        })
        {
            BaseAddress = new Uri("https://api.groq.com")
        };
        var service = new GroqAiService(
            client,
            CreateConfiguration("test-api-key"),
            new FixedRagContextService(string.Empty));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AnalyzeResumeAsync("Resume text"));

        Assert.Contains("HTTP 400", exception.Message);
        Assert.Contains("unsupported_parameter", exception.Message);
        Assert.Contains("does not support this parameter", exception.Message);
        Assert.DoesNotContain("Resume text", exception.Message);
        Assert.DoesNotContain("test-api-key", exception.Message);
    }

    [Fact]
    public async Task ReportsWhenGroqStopsBeforeReturningAnalysisContent()
    {
        using var client = new HttpClient(new GroqHandler
        {
            EmptyContent = true,
            FinishReason = "length"
        })
        {
            BaseAddress = new Uri("https://api.groq.com")
        };
        var service = new GroqAiService(
            client,
            CreateConfiguration("test-api-key"),
            new FixedRagContextService(string.Empty));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AnalyzeResumeAsync("Resume text"));

        Assert.Contains("stopped before completing", exception.Message);
        Assert.Contains("shorter resume", exception.Message);
    }

    private static IConfiguration CreateConfiguration(string? apiKey) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Groq:ApiKey"] = apiKey,
                ["Groq:Model"] = "openai/gpt-oss-20b"
            })
            .Build();

    private sealed class GroqHandler : HttpMessageHandler
    {
        public HttpStatusCode StatusCode { get; init; } = HttpStatusCode.OK;
        public string? ErrorContent { get; init; }
        public bool EmptyContent { get; init; }
        public string? FinishReason { get; init; } = "stop";
        public HttpMethod? Method { get; private set; }
        public string? Path { get; private set; }
        public string? Authorization { get; private set; }
        public string? Model { get; private set; }
        public string? ResponseFormat { get; private set; }
        public bool StrictSchema { get; private set; }
        public string? ReasoningEffort { get; private set; }
        public string Prompt { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            Path = request.RequestUri?.AbsolutePath;
            Authorization = request.Headers.Authorization?.ToString();

            using var body = await JsonDocument.ParseAsync(
                await request.Content!.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            var root = body.RootElement;
            Model = root.GetProperty("model").GetString();
            var responseFormat = root.GetProperty("response_format");
            ResponseFormat = responseFormat.GetProperty("type").GetString();
            StrictSchema = responseFormat.GetProperty("json_schema").GetProperty("strict").GetBoolean();
            ReasoningEffort = root.GetProperty("reasoning_effort").GetString();
            Prompt = string.Join(
                Environment.NewLine,
                root.GetProperty("messages")
                    .EnumerateArray()
                    .Select(message => message.GetProperty("content").GetString()));

            if (ErrorContent is not null)
            {
                return new HttpResponseMessage(StatusCode)
                {
                    Content = new StringContent(ErrorContent)
                };
            }

            return new HttpResponseMessage(StatusCode)
            {
                Content = JsonContent.Create(new
                {
                    choices = new[]
                    {
                        new
                        {
                            message = new
                            {
                                content = EmptyContent
                                    ? null
                                    : """{"skills":["C#"],"strengths":["Built reliable services"],"weaknesses":[],"recommendations":[]}"""
                            },
                            finish_reason = FinishReason
                        }
                    }
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
}
