using System.Runtime.InteropServices;
using System.Windows;

namespace TasklyDev.Services;

/// <summary>Writes text to the WPF Windows clipboard.</summary>
public sealed class WpfClipboardService : IClipboardService
{
    /// <inheritdoc />
    public bool TrySetText(string text)
    {
        try
        {
            Clipboard.SetText(text);
            return true;
        }
        catch (ExternalException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
