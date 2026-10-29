using System.Globalization;
using System.Windows.Data;

namespace TasklyDev.Views;

public sealed class ClipboardHistorySizeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long byteCount || byteCount < 0)
        {
            return string.Empty;
        }

        if (byteCount < 1024)
        {
            return string.Format(culture, "{0:N0} B", byteCount);
        }

        var kilobytes = byteCount / 1024d;
        return kilobytes < 1024
            ? string.Format(culture, "{0:N2} KB", kilobytes)
            : string.Format(culture, "{0:N2} MB", kilobytes / 1024d);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
