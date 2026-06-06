using System.Collections.ObjectModel;
using FluentAssertions;
using MeetingReview.Models;
using MeetingReview.Services;
using MeetingReview.ViewModels;
using NSubstitute;
using Xunit;

namespace MeetingReview.Tests.ViewModels;

[Trait("Category", "ViewModels")]
public class MainViewModelTests
{
    // Three segments: 0–999ms, 1000–1999ms, 2000–2999ms
    private static List<TranscriptSegment> BuildSegments() =>
    [
        new(0,    999,  "Hello",   [new WordTimestamp("Hello",  0,   499)]),
        new(1000, 1999, "world",   [new WordTimestamp("world",  1000, 1499)]),
        new(2000, 2999, "everyone",[new WordTimestamp("everyone",2000,2499)]),
    ];

    private static (MainViewModel main, IVideoPlayerEvents fakePlayer, TranscriptViewModel transcript)
        CreateSubject()
    {
        var segments = BuildSegments();

        var parser = Substitute.For<ITranscriptParserService>();
        parser.ParseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
              .Returns(Task.FromResult<IReadOnlyList<TranscriptSegment>>(segments));
        parser.FindSegmentIndex(Arg.Any<IReadOnlyList<TranscriptSegment>>(), Arg.Any<long>())
              .Returns(ci =>
              {
                  var segs = ci.ArgAt<IReadOnlyList<TranscriptSegment>>(0);
                  var pos  = ci.ArgAt<long>(1);
                  int idx  = -1;
                  for (int i = 0; i < segs.Count; i++)
                      if (segs[i].StartMs <= pos) idx = i;
                  return idx;
              });

        var gemini   = Substitute.For<IGeminiService>();
        var transcript = new TranscriptViewModel(parser);
        var summary    = new SummaryViewModel(gemini);
        var settings   = new SettingsViewModel();
        var fakePlayer = Substitute.For<IVideoPlayerEvents>();

        var main = new MainViewModel(fakePlayer, transcript, summary, settings);
        return (main, fakePlayer, transcript);
    }

    [Fact]
    public async Task NavigateToTime_UpdatesTranscriptActiveWord()
    {
        var (main, _, transcript) = CreateSubject();
        await transcript.LoadAsync("fake.json");

        main.NavigateToTimeCommand.Execute(1000L);

        transcript.ActiveParagraphIndex.Should().Be(1);
        transcript.ActiveWord.Should().NotBeNull();
        transcript.ActiveWord!.Text.Should().Be("world");
    }

    [Fact]
    public async Task NavigateToTime_HighlightsMatchingSummaryTopic()
    {
        var (main, _, transcript) = CreateSubject();
        await transcript.LoadAsync("fake.json");

        main.Summary.Topics = new ObservableCollection<TopicSummary>(
        [
            new() { Title = "A", DetailedContent = "x", StartMs = 0,    EndMs = 999  },
            new() { Title = "B", DetailedContent = "x", StartMs = 1000, EndMs = 1999 },
        ]);

        main.NavigateToTimeCommand.Execute(1500L);

        main.Summary.Topics[0].IsExpanded.Should().BeFalse();
        main.Summary.Topics[1].IsExpanded.Should().BeTrue();
    }

    [Fact]
    public async Task NavigateToTime_SuppressesTimeChangedDuringSeek()
    {
        var (main, fakePlayer, transcript) = CreateSubject();
        await transcript.LoadAsync("fake.json");

        // When Seek(1000) is called, the fake player fires TimeChanged(2500) —
        // a position that would land in segment 2 (index 2).
        // If suppression works, index stays at 1 (from NavigateToTime(1000)).
        // If suppression fails, the TimeChanged handler overwrites it with 2.
        fakePlayer.When(p => p.Seek(1000L))
                  .Do(_ => fakePlayer.TimeChanged += Raise.Event<EventHandler<long>>(fakePlayer, 2500L));

        main.NavigateToTimeCommand.Execute(1000L);

        transcript.ActiveParagraphIndex.Should().Be(1,
            "suppression must prevent the TimeChanged(2500) from overwriting the NavigateToTime(1000) result");
    }

    [Fact]
    public async Task NavigateToTime_SuppressAutoSyncIsFalseAfterCompletion()
    {
        var (main, _, _) = CreateSubject();

        main.NavigateToTimeCommand.Execute(500L);

        main.SuppressAutoSync.Should().BeFalse("the flag must reset after NavigateToTime completes");
    }

    [Fact]
    public async Task VideoTimeChanged_UpdatesTranscriptWhenNotSuppressed()
    {
        var (main, fakePlayer, transcript) = CreateSubject();
        await transcript.LoadAsync("fake.json");

        fakePlayer.TimeChanged += Raise.Event<EventHandler<long>>(fakePlayer, 2000L);

        transcript.ActiveParagraphIndex.Should().Be(2);
    }
}
