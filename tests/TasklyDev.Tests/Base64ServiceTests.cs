using System.Text;
using TasklyDev.Core.Encoding;
using Xunit;

namespace TasklyDev.Tests;

public sealed class Base64ServiceTests
{
    private readonly Base64Service _service = new();

    [Theory]
    [InlineData("")]
    [InlineData("TasklyDev")]
    [InlineData("Hello, 你好 🌱")]
    public void StandardEncodingRoundTripsUtf8Text(string input)
    {
        var encoded = _service.EncodeUtf8(input, Base64Variant.Standard, includePadding: true);

        Assert.Equal(input, _service.DecodeUtf8(encoded, Base64Variant.Standard));
    }

    [Fact]
    public void UrlSafeEncodingUsesUrlAlphabetAndSupportsMissingPadding()
    {
        var encoded = _service.EncodeUtf8("ÿÿ", Base64Variant.UrlSafe, includePadding: false);

        Assert.Equal("w7_Dvw", encoded);
        Assert.Equal("ÿÿ", _service.DecodeUtf8(encoded, Base64Variant.UrlSafe));
    }

    [Fact]
    public void MimeEncodingWrapsAt76CharactersAndCanBeDecoded()
    {
        var input = new string('a', 100);
        var encoded = _service.EncodeUtf8(input, Base64Variant.Mime, includePadding: true);

        Assert.Equal(76, encoded.Split("\r\n")[0].Length);
        Assert.Equal(input, _service.DecodeUtf8(encoded, Base64Variant.Mime));
    }

    [Fact]
    public void DecodeRejectsMalformedBase64()
    {
        Assert.Throws<FormatException>(() => _service.DecodeUtf8("abcde", Base64Variant.Standard));
    }

    [Fact]
    public void DecodeRejectsBytesThatAreNotValidUtf8()
    {
        var encodedInvalidUtf8 = Convert.ToBase64String([0xFF]);

        Assert.Throws<DecoderFallbackException>(() => _service.DecodeUtf8(encodedInvalidUtf8, Base64Variant.Standard));
    }

    [Fact]
    public void EncodeRejectsUnpairedSurrogateInsteadOfReplacingIt()
    {
        Assert.Throws<EncoderFallbackException>(() =>
            _service.EncodeUtf8("\uD800", Base64Variant.Standard, includePadding: true));
    }
}
