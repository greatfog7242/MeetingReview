namespace MeetingReview.Models;

public class ApiUsageRecord
{
    public int Id { get; set; }
    public DateTime CalledAt { get; set; }
    public string ModelVersion { get; set; } = string.Empty;
    public int PromptTokens { get; set; }
    public int CandidateTokens { get; set; }
    public int TotalTokens { get; set; }
    public double EstimatedCostUsd { get; set; }
}
