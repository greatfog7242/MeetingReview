using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;

namespace MeetingReview.Behaviors;

public class SeekSliderBehavior : Behavior<Slider>
{
    public static readonly DependencyProperty SeekCommandProperty =
        DependencyProperty.Register(nameof(SeekCommand), typeof(ICommand), typeof(SeekSliderBehavior));

    public ICommand? SeekCommand
    {
        get => (ICommand?)GetValue(SeekCommandProperty);
        set => SetValue(SeekCommandProperty, value);
    }

    protected override void OnAttached() =>
        AssociatedObject.AddHandler(Thumb.DragCompletedEvent,
            new DragCompletedEventHandler(OnDragCompleted));

    protected override void OnDetaching() =>
        AssociatedObject.RemoveHandler(Thumb.DragCompletedEvent,
            new DragCompletedEventHandler(OnDragCompleted));

    private void OnDragCompleted(object sender, DragCompletedEventArgs e)
    {
        var posMs = (long)AssociatedObject.Value;
        if (SeekCommand?.CanExecute(posMs) == true)
            SeekCommand.Execute(posMs);
    }
}
