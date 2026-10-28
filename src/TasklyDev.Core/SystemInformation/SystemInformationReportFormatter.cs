using System.Globalization;
using System.Text;
using System.Text.Json;

namespace TasklyDev.Core.SystemInformation;

/// <summary>Formats system-information snapshots as plain text or JSON.</summary>
public static class SystemInformationReportFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>Formats a snapshot in the selected export format without redacting values.</summary>
    public static string Format(SystemInformationSnapshot snapshot, SystemInformationExportFormat format)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return format switch
        {
            SystemInformationExportFormat.Txt => FormatText(snapshot),
            SystemInformationExportFormat.Json => JsonSerializer.Serialize(snapshot, JsonOptions),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported report format.")
        };
    }

    private static string FormatText(SystemInformationSnapshot snapshot)
    {
        var builder = new StringBuilder();
        builder.AppendLine("TasklyDev System Information");
        builder.Append("Generated: ")
            .AppendLine(snapshot.GeneratedAt.ToString("O", CultureInfo.InvariantCulture));
        builder.AppendLine("Export redaction: none");

        foreach (var group in snapshot.Groups)
        {
            builder.AppendLine();
            builder.AppendLine($"[{group.Title}]");
            foreach (var field in group.Fields)
            {
                builder.Append(field.Name)
                    .Append(": ")
                    .AppendLine(field.Value);
                builder.Append("  Status: ")
                    .AppendLine(field.IsAvailable ? field.StatusMessage ?? "Available" : field.StatusMessage ?? "Unavailable");
                builder.Append("  Source: ")
                    .AppendLine(field.Source);
            }
        }

        return builder.ToString();
    }
}
