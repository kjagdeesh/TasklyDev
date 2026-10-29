using System.Text.Json.Serialization;

namespace TasklyDev.Core.SystemInformation;

/// <summary>Describes a machine-information value, its source, and its availability.</summary>
public sealed record SystemInformationField(
    string Name,
    string Value,
    string Source,
    bool IsAvailable,
    string? StatusMessage = null,
    double? ProgressPercent = null)
{
    /// <summary>Gets whether this field includes a measurable percentage.</summary>
    [JsonIgnore]
    public bool HasProgress => IsAvailable && ProgressPercent.HasValue;
}
