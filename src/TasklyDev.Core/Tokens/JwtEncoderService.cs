using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TasklyDev.Core.Tokens;

/// <summary>Encodes and signs compact JWTs locally using HS256, HS384, or HS512.</summary>
public sealed class JwtEncoderService : IJwtEncoderService
{
    private const int MaximumJsonDepth = 64;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };
    private static readonly string[] Algorithms = ["HS256", "HS384", "HS512"];

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedAlgorithms => Algorithms;

    /// <inheritdoc />
    public JwtEncodeResult Encode(string? headerJson, string? payloadJson, string? secret, string algorithm)
    {
        if (!Algorithms.Contains(algorithm, StringComparer.Ordinal))
        {
            return Invalid("Select HS256, HS384, or HS512.");
        }

        if (!TryParseObject(headerJson, out var header))
        {
            return Invalid("Header must be a valid JSON object.");
        }

        if (!TryParseObject(payloadJson, out var payload))
        {
            return Invalid("Payload must be a valid JSON object.");
        }

        header!["alg"] = algorithm;
        header["typ"] ??= "JWT";

        var normalizedHeader = header.ToJsonString();
        var normalizedPayload = payload!.ToJsonString();
        var signingInput = $"{EncodeBase64Url(StrictUtf8.GetBytes(normalizedHeader))}.{EncodeBase64Url(StrictUtf8.GetBytes(normalizedPayload))}";
        var key = StrictUtf8.GetBytes(secret ?? string.Empty);
        var signature = Sign(algorithm, key, StrictUtf8.GetBytes(signingInput));
        var token = $"{signingInput}.{EncodeBase64Url(signature)}";
        var prettyHeader = JsonSerializer.Serialize(header, IndentedJsonOptions);

        return new JwtEncodeResult(true, token, prettyHeader, $"JWT encoded locally with {algorithm}. The secret is never stored or sent.");
    }

    private static byte[] Sign(string algorithm, byte[] key, byte[] data) => algorithm switch
    {
        "HS384" => HMACSHA384.HashData(key, data),
        "HS512" => HMACSHA512.HashData(key, data),
        _ => HMACSHA256.HashData(key, data)
    };

    private static bool TryParseObject(string? json, out JsonObject? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            var node = JsonNode.Parse(
                json,
                documentOptions: new JsonDocumentOptions { MaxDepth = MaximumJsonDepth });
            result = node as JsonObject;
            return result is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string EncodeBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static JwtEncodeResult Invalid(string message) => new(false, string.Empty, string.Empty, message);
}
