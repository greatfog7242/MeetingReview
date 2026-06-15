using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MeetingReview.ViewModels;

public partial class VideoPlayerViewModel : ObservableObject, IVideoPlayerEvents, IDisposable
{
    [ObservableProperty] private long _currentPositionMs;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private long _durationMs;
    [ObservableProperty] private bool _isZoomMode;
    [ObservableProperty] private bool _isZoomed;
    [ObservableProperty] private double _volume = 1.0;

    public event EventHandler<long>? TimeChanged;
    public event EventHandler? ZoomResetRequested;

    private MediaElement? _mediaElement;
    private readonly DispatcherTimer _timer;
    private bool _isWaitingForLoad;
    private bool _disposed;

    public VideoPlayerViewModel()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += OnTimerTick;
    }

    public void Initialize(MediaElement mediaElement)
    {
        _mediaElement = mediaElement;
        _mediaElement.Volume = Volume;
        _mediaElement.MediaOpened += OnMediaOpened;
        _mediaElement.MediaFailed += OnMediaFailed;
        _mediaElement.MediaEnded  += OnMediaEnded;
    }

    partial void OnCurrentPositionMsChanged(long value) => TimeChanged?.Invoke(this, value);
    partial void OnVolumeChanged(double value) { if (_mediaElement != null) _mediaElement.Volume = value; }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_mediaElement == null) return;
        var ms = (long)_mediaElement.Position.TotalMilliseconds;
        CurrentPositionMs = ms >= 0 ? ms : 0;
        var dur = _mediaElement.NaturalDuration;
        if (dur.HasTimeSpan)
            DurationMs = (long)dur.TimeSpan.TotalMilliseconds;
    }

    private void OnMediaOpened(object? sender, RoutedEventArgs e)
    {
        var dur = _mediaElement!.NaturalDuration;
        DurationMs = dur.HasTimeSpan ? (long)dur.TimeSpan.TotalMilliseconds : 0;
        if (_isWaitingForLoad)
        {
            _mediaElement.Pause();
            _mediaElement.Position = TimeSpan.Zero;
            IsPlaying = false;
            _isWaitingForLoad = false;
        }
        _timer.Start();
    }

    private void OnMediaFailed(object? sender, ExceptionRoutedEventArgs e)
    {
        _timer.Stop();
        IsPlaying = false;
        var msg = e.ErrorException?.Message ?? "Unknown error";
        if (msg.Contains("0x887A0002") || msg.Contains("0x80070002")
            || msg.Contains("codec", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("format", StringComparison.OrdinalIgnoreCase))
        {
            msg = "This video cannot be played. MacBook Pro .mov files recorded in HEVC (H.265) "
                + "require \"HEVC Video Extensions\" from the Microsoft Store.\n\n"
                + $"Technical details: {msg}";
        }
        MessageBox.Show(msg, "Playback Error", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void OnMediaEnded(object? sender, RoutedEventArgs e)
    {
        IsPlaying = false;
        _timer.Stop();
    }

    [RelayCommand]
    private void TogglePlayPause()
    {
        if (_mediaElement == null) return;
        if (IsPlaying) { _mediaElement.Pause(); IsPlaying = false; }
        else           { _mediaElement.Play();  IsPlaying = true;  }
    }

    [RelayCommand]
    public void Seek(long positionMs)
    {
        if (_mediaElement == null) return;
        _mediaElement.Position = TimeSpan.FromMilliseconds(positionMs);
    }

    public void LoadMedia(string filePath)
    {
        if (_mediaElement == null) return;
        _timer.Stop();
        _mediaElement.Close();
        IsPlaying = false;
        IsZoomed = false;
        IsZoomMode = false;
        ZoomResetRequested?.Invoke(this, EventArgs.Empty);
        _mediaElement.Source = new Uri(filePath);
        _mediaElement.Play();
        _isWaitingForLoad = true;
    }

    [RelayCommand]
    private void EnterZoomMode() => IsZoomMode = true;

    [RelayCommand]
    private void ResetZoom()
    {
        IsZoomed = false;
        IsZoomMode = false;
        ZoomResetRequested?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyZoomRect()
    {
        IsZoomed = true;
        IsZoomMode = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Tick -= OnTimerTick;
        _timer.Stop();
    }
}
