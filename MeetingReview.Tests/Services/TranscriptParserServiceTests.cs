using System.IO;
using FluentAssertions;
using MeetingReview.Services;
using Xunit;

namespace MeetingReview.Tests.Services;

[Trait("Category", "Services")]
public class TranscriptParserServiceTests
{
    [Fact]
    public async Task ParseAsync_ValidWhisperJson_ConvertsSecondsToMs()
    {
        var svc = new TranscriptParserService();
        var json = """
            {
              "text": "Hello world.",
              "segments": [
                {
                  "start": 0.0,
                  "end": 1.5,
                  "text": " Hello world.",
                  "words": [
                    {"word": " Hello", "start": 0.0, "end": 0.5},
                    {"word": " world.", "start": 0.6, "end": 1.5}
                  ]
                }
              ]
            }
            """;
        var path = WriteTempJson(json);
        try
        {
            var segments = await svc.ParseAsync(path);

            segments.Should().HaveCount(1);
            segments[0].StartMs.Should().Be(0);
            segments[0].EndMs.Should().Be(1500);
            segments[0].Text.Should().Be("Hello world.");
            segments[0].Words.Should().HaveCount(2);
            segments[0].Words[0].Word.Should().Be("Hello");
            segments[0].Words[0].StartMs.Should().Be(0);
            segments[0].Words[0].EndMs.Should().Be(500);
            segments[0].Words[1].StartMs.Should().Be(600);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ParseAsync_SegmentsOutOfOrder_ReturnsSortedByStartMs()
    {
        var svc = new TranscriptParserService();
        var json = """
            {
              "segments": [
                {"start": 5.0, "end": 10.0, "text": "Second", "words": null},
                {"start": 0.0, "end": 4.9, "text": "First",  "words": null}
              ]
            }
            """;
        var path = WriteTempJson(json);
        try
        {
            var segments = await svc.ParseAsync(path);
            segments[0].StartMs.Should().Be(0);
            segments[0].Text.Should().Be("First");
            segments[1].StartMs.Should().Be(5000);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ParseAsync_NullWords_ProducesEmptyWordList()
    {
        var svc = new TranscriptParserService();
        var json = """
            {
              "segments": [
                {"start": 0.0, "end": 2.0, "text": "No words", "words": null}
              ]
            }
            """;
        var path = WriteTempJson(json);
        try
        {
            var segments = await svc.ParseAsync(path);
            segments[0].Words.Should().BeEmpty();
        }
        finally { File.Delete(path); }
    }

    private static string WriteTempJson(string json)
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        File.WriteAllText(path, json);
        return path;
    }
}
