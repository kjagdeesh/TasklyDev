using System.Globalization;
using System.Text.RegularExpressions;
using TasklyDev.Core.DotNet;

namespace TasklyDev.Windows.DotNet;

/// <summary>Collects installed .NET SDKs, runtimes, workloads, and host details for this machine.</summary>
public sealed partial class DotNetInspectionService : IDotNetInspectionService
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(15);
    private static readonly string InspectionDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.System);
    private readonly IDotNetCommandRunner _commandRunner;

    /// <summary>Creates the machine-level inspector with a dotnet process runner.</summary>
    public DotNetInspectionService(IDotNetCommandRunner? commandRunner = null)
    {
        _commandRunner = commandRunner ?? new DotNetCommandRunner();
    }

    /// <inheritdoc />
    public async Task<DotNetInspectionResult> InspectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var inspectedAt = DateTimeOffset.Now;
        var diagnostics = new List<DotNetCommandDiagnostic>();
        var dotNetPaths = _commandRunner.ResolveDotNetPaths();
        if (dotNetPaths.Count == 0)
        {
            diagnostics.Add(new DotNetCommandDiagnostic(
                "Resolve dotnet host",
                "Unavailable",
                null,
                TimeSpan.Zero,
                "dotnet.exe was not found on PATH or in standard DOTNET_ROOT/Program Files locations."));
            return new DotNetInspectionResult(
                inspectedAt,
                $"Machine-wide installation inspection; process environment; working directory: {InspectionDirectory}",
                [],
                [],
                [],
                string.Empty,
                string.Empty,
                diagnostics,
                "The .NET host could not be located. Check PATH, DOTNET_ROOT, and the installation location.",
                false);
        }

        var sdks = new List<DotNetSdkInfo>();
        var runtimes = new List<DotNetRuntimeInfo>();
        var infoOutputs = new List<string>();
        var workloadOutputs = new List<string>();
        var hasSuccessfulDiscovery = false;
        foreach (var dotNetPath in dotNetPaths)
        {
            var info = await RunCommandAsync(dotNetPath, ["--info"], diagnostics, cancellationToken).ConfigureAwait(false);
            var sdkList = await RunCommandAsync(dotNetPath, ["--list-sdks"], diagnostics, cancellationToken).ConfigureAwait(false);
            var runtimeList = await RunCommandAsync(dotNetPath, ["--list-runtimes"], diagnostics, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<DotNetSdkInfo> hostSdks =
                sdkList?.Succeeded == true ? ParseSdks(sdkList.StandardOutput) : Array.Empty<DotNetSdkInfo>();

            if (info?.Succeeded == true)
            {
                hasSuccessfulDiscovery = true;
                infoOutputs.Add($"[{dotNetPath}]{System.Environment.NewLine}{info.StandardOutput}");
            }

            if (sdkList?.Succeeded == true)
            {
                hasSuccessfulDiscovery = true;
                sdks.AddRange(hostSdks);
            }

            if (runtimeList?.Succeeded == true)
            {
                hasSuccessfulDiscovery = true;
                runtimes.AddRange(ParseRuntimes(runtimeList.StandardOutput));
            }

            if (hostSdks.Count == 0)
            {
                diagnostics.Add(new DotNetCommandDiagnostic(
                    $"{dotNetPath} workload list",
                    "Skipped",
                    null,
                    TimeSpan.Zero,
                    sdkList?.Succeeded == true
                        ? "This host reported no installed SDKs; dotnet workload list requires an SDK."
                        : "SDK discovery failed for this host, so the workload command was not run."));
            }
            else
            {
                var workloads = await RunCommandAsync(
                    dotNetPath,
                    ["workload", "list"],
                    diagnostics,
                    cancellationToken).ConfigureAwait(false);
                if (workloads?.Succeeded == true)
                {
                    workloadOutputs.Add($"[{dotNetPath}]{System.Environment.NewLine}{workloads.StandardOutput}");
                }
            }
        }

        var uniqueSdks = sdks.DistinctBy(sdk => (sdk.Version, sdk.InstallPath)).ToArray();
        var uniqueRuntimes = runtimes.DistinctBy(runtime => (runtime.Name, runtime.Version, runtime.InstallPath)).ToArray();
        var status = hasSuccessfulDiscovery
            ? $"Machine-wide .NET discovery completed across {dotNetPaths.Count} known installation host(s). Results do not select an SDK for a project."
            : "The .NET host was found, but its discovery commands did not complete successfully. Review command diagnostics.";

        return new DotNetInspectionResult(
            inspectedAt,
            $"Machine-wide installation inspection; PATH, DOTNET_ROOT, standard Program Files, and per-user .NET roots; working directory: {InspectionDirectory}",
            dotNetPaths,
            uniqueSdks,
            uniqueRuntimes,
            string.Join(System.Environment.NewLine + System.Environment.NewLine, workloadOutputs),
            string.Join(System.Environment.NewLine + System.Environment.NewLine, infoOutputs),
            diagnostics,
            status,
            hasSuccessfulDiscovery);
    }

    private async Task<DotNetCommandResult?> RunCommandAsync(
        string dotNetPath,
        IReadOnlyList<string> arguments,
        ICollection<DotNetCommandDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var commandText = $"{dotNetPath} {string.Join(' ', arguments)}";
        var result = await _commandRunner.RunAsync(
            dotNetPath,
            arguments,
            InspectionDirectory,
            CommandTimeout,
            cancellationToken).ConfigureAwait(false);
        var status = result.Succeeded
            ? "Succeeded"
            : result.TimedOut
                ? "Timed out"
                : !result.Started
                    ? "Unavailable"
                    : $"Failed (exit code {result.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "unknown"})";
        var details = result.Succeeded
            ? "Command completed."
            : result.FailureMessage ?? FirstNonEmptyLine(result.StandardError) ?? "No diagnostic details were returned.";

        diagnostics.Add(new DotNetCommandDiagnostic(
            commandText,
            status,
            result.ExitCode,
            result.Duration,
            details));
        return result;
    }

    private static IReadOnlyList<DotNetSdkInfo> ParseSdks(string output) =>
        output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => SdkLineRegex().Match(line.Trim()))
            .Where(match => match.Success)
            .Select(match => new DotNetSdkInfo(match.Groups["version"].Value, match.Groups["path"].Value))
            .ToArray();

    private static IReadOnlyList<DotNetRuntimeInfo> ParseRuntimes(string output) =>
        output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => RuntimeLineRegex().Match(line.Trim()))
            .Where(match => match.Success)
            .Select(match => new DotNetRuntimeInfo(
                match.Groups["name"].Value,
                match.Groups["version"].Value,
                match.Groups["path"].Value))
            .ToArray();

    private static string? FirstNonEmptyLine(string output) =>
        output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0);

    [GeneratedRegex(@"^(?<version>\S+)\s+\[(?<path>.+)\]$", RegexOptions.CultureInvariant)]
    private static partial Regex SdkLineRegex();

    [GeneratedRegex(@"^(?<name>\S+)\s+(?<version>\S+)\s+\[(?<path>.+)\]$", RegexOptions.CultureInvariant)]
    private static partial Regex RuntimeLineRegex();
}
