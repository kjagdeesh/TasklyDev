using System.Globalization;
using System.Windows.Data;

namespace TasklyDev.Views;

public sealed class ClipboardHistoryTitleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string text)
        {
            return string.Empty;
        }

        if (text.Length == 0)
        {
            return "(Empty clipboard text)";
        }

        var title = text.TrimStart();
        var lineBreak = title.IndexOfAny(['\r', '\n']);
        if (lineBreak >= 0)
        {
            title = title[..lineBreak];
        }

        return title.Length == 0 ? "(Whitespace-only clipboard text)" : title;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
