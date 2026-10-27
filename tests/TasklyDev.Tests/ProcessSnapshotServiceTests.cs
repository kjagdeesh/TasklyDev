using System.Diagnostics;
using TasklyDev.Windows.Processes;
using Xunit;

namespace TasklyDev.Tests;

public sealed class ProcessSnapshotServiceTests
{
    [Fact]
    public void SnapshotIncludesTheCurrentProcessWithStableIdentity()
    {
        using var currentProcess = Process.GetCurrentProcess();
        var service = new ProcessSnapshotService();

        var entry = Assert.Single(service.GetSnapshot(), process => process.Id == currentProcess.Id);

        Assert.Equal(currentProcess.ProcessName, entry.Name);
        Assert.True(entry.Id > 0);
        Assert.False(string.IsNullOrWhiteSpace(entry.StartTime));
        Assert.False(string.IsNullOrWhiteSpace(entry.ExecutablePath));
    }
}
