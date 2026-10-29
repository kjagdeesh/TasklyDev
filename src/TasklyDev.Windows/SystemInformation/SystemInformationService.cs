using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using Microsoft.Win32;
using TasklyDev.Core.SystemInformation;

namespace TasklyDev.Windows.SystemInformation;

/// <summary>Collects read-only operating-system and hardware information on Windows.</summary>
public sealed class SystemInformationService : ISystemInformationService
{
    private const string WindowsVersionRegistryPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
    private const string CpuRegistryPath = @"HARDWARE\DESCRIPTION\System\CentralProcessor\0";
    private const string BiosRegistryPath = @"HARDWARE\DESCRIPTION\System\BIOS";
    private const string MachineIdentityRegistryPath = @"SOFTWARE\Microsoft\Cryptography";
    private const string SecureBootRegistryPath = @"SYSTEM\CurrentControlSet\Control\SecureBoot\State";
    private const uint RelationAll = 0xffff;
    private const int RelationProcessorCore = 0;
    private const int RelationCache = 2;
    private const int ErrorInsufficientBuffer = 122;
    private static readonly string[] ByteUnits = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];

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

        var processorTopology = ReadProcessorTopologyFields();
        var groups = new List<SystemInformationGroup>
        {
            new("Operating system", [
                ReadRegistryField("Product", WindowsVersionRegistryPath, "ProductName"),
                ReadRegistryField("Windows edition", WindowsVersionRegistryPath, "EditionID"),
                ReadRegistryField("Release", WindowsVersionRegistryPath, "DisplayVersion"),
                ReadRegistryField("Windows installation type", WindowsVersionRegistryPath, "InstallationType"),
                ReadBuildField(),
                ReadWindowsInstallDateField(),
                ReadRegistryField("Windows Product ID", WindowsVersionRegistryPath, "ProductId"),
                ReadRegistryField("Device ID (Machine GUID)", MachineIdentityRegistryPath, "MachineGuid"),
                Available("OS description", RuntimeInformation.OSDescription, "RuntimeInformation"),
                Available("OS architecture", RuntimeInformation.OSArchitecture.ToString(), "RuntimeInformation")
            ]),
            new("Machine and processor", [
                Available("Machine name", System.Environment.MachineName, "Environment.MachineName"),
                ReadRegistryField("Processor", CpuRegistryPath, "ProcessorNameString"),
                ReadClockField(),
                Available("Logical processors / threads", System.Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture), "Environment.ProcessorCount"),
                ..processorTopology,
                Available("Process architecture", RuntimeInformation.ProcessArchitecture.ToString(), "RuntimeInformation")
            ]),
            ReadFirmwareGroup(),
            ReadGraphicsGroup(),
            ReadMemoryGroup(),
            ReadStorageGroup(),
            ReadEnvironmentGroup(),
            new("Uptime and .NET runtime", [
                Available("System uptime", FormatUptime(System.Environment.TickCount64), "Environment.TickCount64"),
                Available("Estimated last boot", DateTimeOffset.Now.Subtract(TimeSpan.FromMilliseconds(System.Environment.TickCount64))
                    .ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture), "Environment.TickCount64 and current time"),
                Available(".NET runtime", RuntimeInformation.FrameworkDescription, "RuntimeInformation"),
                Available(".NET version", System.Environment.Version.ToString(), "Environment.Version"),
                Available("64-bit process", System.Environment.Is64BitProcess ? "Yes" : "No", "Environment.Is64BitProcess")
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

    private static SystemInformationField ReadClockField()
    {
        var clock = ReadRegistryField("Reported processor speed", CpuRegistryPath, "~MHz");
        if (!clock.IsAvailable)
        {
            return clock;
        }

        return uint.TryParse(clock.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var speedMHz) && speedMHz > 0
            ? Available(clock.Name, $"{speedMHz.ToString(CultureInfo.InvariantCulture)} MHz", clock.Source)
            : Unavailable(clock.Name, clock.Source, "Windows did not report a valid processor speed.");
    }

    private static SystemInformationField ReadWindowsInstallDateField()
    {
        var installDate = ReadRegistryField("Windows installation date", WindowsVersionRegistryPath, "InstallDate");
        if (!installDate.IsAvailable)
        {
            return installDate;
        }

        if (!long.TryParse(installDate.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixSeconds))
        {
            return Unavailable(installDate.Name, installDate.Source, "The installation timestamp is not a valid Unix timestamp.");
        }

        try
        {
            return Available(
                installDate.Name,
                DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
                installDate.Source);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Unavailable(installDate.Name, installDate.Source, "The installation timestamp is outside the supported date range.");
        }
    }

    private static SystemInformationGroup ReadFirmwareGroup() =>
        new("Firmware and motherboard", [
            ReadRegistryField("BIOS manufacturer", BiosRegistryPath, "BIOSVendor"),
            ReadRegistryField("BIOS version", BiosRegistryPath, "BIOSVersion"),
            ReadRegistryField("BIOS release date", BiosRegistryPath, "BIOSReleaseDate"),
            ReadRegistryField("System manufacturer", BiosRegistryPath, "SystemManufacturer"),
            ReadRegistryField("System model", BiosRegistryPath, "SystemProductName"),
            ReadRegistryField("System family", BiosRegistryPath, "SystemFamily"),
            ReadRegistryField("System SKU", BiosRegistryPath, "SystemSKU"),
            ReadRegistryField("Motherboard manufacturer", BiosRegistryPath, "BaseBoardManufacturer"),
            ReadRegistryField("Motherboard model", BiosRegistryPath, "BaseBoardProduct"),
            ReadRegistryField("Motherboard version", BiosRegistryPath, "BaseBoardVersion")
        ]);

    private static SystemInformationGroup ReadGraphicsGroup()
    {
        var adapters = new List<(string Name, uint StateFlags, string RegistryKey)>();
        try
        {
            var deviceSize = Marshal.SizeOf<DisplayDevice>();
            for (uint index = 0; index < 64; index++)
            {
                var device = new DisplayDevice
                {
                    Size = deviceSize,
                    DeviceName = string.Empty,
                    DeviceString = string.Empty,
                    DeviceId = string.Empty,
                    DeviceKey = string.Empty
                };
                if (!EnumDisplayDevices(null, index, ref device, 0))
                {
                    break;
                }

                if (!string.IsNullOrWhiteSpace(device.DeviceString))
                {
                    adapters.Add((device.DeviceString.Trim(), device.StateFlags, device.DeviceKey));
                }
            }
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or MarshalDirectiveException)
        {
            return new SystemInformationGroup("Graphics adapters", [
                Unavailable("Display adapters", "EnumDisplayDevices", exception.Message)
            ]);
        }

        if (adapters.Count == 0)
        {
            return new SystemInformationGroup("Graphics adapters", [
                Unavailable("Display adapters", "EnumDisplayDevices", "Windows did not report any display adapters.")
            ]);
        }

        var fields = new List<SystemInformationField>
        {
            Available("Detected adapters", adapters.Count.ToString(CultureInfo.InvariantCulture), "EnumDisplayDevices")
        };
        for (var index = 0; index < adapters.Count; index++)
        {
            var (name, stateFlags, registryKey) = adapters[index];
            var status = (stateFlags & 0x1) != 0 ? "Attached to desktop" : "Not currently attached";
            if ((stateFlags & 0x4) != 0)
            {
                status += "; primary adapter";
            }

            fields.Add(Available($"Graphics adapter {index + 1}", name, "EnumDisplayDevices"));
            fields.Add(Available($"Graphics adapter {index + 1} status", status, "EnumDisplayDevices"));
            fields.AddRange(ReadGraphicsAdapterDetails(index + 1, registryKey));
        }

        return new SystemInformationGroup("Graphics adapters", fields);
    }

    private static IReadOnlyList<SystemInformationField> ReadGraphicsAdapterDetails(int adapterNumber, string registryDeviceKey)
    {
        const string registryPrefix = @"\Registry\Machine\";
        const string registrySource = "Display adapter registry";
        var driverName = $"Graphics adapter {adapterNumber} driver version";
        var memoryName = $"Graphics adapter {adapterNumber} reported memory";
        if (string.IsNullOrWhiteSpace(registryDeviceKey)
            || !registryDeviceKey.StartsWith(registryPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                Unavailable(driverName, registrySource, "Windows did not provide a readable adapter registry path."),
                Unavailable(memoryName, registrySource, "Windows did not provide a readable adapter registry path.")
            ];
        }

        var registryPath = registryDeviceKey[registryPrefix.Length..];
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(registryPath);
            if (key is null)
            {
                return
                [
                    Unavailable(driverName, registrySource, "The graphics driver registry details are not available."),
                    Unavailable(memoryName, registrySource, "The graphics memory size is not reported by this driver.")
                ];
            }

            var driverVersion = key.GetValue("DriverVersion")?.ToString();
            var memory = key.GetValue("HardwareInformation.qwMemorySize")
                ?? key.GetValue("HardwareInformation.MemorySize");
            return
            [
                string.IsNullOrWhiteSpace(driverVersion)
                    ? Unavailable(driverName, registrySource, "The graphics driver version is not reported.")
                    : Available(driverName, driverVersion, registrySource),
                TryReadUnsignedRegistryValue(memory, out var memoryBytes)
                    ? Available(memoryName, FormatBytes(memoryBytes), registrySource)
                    : Unavailable(memoryName, registrySource, "The graphics memory size is not reported by this driver.")
            ];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return
            [
                Unavailable(driverName, registrySource, exception.Message),
                Unavailable(memoryName, registrySource, exception.Message)
            ];
        }
    }

    private static bool TryReadUnsignedRegistryValue(object? value, out ulong number)
    {
        switch (value)
        {
            case int integer when integer > 0:
                number = (uint)integer;
                return true;
            case long longInteger when longInteger > 0:
                number = (ulong)longInteger;
                return true;
            case uint unsignedInteger when unsignedInteger > 0:
                number = unsignedInteger;
                return true;
            case ulong unsignedLong when unsignedLong > 0:
                number = unsignedLong;
                return true;
            default:
                number = 0;
                return false;
        }
    }

    private static IReadOnlyList<SystemInformationField> ReadProcessorTopologyFields()
    {
        uint bufferLength = 0;
        if (!GetLogicalProcessorInformationEx(RelationAll, IntPtr.Zero, ref bufferLength))
        {
            var errorCode = Marshal.GetLastWin32Error();
            if (errorCode != ErrorInsufficientBuffer || bufferLength == 0)
            {
                return UnavailableProcessorTopologyFields(FormatWin32Error(errorCode));
            }
        }

        if (bufferLength == 0 || bufferLength > int.MaxValue)
        {
            return UnavailableProcessorTopologyFields("Windows returned an invalid processor-topology buffer size.");
        }

        var buffer = Marshal.AllocHGlobal((int)bufferLength);
        try
        {
            if (!GetLogicalProcessorInformationEx(RelationAll, buffer, ref bufferLength))
            {
                return UnavailableProcessorTopologyFields(FormatWin32Error(Marshal.GetLastWin32Error()));
            }

            var bufferSize = (int)bufferLength;
            var coreCount = 0;
            var cacheSizes = new Dictionary<(byte Level, uint Type), ulong>();
            var offset = 0;
            while (offset < bufferSize)
            {
                if (bufferSize - offset < 8)
                {
                    return UnavailableProcessorTopologyFields("Windows returned incomplete processor-topology data.");
                }

                var entry = IntPtr.Add(buffer, offset);
                var relationship = Marshal.ReadInt32(entry);
                var entrySize = Marshal.ReadInt32(entry, 4);
                if (entrySize < 8 || entrySize > bufferSize - offset)
                {
                    return UnavailableProcessorTopologyFields("Windows returned an invalid processor-topology entry.");
                }

                if (relationship == RelationProcessorCore)
                {
                    if (entrySize < 32)
                    {
                        return UnavailableProcessorTopologyFields("Windows returned an incomplete processor-core entry.");
                    }

                    var groupCount = unchecked((ushort)Marshal.ReadInt16(entry, 30));
                    if (entrySize < 32L + groupCount * 16L)
                    {
                        return UnavailableProcessorTopologyFields("Windows returned incomplete processor group data.");
                    }

                    coreCount++;
                }
                else if (relationship == RelationCache)
                {
                    if (entrySize < 36)
                    {
                        return UnavailableProcessorTopologyFields("Windows returned an incomplete processor-cache entry.");
                    }

                    var level = Marshal.ReadByte(entry, 8);
                    var cacheSize = unchecked((uint)Marshal.ReadInt32(entry, 12));
                    var cacheType = unchecked((uint)Marshal.ReadInt32(entry, 16));
                    var key = (level, cacheType);
                    cacheSizes[key] = cacheSizes.GetValueOrDefault(key) + cacheSize;
                }

                offset += entrySize;
            }

            var fields = new List<SystemInformationField>
            {
                coreCount > 0
                    ? Available("Physical processor cores", coreCount.ToString(CultureInfo.InvariantCulture), "GetLogicalProcessorInformationEx")
                    : Unavailable("Physical processor cores", "GetLogicalProcessorInformationEx", "Windows did not report physical core topology."),
                cacheSizes.Count > 0
                    ? Available("Processor cache memory", FormatCacheSizes(cacheSizes), "GetLogicalProcessorInformationEx")
                    : Unavailable("Processor cache memory", "GetLogicalProcessorInformationEx", "Windows did not report processor-cache details.")
            };
            return fields;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static IReadOnlyList<SystemInformationField> UnavailableProcessorTopologyFields(string message) =>
    [
        Unavailable("Physical processor cores", "GetLogicalProcessorInformationEx", message),
        Unavailable("Processor cache memory", "GetLogicalProcessorInformationEx", message)
    ];

    private static string FormatCacheSizes(IReadOnlyDictionary<(byte Level, uint Type), ulong> cacheSizes) =>
        string.Join("; ", cacheSizes
            .OrderBy(cache => cache.Key.Level)
            .ThenBy(cache => cache.Key.Type)
            .Select(cache => $"L{cache.Key.Level} {GetCacheTypeName(cache.Key.Type)}: {FormatBytes(cache.Value)}"));

    private static string GetCacheTypeName(uint cacheType) => cacheType switch
    {
        0 => "Unified",
        1 => "Instruction",
        2 => "Data",
        3 => "Trace",
        _ => "Unknown"
    };

    private static string FormatWin32Error(int errorCode) =>
        errorCode == 0 ? "The Windows API did not return the requested system information." : new Win32Exception(errorCode).Message;

    private static SystemInformationGroup ReadEnvironmentGroup()
    {
        var fields = new List<SystemInformationField>
        {
            ReadCurrentUserField(),
            Available("Interactive session", System.Environment.UserInteractive ? "Yes" : "No", "Environment.UserInteractive"),
            Available("System UI culture", CultureInfo.InstalledUICulture.Name, "CultureInfo.InstalledUICulture"),
            Available("Current user culture", CultureInfo.CurrentCulture.Name, "CultureInfo.CurrentCulture"),
            Available("Windows ANSI code page", CultureInfo.CurrentCulture.TextInfo.ANSICodePage.ToString(CultureInfo.InvariantCulture), "CultureInfo.TextInfo.ANSICodePage"),
            Available("Windows directory", System.Environment.GetFolderPath(System.Environment.SpecialFolder.Windows), "Environment.GetFolderPath"),
            Available("System directory", System.Environment.SystemDirectory, "Environment.SystemDirectory"),
            ReadElevationField(),
            ReadSecureBootField(),
            Available("Hypervisor detected", IsProcessorFeaturePresent(0x40) ? "Yes" : "No", "IsProcessorFeaturePresent"),
            Available("Firmware virtualization enabled", IsProcessorFeaturePresent(21) ? "Yes" : "No or unsupported", "IsProcessorFeaturePresent")
        };

        return new SystemInformationGroup("Environment, security, and virtualization", fields);
    }

    private static SystemInformationField ReadCurrentUserField()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return string.IsNullOrWhiteSpace(identity.Name)
                ? Unavailable("Current user", "WindowsIdentity.GetCurrent", "Windows did not return the current user name.")
                : Available("Current user", identity.Name, "WindowsIdentity.GetCurrent");
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException or InvalidOperationException)
        {
            return Unavailable("Current user", "WindowsIdentity.GetCurrent", exception.Message);
        }
    }

    private static SystemInformationField ReadElevationField()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var isElevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            return Available("Current process administrator role", isElevated ? "Administrator" : "Standard / not elevated", "WindowsPrincipal.IsInRole");
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException or InvalidOperationException)
        {
            return Unavailable("Current process administrator role", "WindowsPrincipal.IsInRole", exception.Message);
        }
    }

    private static SystemInformationField ReadSecureBootField()
    {
        var secureBoot = ReadRegistryField("Secure Boot", SecureBootRegistryPath, "UEFISecureBootEnabled");
        if (!secureBoot.IsAvailable)
        {
            return secureBoot;
        }

        return secureBoot.Value switch
        {
            "1" => Available(secureBoot.Name, "Enabled", secureBoot.Source),
            "0" => Available(secureBoot.Name, "Disabled", secureBoot.Source),
            _ => Unavailable(secureBoot.Name, secureBoot.Source, "Windows returned an unrecognized Secure Boot state.")
        };
    }

    private static SystemInformationGroup ReadMemoryGroup()
    {
        var status = new MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
        };
        var fields = new List<SystemInformationField>();
        if (!GlobalMemoryStatusEx(ref status))
        {
            var error = FormatWin32Error(Marshal.GetLastWin32Error());
            fields.Add(Unavailable("Usable physical memory", "GlobalMemoryStatusEx", error));
            fields.Add(Unavailable("Available physical memory", "GlobalMemoryStatusEx", error));
            fields.Add(Unavailable("Memory load", "GlobalMemoryStatusEx", error));
            fields.Add(Unavailable("Commit limit", "GlobalMemoryStatusEx", error));
            fields.Add(Unavailable("Available commit", "GlobalMemoryStatusEx", error));
            fields.Add(Unavailable("Virtual address space", "GlobalMemoryStatusEx", error));
        }
        else
        {
            fields.Add(Available("Usable physical memory", FormatBytes(status.TotalPhysicalMemory), "GlobalMemoryStatusEx"));
            fields.Add(Available("Available physical memory", FormatBytes(status.AvailablePhysicalMemory), "GlobalMemoryStatusEx"));
            fields.Add(Available("Memory load", $"{status.MemoryLoad.ToString(CultureInfo.InvariantCulture)}%", "GlobalMemoryStatusEx"));
            fields.Add(Available("Commit limit", FormatBytes(status.TotalPageFile), "GlobalMemoryStatusEx"));
            fields.Add(Available("Available commit", FormatBytes(status.AvailablePageFile), "GlobalMemoryStatusEx"));
            fields.Add(Available(
                "Virtual address space",
                $"{FormatBytes(status.TotalVirtual)} total; {FormatBytes(status.AvailableVirtual)} available",
                "GlobalMemoryStatusEx"));
        }

        if (GetPhysicallyInstalledSystemMemory(out var installedMemoryKilobytes))
        {
            fields.Add(installedMemoryKilobytes <= ulong.MaxValue / 1024
                ? Available("Installed physical memory", FormatBytes(installedMemoryKilobytes * 1024), "GetPhysicallyInstalledSystemMemory")
                : Unavailable("Installed physical memory", "GetPhysicallyInstalledSystemMemory", "Windows returned an invalid installed-memory size."));
        }
        else
        {
            fields.Add(Unavailable(
                "Installed physical memory",
                "GetPhysicallyInstalledSystemMemory",
                FormatWin32Error(Marshal.GetLastWin32Error())));
        }

        fields.Add(Available(
            "System page size",
            FormatBytes((ulong)System.Environment.SystemPageSize),
            "Environment.SystemPageSize"));
        return new SystemInformationGroup("Memory", fields);
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

                    var usedSpace = totalSize - availableFreeSpace;
                    var usagePercent = totalSize == 0 ? 0 : (double)usedSpace / totalSize * 100;
                    var volumeLabel = drive.VolumeLabel;
                    var fileSystem = drive.DriveFormat;
                    var volumeDescription = string.IsNullOrWhiteSpace(volumeLabel)
                        ? fileSystem
                        : $"{volumeLabel} ({fileSystem})";
                    fields.Add(Available(
                        $"{drive.Name} {volumeDescription}",
                        $"Type {drive.DriveType}; total {FormatBytes((ulong)totalSize)}; used {FormatBytes((ulong)usedSpace)} ({usagePercent.ToString("N1", CultureInfo.InvariantCulture)}%); free {FormatBytes((ulong)availableFreeSpace)}",
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

    private static string FormatBytes(ulong bytes)
    {
        var size = (double)bytes;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < ByteUnits.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size.ToString("N2", CultureInfo.InvariantCulture)} {ByteUnits[unitIndex]}";
    }

    private static string FormatUptime(long milliseconds) =>
        TimeSpan.FromMilliseconds(milliseconds).ToString("c", CultureInfo.InvariantCulture);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicallyInstalledSystemMemory(out ulong totalMemoryInKilobytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLogicalProcessorInformationEx(uint relationshipType, IntPtr buffer, ref uint returnedLength);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsProcessorFeaturePresent(uint processorFeature);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayDevices(
        string? device,
        uint deviceNumber,
        ref DisplayDevice displayDevice,
        uint flags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int Size;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;

        public uint StateFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceId;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

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
