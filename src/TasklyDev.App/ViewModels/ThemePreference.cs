namespace TasklyDev.ViewModels;

/// <summary>Specifies whether the application uses the Windows, light, or dark theme.</summary>
public enum ThemePreference
{
    /// <summary>Follow the Windows app appearance.</summary>
    System,
    /// <summary>Always use the light theme.</summary>
    Light,
    /// <summary>Always use the dark theme.</summary>
    Dark
}
