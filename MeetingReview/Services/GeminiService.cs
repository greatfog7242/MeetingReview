using System.Net.Http;
using System.Text;
using System.Text.Json;
using MeetingReview.Models;
using MeetingReview.Models.Dtos;

namespace MeetingReview.Services;

public sealed class GeminiService : IGeminiService
{
    private const string ApiBase =
        "https://generativelanguage.googleapis.com/v1beta/models";

    private readonly HttpClient _http;

    public GeminiService(HttpClient http) => _http = http;

    // ── Dropdown (JSON topics) ────────────────────────────────────────────

    public async Task<GeminiCallResult> GenerateSummaryAsync(
        string transcriptText,
        string userPrompt,
        string apiKey,
        string model = "gemini-2.5-flash",
        CancellationToken ct = default)
    {
        var prompt = BuildDropdownPrompt(transcriptText, userPrompt);
        var requestJson = BuildJsonRequestJson(prompt);
        var url = $"{ApiBase}/{model}:generateContent?key={apiKey}";

        var body = await SendWithRetryAsync(url, requestJson, ct);
        var (text, modelVersion, promptTokens, candidateTokens, totalTokens) = ParseResponse(body);

        var topicsJson = StripMarkdownFences(text);
        var dtos = JsonSerializer.Deserialize(topicsJson, AppJsonContext.Default.ListTopicSummaryDto)
                   ?? throw new InvalidDataException("Gemini returned an empty topics list.");

        var topics = dtos.Select(d => new TopicSummary
        {
            Title = d.Title,
            DetailedContent = d.DetailedContent,
            StartMs = d.StartMs,
            EndMs = d.EndMs
        }).ToList();

        return new GeminiCallResult(topics, modelVersion, promptTokens, candidateTokens, totalTokens);
    }

    // ── Markdown / Text (free-text) ───────────────────────────────────────

    public async Task<GeminiTextResult> GenerateTextAsync(
        string transcriptText,
        string userPrompt,
        string apiKey,
        string model = "gemini-2.5-flash",
        CancellationToken ct = default)
    {
        var prompt = BuildFreeTextPrompt(transcriptText, userPrompt);
        var requestJson = BuildFreeTextRequestJson(prompt);
        var url = $"{ApiBase}/{model}:generateContent?key={apiKey}";

        var body = await SendWithRetryAsync(url, requestJson, ct);
        var (text, modelVersion, promptTokens, candidateTokens, totalTokens) = ParseResponse(body);

        return new GeminiTextResult(text, modelVersion, promptTokens, candidateTokens, totalTokens);
    }

    // ── Exportable prompt (user instructions + app additions, no transcript) ──

    public string BuildExportablePrompt(string userPrompt, PromptFormat format)
    {
        var formatInstructions = FormatInstructions(format);
        return $"""
            You are a meeting summarizer. Given the following meeting transcript, {userPrompt}.

            {formatInstructions}

            [transcript omitted]
            """;
    }

    // ── Internal prompt builders ──────────────────────────────────────────

    private static string BuildDropdownPrompt(string transcriptText, string userPrompt) => $"""
        You are a meeting summarizer. Given the following meeting transcript, {userPrompt}.

        {DropdownFormatInstructions}

        TRANSCRIPT:
        {transcriptText}
        """;

    private static string BuildFreeTextPrompt(string transcriptText, string userPrompt) => $"""
        You are a meeting summarizer. Given the following meeting transcript, {userPrompt}.

        {FreeTextFormatInstructions}

        TRANSCRIPT:
        {transcriptText}
        """;

    private static string FormatInstructions(PromptFormat format) => format switch
    {
        PromptFormat.Dropdown => DropdownFormatInstructions,
        PromptFormat.Markdown => FreeTextFormatInstructions,
        PromptFormat.Text     => FreeTextFormatInstructions,
        _                     => FreeTextFormatInstructions
    };

    private const string DropdownFormatInstructions =
        """
        Return ONLY a valid JSON array — no markdown, no explanation, no code fences. Each element must have exactly these fields:
        - "title": short topic title (string)
        - "detailedContent": detailed notes about the topic as discussed (string)
        - "startMs": timestamp in milliseconds where this topic begins (integer)
        - "endMs": timestamp in milliseconds where this topic ends (integer)
        """;

    private const string FreeTextFormatInstructions =
        "Return your response in well-formatted Markdown.";

    // ── Request builders ──────────────────────────────────────────────────

    private static string BuildJsonRequestJson(string prompt)
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

    private static string BuildFreeTextRequestJson(string prompt)
    {
        var escapedPrompt = JsonSerializer.Serialize(prompt);
        return $$"""
            {
              "contents": [{"parts": [{"text": {{escapedPrompt}}}]}],
              "generationConfig": {
                "temperature": 0.3
              }
            }
            """;
    }

    // ── HTTP + response helpers ───────────────────────────────────────────

    private async Task<string> SendWithRetryAsync(string url, string requestJson, CancellationToken ct)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync(ct);

            var errorBody = await response.Content.ReadAsStringAsync(ct);

            if ((int)response.StatusCode == 429 && attempt == 0)
            {
                int delaySec = 30;
                if (response.Headers.TryGetValues("Retry-After", out var vals) &&
                    int.TryParse(vals.FirstOrDefault(), out var ra))
                    delaySec = ra;

                await Task.Delay(TimeSpan.FromSeconds(delaySec), ct);
                continue;
            }

            string detail = TryExtractGeminiError(errorBody)
                            ?? $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";

            throw new HttpRequestException(detail, null, response.StatusCode);
        }

        throw new HttpRequestException("Gemini rate limit — still busy after retry. Wait a minute and try again.");
    }

    private static string? TryExtractGeminiError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err))
            {
                var msg  = err.TryGetProperty("message", out var m) ? m.GetString() : null;
                var code = err.TryGetProperty("status",  out var s) ? s.GetString() : null;
                if (msg != null) return code != null ? $"{code}: {msg}" : msg;
            }
        }
        catch { }
        return null;
    }

    private static (string text, string modelVersion, int promptTokens, int candidateTokens, int totalTokens)
        ParseResponse(string responseBody)
    {
        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        var text = root
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString()
            ?? throw new InvalidDataException("Gemini response contains no text.");

        var modelVersion = root.TryGetProperty("modelVersion", out var mv)
            ? (mv.GetString() ?? "unknown")
            : "unknown";

        int promptTokens = 0, candidateTokens = 0, totalTokens = 0;
        if (root.TryGetProperty("usageMetadata", out var usage))
        {
            if (usage.TryGetProperty("promptTokenCount",     out var p)) promptTokens    = p.GetInt32();
            if (usage.TryGetProperty("candidatesTokenCount", out var c)) candidateTokens = c.GetInt32();
            if (usage.TryGetProperty("totalTokenCount",      out var t)) totalTokens     = t.GetInt32();
        }

        return (text, modelVersion, promptTokens, candidateTokens, totalTokens);
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
