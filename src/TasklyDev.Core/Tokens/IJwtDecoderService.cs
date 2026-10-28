namespace TasklyDev.Core.Tokens;

/// <summary>Decodes compact JWT header and payload data locally without validating signatures.</summary>
public interface IJwtDecoderService
{
    /// <summary>Parses a compact token and returns decoded header, payload, and display claims.</summary>
    /// <param name="token">The untrusted compact JWT text.</param>
    /// <returns>A structured result that never indicates cryptographic validation or trust.</returns>
    JwtParseResult Decode(string? token);
}
