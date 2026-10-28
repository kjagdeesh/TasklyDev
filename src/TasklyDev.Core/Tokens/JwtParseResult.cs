namespace TasklyDev.Core.Tokens;

/// <summary>Describes a local JWT decoding attempt without asserting signature validity or trust.</summary>
public sealed record JwtParseResult(
    bool IsValid,
    string HeaderJson,
    string PayloadJson,
    IReadOnlyList<JwtClaimInfo> Claims,
    int SignatureLength,
    string Message);
