using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TasklyDev.Services;
using TasklyDev.ViewModels;

namespace TasklyDev;

/// <summary>Hosts the application shell, tool navigation, and shared theme resources.</summary>
public partial class MainWindow : Window
{
    private const int ClipboardUpdateMessage = 0x031D;
    private const int TrayCallbackMessage = 0x8000 + 0x36;
    private const int ClipboardUnicodeTextFormat = 13;
    private const int MaximumClipboardUtf16Characters = 1_048_576;
    private const uint NotifyIconAdd = 0;
    private const uint NotifyIconDelete = 2;
    private const uint NotifyIconMessage = 1;
    private const uint NotifyIconIcon = 2;
    private const uint NotifyIconTip = 4;
    private const int TrayIconId = 1;
    private const int TrayLeftButtonUp = 0x0202;
    private const int TrayLeftButtonDoubleClick = 0x0203;
    private const int TrayRightButtonUp = 0x0205;
    private readonly WpfThemeService _themeService;
    private readonly MainWindowViewModel _viewModel;
    private readonly ContextMenu _trayMenu = new();
    private HwndSource? _windowSource;
    private bool _clipboardListenerRegistered;
    private bool _trayIconAdded;
    private bool _allowClose;
    private bool _historyInitialized;

    /// <summary>Creates the main window, view models, and initial theme state.</summary>
    public MainWindow()
        : this(null)
    {
    }

    public MainWindow(MainWindowViewModel? viewModel)
    {
        InitializeComponent();
        _themeService = new WpfThemeService(Application.Current);
        _viewModel = viewModel ?? new MainWindowViewModel(themeService: _themeService);
        DataContext = _viewModel;
        _viewModel.ClipboardHistoryViewModel.CaptureStateChanged += ClipboardHistory_CaptureStateChanged;
        SourceInitialized += MainWindow_SourceInitialized;
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        try
        {
            _themeService.ApplyInitialPreference();
        }
        catch (UnauthorizedAccessException)
        {
            _viewModel.SettingsViewModel.SetSystemThemeUnavailable();
        }
        catch (System.Security.SecurityException)
        {
            _viewModel.SettingsViewModel.SetSystemThemeUnavailable();
        }
        catch (System.IO.IOException)
        {
            _viewModel.SettingsViewModel.SetSystemThemeUnavailable();
        }

        Closed += MainWindow_Closed;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_historyInitialized)
        {
            return;
        }

        _historyInitialized = true;
        await _viewModel.ClipboardHistoryViewModel.InitializeAsync();
        SetClipboardListener(_viewModel.ClipboardHistoryViewModel.IsCapturing);
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        _windowSource = (HwndSource?)PresentationSource.FromVisual(this);
        _windowSource?.AddHook(WindowMessageHook);
    }

    private void ClipboardHistory_CaptureStateChanged(object? sender, bool isCapturing) =>
        SetClipboardListener(isCapturing);

    private void SetClipboardListener(bool isEnabled)
    {
        if (_windowSource is null || _clipboardListenerRegistered == isEnabled)
        {
            return;
        }

        if (isEnabled)
        {
            if (!AddClipboardFormatListener(_windowSource.Handle))
            {
                _viewModel.ClipboardHistoryViewModel.ReportClipboardReadFailure(
                    new Win32Exception(Marshal.GetLastWin32Error()).Message);
                return;
            }

            _clipboardListenerRegistered = true;
        }
        else
        {
            RemoveClipboardFormatListener(_windowSource.Handle);
            _clipboardListenerRegistered = false;
        }
    }

    private IntPtr WindowMessageHook(IntPtr handle, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == ClipboardUpdateMessage && _clipboardListenerRegistered)
        {
            _ = CaptureClipboardTextAsync();
            return IntPtr.Zero;
        }

        if (message == TrayCallbackMessage && _trayIconAdded)
        {
            var trayMessage = unchecked((int)lParam.ToInt64());
            if (trayMessage is TrayLeftButtonUp or TrayLeftButtonDoubleClick)
            {
                RestoreFromTray();
            }
            else if (trayMessage == TrayRightButtonUp)
            {
                ShowTrayMenu();
            }

            handled = true;
        }

        return IntPtr.Zero;
    }

    private async Task CaptureClipboardTextAsync()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await Task.Delay(40);
                if (!_clipboardListenerRegistered)
                {
                    return;
                }

                var (text, isTooLarge) = ReadClipboardText();
                if (isTooLarge)
                {
                    _viewModel.ClipboardHistoryViewModel.ReportOversizedClipboardText();
                    return;
                }

                if (text is not null)
                {
                    await _viewModel.ClipboardHistoryViewModel.CaptureTextAsync(text);
                }

                return;
            }
            catch (Win32Exception exception) when (attempt < 2)
            {
                await Task.Delay(50);
                if (attempt == 1)
                {
                    _viewModel.ClipboardHistoryViewModel.ReportClipboardReadFailure(exception.Message);
                }
            }
            catch (Win32Exception exception)
            {
                _viewModel.ClipboardHistoryViewModel.ReportClipboardReadFailure(exception.Message);
                return;
            }
            catch (ExternalException exception)
            {
                _viewModel.ClipboardHistoryViewModel.ReportClipboardReadFailure(exception.Message);
                return;
            }
        }
    }

    private (string? Text, bool IsTooLarge) ReadClipboardText()
    {
        if (!IsClipboardFormatAvailable(ClipboardUnicodeTextFormat))
        {
            return (null, false);
        }

        if (!OpenClipboard(_windowSource!.Handle))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "The Windows clipboard is temporarily busy.");
        }

        IntPtr globalHandle = IntPtr.Zero;
        var isLocked = false;
        try
        {
            globalHandle = GetClipboardData(ClipboardUnicodeTextFormat);
            if (globalHandle == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not read clipboard text.");
            }

            var byteSize = GlobalSize(globalHandle).ToUInt64();
            if (byteSize < sizeof(char))
            {
                return (null, false);
            }

            var pointer = GlobalLock(globalHandle);
            if (pointer == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not access clipboard text.");
            }

            isLocked = true;
            var characterCount = (int)Math.Min(
                byteSize / sizeof(char),
                (ulong)MaximumClipboardUtf16Characters + 1);
            var clipboardText = Marshal.PtrToStringUni(pointer, characterCount);
            var terminatorIndex = clipboardText?.IndexOf('\0') ?? -1;
            if (terminatorIndex < 0)
            {
                if (characterCount > MaximumClipboardUtf16Characters)
                {
                    return (null, true);
                }

                throw new Win32Exception("Clipboard Unicode text was not null-terminated.");
            }

            return (clipboardText![..terminatorIndex], false);
        }
        finally
        {
            if (isLocked)
            {
                GlobalUnlock(globalHandle);
            }

            CloseClipboard();
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            SetClipboardListener(false);
            return;
        }

        if (!EnsureTrayIcon())
        {
            MessageBox.Show(
                this,
                "TasklyDev could not create a system-tray icon. The application will close, and clipboard capture will stop.",
                "TasklyDev",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            _allowClose = true;
            SetClipboardListener(false);
            return;
        }

        e.Cancel = true;
        ShowInTaskbar = false;
        Hide();
    }

    /// <summary>Allows Windows shutdown to close the window instead of hiding it to the tray.</summary>
    public void PrepareForApplicationExit()
    {
        _allowClose = true;
        SetClipboardListener(false);
        RemoveTrayIcon();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        SetClipboardListener(false);
        RemoveTrayIcon();
        _windowSource?.RemoveHook(WindowMessageHook);
        _viewModel.ClipboardHistoryViewModel.CaptureStateChanged -= ClipboardHistory_CaptureStateChanged;
        _themeService.Dispose();
    }

    private bool EnsureTrayIcon()
    {
        if (_trayIconAdded)
        {
            return true;
        }

        var data = new NativeNotifyIconData
        {
            Size = (uint)Marshal.SizeOf<NativeNotifyIconData>(),
            WindowHandle = new WindowInteropHelper(this).Handle,
            Id = TrayIconId,
            Flags = NotifyIconMessage | NotifyIconIcon | NotifyIconTip,
            CallbackMessage = TrayCallbackMessage,
            IconHandle = LoadIcon(IntPtr.Zero, new IntPtr(32512)),
            Tip = "TasklyDev is running in the system tray."
        };
        _trayIconAdded = data.IconHandle != IntPtr.Zero && Shell_NotifyIcon(NotifyIconAdd, ref data);
        return _trayIconAdded;
    }

    private void RemoveTrayIcon()
    {
        if (!_trayIconAdded)
        {
            return;
        }

        var data = new NativeNotifyIconData
        {
            Size = (uint)Marshal.SizeOf<NativeNotifyIconData>(),
            WindowHandle = new WindowInteropHelper(this).Handle,
            Id = TrayIconId
        };
        Shell_NotifyIcon(NotifyIconDelete, ref data);
        _trayIconAdded = false;
    }

    private void ShowTrayMenu()
    {
        _trayMenu.Items.Clear();
        var restore = new MenuItem { Header = "Open TasklyDev" };
        restore.Click += (_, _) => RestoreFromTray();
        var exit = new MenuItem { Header = "Quit TasklyDev" };
        exit.Click += (_, _) => ExitFromTray();
        _trayMenu.Items.Add(restore);
        _trayMenu.Items.Add(new Separator());
        _trayMenu.Items.Add(exit);
        _trayMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        _trayMenu.IsOpen = true;
    }

    private void RestoreFromTray()
    {
        RemoveTrayIcon();
        ShowInTaskbar = true;
        Show();
        WindowState = WindowState.Maximized;
        Activate();
    }

    private void ExitFromTray()
    {
        _allowClose = true;
        SetClipboardListener(false);
        RemoveTrayIcon();
        Close();
    }

    private void BrandLogo_Loaded(object sender, RoutedEventArgs e)
    {
        var logoPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "TasklyDevLogo.png");
        if (!System.IO.File.Exists(logoPath))
        {
            return;
        }

        var logo = new BitmapImage();
        logo.BeginInit();
        logo.CacheOption = BitmapCacheOption.OnLoad;
        logo.UriSource = new Uri(logoPath, UriKind.Absolute);
        logo.EndInit();
        BrandLogo.Source = logo;
        Icon = logo;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && SearchBox.IsKeyboardFocusWithin)
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.SearchText = string.Empty;
            }

            e.Handled = true;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddClipboardFormatListener(IntPtr windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveClipboardFormatListener(IntPtr windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr ownerWindow);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint format);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr globalMemory);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr globalMemory);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern UIntPtr GlobalSize(IntPtr globalMemory);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadIcon(IntPtr instance, IntPtr iconName);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIcon(uint message, ref NativeNotifyIconData data);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeNotifyIconData
    {
        public uint Size;
        public IntPtr WindowHandle;
        public uint Id;
        public uint Flags;
        public int CallbackMessage;
        public IntPtr IconHandle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string? Tip;
        public uint State;
        public uint StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string? Info;
        public uint TimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string? InfoTitle;
        public uint InfoFlags;
        public Guid ItemGuid;
        public IntPtr BalloonIcon;
    }
}
