using MeetingReview.Models;

namespace MeetingReview.Services;

public interface IGeminiService
{
    Task<GeminiCallResult> GenerateSummaryAsync(
        string transcriptText,
        string userPrompt,
        string apiKey,
        CancellationToken ct = default);
}
