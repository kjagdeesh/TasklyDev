using System.Text;
using TasklyDev.Core.Encoding;
using Xunit;

namespace TasklyDev.Tests;

public sealed class UrlCodecServiceTests
{
    private readonly UrlCodecService _service = new();

    [Theory]
    [InlineData("two words", "two%20words")]
    [InlineData("a+b", "a%2Bb")]
    [InlineData("100%", "100%25")]
    [InlineData("a/b?c=d&x#f", "a%2Fb%3Fc%3Dd%26x%23f")]
    [InlineData("café 你好 🌱", "caf%C3%A9%20%E4%BD%A0%E5%A5%BD%20%F0%9F%8C%B1")]
    public void UrlComponentEncodesUtf8AndPreservesOnlyUnreservedCharacters(string input, string expected)
    {
        var result = _service.Transform(input, UrlEncodingMode.UrlComponent, encode: true);

        Assert.Equal(expected, result.Value);
        Assert.Null(result.Components);
    }

    [Theory]
    [InlineData("two words", "two+words")]
    [InlineData("a+b", "a%2Bb")]
    [InlineData("100%", "100%25")]
    [InlineData("a&b=c", "a%26b%3Dc")]
    [InlineData("café", "caf%C3%A9")]
    public void QueryValueUsesPlusForSpacesAndEscapesLiteralPlusAndSeparators(string input, string expected)
    {
        var result = _service.Transform(input, UrlEncodingMode.QueryValue, encode: true);

        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData("a+b", UrlEncodingMode.UrlComponent, "a+b")]
    [InlineData("a+b", UrlEncodingMode.QueryValue, "a b")]
    [InlineData("a%2Bb", UrlEncodingMode.QueryValue, "a+b")]
    [InlineData("a%2Fb%3Fc%3Dd%26x%23f", UrlEncodingMode.UrlComponent, "a/b?c=d&x#f")]
    [InlineData("caf%C3%A9%20%E4%BD%A0%E5%A5%BD%20%F0%9F%8C%B1", UrlEncodingMode.UrlComponent, "café 你好 🌱")]
    public void DecodesComponentAndQueryValueUsingTheirDistinctPlusRules(
        string input,
        UrlEncodingMode mode,
        string expected)
    {
        var result = _service.Transform(input, mode, encode: false);

        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData("%")]
    [InlineData("%2")]
    [InlineData("%GG")]
    [InlineData("%C3")]
    public void RejectsMalformedEscapesAndInvalidUtf8(string input)
    {
        Assert.ThrowsAny<FormatException>(() =>
            _service.Transform(input, UrlEncodingMode.UrlComponent, encode: false));
    }

    [Theory]
    [InlineData("https://example.com/a b?q=hello world&x=1#frag ment",
        "https://example.com/a%20b?q=hello%20world&x=1#frag%20ment")]
    [InlineData("https://example.com/a%2Fb?x=a+b&y=%2F",
        "https://example.com/a%2Fb?x=a+b&y=%2F")]
    [InlineData("https://example.com/café/你好",
        "https://example.com/caf%C3%A9/%E4%BD%A0%E5%A5%BD")]
    public void FullUrlEncodingPreservesStructureAndExistingEscapes(string input, string expected)
    {
        var result = _service.Transform(input, UrlEncodingMode.FullUrl, encode: true);

        Assert.Equal(expected, result.Value);
        Assert.NotNull(result.Components);
        Assert.Equal("https", result.Components.Scheme);
        Assert.Equal("example.com", result.Components.Authority);
        Assert.StartsWith("/", result.Components.Path);
    }

    [Fact]
    public void FullUrlDecodeDoesNotTurnEscapedReservedCharactersIntoDelimiters()
    {
        var result = _service.Transform(
            "https://example.com/a%2Fb%3Fc?q=hello%20world&plus=a+b",
            UrlEncodingMode.FullUrl,
            encode: false);

        Assert.Equal("https://example.com/a%2Fb%3Fc?q=hello world&plus=a+b", result.Value);
        Assert.NotNull(result.Components);
        Assert.Contains("q=hello%20world", result.Components.Query);
    }

    [Theory]
    [InlineData("https://example.com/%")]
    [InlineData("https://example.com/%2")]
    [InlineData("example.com/path")]
    [InlineData("not a url")]
    public void FullUrlModeRejectsMalformedEscapesAndNonAbsoluteUrls(string input)
    {
        Assert.Throws<FormatException>(() =>
            _service.Transform(input, UrlEncodingMode.FullUrl, encode: false));
    }

    [Fact]
    public void FullUrlModeRejectsMalformedPercentEscapeWhenEncoding()
    {
        Assert.Throws<FormatException>(() =>
            _service.Transform("https://example.com/%GG", UrlEncodingMode.FullUrl, encode: true));
    }

    [Fact]
    public void EncodeRejectsInvalidUtf16InsteadOfReplacingCharacters()
    {
        Assert.Throws<EncoderFallbackException>(() =>
            _service.Transform("\uD800", UrlEncodingMode.UrlComponent, encode: true));
    }
}
