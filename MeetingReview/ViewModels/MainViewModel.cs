using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MeetingReview.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IVideoPlayerEvents _videoPlayerEvents;

    public VideoPlayerViewModel VideoPlayer { get; }
    public TranscriptViewModel Transcript { get; }
    public SummaryViewModel Summary { get; }
    public SettingsViewModel Settings { get; }

    internal bool SuppressAutoSync { get; private set; }

    public MainViewModel(
        VideoPlayerViewModel videoPlayer,
        TranscriptViewModel transcript,
        SummaryViewModel summary,
        SettingsViewModel settings)
    {
        _videoPlayerEvents = videoPlayer;
        VideoPlayer = videoPlayer;
        Transcript = transcript;
        Summary = summary;
        Settings = settings;
        Wire();
    }

    internal MainViewModel(
        IVideoPlayerEvents videoPlayerEvents,
        TranscriptViewModel transcript,
        SummaryViewModel summary,
        SettingsViewModel settings)
    {
        _videoPlayerEvents = videoPlayerEvents;
        VideoPlayer = null!;
        Transcript = transcript;
        Summary = summary;
        Settings = settings;
        Wire();
    }

    private void Wire()
    {
        _videoPlayerEvents.TimeChanged += OnVideoTimeChanged;
        Transcript.NavigationRequested += OnTranscriptNavigationRequested;
        Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.ApiKey))
                Summary.ApiKey = Settings.ApiKey;
        };
        Summary.ApiKey = Settings.ApiKey;
    }

    private void OnVideoTimeChanged(object? sender, long positionMs)
    {
        if (SuppressAutoSync) return;
        Transcript.UpdateActiveWord(positionMs);
        Summary.HighlightTopicAt(positionMs);
    }

    private void OnTranscriptNavigationRequested(object? sender, long positionMs) =>
        NavigateToTime(positionMs);

    [RelayCommand]
    private void NavigateToTime(long positionMs)
    {
        SuppressAutoSync = true;
        try
        {
            _videoPlayerEvents.Seek(positionMs);
            Transcript.UpdateActiveWord(positionMs);
            Summary.HighlightTopicAt(positionMs);
        }
        finally
        {
            SuppressAutoSync = false;
        }
    }

    [RelayCommand]
    private void LoadVideo()
    {
        var path = PickFile("MOV files|*.mov|MP4 files|*.mp4|All files|*.*");
        if (path == null) return;
        VideoPlayer?.LoadMedia(path);
    }

    [RelayCommand]
    private async Task LoadJsonAsync(CancellationToken ct)
    {
        var path = PickFile("JSON files|*.json|All files|*.*");
        if (path == null) return;
        await Transcript.LoadAsync(path, ct);
        Summary.TranscriptText = BuildTranscriptText();
    }

    [RelayCommand]
    private async Task LoadTranscriptAsync(CancellationToken ct)
    {
        var path = PickFile("Text files|*.txt|All files|*.*");
        if (path == null) return;
        Summary.TranscriptText = await File.ReadAllTextAsync(path, ct);
    }

    private string BuildTranscriptText() =>
        string.Join("\n", Transcript.Paragraphs
            .Select(p => string.Join("", p.Words.Select(w => w.Text))));

    private static string? PickFile(string filter)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = filter };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}
