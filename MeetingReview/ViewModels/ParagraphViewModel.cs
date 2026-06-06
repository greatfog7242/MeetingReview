using MeetingReview.Models;

namespace MeetingReview.ViewModels;

public class ParagraphViewModel
{
    internal long StartMs { get; }
    internal long EndMs { get; }
    public string Text { get; }
    public string Speaker { get; }
    public IReadOnlyList<WordViewModel> Words { get; }

    public ParagraphViewModel(TranscriptSegment segment)
    {
        StartMs = segment.StartMs;
        EndMs = segment.EndMs;
        Text = segment.Text;
        Speaker = segment.Speaker;
        Words = segment.Words.Select(w => new WordViewModel(w)).ToList();
    }
}
