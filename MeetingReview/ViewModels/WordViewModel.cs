using CommunityToolkit.Mvvm.ComponentModel;
using MeetingReview.Models;

namespace MeetingReview.ViewModels;

public partial class WordViewModel : ObservableObject
{
    internal long StartMs { get; }
    internal long EndMs { get; }
    public string Text { get; }

    [ObservableProperty] private bool _isActive;

    public WordViewModel(WordTimestamp word)
    {
        StartMs = word.StartMs;
        EndMs = word.EndMs;
        Text = word.Word;
    }
}
