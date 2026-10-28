using System.Globalization;

namespace TasklyDev.Core.Identifiers;

/// <summary>Validates and normalizes GUID/UUID text using the .NET GUID parser.</summary>
public sealed class GuidParserService : IGuidParserService
{
    private static readonly GuidInputFormat[] SupportedFormats =
    [
        GuidInputFormat.D,
        GuidInputFormat.N,
        GuidInputFormat.B,
        GuidInputFormat.P,
        GuidInputFormat.X
    ];

    /// <inheritdoc />
    public GuidParseResult Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Invalid("Enter a GUID or UUID to validate.");
        }

        var value = input.Trim();
        foreach (var format in SupportedFormats)
        {
            if (Guid.TryParseExact(value, format.ToString(), out var parsed))
            {
                var canonical = parsed.ToString("D", CultureInfo.InvariantCulture);
                return new GuidParseResult(
                    true,
                    canonical,
                    format,
                    GetVersionDescription(canonical),
                    "Valid GUID/UUID.");
            }
        }

        return Invalid("Use a valid .NET GUID format: D, N, B, P, or X.");
    }

    private static string GetVersionDescription(string canonical)
    {
        var variantCharacter = canonical[19];
        var versionCharacter = canonical[14];
        if (!int.TryParse(variantCharacter.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var variant) ||
            (variant & 0xC) != 0x8 ||
            !int.TryParse(versionCharacter.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var version) ||
            version is < 1 or > 8)
        {
            return "Unknown";
        }

        return $"Version {version}";
    }

    private static GuidParseResult Invalid(string message) =>
        new(false, null, null, "Unknown", message);
}
