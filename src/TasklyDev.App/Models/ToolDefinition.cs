namespace TasklyDev.Models;

public sealed record ToolDefinition(
    string Name,
    string Description,
    string Route,
    string Glyph,
    string Group);
