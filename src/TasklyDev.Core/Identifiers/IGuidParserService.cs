namespace TasklyDev.Core.Identifiers;

/// <summary>Validates and normalizes GUID/UUID text.</summary>
public interface IGuidParserService
{
    /// <summary>Parses supported .NET GUID formats and returns a canonical value when valid.</summary>
    /// <param name="input">The untrusted GUID/UUID text to inspect.</param>
    /// <returns>A result describing the parse outcome without inferring unsupported UUID metadata.</returns>
    GuidParseResult Parse(string? input);
}
