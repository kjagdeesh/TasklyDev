using TasklyDev.Windows.DotNet;
using Xunit;

namespace TasklyDev.Tests;

public sealed class DotNetCommandRunnerTests
{
    [Fact]
    public async Task ResolvedDotnetHostRunsVersionAsDiscreteArgumentsWithoutShell()
    {
        var runner = new DotNetCommandRunner();
        var dotNetPath = runner.ResolveDotNetPaths().FirstOrDefault();

        Assert.False(string.IsNullOrWhiteSpace(dotNetPath));

        var result = await runner.RunAsync(
            dotNetPath!,
            ["--version"],
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            TimeSpan.FromSeconds(10),
            CancellationToken.None);

        Assert.True(result.Succeeded, result.FailureMessage ?? result.StandardError);
        Assert.Matches(@"^\d+\.\d+\.\d+", result.StandardOutput.Trim());
        Assert.True(result.Duration >= TimeSpan.Zero);
    }
}
