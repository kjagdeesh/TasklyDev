namespace TasklyDev.Core.Tokens;

/// <summary>Describes a local JWT encoding attempt and the compact token it produced.</summary>
public sealed record JwtEncodeResult(
    bool IsValid,
    string Token,
    string HeaderJson,
    string Message);
