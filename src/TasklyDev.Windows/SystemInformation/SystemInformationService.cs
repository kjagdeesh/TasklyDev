using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;
using TasklyDev.Core.SystemInformation;

namespace TasklyDev.Windows.SystemInformation;

/// <summary>Collects read-only operating-system and hardware information on Windows.</summary>
public sealed class SystemInformationService : ISystemInformationService
{
    private const string WindowsVersionRegistryPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
    private const string CpuRegistryPath = @"HARDWARE\DESCRIPTION\System\CentralProcessor\0";

    /// <inheritdoc />
    public Task<SystemInformationSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => CollectSnapshot(cancellationToken), cancellationToken);

    private static SystemInformationSnapshot CollectSnapshot(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
        {
            return new SystemInformationSnapshot(
                DateTimeOffset.Now,
                [
                    new SystemInformationGroup("System", [
                        Unavailable("Windows information", "Platform check", "This reader is available only on Windows.")
                    ])
                ]);
        }

        var groups = new List<SystemInformationGroup>
        {
            new("Operating system", [
                ReadRegistryField("Product", WindowsVersionRegistryPath, "ProductName"),
                ReadRegistryField("Release", WindowsVersionRegistryPath, "DisplayVersion"),
                ReadBuildField(),
                Available("OS description", RuntimeInformation.OSDescription, "RuntimeInformation"),
                Available("OS architecture", RuntimeInformation.OSArchitecture.ToString(), "RuntimeInformation")
            ]),
            new("Machine and processor", [
                Available("Machine name", System.Environment.MachineName, "Environment.MachineName"),
                ReadRegistryField("Processor", CpuRegistryPath, "ProcessorNameString"),
                Available("Logical processors", System.Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture), "Environment.ProcessorCount"),
                Available("Process architecture", RuntimeInformation.ProcessArchitecture.ToString(), "RuntimeInformation")
            ]),
            ReadMemoryGroup(),
            ReadStorageGroup(),
            new("Uptime and .NET runtime", [
                Available("System uptime", FormatUptime(System.Environment.TickCount64), "Environment.TickCount64"),
                Available(".NET runtime", RuntimeInformation.FrameworkDescription, "RuntimeInformation"),
                Available(".NET version", System.Environment.Version.ToString(), "Environment.Version")
            ])
        };

        cancellationToken.ThrowIfCancellationRequested();
        return new SystemInformationSnapshot(DateTimeOffset.Now, groups);
    }

    private static SystemInformationField ReadRegistryField(string name, string keyPath, string valueName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath);
            var value = key?.GetValue(valueName)?.ToString();
            return string.IsNullOrWhiteSpace(value)
                ? Unavailable(name, $"HKLM\\{keyPath}", $"The {valueName} value is not available.")
                : Available(name, value, $"HKLM\\{keyPath}\\{valueName}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return Unavailable(name, $"HKLM\\{keyPath}", $"Registry access failed: {exception.Message}");
        }
    }

    private static SystemInformationField ReadBuildField()
    {
        var build = ReadRegistryField("Build", WindowsVersionRegistryPath, "CurrentBuildNumber");
        if (!build.IsAvailable)
        {
            return build;
        }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(WindowsVersionRegistryPath);
            var revision = key?.GetValue("UBR")?.ToString();
            return Available("Build", string.IsNullOrWhiteSpace(revision) ? build.Value : $"{build.Value}.{revision}",
                $"HKLM\\{WindowsVersionRegistryPath}\\CurrentBuildNumber and UBR");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return new SystemInformationField(
                build.Name,
                build.Value,
                build.Source,
                true,
                $"The build revision could not be read: {exception.Message}");
        }
    }

    private static SystemInformationGroup ReadMemoryGroup()
    {
        var status = new MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
        };
        if (!GlobalMemoryStatusEx(ref status))
        {
            var error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            return new SystemInformationGroup("Memory", [
                Unavailable("Total physical memory", "GlobalMemoryStatusEx", error),
                Unavailable("Available physical memory", "GlobalMemoryStatusEx", error),
                Unavailable("Memory load", "GlobalMemoryStatusEx", error)
            ]);
        }

        return new SystemInformationGroup("Memory", [
            Available("Total physical memory", FormatBytes(status.TotalPhysicalMemory), "GlobalMemoryStatusEx"),
            Available("Available physical memory", FormatBytes(status.AvailablePhysicalMemory), "GlobalMemoryStatusEx"),
            Available("Memory load", $"{status.MemoryLoad.ToString(CultureInfo.InvariantCulture)}%", "GlobalMemoryStatusEx")
        ]);
    }

    private static SystemInformationGroup ReadStorageGroup()
    {
        var fields = new List<SystemInformationField>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives().Where(drive => drive.DriveType == DriveType.Fixed))
            {
                try
                {
                    if (!drive.IsReady)
                    {
                        fields.Add(Unavailable(drive.Name, "DriveInfo", "The volume is not ready."));
                        continue;
                    }

                    var totalSize = drive.TotalSize;
                    var availableFreeSpace = drive.AvailableFreeSpace;
                    if (totalSize < 0 || availableFreeSpace < 0)
                    {
                        fields.Add(Unavailable(drive.Name, "DriveInfo", "The volume returned invalid capacity values."));
                        continue;
                    }

                    fields.Add(Available(
                        drive.Name,
                        $"Total {FormatBytes((ulong)totalSize)}; free {FormatBytes((ulong)availableFreeSpace)}",
                        "DriveInfo"));
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException or ArgumentException)
                {
                    fields.Add(Unavailable(drive.Name, "DriveInfo", exception.Message));
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException or ArgumentException)
        {
            fields.Add(Unavailable("Fixed volumes", "DriveInfo.GetDrives", exception.Message));
        }

        if (fields.Count == 0)
        {
            fields.Add(Unavailable("Fixed volumes", "DriveInfo.GetDrives", "No ready fixed volumes were found."));
        }

        return new SystemInformationGroup("Storage", fields);
    }

    private static SystemInformationField Available(string name, string value, string source) =>
        new(name, value, source, true);

    private static SystemInformationField Unavailable(string name, string source, string statusMessage) =>
        new(name, "Unavailable", source, false, statusMessage);

    private static string FormatBytes(ulong bytes) =>
        $"{bytes / (1024d * 1024d * 1024d):N2} GB";

    private static string FormatUptime(long milliseconds) =>
        TimeSpan.FromMilliseconds(milliseconds).ToString("c", CultureInfo.InvariantCulture);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysicalMemory;
        public ulong AvailablePhysicalMemory;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }
}
