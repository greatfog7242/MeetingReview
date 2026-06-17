using MeetingReview.Models;

namespace MeetingReview.Services;

public interface IGeminiService
{
    Task<GeminiCallResult> GenerateSummaryAsync(
        string transcriptText,
        string userPrompt,
        string apiKey,
        string model = "gemini-2.5-flash",
        CancellationToken ct = default);

    Task<GeminiTextResult> GenerateTextAsync(
        string transcriptText,
        string userPrompt,
        string apiKey,
        string model = "gemini-2.5-flash",
        CancellationToken ct = default);

    string BuildExportablePrompt(string userPrompt, PromptFormat format);
}
