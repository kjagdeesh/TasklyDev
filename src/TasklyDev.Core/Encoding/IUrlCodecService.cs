namespace TasklyDev.Core.Encoding;

/// <summary>Encodes and decodes URL text according to an explicitly selected mode.</summary>
public interface IUrlCodecService
{
    /// <summary>Transforms input text using the selected mode and direction.</summary>
    /// <param name="input">The URL or value to transform.</param>
    /// <param name="mode">The URL interpretation to apply.</param>
    /// <param name="encode">True to encode; false to decode.</param>
    /// <param name="characterEncoding">The character set used to convert escaped text bytes.</param>
    /// <param name="destinationLineEnding">The line ending to apply to transformed text.</param>
    /// <param name="decodeEachLine">Whether to decode each input line as a separate value.</param>
    /// <param name="recursiveDecode">Whether to decode repeatedly, up to 16 passes.</param>
    /// <returns>The transformed value and parsed URL components when in Full URL mode.</returns>
    UrlCodecResult Transform(
        string input,
        UrlEncodingMode mode,
        bool encode,
        UrlCharacterEncoding characterEncoding = UrlCharacterEncoding.Utf8,
        UrlLineEnding destinationLineEnding = UrlLineEnding.Lf,
        bool decodeEachLine = false,
        bool recursiveDecode = false);
}
