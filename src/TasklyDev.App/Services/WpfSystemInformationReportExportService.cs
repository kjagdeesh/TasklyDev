using System.IO;
using System.Security;
using System.Text;
using Microsoft.Win32;
using TasklyDev.Core.SystemInformation;

namespace TasklyDev.Services;

/// <summary>Exports system-information reports with a native Windows save dialog.</summary>
public sealed class WpfSystemInformationReportExportService : ISystemInformationReportExportService
{
    /// <inheritdoc />
    public async Task<SystemInformationReportExportResult> ExportAsync(
        string report,
        SystemInformationExportFormat format)
    {
        ArgumentNullException.ThrowIfNull(report);
        var (extension, filter) = format switch
        {
            SystemInformationExportFormat.Txt => (".txt", "Text report (*.txt)|*.txt"),
            SystemInformationExportFormat.Json => (".json", "JSON report (*.json)|*.json"),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported report format.")
        };
        var dialog = new SaveFileDialog
        {
            Title = "Export system information",
            AddExtension = true,
            DefaultExt = extension,
            Filter = filter,
            FileName = $"tasklydev-system-information-{DateTime.Now:yyyyMMdd-HHmmss}{extension}",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog() != true)
        {
            return new SystemInformationReportExportResult(false, true, null, null);
        }

        try
        {
            await File.WriteAllTextAsync(dialog.FileName, report, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return new SystemInformationReportExportResult(true, false, dialog.FileName, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException)
        {
            return new SystemInformationReportExportResult(false, false, null, exception.Message);
        }
    }
}
