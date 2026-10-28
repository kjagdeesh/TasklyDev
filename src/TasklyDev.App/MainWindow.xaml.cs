using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using TasklyDev.Services;
using TasklyDev.ViewModels;

namespace TasklyDev;

/// <summary>Hosts the application shell, tool navigation, and shared theme resources.</summary>
public partial class MainWindow : Window
{
    private readonly WpfThemeService _themeService;

    /// <summary>Creates the main window, view models, and initial theme state.</summary>
    public MainWindow()
    {
        InitializeComponent();
        _themeService = new WpfThemeService(Application.Current);
        var viewModel = new MainWindowViewModel(themeService: _themeService);
        DataContext = viewModel;
        try
        {
            _themeService.ApplyInitialPreference();
        }
        catch (UnauthorizedAccessException)
        {
            viewModel.SettingsViewModel.SetSystemThemeUnavailable();
        }
        catch (System.Security.SecurityException)
        {
            viewModel.SettingsViewModel.SetSystemThemeUnavailable();
        }
        catch (System.IO.IOException)
        {
            viewModel.SettingsViewModel.SetSystemThemeUnavailable();
        }

        Closed += (_, _) => _themeService.Dispose();
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
}
