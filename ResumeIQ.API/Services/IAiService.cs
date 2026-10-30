using ResumeIQ.API.Models;

namespace ResumeIQ.API.Services;
public interface IAiService
{
    Task<ResumeAnalysisResponse> AnalyzeResumeAsync(
        string resumeText,
        CancellationToken cancellationToken = default);
}