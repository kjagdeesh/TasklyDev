namespace TasklyDev.Core.ClipboardHistory;

/// <summary>Provides bounded, user-controlled plain-text clipboard history operations.</summary>
public interface IClipboardHistoryService
{
    /// <summary>Loads the locally stored entries and remembered capture consent.</summary>
    Task<ClipboardHistorySnapshot> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Updates the persisted capture state after an explicit user action.</summary>
    Task<ClipboardHistorySnapshot> SetCaptureStateAsync(
        ClipboardCaptureState state,
        CancellationToken cancellationToken = default);

    /// <summary>Stores one plain-text clipboard value when capture is active.</summary>
    Task<ClipboardHistorySnapshot> CaptureAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>Sets whether a stored entry is pinned.</summary>
    Task<ClipboardHistorySnapshot> SetPinnedAsync(
        Guid entryId,
        bool isPinned,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes one stored entry.</summary>
    Task<ClipboardHistorySnapshot> DeleteAsync(Guid entryId, CancellationToken cancellationToken = default);

    /// <summary>Deletes all stored entries and retains the current capture state.</summary>
    Task<ClipboardHistorySnapshot> ClearAsync(CancellationToken cancellationToken = default);
}
