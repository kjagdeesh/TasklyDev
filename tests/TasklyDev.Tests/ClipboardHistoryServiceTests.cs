using System.Text;
using TasklyDev.Core.ClipboardHistory;
using TasklyDev.Windows.ClipboardHistory;
using Xunit;

namespace TasklyDev.Tests;

public sealed class ClipboardHistoryServiceTests
{
    [Fact]
    public async Task CaptureRequiresOptInAndEncryptedJsonPersistsConsentAndText()
    {
        var (directory, path) = CreateStoragePath();
        try
        {
            var protector = new FakeTextProtector();
            var service = new ClipboardHistoryService(path, protector);
            await service.LoadAsync();

            var ignored = await service.CaptureAsync("sensitive token");
            Assert.Empty(ignored.Entries);
            Assert.Contains("not active", ignored.StatusMessage);

            await service.SetCaptureStateAsync(ClipboardCaptureState.Capturing);
            var captured = await service.CaptureAsync("sensitive token");
            Assert.Single(captured.Entries);
            Assert.Equal("sensitive token", captured.Entries[0].Text);

            var json = await File.ReadAllTextAsync(path);
            Assert.DoesNotContain("sensitive token", json);
            Assert.Contains("protectedText", json);

            var reloaded = await new ClipboardHistoryService(path, protector).LoadAsync();
            Assert.Equal(ClipboardCaptureState.Capturing, reloaded.CaptureState);
            Assert.Equal("sensitive token", Assert.Single(reloaded.Entries).Text);
        }
        finally
        {
            DeleteTestDirectory(directory);
        }
    }

    [Fact]
    public async Task ConsecutiveDuplicatesAreIgnoredAndCountCapEvictsOldestUnpinned()
    {
        var (directory, path) = CreateStoragePath();
        try
        {
            var service = CreateService(path, new ClipboardHistoryLimits(MaximumEntries: 2));
            await service.SetCaptureStateAsync(ClipboardCaptureState.Capturing);
            await service.CaptureAsync("first");
            var duplicate = await service.CaptureAsync("first");
            Assert.Single(duplicate.Entries);
            Assert.Contains("Duplicate", duplicate.StatusMessage);

            await service.CaptureAsync("second");
            var newest = await service.CaptureAsync("third");

            Assert.Equal(["third", "second"], newest.Entries.Select(entry => entry.Text));
            Assert.Contains("added", newest.StatusMessage);
        }
        finally
        {
            DeleteTestDirectory(directory);
        }
    }

    [Fact]
    public async Task AllPinnedEntriesAtCountCapCauseNewEntryToBeSkipped()
    {
        var (directory, path) = CreateStoragePath();
        try
        {
            var service = CreateService(path, new ClipboardHistoryLimits(MaximumEntries: 2));
            await service.SetCaptureStateAsync(ClipboardCaptureState.Capturing);
            await service.CaptureAsync("first");
            await service.CaptureAsync("second");
            var entries = (await service.LoadAsync()).Entries;
            await service.SetPinnedAsync(entries[0].Id, true);
            await service.SetPinnedAsync(entries[1].Id, true);

            var result = await service.CaptureAsync("third");

            Assert.Equal(2, result.Entries.Count);
            Assert.Contains("all entries pinned", result.StatusMessage);
        }
        finally
        {
            DeleteTestDirectory(directory);
        }
    }

    [Fact]
    public async Task ExpiredUnpinnedEntriesAreRemovedWhilePinnedEntriesRemain()
    {
        var (directory, path) = CreateStoragePath();
        try
        {
            var now = DateTimeOffset.Parse("2026-03-01T10:00:00+00:00");
            var service = CreateService(
                path,
                new ClipboardHistoryLimits(RetentionDays: 90),
                clock: () => now);
            await service.SetCaptureStateAsync(ClipboardCaptureState.Capturing);
            await service.CaptureAsync("pinned item");
            var pinned = Assert.Single((await service.LoadAsync()).Entries);
            await service.SetPinnedAsync(pinned.Id, true);
            await service.CaptureAsync("expired item");

            now = now.AddDays(91);
            var result = await service.LoadAsync();

            Assert.Equal("pinned item", Assert.Single(result.Entries).Text);
            Assert.True(result.Entries[0].IsPinned);
            Assert.Contains("cleaned up", result.StatusMessage);
        }
        finally
        {
            DeleteTestDirectory(directory);
        }
    }

    [Fact]
    public async Task OversizedItemsAreSkippedWithoutRemovingExistingHistory()
    {
        var (directory, path) = CreateStoragePath();
        try
        {
            var service = CreateService(path, new ClipboardHistoryLimits(MaximumItemBytes: 4));
            await service.SetCaptureStateAsync(ClipboardCaptureState.Capturing);
            await service.CaptureAsync("keep");

            var result = await service.CaptureAsync("oversized");

            Assert.Equal("keep", Assert.Single(result.Entries).Text);
            Assert.Contains("skipped", result.StatusMessage);
        }
        finally
        {
            DeleteTestDirectory(directory);
        }
    }

    [Fact]
    public async Task TotalStorageCapEvictsOldestUnpinnedEntries()
    {
        var (directory, path) = CreateStoragePath();
        try
        {
            var service = CreateService(
                path,
                new ClipboardHistoryLimits(
                    MaximumEntries: 10,
                    MaximumItemBytes: 512,
                    MaximumStorageBytes: 700));
            await service.SetCaptureStateAsync(ClipboardCaptureState.Capturing);
            await service.CaptureAsync(new string('a', 150));

            var result = await service.CaptureAsync(new string('b', 150));

            Assert.Single(result.Entries);
            Assert.StartsWith("b", result.Entries[0].Text, StringComparison.Ordinal);
            Assert.True(result.StorageSizeBytes <= 700);
            Assert.Contains("oldest unpinned", result.StatusMessage);
        }
        finally
        {
            DeleteTestDirectory(directory);
        }
    }

    [Fact]
    public async Task ClearDeletesEntriesButKeepsTheCaptureConsentState()
    {
        var (directory, path) = CreateStoragePath();
        try
        {
            var service = CreateService(path);
            await service.SetCaptureStateAsync(ClipboardCaptureState.Capturing);
            await service.CaptureAsync("to delete");

            var result = await service.ClearAsync();

            Assert.Empty(result.Entries);
            Assert.Equal(ClipboardCaptureState.Capturing, result.CaptureState);
            Assert.Contains("deleted", result.StatusMessage);
        }
        finally
        {
            DeleteTestDirectory(directory);
        }
    }

    [Fact]
    public async Task CorruptHistoryIsReportedInsteadOfResetToEmpty()
    {
        var (directory, path) = CreateStoragePath();
        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, "{invalid");

            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() =>
                CreateService(path).LoadAsync());
        }
        finally
        {
            DeleteTestDirectory(directory);
        }
    }

    private static ClipboardHistoryService CreateService(
        string path,
        ClipboardHistoryLimits? limits = null,
        Func<DateTimeOffset>? clock = null) =>
        new(path, new FakeTextProtector(), limits, clock);

    private static (string Directory, string Path) CreateStoragePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"TasklyClipboardTest-{Guid.NewGuid():N}");
        return (directory, Path.Combine(directory, "clipboard-history.json"));
    }

    private static void DeleteTestDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class FakeTextProtector : IClipboardTextProtector
    {
        public string Protect(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

        public string Unprotect(string protectedText) =>
            Encoding.UTF8.GetString(Convert.FromBase64String(protectedText));
    }
}
