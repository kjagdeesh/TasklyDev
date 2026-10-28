namespace TasklyDev.Core.Certificates;

/// <summary>Contains public certificate details and separate local validity and chain assessments.</summary>
public sealed record CertificateInspectionResult(
    string Source,
    string Subject,
    string Issuer,
    string Thumbprint,
    DateTimeOffset NotBefore,
    DateTimeOffset NotAfter,
    bool IsWithinValidityPeriod,
    string DateValidityStatus,
    string ChainTrustStatus,
    IReadOnlyList<string> ChainStatuses,
    IReadOnlyList<string> ChainSubjects,
    string RevocationStatus,
    IReadOnlyList<string> SubjectAlternativeNames,
    IReadOnlyList<string> KeyUsages,
    string PublicKeyAlgorithm,
    string SignatureAlgorithm);
