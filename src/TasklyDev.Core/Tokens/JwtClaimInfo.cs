namespace TasklyDev.Core.Tokens;

/// <summary>Contains a display-safe representation of a JWT claim and optional time interpretation.</summary>
public sealed record JwtClaimInfo(
    string Name,
    string Value,
    string JsonType,
    string? TimeInterpretation);
