namespace TasklyDev.Core.SystemInformation;

/// <summary>Contains one timestamped system-information collection.</summary>
public sealed record SystemInformationSnapshot(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<SystemInformationGroup> Groups)
{
    /// <summary>Gets whether at least one system field was collected successfully.</summary>
    public bool HasAvailableInformation => Groups
        .SelectMany(group => group.Fields)
        .Any(value => value.IsAvailable);

    /// <summary>Gets whether at least one system field was unavailable or incomplete.</summary>
    public bool HasUnavailableInformation => Groups
        .SelectMany(group => group.Fields)
        .Any(value => !value.IsAvailable || !string.IsNullOrWhiteSpace(value.StatusMessage));
}
