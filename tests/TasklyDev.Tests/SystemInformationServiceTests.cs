using TasklyDev.Core.SystemInformation;
using TasklyDev.Windows.SystemInformation;
using Xunit;

namespace TasklyDev.Tests;

public sealed class SystemInformationServiceTests
{
    [Fact]
    public async Task CollectsMachineInformationOffTheCallerThreadAndReportsFieldSources()
    {
        var service = new SystemInformationService();
        var snapshot = await service.GetSnapshotAsync();

        Assert.True(snapshot.HasAvailableInformation);
        Assert.True(snapshot.GeneratedAt <= DateTimeOffset.Now);
        Assert.Contains(snapshot.Groups, group => group.Title == "Operating system");
        Assert.Contains(snapshot.Groups, group => group.Title == "Machine and processor");
        Assert.Contains(snapshot.Groups, group => group.Title == "Firmware and motherboard");
        Assert.Contains(snapshot.Groups, group => group.Title == "Graphics adapters");
        Assert.Contains(snapshot.Groups, group => group.Title == "Memory");
        Assert.Contains(snapshot.Groups, group => group.Title == "Storage");
        Assert.Contains(snapshot.Groups, group => group.Title == "Network adapters");
        Assert.Contains(snapshot.Groups, group => group.Title == "Power and battery");
        Assert.Contains(snapshot.Groups, group => group.Title == "Environment, security, and virtualization");
        Assert.Contains(snapshot.Groups, group => group.Title == "Uptime and .NET runtime");
        var fields = snapshot.Groups.SelectMany(group => group.Fields).ToArray();
        Assert.Contains(fields, field => field.Name == "Windows edition");
        Assert.Contains(fields, field => field.Name == "Device ID (Machine GUID)");
        Assert.Contains(fields, field => field.Name == "Windows Product ID");
        Assert.Contains(fields, field => field.Name == "Logical processors / threads");
        Assert.Contains(fields, field => field.Name == "Physical processor cores");
        Assert.Contains(fields, field => field.Name == "Processor cache memory");
        Assert.Contains(fields, field => field.Name == "BIOS manufacturer");
        Assert.Contains(fields, field => field.Name == "System model");
        Assert.Contains(fields, field => field.Name == "Installed physical memory");
        Assert.Contains(fields, field => field.Name == "Adapters detected");
        Assert.Contains(fields, field => field.Name == "Power source");
        Assert.Contains(fields, field => field.Name == "Time zone");
        Assert.Contains(fields, field => field.Name == "Memory load" && field.HasProgress);
        Assert.Contains(fields, field => field.Name == "Commit usage" && field.HasProgress);
        Assert.Contains(snapshot.Groups.Single(group => group.Title == "Storage").Fields, field => field.HasProgress);
        Assert.True(fields.Single(field => field.Name == "Logical processors / threads").IsAvailable);
        Assert.True(fields.Single(field => field.Name == "Physical processor cores").IsAvailable);
        Assert.True(fields.Single(field => field.Name == "Processor cache memory").IsAvailable);
        Assert.All(fields, field =>
        {
            Assert.False(string.IsNullOrWhiteSpace(field.Name));
            Assert.False(string.IsNullOrWhiteSpace(field.Source));
            Assert.True(field.IsAvailable || !string.IsNullOrWhiteSpace(field.StatusMessage));
            if (field.ProgressPercent is { } progress)
            {
                Assert.InRange(progress, 0, 100);
            }
        });
        Assert.Contains(fields, field => field.IsAvailable);
    }
}
