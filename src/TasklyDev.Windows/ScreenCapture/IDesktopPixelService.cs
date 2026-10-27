using TasklyDev.Core.Colors;

namespace TasklyDev.Windows.ScreenCapture;

public interface IDesktopPixelService
{
    VirtualDesktopBounds GetVirtualDesktopBounds();
    (int X, int Y) GetCursorPosition();
    ColorValue SamplePixel(int x, int y);
}
