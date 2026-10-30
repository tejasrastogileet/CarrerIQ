using System.Text.Json;
using ResumeIQ.API.Models;

namespace ResumeIQ.API.Services;

internal static class ResumeAnalysisResponseParser
{
    public static ResumeAnalysisResponse Parse(string response, string providerName)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            throw new InvalidOperationException($"{providerName} returned an empty AI response.");
        }

        response = response.Trim();
        if (response.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            response = response["```json".Length..];
        }
        else if (response.StartsWith("```", StringComparison.Ordinal))
        {
            response = response["```".Length..];
        }

        if (response.EndsWith("```", StringComparison.Ordinal))
        {
            response = response[..^3];
        }

        try
        {
            return JsonSerializer.Deserialize<ResumeAnalysisResponse>(
                       response.Trim(),
                       new JsonSerializerOptions
                       {
                           PropertyNameCaseInsensitive = true
                       })
                   ?? throw new InvalidOperationException(
                       $"{providerName} response could not be converted to the expected format.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"{providerName} returned invalid JSON.",
                exception);
        }
    }
}
