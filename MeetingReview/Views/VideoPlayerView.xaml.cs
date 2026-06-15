using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using MeetingReview.ViewModels;

namespace MeetingReview.Views;

public partial class VideoPlayerView : System.Windows.Controls.UserControl
{
    // WM_PARENTNOTIFY is sent to a WPF parent HWND whenever a native child window
    // (e.g. the VLC video HWND) receives a mouse button message.
    private const int WM_PARENTNOTIFY = 0x0210;
    private const int WM_LBUTTONDOWN  = 0x0201;

    private HwndSource? _hwndSource;

    public VideoPlayerView()
    {
        InitializeComponent();
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window == null) return;
        _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
        _hwndSource?.AddHook(WndProc);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _hwndSource?.RemoveHook(WndProc);
        _hwndSource = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_PARENTNOTIFY && (wParam.ToInt64() & 0xFFFF) == WM_LBUTTONDOWN)
        {
            // Confirm the click landed inside the video surface (not the transport controls)
            var pos = Mouse.GetPosition(VideoViewElement);
            if (pos.X >= 0 && pos.Y >= 0 &&
                pos.X <= VideoViewElement.ActualWidth &&
                pos.Y <= VideoViewElement.ActualHeight)
            {
                if (DataContext is VideoPlayerViewModel vm)
                    vm.TogglePlayPauseCommand.Execute(null);
            }
        }
        return IntPtr.Zero;
    }
}
