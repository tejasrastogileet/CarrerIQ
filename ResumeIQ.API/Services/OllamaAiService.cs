using System.Net.Http.Json;
using System.Text.Json;
using ResumeIQ.API.Models;

namespace ResumeIQ.API.Services;

public class OllamaAiService : IAiService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IRagContextService _ragContextService;

    public OllamaAiService(
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
            throw new ArgumentException(
                "Resume text cannot be empty.",
                nameof(resumeText));
        }

        var model = _configuration["Ollama:Model"] ?? "gemma3";
        var retrievedContext = await _ragContextService.GetRelevantContextAsync(
            resumeText,
            cancellationToken);

        var prompt = ResumeAnalysisPrompt.Create(resumeText, retrievedContext)
            .ToOllamaPrompt();

        var request = new
        {
            model = model,
            prompt = prompt,
            stream = false,
            format = "json"
        };

        HttpResponseMessage response;

        try
        {
            response = await _httpClient.PostAsJsonAsync(
                "/api/generate",
                request,
                cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException(
                "Unable to connect to Ollama. Make sure Ollama is running.",
                exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                "Ollama did not respond before the analysis timed out.",
                exception);
        }

        using (response)
        {

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Ollama returned HTTP {(int)response.StatusCode}.");
            }

            OllamaResponse? result;
            try
            {
                result = await response.Content
                    .ReadFromJsonAsync<OllamaResponse>(cancellationToken: cancellationToken);
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException(
                    "Ollama returned an invalid response.",
                    exception);
            }

            if (result == null)
            {
                throw new InvalidOperationException(
                    "Ollama returned an empty response.");
            }

            if (string.IsNullOrWhiteSpace(result.Response))
            {
                throw new InvalidOperationException(
                    "Ollama returned an empty AI response.");
            }

            return ResumeAnalysisResponseParser.Parse(result.Response, "Ollama");
        }
    }

    private sealed class OllamaResponse
    {
        public string Response { get; set; } = string.Empty;
    }
}