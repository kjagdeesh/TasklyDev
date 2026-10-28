namespace TasklyDev.Core.Identifiers;

/// <summary>Specifies the .NET GUID string format.</summary>
public enum GuidOutputFormat
{
    /// <summary>Hyphen-separated digits.</summary>
    D,
    /// <summary>Digits without punctuation.</summary>
    N,
    /// <summary>Hyphen-separated digits enclosed in braces.</summary>
    B,
    /// <summary>Hyphen-separated digits enclosed in parentheses.</summary>
    P,
    /// <summary>Brace-enclosed hexadecimal fields.</summary>
    X
}
