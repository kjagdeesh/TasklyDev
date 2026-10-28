using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using TasklyDev.ViewModels;

namespace TasklyDev.Services;

/// <summary>Applies WPF theme resources and optionally follows the Windows app theme.</summary>
public sealed class WpfThemeService : IThemeService, IDisposable
{
    private const string PersonalizationKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValue = "AppsUseLightTheme";
    private readonly Application? _application;
    private ThemePreference _preference = ThemePreference.System;
    private bool _disposed;

    /// <summary>Creates a theme service for the supplied application or the current WPF application.</summary>
    /// <param name="application">Application whose merged resources will be updated.</param>
    public WpfThemeService(Application? application = null)
    {
        _application = application ?? Application.Current;
        if (_application is not null)
        {
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }
    }

    public ThemePreference Preference => _preference;

    public event EventHandler? SystemThemeChanged;
    public event EventHandler? SystemThemeReadFailed;

    /// <inheritdoc />
    public void SetPreference(ThemePreference preference)
    {
        if (!Enum.IsDefined(preference))
        {
            throw new ArgumentOutOfRangeException(nameof(preference));
        }

        ApplyTheme(ResolveTheme(preference));
        _preference = preference;
    }

    /// <summary>Applies the service's initial theme preference to the WPF resources.</summary>
    public void ApplyInitialPreference() => ApplyTheme(ResolveTheme(_preference));

    private static ThemePreference ResolveTheme(ThemePreference preference)
    {
        if (preference != ThemePreference.System)
        {
            return preference;
        }

        using var key = Registry.CurrentUser.OpenSubKey(PersonalizationKey, writable: false);
        var value = key?.GetValue(AppsUseLightThemeValue);
        return value is int lightTheme && lightTheme == 0 ? ThemePreference.Dark : ThemePreference.Light;
    }

    private void ApplyTheme(ThemePreference theme)
    {
        if (_application is null)
        {
            return;
        }

        var uri = new Uri(
            theme == ThemePreference.Dark
                ? "pack://application:,,,/TasklyDev;component/Themes/DarkTheme.xaml"
                : "pack://application:,,,/TasklyDev;component/Themes/LightTheme.xaml",
            UriKind.Absolute);
        var dictionaries = _application.Resources.MergedDictionaries;
        var index = -1;
        for (var dictionaryIndex = 0; dictionaryIndex < dictionaries.Count; dictionaryIndex++)
        {
            if (dictionaries[dictionaryIndex].Source?.OriginalString.Contains("/Themes/", StringComparison.OrdinalIgnoreCase) == true)
            {
                index = dictionaryIndex;
                break;
            }
        }

        var replacement = new ResourceDictionary { Source = uri };
        if (index >= 0)
        {
            dictionaries[index] = replacement;
        }
        else
        {
            dictionaries.Add(replacement);
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (_application is null || _preference != ThemePreference.System || e.Category != UserPreferenceCategory.General)
        {
            return;
        }

        _application.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
        {
            if (_preference == ThemePreference.System)
            {
                try
                {
                    ApplyTheme(ResolveTheme(ThemePreference.System));
                    SystemThemeChanged?.Invoke(this, EventArgs.Empty);
                }
                catch (UnauthorizedAccessException)
                {
                    SystemThemeReadFailed?.Invoke(this, EventArgs.Empty);
                }
                catch (System.Security.SecurityException)
                {
                    SystemThemeReadFailed?.Invoke(this, EventArgs.Empty);
                }
                catch (IOException)
                {
                    SystemThemeReadFailed?.Invoke(this, EventArgs.Empty);
                }
            }
        }));
    }

    /// <summary>Unsubscribes from Windows theme notifications.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_application is not null)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        }
        _disposed = true;
    }
}
