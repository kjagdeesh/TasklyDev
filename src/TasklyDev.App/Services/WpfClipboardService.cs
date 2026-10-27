using System.Runtime.InteropServices;
using System.Windows;

namespace TasklyDev.Services;

public sealed class WpfClipboardService : IClipboardService
{
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
