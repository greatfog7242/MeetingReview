using CommunityToolkit.Mvvm.ComponentModel;
using MeetingReview.Models;

namespace MeetingReview.ViewModels;

public partial class ParagraphViewModel : ObservableObject
{
    internal long StartMs { get; }
    internal long EndMs { get; }
    [ObservableProperty] private string _text;
    public string Speaker { get; }
    public IReadOnlyList<WordViewModel> Words { get; }

    public ParagraphViewModel(TranscriptSegment segment)
    {
        StartMs = segment.StartMs;
        EndMs = segment.EndMs;
        _text = segment.Text;
        Speaker = segment.Speaker;
        Words = segment.Words.Select(w => new WordViewModel(w)).ToList();
    }
}
