namespace TasklyDev.Core.Encoding;

/// <summary>Contains the structural parts of a successfully parsed absolute URL.</summary>
public sealed record UrlComponents(
    string Scheme,
    string Authority,
    string Path,
    string Query,
    string Fragment);
