using TasklyDev.Core.DotNet;
using TasklyDev.Windows.DotNet;
using Xunit;

namespace TasklyDev.Tests;

public sealed class DotNetInspectionServiceTests
{
    [Fact]
    public async Task DiscoversMachineSdkRuntimeWorkloadsAndInfoWithoutProjectSelection()
    {
        var runner = new FakeDotNetCommandRunner([@"C:\Program Files\dotnet\dotnet.exe"])
        {
            Results =
            {
                ["--info"] = Successful("Host:\n  Version: 10.0.0\n  Architecture: x64"),
                ["--list-sdks"] = Successful("10.0.100 [C:\\Program Files\\dotnet\\sdk]\r\n9.0.203 [C:\\Program Files\\dotnet\\sdk]"),
                ["--list-runtimes"] = Successful("Microsoft.AspNetCore.App 10.0.0 [C:\\Program Files\\dotnet\\shared\\Microsoft.AspNetCore.App]\nMicrosoft.NETCore.App 10.0.0 [C:\\Program Files\\dotnet\\shared\\Microsoft.NETCore.App]"),
                ["workload list"] = Successful("Installed Workload Id      Manifest Version\n--------------------------------------------\nwasm-tools                 10.0.0")
            }
        };
        var service = new DotNetInspectionService(runner);

        var result = await service.InspectAsync();

        Assert.True(result.IsAvailable);
        Assert.Equal(@"C:\Program Files\dotnet\dotnet.exe", Assert.Single(result.DotNetPaths));
        Assert.Contains("Machine-wide installation inspection", result.InspectionScope);
        Assert.Equal(2, result.Sdks.Count);
        Assert.Equal("10.0.100", result.Sdks[0].Version);
        Assert.Equal(2, result.Runtimes.Count);
        Assert.Equal("Microsoft.NETCore.App", result.Runtimes[1].Name);
        Assert.Contains("wasm-tools", result.WorkloadOutput);
        Assert.Contains("Architecture: x64", result.InfoOutput);
        Assert.Equal(4, result.Diagnostics.Count);
        Assert.All(result.Diagnostics, diagnostic => Assert.Equal("Succeeded", diagnostic.Status));
        Assert.Equal(4, runner.Calls.Count);
        Assert.Contains(runner.Calls, call => call.Arguments.SequenceEqual(["--list-sdks"]));
        Assert.Contains(runner.Calls, call => call.Arguments.SequenceEqual(["--list-runtimes"]));
        Assert.Contains(runner.Calls, call => call.Arguments.SequenceEqual(["workload", "list"]));
        Assert.DoesNotContain(runner.Calls, call => call.Arguments.Any(argument => argument.Contains("global.json", StringComparison.OrdinalIgnoreCase)));
        Assert.All(runner.Calls, call => Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.System), call.WorkingDirectory));
    }

    [Fact]
    public async Task MissingDotnetHostReturnsExplicitUnavailableResultWithoutRunningCommands()
    {
        var runner = new FakeDotNetCommandRunner([]);
        var service = new DotNetInspectionService(runner);

        var result = await service.InspectAsync();

        Assert.False(result.IsAvailable);
        Assert.Empty(result.DotNetPaths);
        Assert.Contains("could not be located", result.StatusMessage);
        Assert.Contains("Unavailable", Assert.Single(result.Diagnostics).Status);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task InspectsEveryDiscoveredHostAndMergesDuplicateSdkAndRuntimeEntries()
    {
        var runner = new FakeDotNetCommandRunner([@"C:\Program Files\dotnet\dotnet.exe", @"C:\Users\user\.dotnet\dotnet.exe"])
        {
            Results =
            {
                ["--info"] = Successful("Host:\n  Version: 10.0.0"),
                ["--list-sdks"] = Successful("10.0.100 [C:\\Program Files\\dotnet\\sdk]"),
                ["--list-runtimes"] = Successful("Microsoft.NETCore.App 10.0.0 [C:\\Program Files\\dotnet\\shared\\Microsoft.NETCore.App]"),
                ["workload list"] = Successful("No workloads installed.")
            }
        };
        var service = new DotNetInspectionService(runner);

        var result = await service.InspectAsync();

        Assert.True(result.IsAvailable);
        Assert.Equal(2, result.DotNetPaths.Count);
        Assert.Equal(8, runner.Calls.Count);
        Assert.Single(result.Sdks);
        Assert.Single(result.Runtimes);
        Assert.Contains(@"[C:\Program Files\dotnet\dotnet.exe]", result.InfoOutput);
        Assert.Contains(@"[C:\Users\user\.dotnet\dotnet.exe]", result.InfoOutput);
    }

    [Fact]
    public async Task CommandFailuresTimeoutsAndMissingSdkAreReportedIndependently()
    {
        var runner = new FakeDotNetCommandRunner([@"C:\dotnet\dotnet.exe"])
        {
            Results =
            {
                ["--info"] = Successful("info"),
                ["--list-sdks"] = Failed(exitCode: 1, stderr: "SDK command failed."),
                ["--list-runtimes"] = new DotNetCommandResult(true, true, null, TimeSpan.FromSeconds(15), string.Empty, string.Empty, "Timed out."),
                ["workload list"] = Failed(exitCode: 145, stderr: "No SDK found.")
            }
        };
        var service = new DotNetInspectionService(runner);

        var result = await service.InspectAsync();

        Assert.True(result.IsAvailable);
        Assert.Empty(result.Sdks);
        Assert.Empty(result.Runtimes);
        Assert.Equal("Failed (exit code 1)", result.Diagnostics[1].Status);
        Assert.Equal("Timed out", result.Diagnostics[2].Status);
        Assert.Equal("Skipped", result.Diagnostics[3].Status);
        Assert.Equal("SDK command failed.", result.Diagnostics[1].Details);
        Assert.Equal("Timed out.", result.Diagnostics[2].Details);
        Assert.Contains("SDK discovery failed", result.Diagnostics[3].Details);
        Assert.DoesNotContain(runner.Calls, call => call.Arguments.SequenceEqual(["workload", "list"]));
    }

    [Fact]
    public async Task WorkloadCommandFailureIsReportedWhenSdkIsInstalled()
    {
        var runner = new FakeDotNetCommandRunner([@"C:\dotnet\dotnet.exe"])
        {
            Results =
            {
                ["--info"] = Successful("info"),
                ["--list-sdks"] = Successful("10.0.100 [C:\\dotnet\\sdk]"),
                ["--list-runtimes"] = Successful(string.Empty),
                ["workload list"] = Failed(exitCode: 145, stderr: "Workload manifest failed.")
            }
        };
        var service = new DotNetInspectionService(runner);

        var result = await service.InspectAsync();

        Assert.Equal("Failed (exit code 145)", result.Diagnostics[3].Status);
        Assert.Equal("Workload manifest failed.", result.Diagnostics[3].Details);
        Assert.Contains(runner.Calls, call => call.Arguments.SequenceEqual(["workload", "list"]));
    }

    [Fact]
    public async Task WorkloadCommandIsSkippedWhenHostReportsNoSdk()
    {
        var runner = new FakeDotNetCommandRunner([@"C:\dotnet\dotnet.exe"])
        {
            Results =
            {
                ["--info"] = Successful("info"),
                ["--list-sdks"] = Successful(string.Empty),
                ["--list-runtimes"] = Successful(string.Empty)
            }
        };
        var service = new DotNetInspectionService(runner);

        var result = await service.InspectAsync();

        Assert.Equal("Skipped", result.Diagnostics[3].Status);
        Assert.Contains("no installed SDKs", result.Diagnostics[3].Details);
        Assert.DoesNotContain(runner.Calls, call => call.Arguments.SequenceEqual(["workload", "list"]));
    }

    [Fact]
    public async Task CancellationIsPropagatedToStopDiscovery()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var service = new DotNetInspectionService(new FakeDotNetCommandRunner([@"C:\dotnet\dotnet.exe"]));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.InspectAsync(cancellationSource.Token));
    }

    private static DotNetCommandResult Successful(string output) =>
        new(true, false, 0, TimeSpan.FromMilliseconds(20), output, string.Empty, null);

    private static DotNetCommandResult Failed(int exitCode, string stderr) =>
        new(true, false, exitCode, TimeSpan.FromMilliseconds(30), string.Empty, stderr, null);

    private sealed class FakeDotNetCommandRunner(IReadOnlyList<string> dotNetPaths) : IDotNetCommandRunner
    {
        public Dictionary<string, DotNetCommandResult> Results { get; } = new(StringComparer.Ordinal);
        public List<CommandCall> Calls { get; } = [];

        public IReadOnlyList<string> ResolveDotNetPaths() => dotNetPaths;

        public Task<DotNetCommandResult> RunAsync(
            string path,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add(new CommandCall(path, arguments.ToArray(), workingDirectory, timeout));
            var key = string.Join(' ', arguments);
            return Task.FromResult(Results.TryGetValue(key, out var result)
                ? result
                : Failed(1, "No test result configured."));
        }
    }

    private sealed record CommandCall(
        string DotNetPath,
        IReadOnlyList<string> Arguments,
        string WorkingDirectory,
        TimeSpan Timeout);
}
