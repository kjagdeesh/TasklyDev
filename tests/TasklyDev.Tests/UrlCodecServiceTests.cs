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

    [Fact]
    public void FullUrlDecodeAcceptsEntireUrlPercentEncodedAsAComponent()
    {
        var result = _service.Transform(
            "https%3A%2F%2Fexample.com%2Fsearch%3Fq%3DSample%2BTest%26token%3Dabc%253A123",
            UrlEncodingMode.FullUrl,
            encode: false);

        Assert.Equal("https://example.com/search?q=Sample+Test&token=abc%3A123", result.Value);
        Assert.NotNull(result.Components);
        Assert.Equal("https", result.Components.Scheme);
        Assert.Equal("example.com", result.Components.Authority);
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

    [Theory]
    [InlineData(UrlCharacterEncoding.Utf8, "caf%C3%A9")]
    [InlineData(UrlCharacterEncoding.Iso88591, "caf%E9")]
    [InlineData(UrlCharacterEncoding.Windows1252, "caf%E9")]
    [InlineData(UrlCharacterEncoding.Utf16LittleEndian, "caf%E9%00")]
    public void EncodesAndDecodesWithSelectedCharacterEncoding(UrlCharacterEncoding encoding, string expected)
    {
        var encoded = _service.Transform(
            "café",
            UrlEncodingMode.UrlComponent,
            encode: true,
            characterEncoding: encoding);
        var decoded = _service.Transform(
            encoded.Value,
            UrlEncodingMode.UrlComponent,
            encode: false,
            characterEncoding: encoding);

        Assert.Equal(expected, encoded.Value);
        Assert.Equal("café", decoded.Value);
    }

    [Fact]
    public void RejectsCharactersThatCannotBeRepresentedBySelectedEncoding()
    {
        Assert.Throws<EncoderFallbackException>(() =>
            _service.Transform(
                "café",
                UrlEncodingMode.UrlComponent,
                encode: true,
                characterEncoding: UrlCharacterEncoding.Ascii));
    }

    [Fact]
    public void NormalizesEncodedLineEndingsToSelectedDestination()
    {
        var encoded = _service.Transform(
            "first\r\nsecond\rthird\n",
            UrlEncodingMode.UrlComponent,
            encode: true,
            destinationLineEnding: UrlLineEnding.CrLf);
        var decoded = _service.Transform(
            "first%0Asecond%0Athird",
            UrlEncodingMode.UrlComponent,
            encode: false,
            destinationLineEnding: UrlLineEnding.CrLf);

        Assert.Equal("first%0D%0Asecond%0D%0Athird%0D%0A", encoded.Value);
        Assert.Equal("first\r\nsecond\r\nthird", decoded.Value);
    }

    [Fact]
    public void DecodesIndependentLinesAndRepeatedEncodingLayers()
    {
        var result = _service.Transform(
            "one%2520two\nthree%2520four",
            UrlEncodingMode.UrlComponent,
            encode: false,
            decodeEachLine: true,
            recursiveDecode: true);

        Assert.Equal("one two\nthree four", result.Value);
    }
}
