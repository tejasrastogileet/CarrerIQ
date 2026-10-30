using Microsoft.AspNetCore.Mvc;
using ResumeIQ.API.Models;
using ResumeIQ.API.Services;

namespace ResumeIQ.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ResumeController : ControllerBase
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;
    private const long MultipartOverheadBytes = 64 * 1024;

    private readonly IAiService _aiService;
    private readonly IResumeDocumentExtractor _documentExtractor;
    private readonly ILogger<ResumeController> _logger;

    public ResumeController(
        IAiService aiService,
        IResumeDocumentExtractor documentExtractor,
        ILogger<ResumeController> logger)
    {
        _aiService = aiService;
        _documentExtractor = documentExtractor;
        _logger = logger;
    }

    [HttpPost("analyze")]
    public async Task<ActionResult<ResumeAnalysisResponse>> Analyze(
        [FromBody] ResumeAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ResumeText))
        {
            return BadRequest(new
            {
                message = "Resume text cannot be empty."
            });
        }

        return await AnalyzeTextAsync(request.ResumeText, cancellationToken);
    }

    [HttpPost("analyze-file")]
    [RequestSizeLimit(MaxFileSizeBytes + MultipartOverheadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFileSizeBytes + MultipartOverheadBytes)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ResumeAnalysisResponse>> AnalyzeFile(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return BadRequest(new { message = "Select a resume file to analyze." });
        }

        if (file.Length == 0)
        {
            return BadRequest(new { message = "The selected file is empty." });
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new
            {
                message = "The file exceeds the 10 MB limit."
            });
        }

        string resumeText;

        try
        {
            await using var fileStream = file.OpenReadStream();
            resumeText = await _documentExtractor.ExtractTextAsync(
                file.FileName,
                fileStream,
                cancellationToken);
        }
        catch (ResumeDocumentException exception)
        {
            _logger.LogInformation(
                "Resume file was rejected with HTTP {StatusCode}: {ValidationMessage}",
                exception.StatusCode,
                exception.Message);
            return StatusCode(exception.StatusCode, new { message = exception.Message });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new EmptyResult();
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Resume file extraction failed with {ExceptionType}.",
                exception.GetType().FullName);
            return StatusCode(StatusCodes.Status422UnprocessableEntity, new
            {
                message = "The uploaded document could not be read. It may be damaged or unsupported."
            });
        }

        return await AnalyzeTextAsync(resumeText, cancellationToken);
    }

    private async Task<ActionResult<ResumeAnalysisResponse>> AnalyzeTextAsync(
        string resumeText,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _aiService.AnalyzeResumeAsync(resumeText, cancellationToken);
            _logger.LogInformation("Resume AI analysis completed successfully.");
            return Ok(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new EmptyResult();
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogError(
                "Resume AI analysis failed with {ExceptionType}: {ProviderDiagnostic}",
                exception.GetType().FullName,
                exception.Message);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = exception.Message
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Resume analysis failed unexpectedly with {ExceptionType}.",
                exception.GetType().FullName);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Resume analysis could not be completed. Please try again."
            });
        }

    }
}