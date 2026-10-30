using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ResumeIQ.API.Models;

namespace ResumeIQ.API.Services;

public sealed class GroqAiService : IAiService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IRagContextService _ragContextService;

    public GroqAiService(
        HttpClient httpClient,
        IConfiguration configuration,
        IRagContextService ragContextService)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _ragContextService = ragContextService;
    }

    public async Task<ResumeAnalysisResponse> AnalyzeResumeAsync(
        string resumeText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(resumeText))
        {
            throw new ArgumentException("Resume text cannot be empty.", nameof(resumeText));
        }

        var apiKey = _configuration["Groq:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Groq API key is not configured. Set the Groq__ApiKey environment variable and restart the API.");
        }

        var retrievedContext = await _ragContextService.GetRelevantContextAsync(
            resumeText,
            cancellationToken);
        var prompt = ResumeAnalysisPrompt.Create(resumeText, retrievedContext);
        var model = _configuration["Groq:Model"] ?? "openai/gpt-oss-20b";

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/openai/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(new
        {
            model,
            reasoning_effort = "low",
            messages = new[]
            {
                new { role = "system", content = prompt.System },
                new { role = "user", content = prompt.User }
            },
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "resume_analysis",
                    strict = true,
                    schema = new
                    {
                        type = "object",
                        properties = new
                        {
                            skills = new { type = "array", items = new { type = "string" } },
                            strengths = new { type = "array", items = new { type = "string" } },
                            weaknesses = new { type = "array", items = new { type = "string" } },
                            recommendations = new { type = "array", items = new { type = "string" } }
                        },
                        required = new[] { "skills", "strengths", "weaknesses", "recommendations" },
                        additionalProperties = false
                    }
                }
            },
            max_completion_tokens = 2_500,
            temperature = 0.2
        });

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException(
                "Unable to connect to the Groq analysis service.",
                exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                "Groq did not complete the resume analysis before the request timed out.",
                exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var providerError = await ReadProviderErrorAsync(
                    response,
                    cancellationToken);
                throw new InvalidOperationException(
                    $"Groq analysis failed with HTTP {(int)response.StatusCode}{providerError}. Check the Groq model, API key, account limits, and service status.");
            }

            GroqResponse? result;
            try
            {
                result = await response.Content.ReadFromJsonAsync<GroqResponse>(
                    cancellationToken: cancellationToken);
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException("Groq returned an invalid response.", exception);
            }

            var choice = result?.Choices?.FirstOrDefault();
            var content = choice?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                var reason = choice?.FinishReason;
                var refusal = choice?.Message?.Refusal;
                if (!string.IsNullOrWhiteSpace(refusal))
                {
                    throw new InvalidOperationException(
                        "Groq declined to analyze this resume. Try another resume or remove unsupported content.");
                }

                if (string.Equals(reason, "length", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Groq stopped before completing the analysis. Try a shorter resume.");
                }

                throw new InvalidOperationException(
                    $"Groq returned no analysis content (finish_reason={reason ?? "missing"}).");
            }

            return ResumeAnalysisResponseParser.Parse(content, "Groq");
        }
    }

    private static async Task<string> ReadProviderErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        string body;
        try
        {
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException or JsonException)
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("error", out var error)
                || error.ValueKind != JsonValueKind.Object)
            {
                return string.Empty;
            }

            var code = GetJsonString(error, "code");
            var type = GetJsonString(error, "type");
            var message = GetJsonString(error, "message");
            if (message.Length > 300)
            {
                message = message[..300];
            }

            var details = new List<string>();
            if (!string.IsNullOrWhiteSpace(code))
            {
                details.Add($"code={code}");
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                details.Add($"type={type}");
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                details.Add($"message={message}");
            }

            return details.Count > 0
                ? $" ({string.Join("; ", details)})"
                : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string GetJsonString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private sealed class GroqResponse
    {
        [JsonPropertyName("choices")]
        public List<GroqChoice>? Choices { get; set; }
    }

    private sealed class GroqChoice
    {
        [JsonPropertyName("message")]
        public GroqMessage? Message { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    private sealed class GroqMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("refusal")]
        public string? Refusal { get; set; }
    }
}
