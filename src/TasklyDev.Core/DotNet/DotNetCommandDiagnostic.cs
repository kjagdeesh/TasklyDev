namespace TasklyDev.Core.DotNet;

/// <summary>Summarizes one read-only .NET CLI command invocation.</summary>
public sealed record DotNetCommandDiagnostic(
    string Command,
    string Status,
    int? ExitCode,
    TimeSpan Duration,
    string Details);
