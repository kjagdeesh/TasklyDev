namespace TasklyDev.Core.Colors;

public interface IColorService
{
    ColorValue ParseHex(string value);
    ColorValue ParseRgb(string value);
    ColorValue ParseHsl(string value);
    string ToHex(ColorValue color);
    string ToRgb(ColorValue color);
    string ToHsl(ColorValue color);
    HslValue ToHslValue(ColorValue color);
}
