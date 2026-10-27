namespace TasklyDev.Core.Time;

public interface ITimestampService
{
    DateTimeOffset FromUnixTimestamp(long timestamp, UnixTimestampUnit unit);
    long ToUnixTimestamp(DateTimeOffset dateTime, UnixTimestampUnit unit);
    DateTimeOffset ParseIso8601WithOffset(string value);
}
