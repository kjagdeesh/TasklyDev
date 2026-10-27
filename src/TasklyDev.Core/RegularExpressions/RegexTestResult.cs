namespace TasklyDev.Core.RegularExpressions;

public sealed record RegexCaptureGroup(string Name, int Index, int Length, string Value);

public sealed record RegexMatchResult(
    int Index,
    int Length,
    string Value,
    IReadOnlyList<RegexCaptureGroup> Groups);

public sealed record RegexTestResult(
    IReadOnlyList<RegexMatchResult> Matches,
    bool IsTruncated);
