namespace MeetingReview.Models;

public record GeminiCallResult(
    List<TopicSummary> Topics,
    string ModelVersion,
    int PromptTokens,
    int CandidateTokens,
    int TotalTokens
);
