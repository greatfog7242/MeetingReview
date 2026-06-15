using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MeetingReview.ViewModels;

namespace MeetingReview.Views;

public partial class TranscriptView : UserControl
{
    public TranscriptView() => InitializeComponent();

    private void WordTextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox tb && (bool)e.NewValue)
        {
            tb.Focus();
            tb.SelectAll();
        }
    }

    private void WordTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb || tb.DataContext is not WordViewModel word) return;
        if (e.Key is Key.Return or Key.Tab)
        {
            word.CommitEdit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            word.CancelEdit();
            e.Handled = true;
        }
    }

    private void WordTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is WordViewModel word)
            word.CommitEdit();
    }
}
