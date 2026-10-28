namespace TasklyDev.Windows.ClipboardHistory;

/// <summary>Defines the fixed size and retention limits for local clipboard history.</summary>
public sealed record ClipboardHistoryLimits(
    int MaximumEntries = 5_000,
    int RetentionDays = 90,
    int MaximumItemBytes = 1_048_576,
    long MaximumStorageBytes = 100L * 1024 * 1024);
