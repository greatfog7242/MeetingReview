using System.Collections.ObjectModel;
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

    public event EventHandler<long>? NavigationRequested;
    public event EventHandler<string>? TranscriptSaved;

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
}
