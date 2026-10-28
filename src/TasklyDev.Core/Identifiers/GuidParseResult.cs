namespace TasklyDev.Core.Identifiers;

/// <summary>Describes a GUID parse attempt and its normalized result.</summary>
public sealed record GuidParseResult(
    bool IsValid,
    string? CanonicalValue,
    GuidInputFormat? InputFormat,
    string VersionDescription,
    string Message);
