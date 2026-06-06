using System.Diagnostics;
using FluentAssertions;
using MeetingReview.Models;
using MeetingReview.Services;
using Xunit;

namespace MeetingReview.Tests.Services;

[Trait("Category", "Services")]
public class BinarySearchBenchmarkTests
{
    private static List<TranscriptSegment> BuildSegments(int count, long intervalMs = 600) =>
        Enumerable.Range(0, count)
            .Select(i => new TranscriptSegment(
                StartMs: i * intervalMs,
                EndMs: i * intervalMs + intervalMs - 1,
                Text: $"Segment {i}",
                Words: Array.Empty<WordTimestamp>()))
            .ToList();

    [Fact]
    public void FindSegmentIndex_100kEntries_10kQueriesUnder50ms()
    {
        var svc = new TranscriptParserService();
        var segments = BuildSegments(100_000);
        var rng = new Random(42);
        long maxMs = segments[^1].EndMs;

        var queries = Enumerable.Range(0, 10_000)
            .Select(_ => (long)(rng.NextDouble() * maxMs))
            .ToArray();

        var sw = Stopwatch.StartNew();
        foreach (var q in queries)
            svc.FindSegmentIndex(segments, q);
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(50,
            "10,000 binary searches on 100k entries must complete well under 50ms");
    }

    [Fact]
    public void FindSegmentIndex_BeforeFirstSegment_ReturnsMinusOne()
    {
        var svc = new TranscriptParserService();
        var segments = BuildSegments(10, intervalMs: 1000);

        svc.FindSegmentIndex(segments, -1).Should().Be(-1);
    }

    [Fact]
    public void FindSegmentIndex_AfterLastSegment_ReturnsLastIndex()
    {
        var svc = new TranscriptParserService();
        var segments = BuildSegments(10, intervalMs: 1000);

        svc.FindSegmentIndex(segments, segments[^1].EndMs + 5000)
           .Should().Be(segments.Count - 1);
    }

    [Fact]
    public void FindSegmentIndex_ExactStartBoundary_ReturnsCorrectIndex()
    {
        var svc = new TranscriptParserService();
        var segments = BuildSegments(100, intervalMs: 1000);

        svc.FindSegmentIndex(segments, segments[42].StartMs).Should().Be(42);
    }

    [Fact]
    public void FindSegmentIndex_ExactEndBoundary_ReturnsCorrectIndex()
    {
        var svc = new TranscriptParserService();
        var segments = BuildSegments(100, intervalMs: 1000);

        svc.FindSegmentIndex(segments, segments[42].EndMs).Should().Be(42);
    }

    [Fact]
    public void FindSegmentIndex_EmptyList_ReturnsMinusOne()
    {
        var svc = new TranscriptParserService();
        svc.FindSegmentIndex(new List<TranscriptSegment>(), 1000).Should().Be(-1);
    }
}
