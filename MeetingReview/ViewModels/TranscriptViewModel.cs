using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeetingReview.Models;
using MeetingReview.Services;

namespace MeetingReview.ViewModels;

public partial class TranscriptViewModel : ObservableObject
{
    private readonly ITranscriptParserService _parser;
    private IReadOnlyList<TranscriptSegment> _segments = Array.Empty<TranscriptSegment>();
    private string? _jsonPath;
    private string? _txtPath;

    [ObservableProperty] private ObservableCollection<ParagraphViewModel> _paragraphs = new();
    [ObservableProperty] private WordViewModel? _activeWord;
    [ObservableProperty] private int _activeParagraphIndex = -1;
    [ObservableProperty] private bool _hasParagraphs;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _searchStatus = string.Empty;
    [ObservableProperty] private bool _hasSearchResults;

    private readonly List<SearchResult> _searchResults = new();
    private int _searchIndex = -1;

    partial void OnParagraphsChanged(ObservableCollection<ParagraphViewModel> value)
        => HasParagraphs = value.Count > 0;

    partial void OnSearchTextChanged(string value)
    {
        if (!string.IsNullOrEmpty(value)) return;
        ClearSearchHighlights();
        _searchResults.Clear();
        _searchIndex = -1;
        SearchStatus = string.Empty;
        HasSearchResults = false;
    }

    partial void OnHasSearchResultsChanged(bool value)
    {
        SearchNextCommand.NotifyCanExecuteChanged();
        SearchPreviousCommand.NotifyCanExecuteChanged();
    }

    public event EventHandler<long>? NavigationRequested;
    public event EventHandler<string>? TranscriptSaved;
    public event EventHandler<string>? SubtitleExported;

    public TranscriptViewModel(ITranscriptParserService parser) => _parser = parser;

    public async Task LoadAsync(string jsonPath, string txtPath, CancellationToken ct = default)
    {
        _jsonPath = jsonPath;
        _txtPath = txtPath;
        _segments = await _parser.ParseAsync(jsonPath, ct);
        var paragraphs = _segments.Select(s => new ParagraphViewModel(s)).ToList();
        foreach (var para in paragraphs)
            foreach (var word in para.Words)
            {
                var p = para;
                var w = word;
                word.WordEdited += async (_, _) =>
                {
                    try { await OnWordEditedAsync(p, w); }
                    catch { }
                };
            }
        Paragraphs = new ObservableCollection<ParagraphViewModel>(paragraphs);
        ActiveWord = null;
        ActiveParagraphIndex = -1;
        _searchResults.Clear();
        _searchIndex = -1;
        SearchText = string.Empty;
        SearchStatus = string.Empty;
        HasSearchResults = false;
    }

    private async Task OnWordEditedAsync(ParagraphViewModel para, WordViewModel word)
    {
        if (_jsonPath == null || _txtPath == null) return;

        // Rebuild the paragraph's display text from its current word texts
        if (para.Words.Count > 0)
            para.Text = string.Join(" ", para.Words.Select(w => w.Text.Trim()));

        // Backup transcript.txt on the very first edit
        var backupPath = Path.Combine(Path.GetDirectoryName(_txtPath)!, "transcript_save.txt");
        if (!File.Exists(backupPath))
            File.Copy(_txtPath, backupPath);

        // Rewrite transcript.txt from current paragraph texts
        var txt = string.Join("\n\n", Paragraphs.Select(p => p.Text));
        await File.WriteAllTextAsync(_txtPath, txt);
        TranscriptSaved?.Invoke(this, txt);

        // Patch transcript.json in-place so no other JSON fields are lost
        await PatchWordInJsonAsync(_jsonPath, word.StartMs, word.Text);
    }

    private static async Task PatchWordInJsonAsync(string jsonPath, long wordStartMs, string newText)
    {
        var raw = await File.ReadAllTextAsync(jsonPath);
        var root = JsonNode.Parse(raw)!;

        if (root["words"] is JsonArray audioWords)
        {
            // AudioPen format: root-level words array, timestamps in ms
            foreach (var w in audioWords)
                if (w?["startMs"]?.GetValue<long>() == wordStartMs)
                {
                    w["word"] = newText;
                    break;
                }
        }
        else if (root["segments"] is JsonArray segments)
        {
            // Whisper format: nested words with timestamps in fractional seconds
            var targetSec = wordStartMs / 1000.0;
            bool patched = false;
            foreach (var seg in segments)
            {
                if (seg?["words"] is not JsonArray segWords) continue;
                foreach (var w in segWords)
                {
                    var start = w?["start"]?.GetValue<double>() ?? -1;
                    if (Math.Abs(start - targetSec) < 0.015)
                    {
                        w!["word"] = newText;
                        patched = true;
                        break;
                    }
                }
                if (patched)
                {
                    // Rebuild segment text from its updated words
                    seg!["text"] = " " + string.Join(" ", segWords
                        .Select(w => w?["word"]?.GetValue<string>()?.Trim())
                        .Where(t => t != null));
                    break;
                }
            }
        }

        await File.WriteAllTextAsync(jsonPath,
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
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

    [RelayCommand]
    private void ExportSrt()
    {
        if (Paragraphs.Count == 0) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "SRT subtitle file|*.srt",
            FileName = "transcript.srt",
            InitialDirectory = _txtPath != null ? Path.GetDirectoryName(_txtPath) : null
        };
        if (dlg.ShowDialog() != true) return;

        var sb = new StringBuilder();
        int index = 1;
        foreach (var para in Paragraphs)
        {
            var text = para.Words.Count > 0
                ? string.Join(" ", para.Words.Select(w => w.Text.Trim())).Trim()
                : para.Text.Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;

            sb.AppendLine(index.ToString());
            sb.AppendLine($"{FormatSrtTime(para.StartMs)} --> {FormatSrtTime(para.EndMs)}");
            sb.AppendLine(text);
            sb.AppendLine();
            index++;
        }

        File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
        SubtitleExported?.Invoke(this, dlg.FileName);
    }

    private static string FormatSrtTime(long ms)
    {
        var ts = TimeSpan.FromMilliseconds(ms);
        return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2},{ts.Milliseconds:D3}";
    }

    // ── Search ────────────────────────────────────────────────────────────

    // Each result holds the words to highlight and the word to navigate to.
    private record SearchResult(
        WordViewModel? NavigateWord,
        ParagraphViewModel Para,
        IReadOnlyList<WordViewModel> MatchWords);

    [RelayCommand]
    private void Search()
    {
        ClearSearchHighlights();
        _searchResults.Clear();
        _searchIndex = -1;

        var term = SearchText.Trim();
        if (string.IsNullOrEmpty(term))
        {
            SearchStatus = string.Empty;
            HasSearchResults = false;
            return;
        }

        foreach (var para in Paragraphs)
        {
            if (para.Words.Count > 0)
            {
                SearchInWords(para, term);
            }
            else if (para.Text.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                para.IsSearchMatch = true;
                _searchResults.Add(new SearchResult(null, para, Array.Empty<WordViewModel>()));
            }
        }

        HasSearchResults = _searchResults.Count > 0;

        if (_searchResults.Count > 0)
        {
            _searchIndex = 0;
            ActivateSearchResult();
        }
        else
        {
            SearchStatus = "No results";
        }
    }

    private void SearchInWords(ParagraphViewModel para, string term)
    {
        // Build a joined string and a char-range→word-index map so that
        // multi-word phrases can be found and all spanned words highlighted.
        var words = para.Words;
        var wordRanges = new (int Start, int End)[words.Count];
        int pos = 0;
        for (int i = 0; i < words.Count; i++)
        {
            wordRanges[i] = (pos, pos + words[i].Text.Length - 1);
            pos += words[i].Text.Length + 1; // +1 for the space separator
        }
        var joined = string.Join(" ", words.Select(w => w.Text));

        int searchFrom = 0;
        while (true)
        {
            int matchStart = joined.IndexOf(term, searchFrom, StringComparison.OrdinalIgnoreCase);
            if (matchStart < 0) break;
            int matchEnd = matchStart + term.Length - 1;
            searchFrom = matchStart + 1;

            var matchWords = new List<WordViewModel>();
            for (int i = 0; i < words.Count; i++)
            {
                var (wStart, wEnd) = wordRanges[i];
                if (wStart <= matchEnd && wEnd >= matchStart)
                {
                    words[i].IsSearchMatch = true;
                    matchWords.Add(words[i]);
                }
            }

            if (matchWords.Count > 0)
                _searchResults.Add(new SearchResult(matchWords[0], para, matchWords));
        }
    }

    [RelayCommand(CanExecute = nameof(CanNavigateSearch))]
    private void SearchNext()
    {
        _searchIndex = (_searchIndex + 1) % _searchResults.Count;
        ActivateSearchResult();
    }

    [RelayCommand(CanExecute = nameof(CanNavigateSearch))]
    private void SearchPrevious()
    {
        _searchIndex = (_searchIndex - 1 + _searchResults.Count) % _searchResults.Count;
        ActivateSearchResult();
    }

    private bool CanNavigateSearch() => HasSearchResults;

    private void ActivateSearchResult()
    {
        // Clear previous active highlights
        foreach (var r in _searchResults)
        {
            foreach (var w in r.MatchWords) w.IsSearchActive = false;
            if (r.NavigateWord == null) r.Para.IsSearchActive = false;
        }

        var result = _searchResults[_searchIndex];
        if (result.NavigateWord != null)
        {
            foreach (var w in result.MatchWords) w.IsSearchActive = true;
            NavigationRequested?.Invoke(this, result.NavigateWord.StartMs);
        }
        else
        {
            result.Para.IsSearchActive = true;
            NavigationRequested?.Invoke(this, result.Para.StartMs);
        }

        SearchStatus = $"{_searchIndex + 1} of {_searchResults.Count}";
    }

    private void ClearSearchHighlights()
    {
        foreach (var para in Paragraphs)
        {
            para.IsSearchMatch = false;
            para.IsSearchActive = false;
            foreach (var word in para.Words)
            {
                word.IsSearchMatch = false;
                word.IsSearchActive = false;
            }
        }
    }
}
