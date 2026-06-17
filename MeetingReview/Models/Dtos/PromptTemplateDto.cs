using System.Text.Json.Serialization;

namespace MeetingReview.Models.Dtos;

internal record PromptTemplateDto(
    [property: JsonPropertyName("name")]   string Name,
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("format")] int Format
);
