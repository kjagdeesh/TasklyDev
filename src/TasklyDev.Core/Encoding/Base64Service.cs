using System.Text;

namespace TasklyDev.Core.Encoding;

public sealed class Base64Service : IBase64Service
{
    private const int MimeLineLength = 76;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public string EncodeUtf8(string text, Base64Variant variant, bool includePadding)
    {
        ArgumentNullException.ThrowIfNull(text);

        var encoded = Convert.ToBase64String(StrictUtf8.GetBytes(text));
        if (variant == Base64Variant.UrlSafe)
        {
            encoded = encoded.Replace('+', '-').Replace('/', '_');
        }

        if (!includePadding)
        {
            encoded = encoded.TrimEnd('=');
        }

        return variant == Base64Variant.Mime ? WrapMimeLines(encoded) : encoded;
    }

    public string DecodeUtf8(string encoded, Base64Variant variant)
    {
        ArgumentNullException.ThrowIfNull(encoded);

        var normalized = variant == Base64Variant.UrlSafe
            ? NormalizeUrlSafe(encoded)
            : encoded;

        var bytes = Convert.FromBase64String(normalized);
        return StrictUtf8.GetString(bytes);
    }

    private static string NormalizeUrlSafe(string encoded)
    {
        var normalized = encoded.Replace('-', '+').Replace('_', '/');
        var remainder = normalized.Length % 4;
        if (remainder == 1)
        {
            throw new FormatException("The URL-safe Base64 value has an invalid length.");
        }

        return remainder == 0 ? normalized : normalized.PadRight(normalized.Length + (4 - remainder), '=');
    }

    private static string WrapMimeLines(string encoded)
    {
        if (encoded.Length <= MimeLineLength)
        {
            return encoded;
        }

        var lines = new StringBuilder(encoded.Length + encoded.Length / MimeLineLength);
        for (var offset = 0; offset < encoded.Length; offset += MimeLineLength)
        {
            if (offset > 0)
            {
                lines.Append("\r\n");
            }

            var count = Math.Min(MimeLineLength, encoded.Length - offset);
            lines.Append(encoded, offset, count);
        }

        return lines.ToString();
    }
}
