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
    private string? _txtPath;

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
    private async Task LoadJsonAsync(CancellationToken ct)
    {
        var path = PickFile("JSON files|*.json|All files|*.*");
        if (path == null) return;
        try
        {
            await Transcript.LoadAsync(path, ct);
            Summary.TranscriptText = BuildTranscriptText();
            _jsonPath = path;
            UpdateSavePath();
            await Summary.TryLoadSavedAsync(ct);
        }
        catch (Exception ex) { ShowError("Load Timestamps", ex); }
    }

    [RelayCommand]
    private async Task LoadTranscriptAsync(CancellationToken ct)
    {
        var path = PickFile("Text files|*.txt|All files|*.*");
        if (path == null) return;
        try
        {
            Summary.TranscriptText = await File.ReadAllTextAsync(path, ct);
            _txtPath = path;
            UpdateSavePath();
            await Summary.TryLoadSavedAsync(ct);
        }
        catch (Exception ex) { ShowError("Load Transcript", ex); }
    }

    private void UpdateSavePath()
    {
        var source = _jsonPath ?? _txtPath;
        Summary.SavePath = source == null
            ? null
            : Path.Combine(Path.GetDirectoryName(source)!,
                           Path.GetFileNameWithoutExtension(source) + ".summary.json");
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

        // VLC embeds a Win32 child window on top of the WPF surface. If we pass
        // MainWindow as the dialog owner, ShowDialog() disables MainWindow but not
        // VLC's overlay — the dialog opens beneath the video and is invisible.
        // Using a tiny off-screen topmost window as owner keeps MainWindow enabled
        // (VLC renders normally) while the dialog still floats above everything.
        var host = new System.Windows.Window
        {
            Width = 1, Height = 1,
            Left = -10000, Top = -10000,
            WindowStyle = System.Windows.WindowStyle.None,
            ShowInTaskbar = false,
            Topmost = true
        };
        host.Show();
        try
        {
            return dlg.ShowDialog(host) == true ? dlg.FileName : null;
        }
        finally
        {
            host.Close();
        }
    }
}
