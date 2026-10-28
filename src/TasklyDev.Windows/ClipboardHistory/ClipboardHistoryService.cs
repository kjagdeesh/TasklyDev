using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TasklyDev.Core.ClipboardHistory;

namespace TasklyDev.Windows.ClipboardHistory;

/// <summary>Stores bounded, encrypted clipboard history in the current user's LocalAppData folder.</summary>
public sealed class ClipboardHistoryService : IClipboardHistoryService
{
    private const int CurrentSchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly string _storagePath;
    private readonly IClipboardTextProtector _protector;
    private readonly ClipboardHistoryLimits _limits;
    private readonly Func<DateTimeOffset> _clock;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private List<EntryState> _entries = [];
    private ClipboardCaptureState _captureState;
    private long _storageSizeBytes;
    private bool _isLoaded;

    /// <summary>Creates the local encrypted history store using the default retention and storage limits.</summary>
    public ClipboardHistoryService(
        string? storagePath = null,
        IClipboardTextProtector? protector = null,
        ClipboardHistoryLimits? limits = null,
        Func<DateTimeOffset>? clock = null)
    {
        _storagePath = storagePath ?? Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
            "TasklyDev",
            "clipboard-history.json");
        _protector = protector ?? new DpapiClipboardTextProtector();
        _limits = limits ?? new ClipboardHistoryLimits();
        _clock = clock ?? (() => DateTimeOffset.Now);
        ValidateLimits(_limits);
    }

    /// <inheritdoc />
    public Task<ClipboardHistorySnapshot> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => LoadCoreAsync(cancellationToken), cancellationToken);

    private async Task<ClipboardHistorySnapshot> LoadCoreAsync(CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            var previousEntries = _entries;
            var removedExpired = RemoveExpiredEntries();
            var removedForCapacity = RemoveEntriesOverCountLimit();
            var data = SerializeCurrentState();
            var removedForStorage = TrimToStorageLimit(ref data, protectedEntryId: null);
            if (removedExpired || removedForCapacity || removedForStorage)
            {
                try
                {
                    await PersistAsync(data, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    _entries = previousEntries;
                    throw;
                }
            }

            var status = removedExpired || removedForCapacity || removedForStorage
                ? "History loaded and expired or over-limit entries were cleaned up."
                : "History loaded from encrypted local storage.";
            return CreateSnapshot(status);
        }
        finally
        {
            _mutex.Release();
        }
    }

    /// <inheritdoc />
    public Task<ClipboardHistorySnapshot> SetCaptureStateAsync(
        ClipboardCaptureState state,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        return Task.Run(() => SetCaptureStateCoreAsync(state, cancellationToken), cancellationToken);
    }

    private async Task<ClipboardHistorySnapshot> SetCaptureStateCoreAsync(
        ClipboardCaptureState state,
        CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            var previousState = _captureState;
            _captureState = state;
            var persisted = false;
            try
            {
                var data = SerializeCurrentState();
                await PersistAsync(data, cancellationToken).ConfigureAwait(false);
                persisted = true;
            }
            finally
            {
                if (!persisted)
                {
                    _captureState = previousState;
                }
            }

            var status = state switch
            {
                ClipboardCaptureState.Capturing => "Clipboard text capture is active. The user's prior opt-in is remembered.",
                ClipboardCaptureState.Paused => "Clipboard capture is paused. Existing history is retained.",
                _ => "Clipboard capture is disabled. Existing history is retained."
            };
            return CreateSnapshot(status);
        }
        finally
        {
            _mutex.Release();
        }
    }

    /// <inheritdoc />
    public Task<ClipboardHistorySnapshot> CaptureAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Task.Run(() => CaptureCoreAsync(text, cancellationToken), cancellationToken);
    }

    private async Task<ClipboardHistorySnapshot> CaptureCoreAsync(
        string text,
        CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            if (_captureState != ClipboardCaptureState.Capturing)
            {
                return CreateSnapshot("Capture is not active; clipboard content was ignored.");
            }

            if (text.Length == 0)
            {
                return CreateSnapshot("Empty clipboard text was ignored.");
            }

            var byteCount = Encoding.UTF8.GetByteCount(text);
            if (byteCount > _limits.MaximumItemBytes)
            {
                return CreateSnapshot($"Clipboard text was skipped because it exceeds the {_limits.MaximumItemBytes / 1024:N0} KB entry limit.");
            }

            var latest = _entries.MaxBy(entry => entry.CapturedAt);
            if (latest?.Text == text)
            {
                return CreateSnapshot("Duplicate clipboard text was not added.");
            }

            var protectedText = _protector.Protect(text);
            var previousEntries = _entries;
            _entries = [.. _entries];
            RemoveExpiredEntries();
            RemoveEntriesOverCountLimit();

            while (_entries.Count >= _limits.MaximumEntries)
            {
                var oldestUnpinned = OldestUnpinnedEntry();
                if (oldestUnpinned is null)
                {
                    _entries = previousEntries;
                    return CreateSnapshot("History is at its entry limit with all entries pinned; the new item was skipped.");
                }

                _entries.Remove(oldestUnpinned);
            }

            var entry = new EntryState(
                Guid.NewGuid(),
                _clock(),
                text,
                protectedText,
                false,
                byteCount);
            _entries.Add(entry);
            var data = SerializeCurrentState();
            var removedForStorage = TrimToStorageLimit(ref data, entry.Id);
            if (data.Length > _limits.MaximumStorageBytes || !_entries.Any(saved => saved.Id == entry.Id))
            {
                _entries = previousEntries;
                return CreateSnapshot("History reached its storage limit with pinned entries; the new item was skipped.");
            }

            var persisted = false;
            try
            {
                await PersistAsync(data, cancellationToken).ConfigureAwait(false);
                persisted = true;
            }
            finally
            {
                if (!persisted)
                {
                    _entries = previousEntries;
                }
            }

            return CreateSnapshot(removedForStorage
                ? "Clipboard text was added; oldest unpinned entries were removed to stay within the storage limit."
                : "Clipboard text was added to encrypted history.");
        }
        finally
        {
            _mutex.Release();
        }
    }

    /// <inheritdoc />
    public Task<ClipboardHistorySnapshot> SetPinnedAsync(
        Guid entryId,
        bool isPinned,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => SetPinnedCoreAsync(entryId, isPinned, cancellationToken), cancellationToken);

    private async Task<ClipboardHistorySnapshot> SetPinnedCoreAsync(
        Guid entryId,
        bool isPinned,
        CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            var index = _entries.FindIndex(entry => entry.Id == entryId);
            if (index < 0)
            {
                return CreateSnapshot("The selected history entry no longer exists.");
            }

            var previousEntries = _entries;
            _entries = [.. _entries];
            _entries[index] = _entries[index] with { IsPinned = isPinned };
            var persisted = false;
            try
            {
                await PersistAsync(SerializeCurrentState(), cancellationToken).ConfigureAwait(false);
                persisted = true;
            }
            finally
            {
                if (!persisted)
                {
                    _entries = previousEntries;
                }
            }

            return CreateSnapshot(isPinned ? "History entry pinned." : "History entry unpinned.");
        }
        finally
        {
            _mutex.Release();
        }
    }

    /// <inheritdoc />
    public Task<ClipboardHistorySnapshot> DeleteAsync(
        Guid entryId,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => DeleteCoreAsync(entryId, cancellationToken), cancellationToken);

    private async Task<ClipboardHistorySnapshot> DeleteCoreAsync(
        Guid entryId,
        CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            var previousEntries = _entries;
            _entries = [.. _entries];
            var removed = _entries.RemoveAll(entry => entry.Id == entryId);
            if (removed == 0)
            {
                return CreateSnapshot("The selected history entry no longer exists.");
            }

            var persisted = false;
            try
            {
                await PersistAsync(SerializeCurrentState(), cancellationToken).ConfigureAwait(false);
                persisted = true;
            }
            finally
            {
                if (!persisted)
                {
                    _entries = previousEntries;
                }
            }

            return CreateSnapshot("History entry deleted.");
        }
        finally
        {
            _mutex.Release();
        }
    }

    /// <inheritdoc />
    public Task<ClipboardHistorySnapshot> ClearAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => ClearCoreAsync(cancellationToken), cancellationToken);

    private async Task<ClipboardHistorySnapshot> ClearCoreAsync(CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            var previousEntries = _entries;
            _entries = [];
            var persisted = false;
            try
            {
                await PersistAsync(SerializeCurrentState(), cancellationToken).ConfigureAwait(false);
                persisted = true;
            }
            finally
            {
                if (!persisted)
                {
                    _entries = previousEntries;
                }
            }

            return CreateSnapshot("All clipboard history entries were deleted.");
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_isLoaded)
        {
            return;
        }

        string content;
        long storedFileLength;
        try
        {
            await using var stream = new FileStream(
                _storagePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);
            storedFileLength = stream.Length;
            if (storedFileLength > _limits.MaximumStorageBytes)
            {
                throw new InvalidDataException("Clipboard history storage exceeds the configured size limit.");
            }

            using var reader = new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 4096,
                leaveOpen: true);
            content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            InitializeEmptyStore();
            return;
        }
        catch (DirectoryNotFoundException)
        {
            InitializeEmptyStore();
            return;
        }
        var document = JsonSerializer.Deserialize<HistoryDocument>(content, JsonOptions)
            ?? throw new InvalidDataException("Clipboard history storage is empty or invalid.");
        if (document.SchemaVersion != CurrentSchemaVersion || document.Entries is null)
        {
            throw new InvalidDataException("Clipboard history storage uses an unsupported format.");
        }

        if (!Enum.IsDefined(document.CaptureState))
        {
            throw new InvalidDataException("Clipboard history contains an unsupported capture state.");
        }

        _captureState = document.CaptureState;
        _entries = document.Entries.Select(item => new EntryState(
            item.Id,
            item.CapturedAt,
            _protector.Unprotect(item.ProtectedText),
            item.ProtectedText,
            item.IsPinned,
            item.Utf8ByteCount)).ToList();
        _storageSizeBytes = storedFileLength;
        _isLoaded = true;
    }

    private void InitializeEmptyStore()
    {
        _captureState = ClipboardCaptureState.Disabled;
        _entries = [];
        _storageSizeBytes = 0;
        _isLoaded = true;
    }

    private bool RemoveExpiredEntries()
    {
        var cutoff = _clock().AddDays(-_limits.RetentionDays);
        return _entries.RemoveAll(entry => !entry.IsPinned && entry.CapturedAt < cutoff) > 0;
    }

    private bool RemoveEntriesOverCountLimit()
    {
        var removed = false;
        while (_entries.Count > _limits.MaximumEntries)
        {
            var oldestUnpinned = OldestUnpinnedEntry();
            if (oldestUnpinned is null)
            {
                break;
            }

            _entries.Remove(oldestUnpinned);
            removed = true;
        }

        return removed;
    }

    private bool TrimToStorageLimit(ref byte[] data, Guid? protectedEntryId)
    {
        var removedEntries = false;
        while (data.Length > _limits.MaximumStorageBytes)
        {
            var oldestUnpinned = _entries
                .Where(entry => !entry.IsPinned && entry.Id != protectedEntryId)
                .MinBy(entry => entry.CapturedAt);
            if (oldestUnpinned is null)
            {
                return true;
            }

            _entries.Remove(oldestUnpinned);
            removedEntries = true;
            data = SerializeCurrentState();
        }

        return removedEntries;
    }

    private EntryState? OldestUnpinnedEntry() =>
        _entries.Where(entry => !entry.IsPinned).MinBy(entry => entry.CapturedAt);

    private byte[] SerializeCurrentState() =>
        JsonSerializer.SerializeToUtf8Bytes(
            new HistoryDocument
            {
                SchemaVersion = CurrentSchemaVersion,
                CaptureState = _captureState,
                Entries = _entries.Select(entry => new StoredEntry
                {
                    Id = entry.Id,
                    CapturedAt = entry.CapturedAt,
                    ProtectedText = entry.ProtectedText,
                    IsPinned = entry.IsPinned,
                    Utf8ByteCount = entry.Utf8ByteCount
                }).ToList()
            },
            JsonOptions);

    private async Task PersistAsync(byte[] data, CancellationToken cancellationToken)
    {
        if (data.Length > _limits.MaximumStorageBytes)
        {
            throw new InvalidDataException("Clipboard history storage exceeds the configured size limit.");
        }

        var directory = Path.GetDirectoryName(_storagePath)
            ?? throw new InvalidOperationException("The clipboard history storage path has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = _storagePath + ".tmp";
        await File.WriteAllBytesAsync(temporaryPath, data, cancellationToken).ConfigureAwait(false);
        File.Move(temporaryPath, _storagePath, overwrite: true);
        _storageSizeBytes = data.Length;
    }

    private ClipboardHistorySnapshot CreateSnapshot(string statusMessage) =>
        new(
            _captureState,
            _entries.OrderByDescending(entry => entry.CapturedAt)
                .Select(entry => entry.ToPublicEntry())
                .ToArray(),
            _storagePath,
            _storageSizeBytes,
            statusMessage);

    private static void ValidateLimits(ClipboardHistoryLimits limits)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(limits.MaximumEntries, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(limits.RetentionDays, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(limits.MaximumItemBytes, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(limits.MaximumStorageBytes, 0);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class HistoryDocument
    {
        public int SchemaVersion { get; set; }
        public ClipboardCaptureState CaptureState { get; set; }
        public List<StoredEntry>? Entries { get; set; } = [];
    }

    private sealed class StoredEntry
    {
        public Guid Id { get; set; }
        public DateTimeOffset CapturedAt { get; set; }
        public string ProtectedText { get; set; } = string.Empty;
        public bool IsPinned { get; set; }
        public long Utf8ByteCount { get; set; }
    }

    private sealed record EntryState(
        Guid Id,
        DateTimeOffset CapturedAt,
        string Text,
        string ProtectedText,
        bool IsPinned,
        long Utf8ByteCount)
    {
        public ClipboardHistoryEntry ToPublicEntry() =>
            new(Id, CapturedAt, Text, IsPinned, Utf8ByteCount);
    }
}
