using System.Text;
using System.Text.Json;
using TasklyDev.Core.Tokens;
using Xunit;

namespace TasklyDev.Tests;

public sealed class JwtDecoderServiceTests
{
    private readonly JwtDecoderService _service = new();

    [Fact]
    public void DecodesHeaderAndPayloadAndDoesNotClaimSignatureValidation()
    {
        var token = CreateToken(
            """{"alg":"none","typ":"JWT"}""",
            """{"sub":"user-123","admin":true,"roles":["reader","writer"]}""",
            signature: string.Empty);

        var result = _service.Decode(token);

        Assert.True(result.IsValid);
        using var header = JsonDocument.Parse(result.HeaderJson);
        using var payload = JsonDocument.Parse(result.PayloadJson);
        Assert.Equal("none", header.RootElement.GetProperty("alg").GetString());
        Assert.Equal("user-123", payload.RootElement.GetProperty("sub").GetString());
        Assert.Contains("not verified", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, result.SignatureLength);
        Assert.Equal(3, result.Claims.Count);
    }

    [Fact]
    public void DecodesUnicodeTextInHeaderAndPayload()
    {
        var result = _service.Decode(CreateToken(
            """{"typ":"JWT","kid":"你好"}""",
            """{"name":"Zoë 🌱"}"""));

        Assert.True(result.IsValid);
        using var header = JsonDocument.Parse(result.HeaderJson);
        using var payload = JsonDocument.Parse(result.PayloadJson);
        Assert.Equal("你好", header.RootElement.GetProperty("kid").GetString());
        Assert.Equal("Zoë 🌱", payload.RootElement.GetProperty("name").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("a.b")]
    [InlineData("a.b.c.d")]
    [InlineData(".payload.signature")]
    [InlineData("header..signature")]
    public void RejectsWrongSegmentCountOrEmptyRequiredSegments(string token)
    {
        var result = _service.Decode(token);

        Assert.False(result.IsValid);
        Assert.Empty(result.HeaderJson);
        Assert.Empty(result.PayloadJson);
        Assert.Empty(result.Claims);
        Assert.NotEmpty(result.Message);
    }

    [Theory]
    [InlineData("a.b?.c")]
    [InlineData("a=.b.c")]
    [InlineData("a.b.c=")]
    [InlineData("a.b.?")]
    public void RejectsInvalidBase64UrlCharacters(string token)
    {
        var result = _service.Decode(token);

        Assert.False(result.IsValid);
        Assert.Contains("Base64URL", result.Message);
    }

    [Theory]
    [InlineData("A", "e30")]
    [InlineData("e30", "A")]
    [InlineData("eyJhbGciOiJub25lIn0", "eyJ4Ijo")]
    public void RejectsInvalidBase64UrlLengthsOrMalformedJson(string header, string payload)
    {
        var result = _service.Decode($"{header}.{payload}.sig");

        Assert.False(result.IsValid);
        Assert.Empty(result.Claims);
    }

    [Fact]
    public void RejectsNonObjectHeaderOrPayload()
    {
        var result = _service.Decode(CreateToken("[]", """{"sub":"x"}"""));

        Assert.False(result.IsValid);
        Assert.Contains("JSON objects", result.Message);
    }

    [Fact]
    public void InterpretsNumericDateClaimsAndReportsUnsupportedClaimTypes()
    {
        var token = CreateToken(
            """{"alg":"HS256","typ":"JWT"}""",
            """{"exp":1735689600,"nbf":1735689500.5,"iat":"yesterday","sub":"example"}""");

        var result = _service.Decode(token);

        Assert.True(result.IsValid);
        var expiration = Assert.Single(result.Claims, claim => claim.Name == "exp");
        Assert.StartsWith("2025-01-01 00:00:00.000 UTC · local ", expiration.TimeInterpretation);
        var notBefore = Assert.Single(result.Claims, claim => claim.Name == "nbf");
        Assert.StartsWith("2024-12-31 23:58:20.500 UTC · local ", notBefore.TimeInterpretation);
        var issuedAt = Assert.Single(result.Claims, claim => claim.Name == "iat");
        Assert.Contains("Not a numeric date", issuedAt.TimeInterpretation);
    }

    [Fact]
    public void LeavesAbsentOptionalTimeClaimsAbsent()
    {
        var result = _service.Decode(CreateToken(
            """{"alg":"HS256"}""",
            """{"sub":"user"}"""));

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Claims, claim => claim.Name is "exp" or "nbf" or "iat");
    }

    [Fact]
    public void ReportsOutOfRangeNumericDateWithoutFailingTokenDecode()
    {
        var result = _service.Decode(CreateToken(
            """{"alg":"HS256"}""",
            """{"exp":1e300}"""));

        Assert.True(result.IsValid);
        Assert.Contains("outside the supported UTC range", Assert.Single(result.Claims).TimeInterpretation);
    }

    [Fact]
    public void RejectsOversizedToken()
    {
        var result = _service.Decode(new string('a', JwtDecoderService.MaximumTokenLength + 1));

        Assert.False(result.IsValid);
        Assert.Contains("inspection limit", result.Message);
    }

    [Fact]
    public void InvalidUtf8HeaderIsRejected()
    {
        var invalidHeader = Convert.ToBase64String([0xFF]).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var result = _service.Decode($"{invalidHeader}.e30.sig");

        Assert.False(result.IsValid);
        Assert.Contains("UTF-8", result.Message);
    }

    private static string CreateToken(string headerJson, string payloadJson, string signature = "c2ln") =>
        $"{EncodeBase64Url(Encoding.UTF8.GetBytes(headerJson))}.{EncodeBase64Url(Encoding.UTF8.GetBytes(payloadJson))}.{signature}";

    private static string EncodeBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
