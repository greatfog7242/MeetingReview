using System.IO;
using System.Text.Json;
using MeetingReview.Models;

namespace MeetingReview.Services;

public sealed class TranscriptParserService : ITranscriptParserService
{
    public async Task<IReadOnlyList<TranscriptSegment>> ParseAsync(string jsonPath, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(jsonPath);
        var dto = await JsonSerializer.DeserializeAsync(
                      stream, AppJsonContext.Default.WhisperTranscriptDto, ct)
                  ?? throw new InvalidDataException("JSON file is empty or null.");

        var segments = (dto.Segments ?? [])
            .Select(s => new TranscriptSegment(
                StartMs: (long)(s.Start * 1000),
                EndMs: (long)(s.End * 1000),
                Text: s.Text.Trim(),
                Words: (s.Words ?? [])
                    .Select(w => new WordTimestamp(
                        Word: w.Word.Trim(),
                        StartMs: (long)(w.Start * 1000),
                        EndMs: (long)(w.End * 1000)))
                    .ToList()))
            .OrderBy(s => s.StartMs)
            .ToList();

        return segments;
    }

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
