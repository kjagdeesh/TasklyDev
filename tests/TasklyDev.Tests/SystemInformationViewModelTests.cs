using TasklyDev.Core.SystemInformation;
using TasklyDev.Services;
using TasklyDev.ViewModels;
using Xunit;

namespace TasklyDev.Tests;

public sealed class SystemInformationViewModelTests
{
    [Fact]
    public async Task RefreshShowsPartialAvailabilityAndCollectedGroups()
    {
        var snapshot = CreateSnapshot();
        var viewModel = CreateViewModel(snapshot);

        await viewModel.RefreshAsync();

        Assert.True(viewModel.HasLoaded);
        Assert.False(viewModel.IsError);
        Assert.True(viewModel.HasUnavailableInformation);
        Assert.Contains("some fields are unavailable", viewModel.StatusMessage);
        Assert.Single(viewModel.Groups);
        Assert.Equal(snapshot.GeneratedAt, viewModel.GeneratedAt);
    }

    [Fact]
    public async Task CopySummaryIncludesUnredactedMachineInformation()
    {
        var clipboard = new FakeClipboardService();
        var viewModel = CreateViewModel(CreateSnapshot(), clipboard);
        await viewModel.RefreshAsync();

        viewModel.CopySummaryCommand.Execute(null);

        Assert.Contains("QA-WORKSTATION", clipboard.CopiedText);
        Assert.Contains("Export redaction: none", clipboard.CopiedText);
        Assert.Contains("copied", viewModel.StatusMessage);
        Assert.False(viewModel.IsError);
    }

    [Fact]
    public async Task ExportCommandsSendTextAndJsonReportsToTheFileService()
    {
        var exporter = new FakeExportService();
        var viewModel = CreateViewModel(CreateSnapshot(), exportService: exporter);
        await viewModel.RefreshAsync();

        await viewModel.ExportTextAsync();
        await viewModel.ExportJsonAsync();

        Assert.Equal(
            [SystemInformationExportFormat.Txt, SystemInformationExportFormat.Json],
            exporter.Formats);
        Assert.Contains("QA-WORKSTATION", exporter.Reports[0]);
        Assert.Contains("\"machine name\"", exporter.Reports[1], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("saved to", viewModel.StatusMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task ExportCancellationAndFailureAreShownExplicitly()
    {
        var exporter = new FakeExportService
        {
            NextResult = new SystemInformationReportExportResult(false, true, null, null)
        };
        var viewModel = CreateViewModel(CreateSnapshot(), exportService: exporter);
        await viewModel.RefreshAsync();
        await viewModel.ExportTextAsync();
        Assert.Contains("canceled", viewModel.StatusMessage);
        Assert.False(viewModel.IsError);

        exporter.NextResult = new SystemInformationReportExportResult(false, false, null, "Access denied.");
        await viewModel.ExportJsonAsync();
        Assert.Contains("Access denied", viewModel.StatusMessage);
        Assert.True(viewModel.IsError);
    }

    [Fact]
    public async Task ClipboardFailureAndExportBeforeRefreshAreNotReportedAsSuccess()
    {
        var clipboard = new FakeClipboardService { Succeeds = false };
        var viewModel = CreateViewModel(CreateSnapshot(), clipboard);

        viewModel.CopySummaryCommand.Execute(null);
        Assert.Contains("Refresh", viewModel.StatusMessage);
        Assert.True(viewModel.IsError);

        await viewModel.RefreshAsync();
        viewModel.CopySummaryCommand.Execute(null);
        Assert.Contains("could not be copied", viewModel.StatusMessage);
        Assert.True(viewModel.IsError);
    }

    private static SystemInformationSnapshot CreateSnapshot() =>
        new(
            DateTimeOffset.Parse("2026-01-15T10:30:00+00:00"),
            [
                new SystemInformationGroup("Machine", [
                    new SystemInformationField("Machine name", "QA-WORKSTATION", "Environment.MachineName", true),
                    new SystemInformationField("CPU model", "Unavailable", "Registry", false, "Access denied.")
                ])
            ]);

    private static SystemInformationViewModel CreateViewModel(
        SystemInformationSnapshot snapshot,
        FakeClipboardService? clipboard = null,
        FakeExportService? exportService = null) =>
        new(new FixedSystemInformationService(snapshot), clipboard ?? new FakeClipboardService(), exportService ?? new FakeExportService());

    private sealed class FixedSystemInformationService(SystemInformationSnapshot snapshot) : ISystemInformationService
    {
        public Task<SystemInformationSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(snapshot);
        }
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public bool Succeeds { get; init; } = true;
        public string? CopiedText { get; private set; }

        public bool TrySetText(string text)
        {
            CopiedText = text;
            return Succeeds;
        }
    }

    private sealed class FakeExportService : ISystemInformationReportExportService
    {
        public List<SystemInformationExportFormat> Formats { get; } = [];
        public List<string> Reports { get; } = [];
        public SystemInformationReportExportResult NextResult { get; set; } =
            new(true, false, @"C:\reports\system.txt", null);

        public Task<SystemInformationReportExportResult> ExportAsync(
            string report,
            SystemInformationExportFormat format)
        {
            Formats.Add(format);
            Reports.Add(report);
            return Task.FromResult(NextResult);
        }
    }
}
