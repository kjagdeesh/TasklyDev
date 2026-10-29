using System.Globalization;
using System.Windows.Data;

namespace TasklyDev.Views;

public sealed class ClipboardHistoryTitleConverter : IValueConverter
{
    private const int MaximumTitleLength = 120;

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

        if (title.Length == 0)
        {
            return "(Whitespace-only clipboard text)";
        }

        return title.Length <= MaximumTitleLength
            ? title
            : string.Concat(title.AsSpan(0, MaximumTitleLength - 1), "\u2026");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
