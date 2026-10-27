using TasklyDev.ViewModels;

namespace TasklyDev.Services;

public interface IThemeService
{
    ThemePreference Preference { get; }
    event EventHandler? SystemThemeChanged;
    event EventHandler? SystemThemeReadFailed;
    void SetPreference(ThemePreference preference);
}
