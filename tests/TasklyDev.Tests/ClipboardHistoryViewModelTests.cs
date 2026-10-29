using System.Text;
using System.Globalization;
using TasklyDev.Core.ClipboardHistory;
using TasklyDev.Services;
using TasklyDev.ViewModels;
using TasklyDev.Views;
using TasklyDev.Windows.ClipboardHistory;
using Xunit;

namespace TasklyDev.Tests;

public sealed class ClipboardHistoryViewModelTests
{
    [Fact]
    public void ClipboardHistoryTitleTrimsLeadingWhitespaceAndUsesTheFirstLine()
    {
        var converter = new ClipboardHistoryTitleConverter();

        var title = converter.Convert("   Clipboard title\r\nAdditional details", typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("Clipboard title", title);
    }

    [Fact]
    public void ClipboardHistoryTitleBoundsLongSingleLineText()
    {
        var converter = new ClipboardHistoryTitleConverter();

        var title = converter.Convert(new string('x', 1_048_576), typeof(string), null, CultureInfo.InvariantCulture);

        var titleText = Assert.IsType<string>(title);
        Assert.Equal(120, titleText.Length);
        Assert.EndsWith("\u2026", titleText, StringComparison.Ordinal);
    }

    [Fact]
    public void ClipboardHistorySizeFormatsBytesAndLargerUnits()
    {
        var converter = new ClipboardHistorySizeConverter();

        Assert.Equal("4 B", converter.Convert(4L, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("1.00 KB", converter.Convert(1024L, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("1.00 MB", converter.Convert(1_048_576L, typeof(string), null, CultureInfo.InvariantCulture));
    }

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
            await viewModel.CaptureTextAsync("   alpha value");
            await viewModel.CaptureTextAsync("beta value");

            Assert.Equal("2 of 2 entries", viewModel.HistoryCountSummary);
            Assert.True(viewModel.StorageUsageMegabytes > 0);
            Assert.InRange(viewModel.StorageUsageMegabytes, 0, 100);
            Assert.Equal(7, viewModel.SortOptions.Count);

            viewModel.SelectedSortOption = viewModel.SortOptions.Single(option =>
                option.Mode == ClipboardHistorySortMode.ClipboardTextAscending);
            Assert.Equal("   alpha value", viewModel.FilteredEntries[0].Text);

            viewModel.SearchText = "ALPHA";
            Assert.Equal("1 of 2 entries", viewModel.HistoryCountSummary);
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
    public async Task StorageSortOrdersByUtf8SizeAndKeepsPinnedEntriesFirst()
    {
        var (directory, path) = CreateStoragePath();
        try
        {
            var viewModel = CreateViewModel(path);
            await viewModel.InitializeAsync();
            viewModel.ConsentConfirmed = true;
            await viewModel.StartCaptureAsync();
            await viewModel.CaptureTextAsync("a");
            await viewModel.CaptureTextAsync("medium");
            await viewModel.CaptureTextAsync("😀");

            var pinnedEntry = viewModel.Entries.Single(entry => entry.Text == "a");
            viewModel.SelectedEntry = pinnedEntry;
            await viewModel.TogglePinSelectedAsync();

            viewModel.SelectedSortOption = viewModel.SortOptions.Single(option =>
                option.Mode == ClipboardHistorySortMode.LargestFirst);

            Assert.Equal(
                ["a", "medium", "😀"],
                viewModel.FilteredEntries.Select(entry => entry.Text));
            Assert.Equal(4, viewModel.Entries.Single(entry => entry.Text == "😀").Utf8ByteCount);

            viewModel.SelectedSortOption = viewModel.SortOptions.Single(option =>
                option.Mode == ClipboardHistorySortMode.SmallestFirst);

            Assert.Equal(
                ["a", "😀", "medium"],
                viewModel.FilteredEntries.Select(entry => entry.Text));
        }
        finally
        {
            DeleteTestDirectory(directory);
        }
    }

    [Fact]
    public async Task PinnedEntriesStayAtTopAndPinnedOnlySortFiltersTheList()
    {
        var (directory, path) = CreateStoragePath();
        try
        {
            var viewModel = CreateViewModel(path);
            await viewModel.InitializeAsync();
            viewModel.ConsentConfirmed = true;
            await viewModel.StartCaptureAsync();
            await viewModel.CaptureTextAsync("Zulu unpinned");
            await viewModel.CaptureTextAsync("Beta pinned");
            await viewModel.CaptureTextAsync("Alpha pinned");

            foreach (var text in new[] { "Beta pinned", "Alpha pinned" })
            {
                viewModel.SelectedEntry = viewModel.Entries.Single(entry => entry.Text == text);
                await viewModel.TogglePinSelectedAsync();
            }

            foreach (var sortOption in viewModel.SortOptions.Where(option =>
                         option.Mode != ClipboardHistorySortMode.PinnedOnly))
            {
                viewModel.SelectedSortOption = sortOption;

                Assert.Equal(2, viewModel.FilteredEntries.TakeWhile(entry => entry.IsPinned).Count());
                Assert.DoesNotContain(viewModel.FilteredEntries.SkipWhile(entry => entry.IsPinned), entry => entry.IsPinned);
            }

            viewModel.SelectedEntry = viewModel.Entries.Single(entry => entry.Text == "Alpha pinned");
            viewModel.SelectedSortOption = viewModel.SortOptions.Single(option =>
                option.Mode == ClipboardHistorySortMode.PinnedOnly);
            await viewModel.TogglePinSelectedAsync();

            Assert.True(viewModel.HasSelectedEntry);
            Assert.Null(viewModel.SelectedListEntry);
            Assert.Contains("unpinned", viewModel.ActionFeedback, StringComparison.OrdinalIgnoreCase);

            await viewModel.TogglePinSelectedAsync();
            Assert.True(viewModel.HasSelectedEntry);
            Assert.True(viewModel.SelectedEntryIsPinned);

            viewModel.SelectedSortOption = viewModel.SortOptions.Single(option =>
                option.Mode == ClipboardHistorySortMode.NewestFirst);
            viewModel.SelectedListEntry = viewModel.Entries.Single(entry => entry.Text == "Zulu unpinned");
            viewModel.SelectedSortOption = viewModel.SortOptions.Single(option =>
                option.Mode == ClipboardHistorySortMode.PinnedOnly);

            Assert.Equal(2, viewModel.FilteredEntries.Count);
            Assert.All(viewModel.FilteredEntries, entry => Assert.True(entry.IsPinned));
            Assert.Equal("2 of 3 entries", viewModel.HistoryCountSummary);
            Assert.True(viewModel.HasSelectedEntry);
            Assert.Null(viewModel.SelectedListEntry);
        }
        finally
        {
            DeleteTestDirectory(directory);
        }
    }

    [Fact]
    public async Task DeleteSelectedRequiresConfirmationAndRemovesOnlySelectedEntry()
    {
        var (directory, path) = CreateStoragePath();
        try
        {
            var confirmation = new TestConfirmationService { ShouldConfirm = false };
            var viewModel = CreateViewModel(path, confirmation);
            await viewModel.InitializeAsync();
            viewModel.ConsentConfirmed = true;
            await viewModel.StartCaptureAsync();
            await viewModel.CaptureTextAsync("keep this entry");
            await viewModel.CaptureTextAsync("delete this entry");
            viewModel.SelectedEntry = viewModel.Entries.Single(entry => entry.Text == "delete this entry");

            await viewModel.DeleteSelectedAsync();

            Assert.Equal(2, viewModel.Entries.Count);
            Assert.Contains("canceled", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);

            confirmation.ShouldConfirm = true;
            await viewModel.DeleteSelectedAsync();

            Assert.Single(viewModel.Entries);
            Assert.Equal("keep this entry", viewModel.Entries[0].Text);
            Assert.Null(viewModel.SelectedEntry);
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
            Assert.Contains("not copied", viewModel.ActionFeedback, StringComparison.OrdinalIgnoreCase);
            Assert.Single(viewModel.Entries);
        }
        finally
        {
            DeleteTestDirectory(directory);
        }
    }

    [Fact]
    public async Task SuccessfulCopyShowsFeedbackAndKeepsThePreviewSelected()
    {
        var (directory, path) = CreateStoragePath();
        try
        {
            var clipboard = new TestClipboardService();
            var viewModel = CreateViewModel(path, clipboard: clipboard);
            await viewModel.InitializeAsync();
            viewModel.ConsentConfirmed = true;
            await viewModel.StartCaptureAsync();
            await viewModel.CaptureTextAsync("copy this content");
            viewModel.SelectedEntry = Assert.Single(viewModel.Entries);

            viewModel.CopySelected();

            Assert.Equal("copy this content", clipboard.LastSetText);
            Assert.Contains("copied", viewModel.ActionFeedback, StringComparison.OrdinalIgnoreCase);
            Assert.True(viewModel.HasSelectedEntry);

            await viewModel.CaptureTextAsync("copy this content");

            Assert.Single(viewModel.Entries);
            Assert.Contains("copied", viewModel.ActionFeedback, StringComparison.OrdinalIgnoreCase);

            await viewModel.CaptureTextAsync("another clipboard value");
            await viewModel.CaptureTextAsync("copy this content");

            Assert.Equal(3, viewModel.Entries.Count);
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
        public string? LastSetText { get; private set; }

        public bool TrySetText(string text)
        {
            if (!CanSetText)
            {
                return false;
            }

            LastSetText = text;
            return true;
        }
    }

    private sealed class TestConfirmationService : IClipboardHistoryConfirmationService
    {
        public bool ShouldConfirm { get; set; }

        public bool ConfirmDeleteSelectedEntry() => ShouldConfirm;

        public bool ConfirmClearHistory() => ShouldConfirm;
    }
}
