namespace TasklyDev.Models;

/// <summary>Defines a tool's display metadata and sidebar grouping.</summary>
public sealed record ToolDefinition(
    string Name,
    string Description,
    string Route,
    string Glyph,
    string Group);
