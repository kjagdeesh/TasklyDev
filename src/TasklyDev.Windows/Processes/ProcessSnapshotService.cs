using System.ComponentModel;
using System.Diagnostics;

namespace TasklyDev.Windows.Processes;

public sealed class ProcessSnapshotService : IProcessSnapshotService
{
    public IReadOnlyList<ProcessSnapshotEntry> GetSnapshot()
    {
        var snapshot = new List<ProcessSnapshotEntry>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    var name = process.ProcessName;
                    var id = process.Id;
                    var startTime = TryGetStartTime(process);
                    var executablePath = TryGetExecutablePath(process);
                    snapshot.Add(new ProcessSnapshotEntry(id, name, startTime, executablePath));
                }
                catch (ObjectDisposedException)
                {
                    // The process may exit between enumeration and property access.
                }
                catch (InvalidOperationException)
                {
                    // The process may exit between enumeration and property access.
                }
                catch (Win32Exception)
                {
                    // Process metadata can become inaccessible while a snapshot is being collected.
                }
                catch (NotSupportedException)
                {
                    // A process may not expose metadata through the current platform API.
                }
            }
        }

        return snapshot.OrderBy(process => process.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(process => process.Id)
            .ToArray();
    }

    private static string TryGetStartTime(Process process)
    {
        try
        {
            return process.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch (ObjectDisposedException)
        {
            return "Unavailable (process exited)";
        }
        catch (InvalidOperationException)
        {
            return "Unavailable";
        }
        catch (Win32Exception)
        {
            return "Unavailable (access denied)";
        }
        catch (NotSupportedException)
        {
            return "Unavailable";
        }
    }

    private static string TryGetExecutablePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName ?? "Unavailable";
        }
        catch (ObjectDisposedException)
        {
            return "Unavailable (process exited)";
        }
        catch (InvalidOperationException)
        {
            return "Unavailable (process exited)";
        }
        catch (Win32Exception)
        {
            return "Unavailable (access denied)";
        }
        catch (NotSupportedException)
        {
            return "Unavailable";
        }
    }
}
