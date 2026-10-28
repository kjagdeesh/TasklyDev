namespace TasklyDev.Core.SystemInformation;

/// <summary>Describes a machine-information value, its source, and its availability.</summary>
public sealed record SystemInformationField(
    string Name,
    string Value,
    string Source,
    bool IsAvailable,
    string? StatusMessage = null);
