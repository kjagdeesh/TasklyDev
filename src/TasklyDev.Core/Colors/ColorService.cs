using System.Globalization;
using System.Text.RegularExpressions;

namespace TasklyDev.Core.Colors;

public sealed partial class ColorService : IColorService
{
    public ColorValue ParseHex(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var hex = value.Trim();
        if (hex.StartsWith('#'))
        {
            hex = hex[1..];
        }

        if (hex.Length is 3 or 4)
        {
            var expanded = string.Concat(hex.Select(character => new string(character, 2)));
            hex = expanded;
        }

        if (hex.Length is not (6 or 8) || !hex.All(Uri.IsHexDigit))
        {
            throw new FormatException("Enter #RGB, #RGBA, #RRGGBB, or #RRGGBBAA hexadecimal.");
        }

        var red = ParseByte(hex.AsSpan(0, 2));
        var green = ParseByte(hex.AsSpan(2, 2));
        var blue = ParseByte(hex.AsSpan(4, 2));
        var alpha = hex.Length == 8 ? ParseByte(hex.AsSpan(6, 2)) : byte.MaxValue;
        return new ColorValue(red, green, blue, alpha);
    }

    public ColorValue ParseRgb(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var match = RgbPattern().Match(value.Trim());
        if (!match.Success)
        {
            throw new FormatException("Enter RGB as rgb(0, 128, 255) or RGBA with alpha from 0 to 255.");
        }

        if (match.Groups["fn"].Value.Equals("rgba", StringComparison.OrdinalIgnoreCase) != match.Groups["a"].Success)
        {
            throw new FormatException("Use rgb() without alpha or rgba() with alpha from 0 to 255.");
        }

        var red = ParseChannel(match.Groups["r"].Value, "red");
        var green = ParseChannel(match.Groups["g"].Value, "green");
        var blue = ParseChannel(match.Groups["b"].Value, "blue");
        var alpha = match.Groups["a"].Success
            ? ParseChannel(match.Groups["a"].Value, "alpha")
            : byte.MaxValue;
        return new ColorValue(red, green, blue, alpha);
    }

    public ColorValue ParseHsl(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var match = HslPattern().Match(value.Trim());
        if (!match.Success)
        {
            throw new FormatException("Enter HSL as hsl(210, 50%, 40%) or HSLA with alpha from 0 to 1.");
        }

        if (match.Groups["fn"].Value.Equals("hsla", StringComparison.OrdinalIgnoreCase) != match.Groups["a"].Success)
        {
            throw new FormatException("Use hsl() without alpha or hsla() with alpha from 0 to 1.");
        }

        var hue = ParseNumber(match.Groups["h"].Value, "hue");
        var saturation = ParsePercentage(match.Groups["s"].Value, "saturation");
        var lightness = ParsePercentage(match.Groups["l"].Value, "lightness");
        var alpha = match.Groups["a"].Success
            ? ParseUnitAlpha(match.Groups["a"].Value)
            : byte.MaxValue;

        if (hue is < 0 or > 360)
        {
            throw new FormatException("Hue must be between 0 and 360 degrees.");
        }

        var h = hue == 360 ? 0 : hue;
        var s = saturation / 100;
        var l = lightness / 100;
        var chroma = (1 - Math.Abs((2 * l) - 1)) * s;
        var huePrime = h / 60;
        var x = chroma * (1 - Math.Abs((huePrime % 2) - 1));
        var (r1, g1, b1) = huePrime switch
        {
            < 1 => (chroma, x, 0d),
            < 2 => (x, chroma, 0d),
            < 3 => (0d, chroma, x),
            < 4 => (0d, x, chroma),
            < 5 => (x, 0d, chroma),
            _ => (chroma, 0d, x)
        };
        var m = l - (chroma / 2);

        return new ColorValue(ToByte((r1 + m) * 255), ToByte((g1 + m) * 255), ToByte((b1 + m) * 255), alpha);
    }

    public string ToHex(ColorValue color) =>
        color.Alpha == byte.MaxValue
            ? $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}"
            : $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}{color.Alpha:X2}";

    public string ToRgb(ColorValue color) =>
        color.Alpha == byte.MaxValue
            ? string.Create(CultureInfo.InvariantCulture, $"rgb({color.Red}, {color.Green}, {color.Blue})")
            : string.Create(CultureInfo.InvariantCulture, $"rgba({color.Red}, {color.Green}, {color.Blue}, {color.Alpha})");

    public string ToHsl(ColorValue color)
    {
        var hsl = ToHslValue(color);
        var roundedHue = Math.Round(hsl.Hue, 1, MidpointRounding.AwayFromZero);
        var hue = (roundedHue >= 360 ? 0 : roundedHue).ToString("0.#", CultureInfo.InvariantCulture);
        var saturation = hsl.Saturation.ToString("0.#", CultureInfo.InvariantCulture);
        var lightness = hsl.Lightness.ToString("0.#", CultureInfo.InvariantCulture);
        return color.Alpha == byte.MaxValue
            ? $"hsl({hue}, {saturation}%, {lightness}%)"
            : $"hsla({hue}, {saturation}%, {lightness}%, {(color.Alpha / 255d).ToString("0.###", CultureInfo.InvariantCulture)})";
    }

    public HslValue ToHslValue(ColorValue color)
    {
        var red = color.Red / 255d;
        var green = color.Green / 255d;
        var blue = color.Blue / 255d;
        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        var delta = max - min;
        var lightness = (max + min) / 2;

        if (delta == 0)
        {
            return new HslValue(0, 0, lightness * 100);
        }

        var saturation = delta / (1 - Math.Abs((2 * lightness) - 1));
        var hue = max switch
        {
            var value when value == red => 60 * (((green - blue) / delta) % 6),
            var value when value == green => 60 * (((blue - red) / delta) + 2),
            _ => 60 * (((red - green) / delta) + 4)
        };

        if (hue < 0)
        {
            hue += 360;
        }

        return new HslValue(hue, saturation * 100, lightness * 100);
    }

    private static byte ParseByte(ReadOnlySpan<char> value) =>
        byte.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static byte ParseChannel(string value, string channelName)
    {
        if (!byte.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var channel))
        {
            throw new FormatException($"{channelName} channel must be an integer from 0 to 255.");
        }

        return channel;
    }

    private static double ParseNumber(string value, string name)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ||
            !double.IsFinite(number))
        {
            throw new FormatException($"Enter a valid {name} value.");
        }

        return number;
    }

    private static double ParsePercentage(string value, string name)
    {
        var text = value.Trim();
        if (!text.EndsWith('%') ||
            !double.TryParse(text[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ||
            !double.IsFinite(number) ||
            number is < 0 or > 100)
        {
            throw new FormatException($"{name} must be a percentage from 0% to 100%.");
        }

        return number;
    }

    private static byte ParseUnitAlpha(string value)
    {
        var alpha = ParseNumber(value, "alpha");
        if (alpha is < 0 or > 1)
        {
            throw new FormatException("HSL alpha must be between 0 and 1.");
        }

        return ToByte(alpha * 255);
    }

    private static byte ToByte(double value) => (byte)Math.Round(value, MidpointRounding.AwayFromZero);

    [GeneratedRegex(@"^(?<fn>rgba?)\s*\(\s*(?<r>\d+)\s*,\s*(?<g>\d+)\s*,\s*(?<b>\d+)(?:\s*,\s*(?<a>\d+))?\s*\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RgbPattern();

    [GeneratedRegex(@"^(?<fn>hsla?)\s*\(\s*(?<h>[+-]?(?:\d+(?:\.\d*)?|\.\d+))\s*,\s*(?<s>[+-]?(?:\d+(?:\.\d*)?|\.\d+)\s*%)\s*,\s*(?<l>[+-]?(?:\d+(?:\.\d*)?|\.\d+)\s*%)(?:\s*,\s*(?<a>[+-]?(?:\d+(?:\.\d*)?|\.\d+)))?\s*\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HslPattern();
}
