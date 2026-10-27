namespace TasklyDev.Windows.Processes;

public sealed record ProcessSnapshotEntry(
    int Id,
    string Name,
    string StartTime,
    string ExecutablePath);
