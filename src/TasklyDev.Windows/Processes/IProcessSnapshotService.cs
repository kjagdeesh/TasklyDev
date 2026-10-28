namespace TasklyDev.Windows.Processes;

/// <summary>Provides a read-only snapshot of running processes.</summary>
public interface IProcessSnapshotService
{
    /// <summary>Reads process metadata available to the current user.</summary>
    /// <returns>Snapshot entries sorted by process name and then process ID.</returns>
    IReadOnlyList<ProcessSnapshotEntry> GetSnapshot();
}
