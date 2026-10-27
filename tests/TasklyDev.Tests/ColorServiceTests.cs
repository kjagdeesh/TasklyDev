using TasklyDev.Core.Colors;
using Xunit;

namespace TasklyDev.Tests;

public sealed class ColorServiceTests
{
    private readonly ColorService _service = new();

    [Theory]
    [InlineData("#F5B942", 245, 185, 66, 255)]
    [InlineData("#FAB", 255, 170, 187, 255)]
    [InlineData("#3A86FFB3", 58, 134, 255, 179)]
    [InlineData("#4C8A", 68, 204, 136, 170)]
    public void ParsesHexWithOptionalShorthandAndAlpha(string input, byte red, byte green, byte blue, byte alpha)
    {
        var color = _service.ParseHex(input);

        Assert.Equal(new ColorValue(red, green, blue, alpha), color);
    }

    [Theory]
    [InlineData("#12345")]
    [InlineData("GGGGGG")]
    [InlineData("#123456789")]
    public void RejectsMalformedHex(string input) =>
        Assert.Throws<FormatException>(() => _service.ParseHex(input));

    [Theory]
    [InlineData("rgb(255, 0, 128)", 255, 0, 128, 255)]
    [InlineData("rgba(10, 20, 30, 128)", 10, 20, 30, 128)]
    public void ParsesRgbChannels(string input, byte red, byte green, byte blue, byte alpha) =>
        Assert.Equal(new ColorValue(red, green, blue, alpha), _service.ParseRgb(input));

    [Theory]
    [InlineData("rgb(256, 0, 0)")]
    [InlineData("rgba(0, -1, 0, 0)")]
    [InlineData("rgba(0, 0, 0, 256)")]
    [InlineData("rgb(0, 0, 0, 1)")]
    [InlineData("rgba(0, 0, 0)")]
    public void RejectsOutOfRangeOrInvalidRgbInput(string input) =>
        Assert.Throws<FormatException>(() => _service.ParseRgb(input));

    [Fact]
    public void ConvertsHslAndWrapsHueAt360()
    {
        Assert.Equal(new ColorValue(255, 0, 0), _service.ParseHsl("hsl(0, 100%, 50%)"));
        Assert.Equal(new ColorValue(255, 0, 0), _service.ParseHsl("hsl(360, 100%, 50%)"));
        Assert.Equal(new ColorValue(128, 128, 128), _service.ParseHsl("hsl(210, 0%, 50%)"));
    }

    [Theory]
    [InlineData("hsl(-1, 20%, 30%)")]
    [InlineData("hsl(361, 20%, 30%)")]
    [InlineData("hsl(0, 101%, 30%)")]
    [InlineData("hsl(0, 20%, -1%)")]
    [InlineData("hsla(0, 20%, 30%, 1.1)")]
    [InlineData("hsl(0, 20%, 30%, 0.5)")]
    [InlineData("hsla(0, 20%, 30%)")]
    public void RejectsOutOfRangeHslInput(string input) =>
        Assert.Throws<FormatException>(() => _service.ParseHsl(input));

    [Fact]
    public void PreservesAlphaAcrossAllOutputRepresentations()
    {
        var color = _service.ParseHex("#3A86FFB3");

        Assert.Equal("#3A86FFB3", _service.ToHex(color));
        Assert.Equal("rgba(58, 134, 255, 179)", _service.ToRgb(color));
        Assert.Contains(", 0.702)", _service.ToHsl(color));
        Assert.Equal(color, _service.ParseHex(_service.ToHex(color)));
        Assert.Equal(color, _service.ParseRgb(_service.ToRgb(color)));
        Assert.Equal(color, _service.ParseHsl(_service.ToHsl(color)));
    }

    [Fact]
    public void OmitsOpaqueAlphaFromRepresentations()
    {
        var color = new ColorValue(245, 185, 66);

        Assert.Equal("#F5B942", _service.ToHex(color));
        Assert.Equal("rgb(245, 185, 66)", _service.ToRgb(color));
        Assert.StartsWith("hsl(", _service.ToHsl(color));
    }
}
