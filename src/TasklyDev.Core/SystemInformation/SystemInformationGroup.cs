namespace TasklyDev.Core.SystemInformation;

/// <summary>Groups related system-information fields for display and export.</summary>
public sealed record SystemInformationGroup(string Title, IReadOnlyList<SystemInformationField> Fields);
