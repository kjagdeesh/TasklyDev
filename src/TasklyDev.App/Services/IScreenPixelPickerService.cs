using TasklyDev.Core.Colors;

namespace TasklyDev.Services;

public interface IScreenPixelPickerService
{
    Task<ColorValue?> PickPixelAsync();
}
