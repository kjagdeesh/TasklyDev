using TasklyDev.Core.Time;
using Xunit;

namespace TasklyDev.Tests;

public sealed class TimestampServiceTests
{
    private readonly TimestampService _service = new();

    [Theory]
    [InlineData(0, UnixTimestampUnit.Seconds, "1970-01-01T00:00:00.0000000+00:00")]
    [InlineData(0, UnixTimestampUnit.Milliseconds, "1970-01-01T00:00:00.0000000+00:00")]
    [InlineData(1718105680, UnixTimestampUnit.Seconds, "2024-06-11T11:34:40.0000000+00:00")]
    [InlineData(1718105680000, UnixTimestampUnit.Milliseconds, "2024-06-11T11:34:40.0000000+00:00")]
    public void ConvertsUnixTimestampToUtc(long timestamp, UnixTimestampUnit unit, string expectedUtc)
    {
        var result = _service.FromUnixTimestamp(timestamp, unit);

        Assert.Equal(expectedUtc, result.ToString("O"));
    }

    [Theory]
    [InlineData("1969-12-31T23:59:59Z", UnixTimestampUnit.Seconds, -1)]
    [InlineData("1969-12-31T23:59:59.999Z", UnixTimestampUnit.Milliseconds, -1)]
    [InlineData("2024-06-11T17:04:40+05:30", UnixTimestampUnit.Seconds, 1718105680)]
    [InlineData("2024-06-11T11:34:40Z", UnixTimestampUnit.Milliseconds, 1718105680000)]
    public void ConvertsExplicitIsoDateTimeToUnixTimestamp(string input, UnixTimestampUnit unit, long expected)
    {
        var dateTime = _service.ParseIso8601WithOffset(input);

        Assert.Equal(expected, _service.ToUnixTimestamp(dateTime, unit));
    }

    [Theory]
    [InlineData("2024-06-11T11:34:40")]
    [InlineData("not a date")]
    [InlineData("2024-06-11T11:34:40+99:99")]
    public void IsoDateTimeRequiresValidExplicitOffset(string input)
    {
        Assert.Throws<FormatException>(() => _service.ParseIso8601WithOffset(input));
    }

    [Fact]
    public void RejectsTimestampOutsideDateTimeOffsetRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.FromUnixTimestamp(long.MaxValue, UnixTimestampUnit.Seconds));
    }

    [Fact]
    public void SecondsAndMillisecondsRemainDistinct()
    {
        var seconds = _service.FromUnixTimestamp(1000, UnixTimestampUnit.Seconds);
        var milliseconds = _service.FromUnixTimestamp(1000, UnixTimestampUnit.Milliseconds);

        Assert.Equal(TimeSpan.FromMinutes(16).Add(TimeSpan.FromSeconds(39)), seconds - milliseconds);
    }
}
