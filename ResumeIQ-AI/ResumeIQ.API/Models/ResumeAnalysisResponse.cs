namespace ResumeIQ.API.Models;

public class ResumeAnalysisResponse
{
    public List<string> Skills { get; set; } = [];
    public List<string> Strengths { get; set; } = [];

    public List<string> Weaknesses { get; set; } = [];
    public List<string> Recommendations { get; set; } = [];
}