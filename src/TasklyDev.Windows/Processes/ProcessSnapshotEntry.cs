namespace TasklyDev.Windows.Processes;

/// <summary>Stores the process metadata collected for one process snapshot row.</summary>
public sealed record ProcessSnapshotEntry(
    int Id,
    string Name,
    string StartTime,
    string ExecutablePath);
