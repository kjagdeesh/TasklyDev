namespace TasklyDev.Windows.ClipboardHistory;

/// <summary>Protects clipboard text at rest for the current Windows user.</summary>
public interface IClipboardTextProtector
{
    /// <summary>Encrypts text into a safe-to-serialize Base64 payload.</summary>
    string Protect(string text);

    /// <summary>Decrypts a Base64 payload protected for the current Windows user.</summary>
    string Unprotect(string protectedText);
}
