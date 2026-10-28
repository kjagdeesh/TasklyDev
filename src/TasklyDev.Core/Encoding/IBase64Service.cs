namespace TasklyDev.Core.Encoding;

/// <summary>Encodes UTF-8 text as Base64 and decodes Base64 data to text.</summary>
public interface IBase64Service
{
    /// <summary>Encodes text using the selected Base64 variant.</summary>
    /// <param name="text">Text to encode as UTF-8.</param>
    /// <param name="variant">Base64 alphabet and line-wrapping variant.</param>
    /// <param name="includePadding">Whether to retain trailing equals-sign padding.</param>
    /// <returns>The encoded Base64 representation.</returns>
    string EncodeUtf8(string text, Base64Variant variant, bool includePadding);

    /// <summary>Decodes a Base64 value and interprets the result as strict UTF-8.</summary>
    /// <param name="encoded">Base64 text to decode.</param>
    /// <param name="variant">Base64 alphabet variant used by the input.</param>
    /// <returns>The decoded text.</returns>
    string DecodeUtf8(string encoded, Base64Variant variant);
}
