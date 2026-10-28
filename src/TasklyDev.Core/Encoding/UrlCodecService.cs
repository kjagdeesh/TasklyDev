using System.Buffers;
using System.Globalization;
using System.Text;

namespace TasklyDev.Core.Encoding;

/// <summary>Implements strict UTF-8 percent encoding for full URLs, components, and query values.</summary>
public sealed class UrlCodecService : IUrlCodecService
{
    private const string UnreservedCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
    private const string ReservedCharacters = ":/?#[]@!$&'()*+,;=";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <inheritdoc />
    public UrlCodecResult Transform(string input, UrlEncodingMode mode, bool encode)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported URL encoding mode.");
        }

        _ = StrictUtf8.GetByteCount(input);
        var value = mode switch
        {
            UrlEncodingMode.FullUrl when encode => EncodeFullUrl(input),
            UrlEncodingMode.FullUrl => DecodeFullUrl(input),
            UrlEncodingMode.UrlComponent when encode => EncodeComponent(input, queryValue: false),
            UrlEncodingMode.UrlComponent => DecodeComponent(input, plusAsSpace: false),
            UrlEncodingMode.QueryValue when encode => EncodeComponent(input, queryValue: true),
            UrlEncodingMode.QueryValue => DecodeComponent(input, plusAsSpace: true),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported URL encoding mode.")
        };

        var components = mode == UrlEncodingMode.FullUrl
            ? ParseComponentsForDisplay(value)
            : null;
        return new UrlCodecResult(value, components);
    }

    private static string EncodeFullUrl(string input)
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
            AppendEscapedRune(builder, rune);
            index += rune.Utf16SequenceLength;
        }

        var value = builder.ToString();
        if (!Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            throw new FormatException("Enter a valid absolute URL, including its scheme.");
        }

        return value;
    }

    private static string DecodeFullUrl(string input)
    {
        ValidatePercentEscapes(input);
        if (!Uri.TryCreate(input, UriKind.Absolute, out _))
        {
            throw new FormatException("Enter a valid absolute URL, including its scheme.");
        }

        var value = DecodePercentRuns(input, plusAsSpace: false, preserveReservedAscii: true);
        if (!Uri.TryCreate(EscapeForUrlValidation(value), UriKind.Absolute, out _))
        {
            throw new FormatException("Input is not a valid absolute URL.");
        }

        return value;
    }

    private static string EncodeComponent(string input, bool queryValue)
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
                AppendEscapedRune(builder, rune);
            }
        }

        return builder.ToString();
    }

    private static string DecodeComponent(string input, bool plusAsSpace)
    {
        ValidatePercentEscapes(input);
        return DecodePercentRuns(input, plusAsSpace, preserveReservedAscii: false);
    }

    private static string DecodePercentRuns(string input, bool plusAsSpace, bool preserveReservedAscii)
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
                decoded = StrictUtf8.GetString(encodedBytes);
            }
            catch (DecoderFallbackException exception)
            {
                throw new FormatException("Percent escapes must contain valid UTF-8.", exception);
            }

            if (!preserveReservedAscii)
            {
                builder.Append(decoded);
                continue;
            }

            var byteIndex = 0;
            while (byteIndex < bytes.Count)
            {
                var status = Rune.DecodeFromUtf8(encodedBytes.AsSpan(byteIndex), out var rune, out var consumed);
                if (status != OperationStatus.Done)
                {
                    throw new DecoderFallbackException("Percent escapes do not contain valid UTF-8.");
                }

                if (rune.IsAscii && ReservedCharacters.Contains((char)rune.Value))
                {
                    for (var offset = 0; offset < consumed; offset++)
                    {
                        builder.Append('%');
                        builder.Append(bytes[byteIndex + offset].ToString("X2", CultureInfo.InvariantCulture));
                    }
                }
                else
                {
                    builder.Append(rune.ToString());
                }

                byteIndex += consumed;
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

    private static void AppendEscapedRune(StringBuilder builder, Rune rune)
    {
        Span<byte> bytes = stackalloc byte[4];
        var byteCount = rune.EncodeToUtf8(bytes);
        for (var index = 0; index < byteCount; index++)
        {
            builder.Append('%');
            builder.Append(bytes[index].ToString("X2", CultureInfo.InvariantCulture));
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
