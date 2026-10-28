namespace TasklyDev.Core.Tokens;

/// <summary>Builds signed compact JWTs locally from header JSON, payload JSON, and a shared secret.</summary>
public interface IJwtEncoderService
{
    /// <summary>Gets the signing algorithms supported by the encoder.</summary>
    IReadOnlyList<string> SupportedAlgorithms { get; }

    /// <summary>Creates a compact JWT signed with the selected HMAC algorithm.</summary>
    /// <param name="headerJson">Header JSON object; its <c>alg</c> value is set to the selected algorithm.</param>
    /// <param name="payloadJson">Payload JSON object.</param>
    /// <param name="secret">UTF-8 shared secret used for the HMAC signature.</param>
    /// <param name="algorithm">One of <see cref="SupportedAlgorithms"/>.</param>
    /// <returns>The encoded token or a validation message.</returns>
    JwtEncodeResult Encode(string? headerJson, string? payloadJson, string? secret, string algorithm);
}
