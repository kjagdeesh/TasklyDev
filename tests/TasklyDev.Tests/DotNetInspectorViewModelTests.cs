using TasklyDev.Core.DotNet;
using TasklyDev.ViewModels;
using Xunit;

namespace TasklyDev.Tests;

public sealed class DotNetInspectorViewModelTests
{
    [Fact]
    public async Task RefreshDisplaysMachineSdkRuntimeWorkloadAndCommandData()
    {
        var service = new FixedInspectionService(CreateResult());
        var viewModel = new DotNetInspectorViewModel(service);

        await viewModel.RefreshAsync();

        Assert.True(viewModel.HasInspected);
        Assert.False(viewModel.IsError);
        Assert.Equal("10.0.100", Assert.Single(viewModel.Sdks).Version);
        Assert.Single(viewModel.Runtimes);
        Assert.Equal(@"C:\Program Files\dotnet\dotnet.exe", viewModel.DotNetPath);
        Assert.Contains("Machine-wide", viewModel.InspectionScope);
        Assert.Contains("wasm-tools", viewModel.WorkloadOutput);
        Assert.Single(viewModel.Diagnostics);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task MissingDotnetIsShownAsUnavailableRatherThanEmptySuccess()
    {
        var result = CreateResult() with
        {
            DotNetPaths = [],
            Sdks = [],
            Runtimes = [],
            IsAvailable = false,
            StatusMessage = "The .NET host could not be located."
        };
        var viewModel = new DotNetInspectorViewModel(new FixedInspectionService(result));

        await viewModel.RefreshAsync();

        Assert.True(viewModel.HasInspected);
        Assert.True(viewModel.IsError);
        Assert.Equal("Unavailable", viewModel.DotNetPath);
        Assert.True(viewModel.HasNoSdks);
        Assert.Contains("could not be located", viewModel.StatusMessage);
    }

    [Fact]
    public async Task ServiceFailureIsNotSilentlyChangedIntoSuccessfulEmptyState()
    {
        var viewModel = new DotNetInspectorViewModel(new FixedInspectionService(CreateResult())
        {
            Failure = new InvalidOperationException("Inspection service failed.")
        });

        await Assert.ThrowsAsync<InvalidOperationException>(viewModel.RefreshAsync);

        Assert.False(viewModel.HasInspected);
        Assert.False(viewModel.IsBusy);
    }

    private static DotNetInspectionResult CreateResult() =>
        new(
            DateTimeOffset.Now,
            "Machine-wide installation inspection; process environment; working directory: C:\\Windows\\System32",
            [@"C:\Program Files\dotnet\dotnet.exe"],
            [new DotNetSdkInfo("10.0.100", @"C:\Program Files\dotnet\sdk")],
            [new DotNetRuntimeInfo("Microsoft.NETCore.App", "10.0.0", @"C:\Program Files\dotnet\shared\Microsoft.NETCore.App")],
            "Installed Workload Id\nwasm-tools",
            "Host Version: 10.0.0",
            [new DotNetCommandDiagnostic("dotnet --info", "Succeeded", 0, TimeSpan.FromMilliseconds(20), "Command completed.")],
            "Machine-wide .NET discovery completed.",
            true);

    private sealed class FixedInspectionService(DotNetInspectionResult result) : IDotNetInspectionService
    {
        public Exception? Failure { get; init; }

        public Task<DotNetInspectionResult> InspectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Failure is not null)
            {
                return Task.FromException<DotNetInspectionResult>(Failure);
            }

            return Task.FromResult(result);
        }
    }
}
