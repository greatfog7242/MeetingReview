using MeetingReview.Models;

namespace MeetingReview.Services;

public interface ITranscriptParserService
{
    Task<IReadOnlyList<TranscriptSegment>> ParseAsync(string jsonPath, CancellationToken ct = default);
    int FindSegmentIndex(IReadOnlyList<TranscriptSegment> segments, long positionMs);
}
