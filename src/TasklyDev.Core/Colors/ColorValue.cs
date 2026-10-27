namespace TasklyDev.Core.Colors;

public readonly record struct ColorValue(byte Red, byte Green, byte Blue, byte Alpha = byte.MaxValue);

public readonly record struct HslValue(double Hue, double Saturation, double Lightness);
