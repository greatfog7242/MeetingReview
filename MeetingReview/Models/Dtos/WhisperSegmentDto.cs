using System.Text.Json.Serialization;

namespace MeetingReview.Models.Dtos;

internal record WhisperSegmentDto(
    [property: JsonPropertyName("start")] double Start,
    [property: JsonPropertyName("end")] double End,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("words")] List<WhisperWordDto>? Words
);
