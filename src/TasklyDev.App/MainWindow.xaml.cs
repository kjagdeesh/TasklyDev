using System.Windows;
using System.Windows.Input;
using TasklyDev.Services;
using TasklyDev.ViewModels;

namespace TasklyDev;

public partial class MainWindow : Window
{
    private readonly WpfThemeService _themeService;

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
