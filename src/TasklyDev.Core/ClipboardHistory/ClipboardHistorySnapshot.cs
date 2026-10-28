namespace TasklyDev.Core.ClipboardHistory;

/// <summary>Contains the current clipboard capture state, entries, and storage status.</summary>
public sealed record ClipboardHistorySnapshot(
    ClipboardCaptureState CaptureState,
    IReadOnlyList<ClipboardHistoryEntry> Entries,
    string StoragePath,
    long StorageSizeBytes,
    string StatusMessage);
