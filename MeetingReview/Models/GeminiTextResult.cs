namespace MeetingReview.Models;

public record GeminiTextResult(
    string Text,
    string ModelVersion,
    int PromptTokens,
    int CandidateTokens,
    int TotalTokens
);
