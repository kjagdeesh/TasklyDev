using TasklyDev.Core.Conversion;
using Xunit;

namespace TasklyDev.Tests;

public sealed class NumberBaseServiceTests
{
    private readonly NumberBaseService _service = new();

    [Theory]
    [InlineData("11111111", 2, 10, "255")]
    [InlineData("377", 8, 16, "FF")]
    [InlineData("255", 10, 2, "11111111")]
    [InlineData("Z", 36, 10, "35")]
    [InlineData("10", 2, 36, "2")]
    public void ConvertsAcrossSupportedBases(string input, int sourceBase, int targetBase, string expected)
    {
        Assert.Equal(expected, _service.Convert(input, sourceBase, targetBase));
    }

    [Theory]
    [InlineData("0", 10, 2)]
    [InlineData("-FF", 16, 10)]
    [InlineData("+101", 2, 10)]
    [InlineData(" 1A ", 16, 10)]
    public void SupportsZeroSignsWhitespaceAndCaseInsensitiveDigits(string input, int sourceBase, int targetBase)
    {
        var result = _service.Convert(input, sourceBase, targetBase);
        var roundTrip = _service.Convert(result, targetBase, sourceBase);

        Assert.Equal(input.Trim().TrimStart('+').ToUpperInvariant(), roundTrip);
    }

    [Fact]
    public void ConvertsVeryLargeIntegersWithoutPrecisionLoss()
    {
        const string input = "12345678901234567890123456789012345678901234567890";

        var hexadecimal = _service.Convert(input, 10, 16);

        Assert.Equal(input, _service.Convert(hexadecimal, 16, 10));
        Assert.True(hexadecimal.Length > 16);
    }

    [Fact]
    public void RejectsDigitsThatAreInvalidForSelectedBase()
    {
        Assert.Throws<FormatException>(() => _service.Convert("102", 2, 10));
        Assert.Throws<FormatException>(() => _service.Convert("G", 16, 10));
        Assert.Throws<FormatException>(() => _service.Convert(string.Empty, 10, 2));
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(10, 37)]
    public void RejectsBasesOutsideTwoThroughThirtySix(int sourceBase, int targetBase)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.Convert("1", sourceBase, targetBase));
    }
}
