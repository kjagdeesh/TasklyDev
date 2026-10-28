namespace TasklyDev.Core.Encoding;

/// <summary>Contains a URL transformation result and parsed components when applicable.</summary>
public sealed record UrlCodecResult(string Value, UrlComponents? Components);
