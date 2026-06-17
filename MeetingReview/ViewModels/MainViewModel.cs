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

    private string? _jsonPath;

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
        Transcript.TranscriptSaved += (_, text) => Summary.TranscriptText = text;
        Transcript.SubtitleExported += (_, path) => VideoPlayer?.LoadSubtitles(path);
        Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.ApiKey))
                Summary.ApiKey = Settings.ApiKey;
            if (e.PropertyName == nameof(SettingsViewModel.GeminiModel))
                Summary.GeminiModel = Settings.GeminiModel;
        };
        Summary.ApiKey = Settings.ApiKey;
        Summary.GeminiModel = Settings.GeminiModel;
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
        try { VideoPlayer?.LoadMedia(path); }
        catch (Exception ex) { ShowError("Load Video", ex); }
    }

    [RelayCommand]
    private async Task LoadRecordingAsync(CancellationToken ct)
    {
        var folder = PickFolder();
        if (folder == null) return;

        var txtPath  = Path.Combine(folder, "transcript.txt");
        var jsonPath = Path.Combine(folder, "transcript.json");

        var missing = new[] { txtPath, jsonPath }.Where(p => !File.Exists(p)).ToList();
        if (missing.Count > 0)
        {
            System.Windows.MessageBox.Show(
                $"Missing required file(s) in folder:\n{string.Join("\n", missing.Select(Path.GetFileName))}",
                "Load Recording failed",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            return;
        }

        try
        {
            await Transcript.LoadAsync(jsonPath, txtPath, ct);
            Summary.TranscriptText = await File.ReadAllTextAsync(txtPath, ct);
            _jsonPath = jsonPath;
            UpdateSavePath();

            var srtPath = Path.Combine(folder, "transcript.srt");
            if (File.Exists(srtPath))
                VideoPlayer?.LoadSubtitles(srtPath);
        }
        catch (Exception ex) { ShowError("Load Recording", ex); }
    }

    private void UpdateSavePath()
    {
        Summary.BaseRecordingPath = _jsonPath == null
            ? null
            : Path.Combine(Path.GetDirectoryName(_jsonPath)!,
                           Path.GetFileNameWithoutExtension(_jsonPath));
    }

    private string BuildTranscriptText() =>
        string.Join("\n", Transcript.Paragraphs
            .Select(p => p.Words.Count > 0
                ? string.Join("", p.Words.Select(w => w.Text))
                : p.Text));

    private static void ShowError(string operation, Exception ex) =>
        System.Windows.MessageBox.Show(
            ex.Message, $"{operation} failed",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error);

    private static string? PickFile(string filter)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = filter };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    private static string? PickFolder()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Select recording folder" };
        return dlg.ShowDialog() == true ? dlg.FolderName : null;
    }
}
