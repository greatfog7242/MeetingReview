using System.Text.Json.Serialization;

namespace MeetingReview.Models.Dtos;

internal record SummarySaveDto(
    [property: JsonPropertyName("prompt")]       string Prompt,
    [property: JsonPropertyName("generatedAt")]  DateTime GeneratedAt,
    [property: JsonPropertyName("topics")]       List<TopicSummaryDto> Topics
);
