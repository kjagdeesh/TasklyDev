namespace TasklyDev.Windows.DotNet;

/// <summary>Contains the result of one bounded dotnet CLI process invocation.</summary>
public sealed record DotNetCommandResult(
    bool Started,
    bool TimedOut,
    int? ExitCode,
    TimeSpan Duration,
    string StandardOutput,
    string StandardError,
    string? FailureMessage)
{
    /// <summary>Gets whether the process completed successfully.</summary>
    public bool Succeeded => Started && !TimedOut && ExitCode == 0;
}
