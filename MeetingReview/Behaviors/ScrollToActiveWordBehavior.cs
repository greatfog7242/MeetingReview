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
            FindElement(AssociatedObject, target)?.BringIntoView();
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
