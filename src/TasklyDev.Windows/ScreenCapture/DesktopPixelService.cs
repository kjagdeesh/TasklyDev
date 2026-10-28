using System.ComponentModel;
using System.Runtime.InteropServices;
using TasklyDev.Core.Colors;

namespace TasklyDev.Windows.ScreenCapture;

/// <summary>Uses Windows APIs to read desktop bounds, cursor position, and pixel colors.</summary>
public sealed class DesktopPixelService : IDesktopPixelService
{
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;
    private const uint InvalidColorRef = uint.MaxValue;

    /// <inheritdoc />
    public VirtualDesktopBounds GetVirtualDesktopBounds()
    {
        var bounds = new VirtualDesktopBounds(
            GetSystemMetrics(SmXVirtualScreen),
            GetSystemMetrics(SmYVirtualScreen),
            GetSystemMetrics(SmCxVirtualScreen),
            GetSystemMetrics(SmCyVirtualScreen));
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new InvalidOperationException("The virtual desktop dimensions could not be read.");
        }

        return bounds;
    }

    /// <inheritdoc />
    public (int X, int Y) GetCursorPosition()
    {
        if (!GetCursorPos(out var point))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "The screen cursor position could not be read.");
        }

        return (point.X, point.Y);
    }

    /// <inheritdoc />
    public ColorValue SamplePixel(int x, int y)
    {
        var bounds = GetVirtualDesktopBounds();
        if (x < bounds.X || y < bounds.Y || (long)x >= (long)bounds.X + bounds.Width || (long)y >= (long)bounds.Y + bounds.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "The selected point is outside the virtual desktop.");
        }

        var screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "The screen device context could not be opened.");
        }

        try
        {
            var colorRef = GetPixel(screenDc, x, y);
            if (colorRef == InvalidColorRef)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "The selected screen pixel could not be sampled.");
            }

            return new ColorValue(
                (byte)(colorRef & 0xFF),
                (byte)((colorRef >> 8) & 0xFF),
                (byte)((colorRef >> 16) & 0xFF));
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetDC(IntPtr window);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern uint GetPixel(IntPtr deviceContext, int x, int y);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int ReleaseDC(IntPtr window, IntPtr deviceContext);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint
    {
        public readonly int X;
        public readonly int Y;
    }
}
