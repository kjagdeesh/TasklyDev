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
        Assert.Contains(snapshot.Groups, group => group.Title == "Memory");
        Assert.Contains(snapshot.Groups, group => group.Title == "Storage");
        Assert.Contains(snapshot.Groups, group => group.Title == "Uptime and .NET runtime");
        Assert.All(snapshot.Groups.SelectMany(group => group.Fields), field =>
        {
            Assert.False(string.IsNullOrWhiteSpace(field.Name));
            Assert.False(string.IsNullOrWhiteSpace(field.Source));
            Assert.True(field.IsAvailable || !string.IsNullOrWhiteSpace(field.StatusMessage));
        });
        Assert.Contains(snapshot.Groups.SelectMany(group => group.Fields), field => field.IsAvailable);
    }
}
