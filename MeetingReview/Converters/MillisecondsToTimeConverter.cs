using System.Globalization;
using System.Windows.Data;

namespace MeetingReview.Converters;

[ValueConversion(typeof(long), typeof(string))]
public class MillisecondsToTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is long ms && ms >= 0
            ? TimeSpan.FromMilliseconds(ms).ToString(@"hh\:mm\:ss")
            : "00:00:00";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
