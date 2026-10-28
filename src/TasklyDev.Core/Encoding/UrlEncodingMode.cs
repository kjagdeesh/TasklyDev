namespace TasklyDev.Core.Encoding;

/// <summary>Defines how URL text is encoded or decoded.</summary>
public enum UrlEncodingMode
{
    /// <summary>Processes an absolute URL while preserving its structural delimiters.</summary>
    FullUrl,

    /// <summary>Processes one URL component using RFC 3986 unreserved characters.</summary>
    UrlComponent,

    /// <summary>Processes one query value using form-style plus-for-space behavior.</summary>
    QueryValue
}
