using System.ComponentModel;
using TasklyDev.ViewModels;
using TasklyDev.Windows.Processes;
using Xunit;

namespace TasklyDev.Tests;

public sealed class ProcessLookupViewModelTests
{
    [Fact]
    public async Task RefreshLoadsSnapshotAndSupportsCaseInsensitiveNameSearch()
    {
        var service = new FixedProcessSnapshotService(
        [
            new ProcessSnapshotEntry(12, "Editor", "2026-10-27 12:00:00", @"C:\Tools\editor.exe"),
            new ProcessSnapshotEntry(34, "Terminal", "Unavailable", "Unavailable (access denied)")
        ]);
        var viewModel = new ProcessLookupViewModel(service);

        await viewModel.RefreshAsync();

        Assert.Equal(2, viewModel.SnapshotCount);
        Assert.Equal(2, viewModel.Processes.Count);
        Assert.Contains("Showing 2 of 2", viewModel.StatusMessage);

        viewModel.SearchText = "TERM";

        var result = Assert.Single(viewModel.Processes);
        Assert.Equal("Terminal", result.Name);
        Assert.Equal("Unavailable (access denied)", result.ExecutablePath);
    }

    [Fact]
    public async Task NumericSearchMatchesExactPidOnly()
    {
        var viewModel = new ProcessLookupViewModel(new FixedProcessSnapshotService(
        [
            new ProcessSnapshotEntry(123, "parent123", "Unavailable", "Unavailable"),
            new ProcessSnapshotEntry(23, "worker", "Unavailable", "Unavailable")
        ]));

        await viewModel.RefreshAsync();
        viewModel.SearchText = "23";

        Assert.Equal(23, Assert.Single(viewModel.Processes).Id);
    }

    [Fact]
    public async Task NoResultsStateAppearsAfterSearchOrEmptySnapshot()
    {
        var viewModel = new ProcessLookupViewModel(new FixedProcessSnapshotService([]));

        await viewModel.RefreshAsync();
        Assert.True(viewModel.HasNoResults);

        viewModel.SearchText = "missing";
        Assert.True(viewModel.HasNoResults);
    }

    [Fact]
    public async Task SnapshotFailureIsShownWithoutThrowingOrRetainingStaleRows()
    {
        var service = new FixedProcessSnapshotService(
        [
            new ProcessSnapshotEntry(12, "Editor", "Unavailable", "Unavailable")
        ]);
        var viewModel = new ProcessLookupViewModel(service);
        await viewModel.RefreshAsync();

        service.Failure = new UnauthorizedAccessException();
        await viewModel.RefreshAsync();

        Assert.True(viewModel.IsError);
        Assert.False(viewModel.HasSnapshot);
        Assert.Empty(viewModel.Processes);
        Assert.Contains("Access was denied", viewModel.StatusMessage);
    }

    [Fact]
    public async Task RefreshCommandRequestsANewSnapshot()
    {
        var service = new FixedProcessSnapshotService([]);
        var viewModel = new ProcessLookupViewModel(service);

        await viewModel.RefreshAsync();
        Assert.Equal(1, service.CallCount);
        await viewModel.RefreshAsync();
        Assert.Equal(2, service.CallCount);
    }

    private sealed class FixedProcessSnapshotService(IReadOnlyList<ProcessSnapshotEntry> entries) : IProcessSnapshotService
    {
        public Exception? Failure { get; set; }
        public int CallCount { get; private set; }

        public IReadOnlyList<ProcessSnapshotEntry> GetSnapshot()
        {
            CallCount++;
            if (Failure is UnauthorizedAccessException unauthorizedAccessException)
            {
                throw unauthorizedAccessException;
            }

            if (Failure is Win32Exception win32Exception)
            {
                throw win32Exception;
            }

            if (Failure is InvalidOperationException invalidOperationException)
            {
                throw invalidOperationException;
            }

            return entries;
        }
    }
}
