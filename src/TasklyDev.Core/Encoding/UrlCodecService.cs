using System.Globalization;
using System.Text;

namespace TasklyDev.Core.Encoding;

/// <summary>Implements charset-aware percent encoding for full URLs, components, and query values.</summary>
public sealed class UrlCodecService : IUrlCodecService
{
    private const string UnreservedCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
    private const string ReservedCharacters = ":/?#[]@!$&'()*+,;=";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private const int MaximumDecodePasses = 16;

    static UrlCodecService() =>
        System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    /// <inheritdoc />
    public UrlCodecResult Transform(
        string input,
        UrlEncodingMode mode,
        bool encode,
        UrlCharacterEncoding characterEncoding = UrlCharacterEncoding.Utf8,
        UrlLineEnding destinationLineEnding = UrlLineEnding.Lf,
        bool decodeEachLine = false,
        bool recursiveDecode = false)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported URL encoding mode.");
        }

        if (!Enum.IsDefined(characterEncoding))
        {
            throw new ArgumentOutOfRangeException(nameof(characterEncoding), characterEncoding, "Unsupported character encoding.");
        }

        if (!Enum.IsDefined(destinationLineEnding))
        {
            throw new ArgumentOutOfRangeException(nameof(destinationLineEnding), destinationLineEnding, "Unsupported line separator.");
        }

        _ = StrictUtf8.GetByteCount(input);
        var encoding = CreateEncoding(characterEncoding);
        var value = encode
            ? TransformSingle(NormalizeLineEndings(input, destinationLineEnding), mode, encode: true, encoding)
            : DecodeWithOptions(input, mode, encoding, destinationLineEnding, decodeEachLine, recursiveDecode);

        if (!encode)
        {
            value = NormalizeLineEndings(value, destinationLineEnding);
        }

        var components = mode == UrlEncodingMode.FullUrl
            ? ParseComponentsForDisplay(value)
            : null;
        return new UrlCodecResult(value, components);
    }

    private static string TransformSingle(string input, UrlEncodingMode mode, bool encode, System.Text.Encoding encoding) =>
        mode switch
        {
            UrlEncodingMode.FullUrl when encode => EncodeFullUrl(input, encoding),
            UrlEncodingMode.FullUrl => DecodeFullUrl(input, encoding),
            UrlEncodingMode.UrlComponent when encode => EncodeComponent(input, queryValue: false, encoding),
            UrlEncodingMode.UrlComponent => DecodeComponent(input, plusAsSpace: false, encoding),
            UrlEncodingMode.QueryValue when encode => EncodeComponent(input, queryValue: true, encoding),
            UrlEncodingMode.QueryValue => DecodeComponent(input, plusAsSpace: true, encoding),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported URL encoding mode.")
        };

    private static string DecodeWithOptions(
        string input,
        UrlEncodingMode mode,
        System.Text.Encoding encoding,
        UrlLineEnding destinationLineEnding,
        bool decodeEachLine,
        bool recursiveDecode)
    {
        if (!decodeEachLine)
        {
            return DecodeRepeatedly(input, mode, encoding, recursiveDecode);
        }

        var lines = NormalizeLineEndings(input, UrlLineEnding.Lf).Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (lines[index].Length > 0)
            {
                lines[index] = DecodeRepeatedly(lines[index], mode, encoding, recursiveDecode);
            }
        }

        return string.Join(GetLineSeparator(destinationLineEnding), lines);
    }

    private static string DecodeRepeatedly(
        string input,
        UrlEncodingMode mode,
        System.Text.Encoding encoding,
        bool recursiveDecode)
    {
        var decoded = input;
        var passes = recursiveDecode ? MaximumDecodePasses : 1;
        for (var pass = 0; pass < passes; pass++)
        {
            string next;
            try
            {
                next = TransformSingle(decoded, mode, encode: false, encoding);
            }
            catch (FormatException) when (recursiveDecode && pass > 0)
            {
                break;
            }

            if (next == decoded)
            {
                break;
            }

            decoded = next;
        }

        return decoded;
    }

    private static System.Text.Encoding CreateEncoding(UrlCharacterEncoding characterEncoding) =>
        System.Text.Encoding.GetEncoding(
            (int)characterEncoding,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);

    private static string NormalizeLineEndings(string value, UrlLineEnding destinationLineEnding)
    {
        var normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        return destinationLineEnding == UrlLineEnding.CrLf
            ? normalized.Replace("\n", "\r\n", StringComparison.Ordinal)
            : normalized;
    }

    private static string GetLineSeparator(UrlLineEnding lineEnding) =>
        lineEnding == UrlLineEnding.CrLf ? "\r\n" : "\n";

    private static string EncodeFullUrl(string input, System.Text.Encoding encoding)
    {
        ValidatePercentEscapes(input);
        var builder = new StringBuilder(input.Length);
        for (var index = 0; index < input.Length;)
        {
            var character = input[index];
            if (character == '%')
            {
                builder.Append(input, index, 3);
                index += 3;
                continue;
            }

            if (character <= 0x7F && (UnreservedCharacters.Contains(character) || ReservedCharacters.Contains(character)))
            {
                builder.Append(character);
                index++;
                continue;
            }

            var rune = Rune.GetRuneAt(input, index);
            AppendEscapedRune(builder, rune, encoding);
            index += rune.Utf16SequenceLength;
        }

        var value = builder.ToString();
        if (!Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            throw new FormatException("Enter a valid absolute URL, including its scheme.");
        }

        return value;
    }

    private static string DecodeFullUrl(string input, System.Text.Encoding encoding)
    {
        ValidatePercentEscapes(input);

        if (Uri.TryCreate(input, UriKind.Absolute, out _))
        {
            var decodedAbsoluteUrl = DecodePercentRuns(
                input,
                plusAsSpace: false,
                preserveReservedAscii: true,
                encoding);
            if (!Uri.TryCreate(EscapeForUrlValidation(decodedAbsoluteUrl), UriKind.Absolute, out _))
            {
                throw new FormatException("Input is not a valid absolute URL.");
            }

            return decodedAbsoluteUrl;
        }

        var decodedWholeUrl = DecodePercentRuns(
            input,
            plusAsSpace: false,
            preserveReservedAscii: false,
            encoding);
        if (!Uri.TryCreate(EscapeForUrlValidation(decodedWholeUrl), UriKind.Absolute, out _))
        {
            throw new FormatException("Enter a valid absolute URL or a percent-encoded absolute URL.");
        }

        return decodedWholeUrl;
    }

    private static string EncodeComponent(string input, bool queryValue, System.Text.Encoding encoding)
    {
        var builder = new StringBuilder(input.Length);
        foreach (var rune in input.EnumerateRunes())
        {
            if (queryValue && rune.Value == ' ')
            {
                builder.Append('+');
            }
            else if (rune.IsAscii && UnreservedCharacters.Contains((char)rune.Value))
            {
                builder.Append((char)rune.Value);
            }
            else
            {
                AppendEscapedRune(builder, rune, encoding);
            }
        }

        return builder.ToString();
    }

    private static string DecodeComponent(string input, bool plusAsSpace, System.Text.Encoding encoding)
    {
        ValidatePercentEscapes(input);
        return DecodePercentRuns(input, plusAsSpace, preserveReservedAscii: false, encoding);
    }

    private static string DecodePercentRuns(
        string input,
        bool plusAsSpace,
        bool preserveReservedAscii,
        System.Text.Encoding encoding)
    {
        var builder = new StringBuilder(input.Length);
        for (var index = 0; index < input.Length;)
        {
            var character = input[index];
            if (character == '+' && plusAsSpace)
            {
                builder.Append(' ');
                index++;
                continue;
            }

            if (character != '%')
            {
                builder.Append(character);
                index++;
                continue;
            }

            var bytes = new List<byte>();
            while (index < input.Length && input[index] == '%')
            {
                bytes.Add((byte)((HexValue(input[index + 1]) << 4) | HexValue(input[index + 2])));
                index += 3;
            }

            var encodedBytes = bytes.ToArray();
            string decoded;
            try
            {
                decoded = encoding.GetString(encodedBytes);
            }
            catch (DecoderFallbackException exception)
            {
                throw new FormatException($"Percent escapes are not valid {encoding.WebName} data.", exception);
            }

            if (!preserveReservedAscii)
            {
                builder.Append(decoded);
                continue;
            }

            foreach (var rune in decoded.EnumerateRunes())
            {
                if (rune.IsAscii && ReservedCharacters.Contains((char)rune.Value))
                {
                    foreach (var encodedByte in encoding.GetBytes(rune.ToString()))
                    {
                        builder.Append('%');
                        builder.Append(encodedByte.ToString("X2", CultureInfo.InvariantCulture));
                    }
                }
                else
                {
                    builder.Append(rune.ToString());
                }
            }
        }

        return builder.ToString();
    }

    private static string EscapeForUrlValidation(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length;)
        {
            var character = value[index];
            if (character == '%' &&
                index + 2 < value.Length &&
                IsHexDigit(value[index + 1]) &&
                IsHexDigit(value[index + 2]))
            {
                builder.Append(character);
                builder.Append(value, index + 1, 2);
                index += 3;
                continue;
            }

            if (character <= 0x7F && UnreservedCharacters.Contains(character))
            {
                builder.Append(character);
                index++;
                continue;
            }

            if (character <= 0x7F && ReservedCharacters.Contains(character))
            {
                builder.Append(character);
                index++;
                continue;
            }

            if (character == '%')
            {
                builder.Append("%25");
                index++;
                continue;
            }

            var rune = Rune.GetRuneAt(value, index);
            AppendEscapedRune(builder, rune);
            index += rune.Utf16SequenceLength;
        }

        return builder.ToString();
    }

    private static UrlComponents? ParseComponentsForDisplay(string value)
    {
        var parseable = EscapeForUrlValidation(value);
        if (!Uri.TryCreate(parseable, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return new UrlComponents(
            uri.Scheme,
            uri.Authority,
            uri.AbsolutePath,
            uri.Query,
            uri.Fragment);
    }

    private static void ValidatePercentEscapes(string input)
    {
        for (var index = 0; index < input.Length; index++)
        {
            if (input[index] != '%')
            {
                continue;
            }

            if (index + 2 >= input.Length ||
                !IsHexDigit(input[index + 1]) ||
                !IsHexDigit(input[index + 2]))
            {
                throw new FormatException("Percent signs must be followed by two hexadecimal digits.");
            }

            index += 2;
        }
    }

    private static void AppendEscapedRune(StringBuilder builder, Rune rune) =>
        AppendEscapedBytes(builder, StrictUtf8.GetBytes(rune.ToString()));

    private static void AppendEscapedRune(StringBuilder builder, Rune rune, System.Text.Encoding encoding) =>
        AppendEscapedBytes(builder, encoding.GetBytes(rune.ToString()));

    private static void AppendEscapedBytes(StringBuilder builder, byte[] bytes)
    {
        foreach (var value in bytes)
        {
            builder.Append('%');
            builder.Append(value.ToString("X2", CultureInfo.InvariantCulture));
        }
    }

    private static bool IsHexDigit(char value) =>
        value is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

    private static int HexValue(char value) => value switch
    {
        >= '0' and <= '9' => value - '0',
        >= 'a' and <= 'f' => value - 'a' + 10,
        >= 'A' and <= 'F' => value - 'A' + 10,
        _ => throw new FormatException("Percent escapes must use hexadecimal digits.")
    };
}
