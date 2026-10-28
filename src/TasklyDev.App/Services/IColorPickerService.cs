using TasklyDev.Core.Colors;

namespace TasklyDev.Services;

/// <summary>Provides a native color-selection dialog.</summary>
public interface IColorPickerService
{
    /// <summary>Prompts the user to select a color, initialized to the supplied value.</summary>
    /// <param name="initialColor">Color initially selected in the dialog.</param>
    /// <returns>The chosen color, or <see langword="null"/> when the dialog is canceled.</returns>
    Task<ColorValue?> PickColorAsync(ColorValue initialColor);
}
