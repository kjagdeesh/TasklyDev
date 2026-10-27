using System.Security;
using TasklyDev.Services;
using TasklyDev.ViewModels;
using TasklyDev.Windows.Environment;
using Xunit;

namespace TasklyDev.Tests;

public sealed class EnvironmentVariableViewModelTests
{
    [Fact]
    public async Task LoadsScopeAndFiltersByNameOrValueCaseInsensitively()
    {
        var service = new FixedEnvironmentVariableService(
        [
            new EnvironmentVariableEntry("API_TOKEN", "private-token-value"),
            new EnvironmentVariableEntry("EMPTY_VALUE", string.Empty),
            new EnvironmentVariableEntry("PATH", @"C:\Windows")
        ]);
        var viewModel = CreateViewModel(service);

        await viewModel.RefreshAsync();

        Assert.Equal(3, viewModel.SnapshotCount);
        Assert.Equal(EnvironmentVariableScope.Process, service.LastScope);
        Assert.Single(viewModel.Variables, item => item.Value == string.Empty);

        viewModel.SearchText = "token-value";
        Assert.Equal("API_TOKEN", Assert.Single(viewModel.Variables).Name);

        viewModel.SearchText = "path";
        Assert.Equal("PATH", Assert.Single(viewModel.Variables).Name);
    }

    [Fact]
    public async Task SelectionShowsAndCopiesCompleteValue()
    {
        var clipboard = new RecordingClipboard();
        var viewModel = CreateViewModel(
            new FixedEnvironmentVariableService(
            [
                new EnvironmentVariableEntry("LONG_VALUE", new string('x', 500))
            ]),
            clipboard);

        await viewModel.RefreshAsync();
        viewModel.SelectedVariable = Assert.Single(viewModel.Variables);
        viewModel.CopyValueCommand.Execute(null);

        Assert.True(viewModel.HasSelection);
        Assert.Equal(new string('x', 500), viewModel.SelectedValue);
        Assert.Equal(viewModel.SelectedValue, clipboard.Value);
        Assert.Contains("copied", viewModel.StatusMessage);
    }

    [Fact]
    public async Task EmptyScopeAndNoMatchesHaveDistinctObservableStates()
    {
        var viewModel = CreateViewModel(new FixedEnvironmentVariableService([]));

        await viewModel.RefreshAsync();
        Assert.True(viewModel.HasNoResults);
        Assert.Equal(0, viewModel.SnapshotCount);

        var populated = CreateViewModel(new FixedEnvironmentVariableService(
        [
            new EnvironmentVariableEntry("PATH", "somewhere")
        ]));
        await populated.RefreshAsync();
        populated.SearchText = "not-found";
        Assert.True(populated.HasNoResults);
    }

    [Fact]
    public async Task ScopeSelectionLoadsTheSelectedScope()
    {
        var service = new FixedEnvironmentVariableService(
        [
            new EnvironmentVariableEntry("USER_VALUE", "user")
        ]);
        var viewModel = CreateViewModel(service);

        viewModel.SelectedScope = EnvironmentVariableScope.User;
        await WaitForLoadAsync(viewModel);

        Assert.Equal(EnvironmentVariableScope.User, service.LastScope);
        Assert.Equal("USER_VALUE", Assert.Single(viewModel.Variables).Name);
    }

    [Fact]
    public async Task AccessFailureIsSurfacedAndStaleValuesAreRemoved()
    {
        var service = new FixedEnvironmentVariableService(
        [
            new EnvironmentVariableEntry("PATH", "value")
        ]);
        var viewModel = CreateViewModel(service);
        await viewModel.RefreshAsync();
        service.Failure = new UnauthorizedAccessException();

        await viewModel.RefreshAsync();

        Assert.True(viewModel.IsError);
        Assert.False(viewModel.HasLoaded);
        Assert.Empty(viewModel.Variables);
        Assert.Contains("Access was denied", viewModel.StatusMessage);
    }

    [Fact]
    public async Task ClipboardFailureIsReported()
    {
        var clipboard = new RecordingClipboard { Succeeds = false };
        var viewModel = CreateViewModel(
            new FixedEnvironmentVariableService([new EnvironmentVariableEntry("KEY", "value")]),
            clipboard);
        await viewModel.RefreshAsync();
        viewModel.SelectedVariable = Assert.Single(viewModel.Variables);

        viewModel.CopyValueCommand.Execute(null);

        Assert.True(viewModel.IsError);
        Assert.Contains("Could not copy", viewModel.StatusMessage);
    }

    private static EnvironmentVariableViewModel CreateViewModel(
        FixedEnvironmentVariableService service,
        RecordingClipboard? clipboard = null) =>
        new(service, clipboard ?? new RecordingClipboard());

    private static async Task WaitForLoadAsync(EnvironmentVariableViewModel viewModel)
    {
        for (var attempt = 0; attempt < 100 && viewModel.IsBusy; attempt++)
        {
            await Task.Delay(10);
        }

        if (viewModel.IsBusy)
        {
            await viewModel.RefreshAsync();
        }
    }

    private sealed class FixedEnvironmentVariableService(IReadOnlyList<EnvironmentVariableEntry> entries) : IEnvironmentVariableService
    {
        public EnvironmentVariableScope LastScope { get; private set; }
        public Exception? Failure { get; set; }

        public IReadOnlyList<EnvironmentVariableEntry> GetVariables(EnvironmentVariableScope scope)
        {
            LastScope = scope;
            if (Failure is UnauthorizedAccessException unauthorizedAccessException)
            {
                throw unauthorizedAccessException;
            }

            if (Failure is SecurityException securityException)
            {
                throw securityException;
            }

            if (Failure is InvalidOperationException invalidOperationException)
            {
                throw invalidOperationException;
            }

            return entries;
        }
    }

    private sealed class RecordingClipboard : IClipboardService
    {
        public string? Value { get; private set; }
        public bool Succeeds { get; set; } = true;

        public bool TrySetText(string text)
        {
            Value = text;
            return Succeeds;
        }
    }
}
