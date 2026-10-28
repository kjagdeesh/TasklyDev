namespace TasklyDev.Core.DotNet;

/// <summary>Describes a .NET SDK found in a machine-wide SDK enumeration.</summary>
public sealed record DotNetSdkInfo(string Version, string InstallPath);
