namespace TasklyDev.Core.DotNet;

/// <summary>Describes a .NET runtime found in a machine-wide runtime enumeration.</summary>
public sealed record DotNetRuntimeInfo(string Name, string Version, string InstallPath);
