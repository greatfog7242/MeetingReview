using System.Net.Http;
using System.Text;
using System.Text.Json;
using MeetingReview.Models;
using MeetingReview.Models.Dtos;

namespace MeetingReview.Services;

public sealed class GeminiService : IGeminiService
{
    private const string BaseUrl =
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent";

    private readonly HttpClient _http;

    public GeminiService(HttpClient http) => _http = http;

    public async Task<List<TopicSummary>> GenerateSummaryAsync(
        string transcriptText,
        string userPrompt,
        string apiKey,
        CancellationToken ct = default)
    {
        var prompt = BuildPrompt(transcriptText, userPrompt);
        var requestJson = BuildRequestJson(prompt);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}?key={apiKey}");
        request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        var topicsJson = StripMarkdownFences(ExtractCandidateText(body));

        var dtos = JsonSerializer.Deserialize(topicsJson, AppJsonContext.Default.ListTopicSummaryDto)
                   ?? throw new InvalidDataException("Gemini returned an empty topics list.");

        return dtos.Select(d => new TopicSummary
        {
            Title = d.Title,
            DetailedContent = d.DetailedContent,
            StartMs = d.StartMs,
            EndMs = d.EndMs
        }).ToList();
    }

    private static string BuildPrompt(string transcriptText, string userPrompt) => $"""
        You are a meeting summarizer. Given the following meeting transcript, {userPrompt}.

        Return ONLY a valid JSON array — no markdown, no explanation, no code fences. Each element must have exactly these fields:
        - "title": short topic title (string)
        - "detailedContent": detailed notes about the topic as discussed (string)
        - "startMs": timestamp in milliseconds where this topic begins (integer)
        - "endMs": timestamp in milliseconds where this topic ends (integer)

        TRANSCRIPT:
        {transcriptText}
        """;

    private static string BuildRequestJson(string prompt)
    {
        var escapedPrompt = JsonSerializer.Serialize(prompt);
        return $$"""
            {
              "contents": [{"parts": [{"text": {{escapedPrompt}}}]}],
              "generationConfig": {
                "responseMimeType": "application/json",
                "temperature": 0.2
              }
            }
            """;
    }

    private static string ExtractCandidateText(string responseBody)
    {
        using var doc = JsonDocument.Parse(responseBody);
        return doc.RootElement
                   .GetProperty("candidates")[0]
                   .GetProperty("content")
                   .GetProperty("parts")[0]
                   .GetProperty("text")
                   .GetString()
               ?? throw new InvalidDataException("Gemini response contains no text.");
    }

    private static string StripMarkdownFences(string text)
    {
        var t = text.Trim();
        if (!t.StartsWith("```")) return t;
        var firstNewline = t.IndexOf('\n');
        if (firstNewline >= 0) t = t[(firstNewline + 1)..];
        if (t.EndsWith("```")) t = t[..^3].TrimEnd();
        return t.Trim();
    }
}
