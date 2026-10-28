namespace TasklyDev.Core.ClipboardHistory;

/// <summary>Represents one captured plain-text clipboard entry.</summary>
public sealed record ClipboardHistoryEntry(
    Guid Id,
    DateTimeOffset CapturedAt,
    string Text,
    bool IsPinned,
    long Utf8ByteCount);
