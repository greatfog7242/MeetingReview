using System.Text.Json.Serialization;

namespace MeetingReview.Models.Dtos;

internal record WhisperWordDto(
    [property: JsonPropertyName("word")] string Word,
    [property: JsonPropertyName("start")] double Start,
    [property: JsonPropertyName("end")] double End
);
