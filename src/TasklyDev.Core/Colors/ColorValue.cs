namespace TasklyDev.Core.Colors;

/// <summary>Represents red, green, blue, and alpha channels on an 8-bit scale.</summary>
public readonly record struct ColorValue(byte Red, byte Green, byte Blue, byte Alpha = byte.MaxValue);

/// <summary>Represents hue in degrees and normalized saturation and lightness.</summary>
public readonly record struct HslValue(double Hue, double Saturation, double Lightness);
