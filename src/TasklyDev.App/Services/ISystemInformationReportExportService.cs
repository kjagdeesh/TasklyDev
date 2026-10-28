using TasklyDev.Core.SystemInformation;

namespace TasklyDev.Services;

/// <summary>Saves a system-information report selected by the user.</summary>
public interface ISystemInformationReportExportService
{
    /// <summary>Prompts for a destination and writes the report in UTF-8.</summary>
    Task<SystemInformationReportExportResult> ExportAsync(
        string report,
        SystemInformationExportFormat format);
}
