using MeetingReview.Models;

namespace MeetingReview.ViewModels;

public class ParagraphViewModel
{
    internal long StartMs { get; }
    internal long EndMs { get; }
    public string Text { get; }
    public IReadOnlyList<WordViewModel> Words { get; }

    public ParagraphViewModel(TranscriptSegment segment)
    {
        StartMs = segment.StartMs;
        EndMs = segment.EndMs;
        Text = segment.Text;
        Words = segment.Words.Select(w => new WordViewModel(w)).ToList();
    }
}
