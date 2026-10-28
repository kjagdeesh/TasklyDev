using TasklyDev.ViewModels;

namespace TasklyDev.Services;

/// <summary>Manages the active application theme and tracks Windows theme changes.</summary>
public interface IThemeService
{
    /// <summary>Gets the current theme preference.</summary>
    ThemePreference Preference { get; }
    /// <summary>Raised when the Windows app theme changes while following the system preference.</summary>
    event EventHandler? SystemThemeChanged;
    /// <summary>Raised when the Windows app theme cannot be read.</summary>
    event EventHandler? SystemThemeReadFailed;
    /// <summary>Sets the theme preference and applies the corresponding theme.</summary>
    void SetPreference(ThemePreference preference);
}
