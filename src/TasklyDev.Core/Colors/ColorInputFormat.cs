namespace TasklyDev.Core.Colors;

/// <summary>Identifies the color notation selected for input parsing.</summary>
public enum ColorInputFormat
{
    /// <summary>Hexadecimal channel notation.</summary>
    Hex,
    /// <summary>Red, green, blue, and optional alpha notation.</summary>
    Rgb,
    /// <summary>Hue, saturation, lightness, and optional alpha notation.</summary>
    Hsl
}
