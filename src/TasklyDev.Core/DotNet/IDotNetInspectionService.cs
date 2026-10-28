namespace TasklyDev.Core.DotNet;

/// <summary>Provides read-only inspection of .NET installations on the current machine.</summary>
public interface IDotNetInspectionService
{
    /// <summary>Inspects installed SDKs, runtimes, workloads, and dotnet host information.</summary>
    /// <param name="cancellationToken">Token used to cancel the bounded CLI inspection.</param>
    /// <returns>A snapshot explicitly describing available and unavailable command results.</returns>
    Task<DotNetInspectionResult> InspectAsync(CancellationToken cancellationToken = default);
}
