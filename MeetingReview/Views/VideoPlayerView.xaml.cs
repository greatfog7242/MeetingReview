using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MeetingReview.ViewModels;

namespace MeetingReview.Views;

public partial class VideoPlayerView : System.Windows.Controls.UserControl
{
    private bool _isDragging;
    private Point _dragStart;

    public VideoPlayerView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is VideoPlayerViewModel vm)
        {
            vm.Initialize(VideoElement);
            vm.ZoomResetRequested += (_, _) => ResetZoomTransform();
        }
        Keyboard.Focus(this);
    }

    private void VideoContainer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is VideoPlayerViewModel vm && !vm.IsZoomMode)
            vm.TogglePlayPauseCommand.Execute(null);
    }

    private void SelectionCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(VideoContainer);
        _isDragging = true;
        SelectionCanvas.CaptureMouse();
        Canvas.SetLeft(SelectionRect, _dragStart.X);
        Canvas.SetTop(SelectionRect,  _dragStart.Y);
        SelectionRect.Width  = 0;
        SelectionRect.Height = 0;
        SelectionRect.Visibility = Visibility.Visible;
    }

    private void SelectionCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;
        var cur = e.GetPosition(VideoContainer);
        Canvas.SetLeft(SelectionRect, Math.Min(cur.X, _dragStart.X));
        Canvas.SetTop(SelectionRect,  Math.Min(cur.Y, _dragStart.Y));
        SelectionRect.Width  = Math.Abs(cur.X - _dragStart.X);
        SelectionRect.Height = Math.Abs(cur.Y - _dragStart.Y);
    }

    private void SelectionCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        SelectionCanvas.ReleaseMouseCapture();
        SelectionRect.Visibility = Visibility.Collapsed;
        e.Handled = true;

        var end  = e.GetPosition(VideoContainer);
        double selX = Math.Min(end.X, _dragStart.X), selW = Math.Abs(end.X - _dragStart.X);
        double selY = Math.Min(end.Y, _dragStart.Y), selH = Math.Abs(end.Y - _dragStart.Y);

        if (selW > 4 && selH > 4)
            ApplyZoom(selX, selY, selW, selH);
        else if (DataContext is VideoPlayerViewModel vm)
            vm.ResetZoomCommand.Execute(null);
    }

    private void ApplyZoom(double selX, double selY, double selW, double selH)
    {
        double cW = VideoContainer.ActualWidth;
        double cH = VideoContainer.ActualHeight;
        if (cW <= 0 || cH <= 0) return;

        double videoAspect = (VideoElement.NaturalVideoWidth > 0 && VideoElement.NaturalVideoHeight > 0)
            ? (double)VideoElement.NaturalVideoWidth / VideoElement.NaturalVideoHeight
            : cW / cH;
        double containerAspect = cW / cH;

        double lbX, lbY, lbW, lbH;
        if (videoAspect > containerAspect)
        {
            lbW = cW;  lbH = cW / videoAspect;
            lbX = 0;   lbY = (cH - lbH) / 2.0;
        }
        else
        {
            lbH = cH;  lbW = cH * videoAspect;
            lbY = 0;   lbX = (cW - lbW) / 2.0;
        }

        double clampedX = Math.Max(selX, lbX);
        double clampedY = Math.Max(selY, lbY);
        double clampedR = Math.Min(selX + selW, lbX + lbW);
        double clampedB = Math.Min(selY + selH, lbY + lbH);

        if (clampedR - clampedX < 4 || clampedB - clampedY < 4)
        {
            if (DataContext is VideoPlayerViewModel vm) vm.ResetZoomCommand.Execute(null);
            return;
        }

        double scale = Math.Min(cW / (clampedR - clampedX), cH / (clampedB - clampedY));
        VideoScale.ScaleX    = scale;
        VideoScale.ScaleY    = scale;
        VideoTranslate.X = -clampedX * scale;
        VideoTranslate.Y = -clampedY * scale;

        if (DataContext is VideoPlayerViewModel vm2) vm2.ApplyZoomRect();
    }

    private void ResetZoomTransform()
    {
        VideoScale.ScaleX = VideoScale.ScaleY = 1.0;
        VideoTranslate.X  = VideoTranslate.Y  = 0;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is VideoPlayerViewModel vm && vm.IsZoomMode)
        {
            _isDragging = false;
            SelectionRect.Visibility = Visibility.Collapsed;
            vm.ResetZoomCommand.Execute(null);
        }
    }
}
