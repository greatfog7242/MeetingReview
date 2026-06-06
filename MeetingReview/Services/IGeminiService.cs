using MeetingReview.Models;

namespace MeetingReview.Services;

public interface IGeminiService
{
    Task<List<TopicSummary>> GenerateSummaryAsync(
        string transcriptText,
        string userPrompt,
        string apiKey,
        CancellationToken ct = default);
}
