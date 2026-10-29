using System.Text;
using TasklyDev.Core.ClipboardHistory;
using TasklyDev.Services;
using TasklyDev.ViewModels;
using TasklyDev.Windows.ClipboardHistory;
using Xunit;

namespace TasklyDev.Tests;

public sealed class ClipboardHistoryViewModelTests
{
    [Fact]
    public async Task CaptureRequiresConsentAndPreviouslyOptedInStateResumesAfterReload()
    {
        var (directory, path) = CreateStoragePath();
        try
        {
            var viewModel = CreateViewModel(path);
            await viewModel.InitializeAsync();

            Assert.Equal(ClipboardCaptureState.Disabled, viewModel.CaptureState);
            await viewModel.StartCaptureAsync();
            Assert.Equal(ClipboardCaptureState.Disabled, viewModel.CaptureState);
            Assert.True(viewModel.IsError);

            viewModel.ConsentConfirmed = true;
            await viewModel.StartCaptureAsync();
            await viewModel.CaptureTextAsync("private text");

            Assert.Equal(ClipboardCaptureState.Capturing, viewModel.CaptureState);
            Assert.False(viewModel.ConsentConfirmed);
            Assert.Equal("private text", Assert.Single(viewModel.Entries).Text);

            var reloaded = CreateViewModel(path);
            await reloaded.InitializeAsync();

            Assert.Equal(ClipboardCaptureState.Capturing, reloaded.CaptureState);
            Assert.False(reloaded.ConsentConfirmed);
            Assert.Equal("private text", Assert.Single(reloaded.Entries).Text);
        }
        finally
        {
            DeleteTestDirectory(directory);
        }
    }

    [Fact]
    public async Task PauseResumeAndDisableControlCaptureWithoutDeletingHistory()
    {
        var (directory, path) = CreateStoragePath();
        try
        {
            var viewModel = CreateViewModel(path);
            await viewModel.InitializeAsync();
            viewModel.ConsentConfirmed = true;
            await viewModel.StartCaptureAsync();
            await viewModel.PauseCaptureAsync();
            await viewModel.CaptureTextAsync("ignored while paused");

            Assert.True(viewModel.IsPaused);
            Assert.Empty(viewModel.Entries);

            await viewModel.ResumeCaptureAsync();
            await viewModel.CaptureTextAsync("keep after disabling");
            await viewModel.DisableCaptureAsync();

            Assert.True(viewModel.IsCaptureDisabled);
            Assert.Single(viewModel.Entries);
            Assert.False(viewModel.ConsentConfirmed);
        }
        finally
        {
            DeleteTestDirectory(directory);
        }
    }

    [Fact]
    public async Task SearchPinDeleteAndConfirmedClearOperateOnSelectedEntries()
    {
        var (directory, path) = CreateStoragePath();
        try
        {
            var confirmation = new TestConfirmationService { ShouldConfirm = false };
            var viewModel = CreateViewModel(path, confirmation);
            await viewModel.InitializeAsync();
            viewModel.ConsentConfirmed = true;
            await viewModel.StartCaptureAsync();
            await viewModel.CaptureTextAsync("alpha value");
            await viewModel.CaptureTextAsync("beta value");

            viewModel.SearchText = "ALPHA";
            var selected = Assert.Single(viewModel.FilteredEntries);
            viewModel.SelectedEntry = selected;
            await viewModel.TogglePinSelectedAsync();

            Assert.True(Assert.Single(viewModel.Entries, entry => entry.Id == selected.Id).IsPinned);

            await viewModel.ClearAllAsync();
            Assert.Equal(2, viewModel.Entries.Count);
            Assert.Contains("canceled", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);

            confirmation.ShouldConfirm = true;
            await viewModel.ClearAllAsync();
            Assert.Empty(viewModel.Entries);
            Assert.Equal(ClipboardCaptureState.Capturing, viewModel.CaptureState);

            viewModel.SearchText = "ALPHA";
            Assert.True(viewModel.HasSearchText);
            viewModel.ClearSearchCommand.Execute(null);
            Assert.Empty(viewModel.SearchText);
            Assert.False(viewModel.HasSearchText);
        }
        finally
        {
            DeleteTestDirectory(directory);
        }
    }

    [Fact]
    public async Task CopyFailureIsReportedWithoutLosingTheSelectedEntry()
    {
        var (directory, path) = CreateStoragePath();
        try
        {
            var clipboard = new TestClipboardService { CanSetText = false };
            var viewModel = CreateViewModel(path, clipboard: clipboard);
            await viewModel.InitializeAsync();
            viewModel.ConsentConfirmed = true;
            await viewModel.StartCaptureAsync();
            await viewModel.CaptureTextAsync("keep me");
            viewModel.SelectedEntry = Assert.Single(viewModel.Entries);

            viewModel.CopySelected();

            Assert.True(viewModel.IsError);
            Assert.Contains("not copied", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Single(viewModel.Entries);
        }
        finally
        {
            DeleteTestDirectory(directory);
        }
    }

    private static ClipboardHistoryViewModel CreateViewModel(
        string path,
        TestConfirmationService? confirmation = null,
        TestClipboardService? clipboard = null) =>
        new(
            new ClipboardHistoryService(path, new TestTextProtector()),
            clipboard ?? new TestClipboardService(),
            confirmation ?? new TestConfirmationService());

    private static (string Directory, string Path) CreateStoragePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"TasklyClipboardViewModelTest-{Guid.NewGuid():N}");
        return (directory, Path.Combine(directory, "clipboard-history.json"));
    }

    private static void DeleteTestDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class TestTextProtector : IClipboardTextProtector
    {
        public string Protect(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

        public string Unprotect(string protectedText) =>
            Encoding.UTF8.GetString(Convert.FromBase64String(protectedText));
    }

    private sealed class TestClipboardService : IClipboardService
    {
        public bool CanSetText { get; init; } = true;

        public bool TrySetText(string text) => CanSetText;
    }

    private sealed class TestConfirmationService : IClipboardHistoryConfirmationService
    {
        public bool ShouldConfirm { get; set; }

        public bool ConfirmClearHistory() => ShouldConfirm;
    }
}
