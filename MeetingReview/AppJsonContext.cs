using System.Text.Json.Serialization;
using MeetingReview.Models.Dtos;

namespace MeetingReview;

[JsonSerializable(typeof(WhisperTranscriptDto))]
[JsonSerializable(typeof(List<WhisperSegmentDto>))]
[JsonSerializable(typeof(List<WhisperWordDto>))]
[JsonSerializable(typeof(List<TopicSummaryDto>))]
[JsonSerializable(typeof(SummarySaveDto))]
internal partial class AppJsonContext : JsonSerializerContext { }
