namespace TasklyDev.Core.Certificates;

/// <summary>Provides the identifying fields shown for a certificate in a Windows store.</summary>
public sealed record CertificateListItem(
    string Thumbprint,
    string Subject,
    string Issuer,
    DateTimeOffset NotBefore,
    DateTimeOffset NotAfter);
