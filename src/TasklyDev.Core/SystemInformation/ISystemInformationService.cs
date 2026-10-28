namespace TasklyDev.Core.SystemInformation;

/// <summary>Provides read-only system-information snapshots.</summary>
public interface ISystemInformationService
{
    /// <summary>Collects the current machine's available system information.</summary>
    Task<SystemInformationSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
