namespace TasklyDev.Core.Encoding;

/// <summary>Defines the line separator used for transformed text.</summary>
public enum UrlLineEnding
{
    /// <summary>Line feed, commonly used by Unix and Linux systems.</summary>
    Lf,

    /// <summary>Carriage return followed by line feed, commonly used by Windows.</summary>
    CrLf
}
