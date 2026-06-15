using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using MeetingReview.ViewModels;

namespace MeetingReview.Views;

public partial class VideoPlayerView : System.Windows.Controls.UserControl
{
    private const int WH_MOUSE     = 7;
    private const int HC_ACTION    = 0;
    private const int WM_LBUTTONDOWN = 0x0201;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEHOOKSTRUCT
    {
        public POINT   pt;
        public IntPtr  hwnd;
        public uint    wHitTestCode;
        public IntPtr  dwExtraInfo;
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int idHook, HookProc fn, IntPtr hMod, uint threadId);
    [DllImport("user32.dll")] private static extern bool   UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] private static extern uint  GetCurrentThreadId();

    private HookProc? _hookProc;   // held in a field to prevent GC collection
    private IntPtr    _hookHandle;

    public VideoPlayerView()
    {
        InitializeComponent();
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hookProc   = MouseHookProc;
        _hookHandle = SetWindowsHookEx(WH_MOUSE, _hookProc, IntPtr.Zero, GetCurrentThreadId());
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == HC_ACTION && wParam.ToInt32() == WM_LBUTTONDOWN)
        {
            var hs = Marshal.PtrToStructure<MOUSEHOOKSTRUCT>(lParam);
            var screenPt = new Point(hs.pt.X, hs.pt.Y);

            // Queue on the dispatcher so we're not calling back into WPF from within the hook
            Dispatcher.BeginInvoke(() =>
            {
                var localPt = VideoViewElement.PointFromScreen(screenPt);
                if (localPt.X >= 0 && localPt.Y >= 0 &&
                    localPt.X <= VideoViewElement.ActualWidth &&
                    localPt.Y <= VideoViewElement.ActualHeight)
                {
                    if (DataContext is VideoPlayerViewModel vm)
                        vm.TogglePlayPauseCommand.Execute(null);
                }
            });
        }
        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }
}
