namespace TasklyDev.Windows.ScreenCapture;

/// <summary>Describes a rectangle in virtual-desktop screen coordinates.</summary>
public readonly record struct VirtualDesktopBounds(int X, int Y, int Width, int Height);
