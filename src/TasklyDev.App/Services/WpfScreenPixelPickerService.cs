using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using TasklyDev.Core.Colors;
using TasklyDev.Windows.ScreenCapture;

namespace TasklyDev.Services;

public sealed class WpfScreenPixelPickerService(IDesktopPixelService desktop) : IScreenPixelPickerService
{
    public async Task<ColorValue?> PickPixelAsync()
    {
        var bounds = desktop.GetVirtualDesktopBounds();
        var overlay = new PixelPickerOverlayWindow(bounds, desktop);
        var owner = Application.Current?.MainWindow;
        if (owner is not null && owner.IsVisible)
        {
            overlay.Owner = owner;
        }

        var confirmed = overlay.ShowDialog() == true;
        if (overlay.CaptureError is not null)
        {
            throw new Win32Exception(overlay.CaptureError.NativeErrorCode, overlay.CaptureError.Message);
        }

        if (!confirmed || overlay.SelectedPoint is not { } point)
        {
            return null;
        }

        await Task.Delay(80);
        return desktop.SamplePixel(point.X, point.Y);
    }

    private sealed class PixelPickerOverlayWindow : Window
    {
        private const int HwndTopmost = -1;
        private const uint SwpShowWindow = 0x0040;
        private readonly VirtualDesktopBounds _bounds;
        private readonly IDesktopPixelService _desktop;

        public PixelPickerOverlayWindow(VirtualDesktopBounds bounds, IDesktopPixelService desktop)
        {
            _bounds = bounds;
            _desktop = desktop;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.Manual;
            ShowInTaskbar = false;
            Topmost = true;
            AllowsTransparency = true;
            Background = new SolidColorBrush(Color.FromArgb(110, 20, 22, 27));
            Left = 0;
            Top = 0;
            Width = 400;
            Height = 200;
            Content = BuildContent();
            Loaded += PositionOnVirtualDesktop;
            PreviewKeyDown += HandlePreviewKeyDown;
            PreviewMouseLeftButtonDown += HandleMouseClick;
            Deactivated += HandleDeactivated;
        }

        public (int X, int Y)? SelectedPoint { get; private set; }
        public Win32Exception? CaptureError { get; private set; }

        private static UIElement BuildContent()
        {
            var instruction = new TextBlock
            {
                Text = "Click anywhere to pick a pixel\nPress Esc to cancel",
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(235, 25, 28, 31)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(245, 185, 66)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(28, 22, 28, 22),
                Child = instruction,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var panel = new Grid();
            panel.Children.Add(card);
            return panel;
        }

        private void PositionOnVirtualDesktop(object sender, RoutedEventArgs e)
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (!SetWindowPos(
                    handle,
                    new IntPtr(HwndTopmost),
                    _bounds.X,
                    _bounds.Y,
                    _bounds.Width,
                    _bounds.Height,
                    SwpShowWindow))
            {
                CaptureError = new Win32Exception(Marshal.GetLastWin32Error(), "The pixel picker overlay could not cover the virtual desktop.");
                DialogResult = false;
            }
        }

        private void HandlePreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                e.Handled = true;
            }
        }

        private void HandleMouseClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                SelectedPoint = _desktop.GetCursorPosition();
                DialogResult = true;
                e.Handled = true;
            }
            catch (Win32Exception exception)
            {
                CaptureError = exception;
                DialogResult = false;
            }
        }

        private void HandleDeactivated(object? sender, EventArgs e)
        {
            if (IsVisible)
            {
                DialogResult = false;
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(
            IntPtr window,
            IntPtr insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);

    }
}
