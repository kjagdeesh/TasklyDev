namespace TasklyDev.Windows.DotNet;

/// <summary>Resolves and executes a .NET CLI process without invoking a command shell.</summary>
public interface IDotNetCommandRunner
{
    /// <summary>Gets resolved dotnet host paths from PATH and known installation roots.</summary>
    IReadOnlyList<string> ResolveDotNetPaths();

    /// <summary>Runs dotnet with discrete arguments in a controlled working directory.</summary>
    Task<DotNetCommandResult> RunAsync(
        string dotNetPath,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
