namespace TasklyDev.Services;

/// <summary>Provides clipboard operations used by the desktop application.</summary>
public interface IClipboardService
{
    /// <summary>Attempts to place text on the Windows clipboard.</summary>
    /// <param name="text">Text to copy.</param>
    /// <returns><see langword="true"/> when the text was copied; otherwise, <see langword="false"/>.</returns>
    bool TrySetText(string text);
}
