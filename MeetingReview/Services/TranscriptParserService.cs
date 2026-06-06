using System.IO;
using System.Text.Json;
using MeetingReview.Models;
using MeetingReview.Models.Dtos;

namespace MeetingReview.Services;

public sealed class TranscriptParserService : ITranscriptParserService
{
    // Words separated by more than this become a new paragraph
    private const long ParagraphGapMs = 2500;

    public async Task<IReadOnlyList<TranscriptSegment>> ParseAsync(string jsonPath, CancellationToken ct = default)
    {
        var json = await File.ReadAllTextAsync(jsonPath, ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // AudioPen format: root has a "words" array with per-word ms timestamps
        if (root.TryGetProperty("words", out var wordsEl) &&
            wordsEl.ValueKind == JsonValueKind.Array)
            return ParseAudioPen(wordsEl);

        // Whisper format: root has a "segments" array with time in fractional seconds
        var dto = JsonSerializer.Deserialize(json, AppJsonContext.Default.WhisperTranscriptDto)
                  ?? throw new InvalidDataException("JSON file is empty or null.");
        return ParseWhisper(dto);
    }

    // ── AudioPen ─────────────────────────────────────────────────────────────

    private static IReadOnlyList<TranscriptSegment> ParseAudioPen(JsonElement wordsEl)
    {
        var bucket = new List<(string word, long startMs, long endMs, string speaker)>();
        var segments = new List<TranscriptSegment>();

        foreach (var w in wordsEl.EnumerateArray())
        {
            var word    = w.GetProperty("word").GetString() ?? "";
            var startMs = w.GetProperty("startMs").GetInt64();
            var endMs   = w.GetProperty("endMs").GetInt64();
            var speaker = w.TryGetProperty("speaker", out var sp) ? (sp.GetString() ?? "") : "";

            if (bucket.Count > 0)
            {
                var (_, _, prevEnd, prevSpeaker) = bucket[^1];
                bool gapBreak     = startMs - prevEnd > ParagraphGapMs;
                bool speakerBreak = speaker != "" && prevSpeaker != "" && speaker != prevSpeaker;
                if (gapBreak || speakerBreak)
                {
                    segments.Add(FlushBucket(bucket));
                    bucket.Clear();
                }
            }

            bucket.Add((word, startMs, endMs, speaker));
        }

        if (bucket.Count > 0)
            segments.Add(FlushBucket(bucket));

        return segments;
    }

    private static TranscriptSegment FlushBucket(
        List<(string word, long startMs, long endMs, string speaker)> bucket)
    {
        var words = bucket.Select(b => new WordTimestamp(b.word, b.startMs, b.endMs)).ToList();
        var text  = string.Join(" ", bucket.Select(b => b.word));
        return new TranscriptSegment(
            StartMs: bucket[0].startMs,
            EndMs:   bucket[^1].endMs,
            Text:    text,
            Words:   words,
            Speaker: bucket[0].speaker);
    }

    // ── Whisper ──────────────────────────────────────────────────────────────

    private static IReadOnlyList<TranscriptSegment> ParseWhisper(WhisperTranscriptDto dto)
    {
        return (dto.Segments ?? [])
            .Select(s => new TranscriptSegment(
                StartMs: (long)(s.Start * 1000),
                EndMs:   (long)(s.End * 1000),
                Text:    s.Text.Trim(),
                Words:   (s.Words ?? [])
                    .Select(w => new WordTimestamp(
                        Word:    w.Word.Trim(),
                        StartMs: (long)(w.Start * 1000),
                        EndMs:   (long)(w.End * 1000)))
                    .ToList()))
            .OrderBy(s => s.StartMs)
            .ToList();
    }

    // ── Lookup ───────────────────────────────────────────────────────────────

    public int FindSegmentIndex(IReadOnlyList<TranscriptSegment> segments, long positionMs)
    {
        if (segments.Count == 0) return -1;

        int lo = 0, hi = segments.Count - 1, result = -1;
        while (lo <= hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (segments[mid].StartMs <= positionMs)
            {
                result = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }
        return result;
    }
}
