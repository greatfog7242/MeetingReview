namespace MeetingReview.Models;

public record TranscriptSegment(long StartMs, long EndMs, string Text, IReadOnlyList<WordTimestamp> Words, string Speaker = "");
