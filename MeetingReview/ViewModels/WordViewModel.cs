using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeetingReview.Models;

namespace MeetingReview.ViewModels;

public partial class WordViewModel : ObservableObject
{
    internal long StartMs { get; }
    internal long EndMs { get; }

    [ObservableProperty] private string _text;
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private bool _isEditing;

    private string _originalText = "";

    public event EventHandler? WordEdited;

    public WordViewModel(WordTimestamp word)
    {
        StartMs = word.StartMs;
        EndMs = word.EndMs;
        _text = word.Word;
    }

    [RelayCommand]
    private void BeginEdit()
    {
        _originalText = Text;
        IsEditing = true;
    }

    public void CommitEdit()
    {
        if (!IsEditing) return;
        IsEditing = false;
        if (Text != _originalText)
            WordEdited?.Invoke(this, EventArgs.Empty);
    }

    public void CancelEdit()
    {
        Text = _originalText;
        IsEditing = false;
    }
}
