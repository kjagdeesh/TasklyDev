namespace TasklyDev.Core.Time;

/// <summary>Converts Unix timestamps and offset-aware ISO 8601 date/time values.</summary>
public interface ITimestampService
{
    /// <summary>Converts a Unix timestamp to a UTC <see cref="DateTimeOffset"/>.</summary>
    DateTimeOffset FromUnixTimestamp(long timestamp, UnixTimestampUnit unit);
    /// <summary>Converts a date/time value to the selected Unix timestamp unit.</summary>
    long ToUnixTimestamp(DateTimeOffset dateTime, UnixTimestampUnit unit);
    /// <summary>Parses an ISO 8601 date/time only when its UTC marker or offset is explicit.</summary>
    DateTimeOffset ParseIso8601WithOffset(string value);
}
