using System.Diagnostics;

namespace TasklyDev.Windows.DotNet;

/// <summary>Runs bounded dotnet commands using ProcessStartInfo argument lists, never a shell.</summary>
public sealed class DotNetCommandRunner : IDotNetCommandRunner
{
    /// <inheritdoc />
    public IReadOnlyList<string> ResolveDotNetPaths()
    {
        var executableName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        var path = System.Environment.GetEnvironmentVariable("PATH");
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(path))
        {
            foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(directory.Trim('"'), executableName);
                if (File.Exists(candidate))
                {
                    candidates.Add(Path.GetFullPath(candidate));
                }
            }
        }

        var roots = new[]
        {
            System.Environment.GetEnvironmentVariable("DOTNET_ROOT"),
            System.Environment.GetEnvironmentVariable("DOTNET_ROOT_X64"),
            System.Environment.GetEnvironmentVariable("DOTNET_ROOT_X86"),
            System.Environment.GetEnvironmentVariable("DOTNET_ROOT_ARM64"),
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles) is { Length: > 0 } programFiles
                ? Path.Combine(programFiles, "dotnet")
                : null,
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86) is { Length: > 0 } programFilesX86
                ? Path.Combine(programFilesX86, "dotnet")
                : null,
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile) is { Length: > 0 } userProfile
                ? Path.Combine(userProfile, ".dotnet")
                : null
        };

        return roots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(root => Path.Combine(root!, executableName))
            .Where(File.Exists)
            .Select(Path.GetFullPath)
            .Concat(candidates)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<DotNetCommandResult> RunAsync(
        string dotNetPath,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dotNetPath);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var startInfo = new ProcessStartInfo
        {
            FileName = dotNetPath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (!process.Start())
            {
                stopwatch.Stop();
                return new DotNetCommandResult(
                    false, false, null, stopwatch.Elapsed, string.Empty, string.Empty,
                    "Windows did not start the dotnet process.");
            }

            var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeoutSource = new CancellationTokenSource(timeout);
            using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutSource.Token);

            try
            {
                await process.WaitForExitAsync(linkedSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                TryKill(process);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                stopwatch.Stop();
                return new DotNetCommandResult(
                    true,
                    true,
                    null,
                    stopwatch.Elapsed,
                    await standardOutputTask.ConfigureAwait(false),
                    await standardErrorTask.ConfigureAwait(false),
                    $"Command exceeded the {timeout.TotalSeconds:0}-second time limit.");
            }

            var standardOutput = await standardOutputTask.ConfigureAwait(false);
            var standardError = await standardErrorTask.ConfigureAwait(false);
            stopwatch.Stop();
            return new DotNetCommandResult(
                true,
                false,
                process.ExitCode,
                stopwatch.Elapsed,
                standardOutput,
                standardError,
                null);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            stopwatch.Stop();
            return new DotNetCommandResult(
                false,
                false,
                null,
                stopwatch.Elapsed,
                string.Empty,
                string.Empty,
                exception.Message);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }
}
