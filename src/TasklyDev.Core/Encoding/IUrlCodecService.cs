namespace TasklyDev.Core.Encoding;

/// <summary>Encodes and decodes URL text according to an explicitly selected mode.</summary>
public interface IUrlCodecService
{
    /// <summary>Transforms input text using the selected mode and direction.</summary>
    /// <param name="input">The URL or value to transform.</param>
    /// <param name="mode">The URL interpretation to apply.</param>
    /// <param name="encode">True to encode; false to decode.</param>
    /// <returns>The transformed value and parsed URL components when in Full URL mode.</returns>
    UrlCodecResult Transform(string input, UrlEncodingMode mode, bool encode);
}
