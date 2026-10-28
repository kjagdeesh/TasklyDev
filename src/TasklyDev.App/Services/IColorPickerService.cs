using TasklyDev.Core.Colors;

namespace TasklyDev.Services;

public interface IColorPickerService
{
    Task<ColorValue?> PickColorAsync(ColorValue initialColor);
}
