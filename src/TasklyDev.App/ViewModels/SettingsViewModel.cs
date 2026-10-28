using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using TasklyDev.Services;

namespace TasklyDev.ViewModels;

/// <summary>Exposes theme preferences and application information to the settings screen.</summary>
public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly IThemeService _themeService;
    private ThemePreference _selectedTheme;
    private string _statusMessage = string.Empty;
    private bool _isError;

    /// <summary>Creates the settings view model and subscribes to system-theme notifications.</summary>
    public SettingsViewModel(IThemeService themeService)
    {
        _themeService = themeService;
        _selectedTheme = themeService.Preference;
        _themeService.SystemThemeChanged += OnSystemThemeChanged;
        _themeService.SystemThemeReadFailed += OnSystemThemeReadFailed;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Array ThemeOptions => Enum.GetValues<ThemePreference>();
    public string ProductName => "TasklyDev";
    public string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
    public string RuntimeVersion => $".NET {Environment.Version.Major}.{Environment.Version.Minor}";
    public string PrivacyStatement =>
        "TasklyDev runs utility operations locally. It does not send telemetry or upload tool inputs. Inputs and environment-variable values are not persisted.";
    public string LicenseStatement =>
        "TasklyDev uses .NET and Windows Presentation Foundation. Release distributions will include applicable third-party license notices.";

    public ThemePreference SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (_selectedTheme == value)
            {
                return;
            }

            var previousTheme = _selectedTheme;
            _selectedTheme = value;
            OnPropertyChanged();
            try
            {
                _themeService.SetPreference(value);
                IsError = false;
                StatusMessage = value == ThemePreference.System
                    ? "Following the Windows app theme. Theme preference is session-only."
                    : $"{value} theme applied. Theme preference is session-only.";
            }
            catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException or System.Security.SecurityException or IOException)
            {
                _selectedTheme = previousTheme;
                OnPropertyChanged();
                IsError = true;
                StatusMessage = "The selected theme could not be applied. " + exception.Message;
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value)
            {
                return;
            }

            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public bool IsError
    {
        get => _isError;
        private set
        {
            if (_isError == value)
            {
                return;
            }

            _isError = value;
            OnPropertyChanged();
        }
    }

    private void OnSystemThemeChanged(object? sender, EventArgs e)
    {
        if (SelectedTheme == ThemePreference.System)
        {
            StatusMessage = "Windows app theme changed; TasklyDev updated to match.";
            IsError = false;
        }
    }

    private void OnSystemThemeReadFailed(object? sender, EventArgs e) => SetSystemThemeUnavailable();

    /// <summary>Reports that the Windows theme preference could not be read.</summary>
    public void SetSystemThemeUnavailable()
    {
        IsError = true;
        StatusMessage = "Windows app appearance could not be read. The existing theme is kept; choose Light or Dark to set it manually.";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
