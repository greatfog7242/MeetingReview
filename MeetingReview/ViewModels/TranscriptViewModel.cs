using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeetingReview.Models;
using MeetingReview.Services;

namespace MeetingReview.ViewModels;

public partial class TranscriptViewModel : ObservableObject
{
    private readonly ITranscriptParserService _parser;
    private IReadOnlyList<TranscriptSegment> _segments = Array.Empty<TranscriptSegment>();

    [ObservableProperty] private ObservableCollection<ParagraphViewModel> _paragraphs = new();
    [ObservableProperty] private WordViewModel? _activeWord;
    [ObservableProperty] private int _activeParagraphIndex = -1;

    public event EventHandler<long>? NavigationRequested;

    public TranscriptViewModel(ITranscriptParserService parser) => _parser = parser;

    public async Task LoadAsync(string jsonPath, CancellationToken ct = default)
    {
        _segments = await _parser.ParseAsync(jsonPath, ct);
        Paragraphs = new ObservableCollection<ParagraphViewModel>(
            _segments.Select(s => new ParagraphViewModel(s)));
        ActiveWord = null;
        ActiveParagraphIndex = -1;
    }

    public void UpdateActiveWord(long positionMs)
    {
        var idx = _parser.FindSegmentIndex(_segments, positionMs);
        if (idx < 0 || idx >= Paragraphs.Count) return;

        ActiveParagraphIndex = idx;

        WordViewModel? found = null;
        foreach (var word in Paragraphs[idx].Words)
        {
            if (word.StartMs <= positionMs)
                found = word;
            else
                break;
        }

        if (found == ActiveWord) return;
        if (ActiveWord != null) ActiveWord.IsActive = false;
        ActiveWord = found;
        if (ActiveWord != null) ActiveWord.IsActive = true;
    }

    [RelayCommand]
    private void SelectWord(WordViewModel? word)
    {
        if (word == null) return;
        NavigationRequested?.Invoke(this, word.StartMs);
    }
}
