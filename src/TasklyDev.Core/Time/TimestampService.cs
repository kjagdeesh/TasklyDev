using System.Globalization;
using System.Text.RegularExpressions;

namespace TasklyDev.Core.Time;

/// <summary>Implements Unix timestamp conversion and explicit-offset ISO 8601 parsing.</summary>
public sealed partial class TimestampService : ITimestampService
{
    /// <inheritdoc />
    public DateTimeOffset FromUnixTimestamp(long timestamp, UnixTimestampUnit unit) =>
        unit switch
        {
            UnixTimestampUnit.Seconds => DateTimeOffset.FromUnixTimeSeconds(timestamp),
            UnixTimestampUnit.Milliseconds => DateTimeOffset.FromUnixTimeMilliseconds(timestamp),
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported Unix timestamp unit.")
        };

    /// <inheritdoc />
    public long ToUnixTimestamp(DateTimeOffset dateTime, UnixTimestampUnit unit) =>
        unit switch
        {
            UnixTimestampUnit.Seconds => dateTime.ToUnixTimeSeconds(),
            UnixTimestampUnit.Milliseconds => dateTime.ToUnixTimeMilliseconds(),
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported Unix timestamp unit.")
        };

    /// <inheritdoc />
    public DateTimeOffset ParseIso8601WithOffset(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var input = value.Trim();
        if (!HasExplicitOffset().IsMatch(input) ||
            !DateTimeOffset.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
        {
            throw new FormatException("Enter an ISO 8601 date/time with an explicit UTC marker (Z) or numeric offset.");
        }

        return result;
    }

    [GeneratedRegex(@"(?:[zZ]|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex HasExplicitOffset();
}
