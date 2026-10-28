using TasklyDev.Core.Identifiers;
using Xunit;

namespace TasklyDev.Tests;

public sealed class GuidParserServiceTests
{
    private readonly GuidParserService _service = new();

    [Theory]
    [InlineData("D", "00112233-4455-6677-8899-aabbccddeeff")]
    [InlineData("N", "00112233445566778899aabbccddeeff")]
    [InlineData("B", "{00112233-4455-6677-8899-aabbccddeeff}")]
    [InlineData("P", "(00112233-4455-6677-8899-aabbccddeeff)")]
    [InlineData("X", "{0x00112233,0x4455,0x6677,{0x88,0x99,0xaa,0xbb,0xcc,0xdd,0xee,0xff}}")]
    public void ParsesSupportedFormatsAndReturnsCanonicalValue(string format, string input)
    {
        var result = _service.Parse(input);

        Assert.True(result.IsValid);
        Assert.Equal("00112233-4455-6677-8899-aabbccddeeff", result.CanonicalValue);
        Assert.Equal(format, result.InputFormat?.ToString());
    }

    [Fact]
    public void TrimsInputAndNormalizesUppercaseToLowercase()
    {
        var result = _service.Parse("  {00112233-4455-6677-8899-AABBCCDDEEFF}  ");

        Assert.True(result.IsValid);
        Assert.Equal("00112233-4455-6677-8899-aabbccddeeff", result.CanonicalValue);
        Assert.Equal(GuidInputFormat.B, result.InputFormat);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-guid")]
    [InlineData("00112233-4455-6677-8899-aabbccddeef")]
    [InlineData("{00112233-4455-6677-8899-aabbccddeeff")]
    public void RejectsEmptyOrMalformedInputWithoutCanonicalOutput(string? input)
    {
        var result = _service.Parse(input);

        Assert.False(result.IsValid);
        Assert.Null(result.CanonicalValue);
        Assert.Null(result.InputFormat);
        Assert.Equal("Unknown", result.VersionDescription);
        Assert.NotEmpty(result.Message);
    }

    [Theory]
    [InlineData("00112233-4455-1677-8899-aabbccddeeff", "Version 1")]
    [InlineData("00112233-4455-4677-8899-aabbccddeeff", "Version 4")]
    [InlineData("00112233-4455-7677-8899-aabbccddeeff", "Version 7")]
    [InlineData("00112233-4455-8677-8899-aabbccddeeff", "Version 8")]
    [InlineData("00112233-4455-9677-8899-aabbccddeeff", "Unknown")]
    [InlineData("00112233-4455-4677-7089-aabbccddeeff", "Unknown")]
    [InlineData("00000000-0000-0000-0000-000000000000", "Unknown")]
    public void ReportsOnlyRecognizedRfcVariantAndVersion(string input, string expectedVersion)
    {
        var result = _service.Parse(input);

        Assert.True(result.IsValid);
        Assert.Equal(expectedVersion, result.VersionDescription);
    }
}
