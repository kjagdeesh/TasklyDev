using System.Text.Json;
using TasklyDev.Core.Tokens;
using Xunit;

namespace TasklyDev.Tests;

public sealed class JwtEncoderServiceTests
{
    private readonly JwtEncoderService _encoder = new();
    private readonly JwtDecoderService _decoder = new();

    [Fact]
    public void Hs256MatchesTheJwtIoReferenceToken()
    {
        var result = _encoder.Encode(
            "{\"alg\":\"HS256\",\"typ\":\"JWT\"}",
            "{\"sub\":\"1234567890\",\"name\":\"John Doe\",\"iat\":1516239022}",
            "your-256-bit-secret",
            "HS256");

        Assert.True(result.IsValid, result.Message);
        Assert.Equal(
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c",
            result.Token);
    }

    [Theory]
    [InlineData("HS384")]
    [InlineData("HS512")]
    public void EncodedTokenRoundTripsThroughTheDecoderAndUpdatesAlg(string algorithm)
    {
        var result = _encoder.Encode("{\"typ\":\"JWT\"}", "{\"name\":\"Ada\"}", "secret", algorithm);
        var decoded = _decoder.Decode(result.Token);

        Assert.True(decoded.IsValid, decoded.Message);
        using var header = JsonDocument.Parse(decoded.HeaderJson);
        Assert.Equal(algorithm, header.RootElement.GetProperty("alg").GetString());
        Assert.Contains("Ada", decoded.PayloadJson);
    }

    [Theory]
    [InlineData("not json", "{}", "Header")]
    [InlineData("{}", "[1]", "Payload")]
    [InlineData("{}", "", "Payload")]
    public void InvalidJsonIsRejected(string header, string payload, string expected)
    {
        var result = _encoder.Encode(header, payload, "secret", "HS256");

        Assert.False(result.IsValid);
        Assert.Contains(expected, result.Message);
    }

    [Fact]
    public void UnsupportedAlgorithmIsRejected()
    {
        Assert.False(_encoder.Encode("{}", "{}", "secret", "none").IsValid);
    }

    [Fact]
    public void DecoderAcceptsBearerPrefixAndLineBreaks()
    {
        var token = _encoder.Encode("{}", "{\"a\":1}", "s", "HS256").Token;
        var wrapped = "Bearer " + token[..20] + "\r\n" + token[20..];

        Assert.True(_decoder.Decode(wrapped).IsValid);
    }
}
