using System.Text.Json;
using TasklyDev.Core.SystemInformation;
using Xunit;

namespace TasklyDev.Tests;

public sealed class SystemInformationReportFormatterTests
{
    [Fact]
    public void TextReportIncludesTimestampAvailabilitySourcesAndUnredactedValues()
    {
        var snapshot = CreateSnapshot();

        var report = SystemInformationReportFormatter.Format(snapshot, SystemInformationExportFormat.Txt);

        Assert.Contains("TasklyDev System Information", report);
        Assert.Contains("Export redaction: none", report);
        Assert.Contains("Machine name: QA-WORKSTATION", report);
        Assert.Contains("HKLM\\Machine", report);
        Assert.Contains("Status: access denied", report);
    }

    [Fact]
    public void JsonReportPreservesValuesAndUnavailableFieldState()
    {
        var snapshot = CreateSnapshot();

        var report = SystemInformationReportFormatter.Format(snapshot, SystemInformationExportFormat.Json);

        using var document = JsonDocument.Parse(report);
        var root = document.RootElement;
        Assert.Equal("2026-01-15T10:30:00+00:00", root.GetProperty("generatedAt").GetString());
        var fields = root.GetProperty("groups")[0].GetProperty("fields");
        Assert.Equal("QA-WORKSTATION", fields[0].GetProperty("value").GetString());
        Assert.True(fields[0].GetProperty("isAvailable").GetBoolean());
        Assert.False(fields[1].GetProperty("isAvailable").GetBoolean());
        Assert.Equal("access denied", fields[1].GetProperty("statusMessage").GetString());
    }

    [Fact]
    public void UnsupportedReportFormatIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SystemInformationReportFormatter.Format(CreateSnapshot(), (SystemInformationExportFormat)100));
    }

    [Fact]
    public void AvailableFieldWithStatusMessageIsReportedAsPartialInformation()
    {
        var snapshot = new SystemInformationSnapshot(
            DateTimeOffset.UtcNow,
            [
                new SystemInformationGroup("Operating system", [
                    new SystemInformationField("Build", "26100", "Registry", true, "Build revision unavailable.")
                ])
            ]);

        Assert.True(snapshot.HasAvailableInformation);
        Assert.True(snapshot.HasUnavailableInformation);
        Assert.Contains("Status: Build revision unavailable.", SystemInformationReportFormatter.Format(snapshot, SystemInformationExportFormat.Txt));
    }

    private static SystemInformationSnapshot CreateSnapshot() =>
        new(
            new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero),
            [
                new SystemInformationGroup("Machine", [
                    new SystemInformationField("Machine name", "QA-WORKSTATION", @"HKLM\Machine", true),
                    new SystemInformationField("Fixed volume", "Unavailable", "DriveInfo", false, "access denied")
                ])
            ]);
}
