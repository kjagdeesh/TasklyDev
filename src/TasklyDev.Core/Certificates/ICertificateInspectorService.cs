namespace TasklyDev.Core.Certificates;

/// <summary>Inspects public certificates from local files and Windows certificate stores.</summary>
public interface ICertificateInspectorService
{
    /// <summary>Inspects one public certificate file without importing or exporting private keys.</summary>
    Task<CertificateInspectionResult> InspectFileAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>Lists certificate metadata from an accessible Windows personal store.</summary>
    Task<IReadOnlyList<CertificateListItem>> GetStoreCertificatesAsync(
        CertificateStoreScope scope,
        CancellationToken cancellationToken = default);

    /// <summary>Inspects a certificate selected by thumbprint from a Windows personal store.</summary>
    Task<CertificateInspectionResult> InspectStoreCertificateAsync(
        CertificateStoreScope scope,
        string thumbprint,
        CancellationToken cancellationToken = default);
}
