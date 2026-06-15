using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors;
using MeetingReview.ViewModels;

namespace MeetingReview.Behaviors;

public class ScrollToActiveWordBehavior : Behavior<FrameworkElement>
{
    public static readonly DependencyProperty TranscriptProperty =
        DependencyProperty.Register(nameof(Transcript), typeof(TranscriptViewModel),
            typeof(ScrollToActiveWordBehavior),
            new PropertyMetadata(null, OnTranscriptChanged));

    public TranscriptViewModel? Transcript
    {
        get => (TranscriptViewModel?)GetValue(TranscriptProperty);
        set => SetValue(TranscriptProperty, value);
    }

    private static void OnTranscriptChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (ScrollToActiveWordBehavior)d;
        if (e.OldValue is TranscriptViewModel old) old.PropertyChanged -= self.OnVmPropertyChanged;
        if (e.NewValue is TranscriptViewModel next) next.PropertyChanged += self.OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TranscriptViewModel.ActiveWord)) return;
        var target = Transcript?.ActiveWord;
        if (target == null) return;

        // Defer until after layout so the element is in the visual tree.
        AssociatedObject.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
        {
            var element = FindElement(AssociatedObject, target);
            if (element == null) return;

            if (AssociatedObject is not System.Windows.Controls.ScrollViewer sv)
            {
                element.BringIntoView();
                return;
            }

            try
            {
                // elementTop: Y position of the word relative to the visible viewport top.
                // Negative  → word is above the viewport.
                // > viewport → word is below the viewport.
                var elementTop = element.TransformToAncestor(sv).Transform(default).Y;
                var viewport   = sv.ViewportHeight;

                // Only scroll when the word enters the bottom third or leaves the viewport.
                // Target position: 1/3 from the top of the viewport.
                if (elementTop > viewport * 2.0 / 3.0 || elementTop < 0)
                {
                    var targetOffset = sv.VerticalOffset + elementTop - viewport / 3.0;
                    sv.ScrollToVerticalOffset(Math.Max(0, targetOffset));
                }
            }
            catch (InvalidOperationException)
            {
                element.BringIntoView();
            }
        });
    }

    private static FrameworkElement? FindElement(DependencyObject parent, object dataContext)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement fe && fe.DataContext == dataContext)
                return fe;
            var found = FindElement(child, dataContext);
            if (found != null) return found;
        }
        return null;
    }
}
