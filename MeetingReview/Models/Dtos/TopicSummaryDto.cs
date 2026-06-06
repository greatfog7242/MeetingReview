using System.Text.Json.Serialization;

namespace MeetingReview.Models.Dtos;

internal record TopicSummaryDto(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("detailedContent")] string DetailedContent,
    [property: JsonPropertyName("startMs")] long StartMs,
    [property: JsonPropertyName("endMs")] long EndMs
);
