namespace TasklyDev.Core.Encoding;

/// <summary>Identifies the Base64 alphabet and formatting convention.</summary>
public enum Base64Variant
{
    /// <summary>Uses the standard Base64 alphabet.</summary>
    Standard,
    /// <summary>Uses URL-safe characters in place of plus and slash.</summary>
    UrlSafe,
    /// <summary>Uses the standard alphabet with lines wrapped at 76 characters.</summary>
    Mime
}
