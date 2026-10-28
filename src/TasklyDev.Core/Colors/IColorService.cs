namespace TasklyDev.Core.Colors;

/// <summary>Parses and formats colors in HEX, RGB, and HSL representations.</summary>
public interface IColorService
{
    /// <summary>Parses a color represented by hexadecimal channel values.</summary>
    ColorValue ParseHex(string value);
    /// <summary>Parses a CSS-style RGB or RGBA color string.</summary>
    ColorValue ParseRgb(string value);
    /// <summary>Parses a CSS-style HSL or HSLA color string.</summary>
    ColorValue ParseHsl(string value);
    /// <summary>Formats a color as hexadecimal channels, including alpha when needed.</summary>
    string ToHex(ColorValue color);
    /// <summary>Formats a color as an RGB or RGBA string.</summary>
    string ToRgb(ColorValue color);
    /// <summary>Formats a color as an HSL or HSLA string.</summary>
    string ToHsl(ColorValue color);
    /// <summary>Converts RGB channels to normalized HSL components.</summary>
    HslValue ToHslValue(ColorValue color);
}
