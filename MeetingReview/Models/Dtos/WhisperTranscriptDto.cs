using System.Text.Json.Serialization;

namespace MeetingReview.Models.Dtos;

internal record WhisperTranscriptDto(
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("segments")] List<WhisperSegmentDto>? Segments
);
