using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;

namespace MeetingReview.ViewModels;

public partial class VideoPlayerViewModel : ObservableObject, IVideoPlayerEvents, IDisposable
{
    public MediaPlayer MediaPlayer { get; }

    [ObservableProperty] private long _currentPositionMs;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private long _durationMs;

    public event EventHandler<long>? TimeChanged;

    private readonly LibVLC _libVlc;
    private readonly DispatcherTimer _timer;

    public VideoPlayerViewModel()
    {
        Core.Initialize();
        _libVlc = new LibVLC();
        MediaPlayer = new MediaPlayer(_libVlc);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += OnTimerTick;
    }

    partial void OnCurrentPositionMsChanged(long value) => TimeChanged?.Invoke(this, value);

    private void OnTimerTick(object? sender, EventArgs e)
    {
        var time = MediaPlayer.Time;
        CurrentPositionMs = time >= 0 ? time : 0;
        IsPlaying = MediaPlayer.IsPlaying;
        var length = MediaPlayer.Length;
        DurationMs = length >= 0 ? length : 0;
    }

    [RelayCommand]
    private void Play() => MediaPlayer.Play();

    [RelayCommand]
    private void Pause() => MediaPlayer.Pause();

    [RelayCommand]
    public void Seek(long positionMs) => MediaPlayer.Time = positionMs;

    public void LoadMedia(string filePath)
    {
        _timer.Stop();
        MediaPlayer.Stop();
        using var media = new Media(_libVlc, new Uri(filePath));
        MediaPlayer.Media = media;
        _timer.Start();
    }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Tick -= OnTimerTick;
        _timer.Stop();
        // Set Media = null before disposing to release the native VLC media handle
        // cleanly. Do NOT call MediaPlayer.Stop() explicitly — it AVEs if the
        // Win32 rendering HWND is in a transitional state during window close.
        // libvlc_media_player_release (called by Dispose) handles the stop internally.
        MediaPlayer.Media = null;
        MediaPlayer.Dispose();
        _libVlc.Dispose();
    }
}
