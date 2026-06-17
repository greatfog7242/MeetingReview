using System.Text.Json.Serialization;

namespace MeetingReview.Models.Dtos;

internal record SummarySaveDto(
    [property: JsonPropertyName("prompt")]       string Prompt,
    [property: JsonPropertyName("generatedAt")]  DateTime GeneratedAt,
    [property: JsonPropertyName("topics")]       List<TopicSummaryDto> Topics,
    [property: JsonPropertyName("format")]       int Format = 0,
    [property: JsonPropertyName("responseText")] string? ResponseText = null
);
