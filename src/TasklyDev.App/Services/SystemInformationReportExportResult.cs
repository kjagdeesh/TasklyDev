namespace TasklyDev.Services;

/// <summary>Describes the outcome of saving a system-information report.</summary>
public sealed record SystemInformationReportExportResult(
    bool Succeeded,
    bool WasCancelled,
    string? FilePath,
    string? ErrorMessage);
