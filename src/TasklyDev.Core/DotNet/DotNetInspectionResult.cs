namespace TasklyDev.Core.DotNet;

/// <summary>Contains machine-level .NET SDK, runtime, workload, and installation information.</summary>
public sealed record DotNetInspectionResult(
    DateTimeOffset InspectedAt,
    string InspectionScope,
    IReadOnlyList<string> DotNetPaths,
    IReadOnlyList<DotNetSdkInfo> Sdks,
    IReadOnlyList<DotNetRuntimeInfo> Runtimes,
    string WorkloadOutput,
    string InfoOutput,
    IReadOnlyList<DotNetCommandDiagnostic> Diagnostics,
    string StatusMessage,
    bool IsAvailable);
