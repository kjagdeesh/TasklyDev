using System.Globalization;
using System.Text;
using System.Text.Json;

namespace TasklyDev.Core.Tokens;

/// <summary>Decodes compact JWT data locally and interprets registered time claims informationally.</summary>
public sealed class JwtDecoderService : IJwtDecoderService
{
    /// <summary>Maximum compact-token size accepted for local inspection.</summary>
    public const int MaximumTokenLength = 1_048_576;

    private const int MaximumJsonDepth = 64;
    private const int MaximumExpandedNumberLength = 4096;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly HashSet<string> TimeClaimNames = new(StringComparer.Ordinal)
    {
        "exp",
        "nbf",
        "iat"
    };

    /// <inheritdoc />
    public JwtParseResult Decode(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Invalid("Enter a compact JWT to inspect.");
        }

        token = NormalizeToken(token);
        if (token.Length > MaximumTokenLength)
        {
            return Invalid($"Token exceeds the {MaximumTokenLength:N0}-character inspection limit.");
        }

        var segments = token.Split('.');
        if (segments.Length != 3 || segments[0].Length == 0 || segments[1].Length == 0)
        {
            return Invalid("A compact JWT must contain header, payload, and signature segments separated by two periods.");
        }

        try
        {
            var headerBytes = DecodeBase64UrlSegment(segments[0]);
            var payloadBytes = DecodeBase64UrlSegment(segments[1]);
            ValidateBase64UrlSegment(segments[2]);

            using var header = ParseObject(headerBytes);
            using var payload = ParseObject(payloadBytes);
            var headerJson = FormatJson(header.RootElement, indented: true);
            var payloadJson = FormatJson(payload.RootElement, indented: true);
            var claims = CreateClaims(payload.RootElement);

            return new JwtParseResult(
                true,
                headerJson,
                payloadJson,
                claims,
                segments[2].Length,
                "JWT decoded locally. Signature and trust were not verified.");
        }
        catch (FormatException)
        {
            return Invalid("Token contains an invalid Base64URL segment.");
        }
        catch (DecoderFallbackException)
        {
            return Invalid("Decoded header or payload is not valid UTF-8.");
        }
        catch (JsonException)
        {
            return Invalid("Decoded header and payload must be valid JSON objects.");
        }
    }

    private static string NormalizeToken(string token)
    {
        var trimmed = token.Trim().Trim('"', '\'');
        if (trimmed.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[7..];
        }

        return string.Concat(trimmed.Where(character => !char.IsWhiteSpace(character)));
    }

    private static JsonDocument ParseObject(byte[] utf8Json)
    {
        var document = JsonDocument.Parse(
            utf8Json,
            new JsonDocumentOptions { MaxDepth = MaximumJsonDepth });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            document.Dispose();
            throw new JsonException("JWT header and payload must be JSON objects.");
        }

        return document;
    }

    private static IReadOnlyList<JwtClaimInfo> CreateClaims(JsonElement payload)
    {
        var claims = new List<JwtClaimInfo>();
        foreach (var property in payload.EnumerateObject())
        {
            var value = property.Value;
            var jsonType = GetJsonType(value.ValueKind);
            var displayValue = value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : FormatJson(value, indented: false);
            var timeInterpretation = TimeClaimNames.Contains(property.Name)
                ? InterpretNumericDate(value)
                : null;

            claims.Add(new JwtClaimInfo(property.Name, displayValue, jsonType, timeInterpretation));
        }

        return claims;
    }

    private static string FormatJson(JsonElement element, bool indented)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
                   buffer,
                   new JsonWriterOptions { Indented = indented, MaxDepth = MaximumJsonDepth }))
        {
            WriteFormattedElement(writer, element);
        }

        return StrictUtf8.GetString(buffer.ToArray());
    }

    private static void WriteFormattedElement(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    WriteFormattedElement(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteFormattedElement(writer, item);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(ExpandExponent(element.GetRawText()));
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static string ExpandExponent(string number)
    {
        var exponentIndex = number.IndexOfAny(['e', 'E']);
        if (exponentIndex < 0 ||
            !long.TryParse(
                number.AsSpan(exponentIndex + 1),
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var exponent))
        {
            return number;
        }

        if (exponent is > MaximumExpandedNumberLength or < -MaximumExpandedNumberLength)
        {
            return number;
        }

        var mantissa = number.AsSpan(0, exponentIndex);
        var isNegative = mantissa[0] == '-';
        if (isNegative)
        {
            mantissa = mantissa[1..];
        }

        var decimalIndex = mantissa.IndexOf('.');
        var integerDigits = decimalIndex < 0 ? mantissa.Length : decimalIndex;
        var digits = decimalIndex < 0
            ? mantissa.ToString()
            : string.Concat(mantissa[..decimalIndex], mantissa[(decimalIndex + 1)..]);
        var expandedDecimalIndex = (long)integerDigits + exponent;

        var firstNonZero = 0;
        while (firstNonZero < digits.Length && digits[firstNonZero] == '0')
        {
            firstNonZero++;
        }

        if (firstNonZero == digits.Length)
        {
            return isNegative ? "-0" : "0";
        }

        digits = digits[firstNonZero..];
        expandedDecimalIndex -= firstNonZero;

        var outputLength = expandedDecimalIndex <= 0
            ? 2L - expandedDecimalIndex + digits.Length
            : Math.Max(expandedDecimalIndex, digits.Length) +
              (expandedDecimalIndex < digits.Length ? 1 : 0);
        if (isNegative)
        {
            outputLength++;
        }

        if (outputLength > MaximumExpandedNumberLength)
        {
            return number;
        }

        var sign = isNegative ? "-" : string.Empty;
        if (expandedDecimalIndex <= 0)
        {
            return $"{sign}0.{new string('0', (int)-expandedDecimalIndex)}{digits}";
        }

        if (expandedDecimalIndex >= digits.Length)
        {
            return $"{sign}{digits}{new string('0', (int)(expandedDecimalIndex - digits.Length))}";
        }

        return $"{sign}{digits.Insert((int)expandedDecimalIndex, ".")}";
    }

    private static string? InterpretNumericDate(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var seconds))
        {
            return "Not a numeric date; no time interpretation provided.";
        }

        if (double.IsNaN(seconds) || double.IsInfinity(seconds) ||
            seconds < DateTimeOffset.MinValue.ToUnixTimeSeconds() ||
            seconds > DateTimeOffset.MaxValue.ToUnixTimeSeconds())
        {
            return "Numeric date is outside the supported UTC range.";
        }

        try
        {
            var milliseconds = checked((long)Math.Truncate(seconds * 1000d));
            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
            var utc = timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff 'UTC'", CultureInfo.InvariantCulture);
            var local = timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);
            return $"{utc} · local {local}";
        }
        catch (ArgumentOutOfRangeException)
        {
            return "Numeric date is outside the supported UTC range.";
        }
        catch (OverflowException)
        {
            return "Numeric date is outside the supported UTC range.";
        }
    }

    private static byte[] DecodeBase64UrlSegment(string segment)
    {
        ValidateBase64UrlSegment(segment);
        var normalized = segment.Replace('-', '+').Replace('_', '/');
        var remainder = normalized.Length % 4;
        if (remainder == 1)
        {
            throw new FormatException("Invalid Base64URL length.");
        }

        normalized = remainder == 0
            ? normalized
            : normalized.PadRight(normalized.Length + (4 - remainder), '=');
        var bytes = Convert.FromBase64String(normalized);
        var canonical = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        if (!string.Equals(canonical, segment, StringComparison.Ordinal))
        {
            throw new FormatException("Non-canonical Base64URL segment.");
        }

        _ = StrictUtf8.GetString(bytes);
        return bytes;
    }

    private static void ValidateBase64UrlSegment(string segment)
    {
        if (segment.Any(character =>
                character is not (>= 'A' and <= 'Z') and
                not (>= 'a' and <= 'z') and
                not (>= '0' and <= '9') and
                not '-' and not '_'))
        {
            throw new FormatException("Invalid Base64URL characters.");
        }

        if (segment.Length % 4 == 1)
        {
            throw new FormatException("Invalid Base64URL length.");
        }
    }

    private static string GetJsonType(JsonValueKind kind) => kind switch
    {
        JsonValueKind.String => "String",
        JsonValueKind.Number => "Number",
        JsonValueKind.True or JsonValueKind.False => "Boolean",
        JsonValueKind.Object => "Object",
        JsonValueKind.Array => "Array",
        JsonValueKind.Null => "Null",
        _ => "Undefined"
    };

    private static JwtParseResult Invalid(string message) =>
        new(false, string.Empty, string.Empty, [], 0, message);
}
