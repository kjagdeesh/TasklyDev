namespace TasklyDev.Windows.Processes;

public interface IProcessSnapshotService
{
    IReadOnlyList<ProcessSnapshotEntry> GetSnapshot();
}
