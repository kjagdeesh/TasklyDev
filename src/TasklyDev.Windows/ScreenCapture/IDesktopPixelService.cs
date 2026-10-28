using TasklyDev.Core.Colors;

namespace TasklyDev.Windows.ScreenCapture;

/// <summary>Reads virtual desktop geometry, cursor position, and screen pixel colors.</summary>
public interface IDesktopPixelService
{
    /// <summary>Gets the bounds of all monitors in virtual-desktop coordinates.</summary>
    VirtualDesktopBounds GetVirtualDesktopBounds();
    /// <summary>Gets the current cursor position in screen coordinates.</summary>
    (int X, int Y) GetCursorPosition();
    /// <summary>Samples the color of a pixel at the specified screen coordinate.</summary>
    ColorValue SamplePixel(int x, int y);
}
