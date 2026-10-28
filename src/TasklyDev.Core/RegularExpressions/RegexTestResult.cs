namespace TasklyDev.Core.RegularExpressions;

/// <summary>Describes a successful regular-expression capture group.</summary>
public sealed record RegexCaptureGroup(string Name, int Index, int Length, string Value);

/// <summary>Describes a match and its successful capture groups.</summary>
public sealed record RegexMatchResult(
    int Index,
    int Length,
    string Value,
    IReadOnlyList<RegexCaptureGroup> Groups);

/// <summary>Contains the displayed regular-expression matches and truncation state.</summary>
public sealed record RegexTestResult(
    IReadOnlyList<RegexMatchResult> Matches,
    bool IsTruncated);
