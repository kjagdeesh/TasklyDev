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
    public void ExpandsExponentNotationWithoutLosingNumericPrecision()
    {
        var result = _service.Decode(CreateToken(
            """{"alg":"HS256"}""",
            """{"large":1.516239022222222e+182,"small":1.25e-4,"zero":0e+12}"""));
        var expandedLargeNumber = "1516239022222222" + new string('0', 167);

        Assert.True(result.IsValid, result.Message);
        Assert.Contains($"\"large\": {expandedLargeNumber}", result.PayloadJson);
        Assert.Contains($"\"small\": 0.000125", result.PayloadJson);
        Assert.Contains("\"zero\": 0", result.PayloadJson);
        Assert.Equal(expandedLargeNumber, Assert.Single(result.Claims, claim => claim.Name == "large").Value);
        Assert.Equal("0.000125", Assert.Single(result.Claims, claim => claim.Name == "small").Value);
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

    [Fact]
    public void EnterpriseTokenWithNamespacedClaimsAndArrayAudienceDecodes()
    {
        var token = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCIsImtpZCI6ImF1dGgtay04ZjJjOWIifQ.eyJpc3MiOiJodHRwczovL2F1dGguZW50ZXJwcmlzZS5jb20iLCJzdWIiOiJ1c3JfMGhuODl4MnAzayIsImF1ZCI6WyJodHRwczovL2FwaS5lbnRlcnByaXNlLmNvbS92MiIsImh0dHBzOi8vY2xpZW50LmRhc2hib2FyZC5jb20iXSwiZXhwIjoxNzk4NTQxMzAwLCJuYmYiOjE3Njg1NDEzMDAsImlhdCI6MTc2ODU0MTMwMCwianRpIjoiOGYyYzlzOWItNGEyZC00ZjNjLThjMmEtOWIzYzRkNWUzZjJhIiwiYWN0IjoiMzhkOWEyOTMtMzhhMC00OTI5LTk2ZTItZDMyZmE2ZGU3YjBiIiwiY2xpZW50X2lkIjoiYXBwX2Zyb250ZW5kX3BybyIsInNjb3BlIjoib3BlbmlkIHByb2ZpbGUgZW1haWwgdXNlcjpkYXRhOnJlYWQgdXNlcjpkYXRhOndyaXRlIGFkbWluOmF1ZGl0IiwiaHR0cHM6Ly9lbnRlcnByaXNlLmNvbS9jbGFpbXMvdXNlcl9tZXRhZGF0YSI6eyJmaXJzdF9uYW1lIjoiQWxleGEiLCJsYXN0X25hbWUiOiJWYW5kZXJiZXJnIiwiZW1haWwiOiJhbGV4YS52QGVudGVycHJpc2UuY29tIiwiZW1haWxfdmVyaWZpZWQiOnRydWUsInBob25lX251bWJlciI6IisxNTU1MDE5OTk4OCJ9LCJodHRwczovL2VudGVycHJpc2UuY29tL2NsYWltcy9yb2xlcyI6WyJkZXZlbG9wZXIiLCJhdWRpdG9yIl0sImh0dHBzOi8vY2xpZW50LmRhc2hib2FyZC5jb20vY29udGV4dCI6eyJ0ZW5hbnRfaWQiOiJjdXN0X2FjbWVfY29ycF85OSIsImxvY2F0aW9uIjoiVVMtTlkiLCJpcF9hZGRyZXNzIjoiMTkyLjAuMi42MiIsImRldmljZV9vcyI6Im1hY09TIn19.SlnN1N42iM8Fj1L5LqZt9r0X5Vz8mK2n3P5Qz7w9b1c0Y8N3m5x7z9L1V3b5c7NzlL2b3Y4c5TzdeZmdoS2kxTTVTck1wU1pSMXBUVFZSVFZVNVVVRlpWVTFSU1ZGWlhUVlpXVTFVVVZGWlhWVlpWVVRGU1ZGWlhUVlpXVTFVVVZGWlhWVlpWVVRGU1ZGWlhUVlpXVTFVVVZGWlhWVlpWVVRGU1ZGWlhUVlpXVTFVVVZGWlhWVlpWVVQ";

        var result = _service.Decode(token);

        Assert.True(result.IsValid, result.Message);
        Assert.Contains("RS256", result.HeaderJson);
        Assert.Contains("user_metadata", result.PayloadJson);
    }

    private static string CreateToken(string headerJson, string payloadJson, string signature = "c2ln") =>
        $"{EncodeBase64Url(Encoding.UTF8.GetBytes(headerJson))}.{EncodeBase64Url(Encoding.UTF8.GetBytes(payloadJson))}.{signature}";

    private static string EncodeBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
