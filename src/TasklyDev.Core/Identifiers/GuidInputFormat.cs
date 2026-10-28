namespace TasklyDev.Core.Identifiers;

/// <summary>Supported .NET string representations for GUID input.</summary>
public enum GuidInputFormat
{
    /// <summary>Hyphenated digits.</summary>
    D,

    /// <summary>Digits without punctuation.</summary>
    N,

    /// <summary>Hyphenated digits enclosed in braces.</summary>
    B,

    /// <summary>Hyphenated digits enclosed in parentheses.</summary>
    P,

    /// <summary>Hexadecimal components enclosed in braces.</summary>
    X
}
