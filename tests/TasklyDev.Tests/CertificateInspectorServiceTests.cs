using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using TasklyDev.Core.Certificates;
using TasklyDev.Windows.Certificates;
using Xunit;

namespace TasklyDev.Tests;

public sealed class CertificateInspectorServiceTests
{
    [Fact]
    public async Task InspectsPublicCertificateFieldsAndReportsLocalTrustSeparately()
    {
        var (directory, path) = CreateCertificatePath(".cer");
        try
        {
            Directory.CreateDirectory(directory);
            using var certificate = CreateCertificate(
                DateTimeOffset.Now.AddDays(-1),
                DateTimeOffset.Now.AddDays(10),
                includeSubjectAlternativeNames: true);
            await File.WriteAllBytesAsync(path, certificate.Export(X509ContentType.Cert));

            var result = await new CertificateInspectorService().InspectFileAsync(path);

            Assert.Equal("localhost", result.Subject);
            Assert.Equal(40, result.Thumbprint.Length);
            Assert.Equal("Within validity dates", result.DateValidityStatus);
            Assert.True(result.IsWithinValidityPeriod);
            Assert.StartsWith("Not trusted", result.ChainTrustStatus, StringComparison.Ordinal);
            Assert.Contains(result.ChainStatuses, status => status.Contains("UntrustedRoot", StringComparison.Ordinal));
            Assert.Contains(result.SubjectAlternativeNames, name => name == "DNS: localhost");
            Assert.Contains(result.SubjectAlternativeNames, name => name == $"IP: {IPAddress.Loopback}");
            Assert.Contains(result.KeyUsages, usage => usage == nameof(X509KeyUsageFlags.DigitalSignature));
            Assert.Contains("Not checked", result.RevocationStatus, StringComparison.Ordinal);
            Assert.Contains("1.2.840.113549.1.1.11", result.SignatureAlgorithm, StringComparison.Ordinal);
        }
        finally
        {
            DeleteTestDirectory(directory);
        }
    }

    [Theory]
    [InlineData(-20, -10, "Expired")]
    [InlineData(10, 20, "Not yet valid")]
    public async Task ReportsExpiredAndNotYetValidDatesWithoutCallingThemTrusted(
        int notBeforeOffsetDays,
        int notAfterOffsetDays,
        string expectedDateStatus)
    {
        var (directory, path) = CreateCertificatePath(".der");
        try
        {
            Directory.CreateDirectory(directory);
            using var certificate = CreateCertificate(
                DateTimeOffset.Now.AddDays(notBeforeOffsetDays),
                DateTimeOffset.Now.AddDays(notAfterOffsetDays),
                includeSubjectAlternativeNames: false);
            await File.WriteAllBytesAsync(path, certificate.Export(X509ContentType.Cert));

            var result = await new CertificateInspectorService().InspectFileAsync(path);

            Assert.Equal(expectedDateStatus, result.DateValidityStatus);
            Assert.False(result.IsWithinValidityPeriod);
            Assert.StartsWith("Not trusted", result.ChainTrustStatus, StringComparison.Ordinal);
            Assert.Contains("No Subject Alternative Name", Assert.Single(result.SubjectAlternativeNames));
        }
        finally
        {
            DeleteTestDirectory(directory);
        }
    }

    [Fact]
    public async Task RejectsPrivateKeyPemFilesAndUnsupportedContainerFormats()
    {
        var (directory, path) = CreateCertificatePath(".pem");
        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, "-----BEGIN PRIVATE KEY-----\nnot a key\n-----END PRIVATE KEY-----");

            await Assert.ThrowsAsync<NotSupportedException>(() =>
                new CertificateInspectorService().InspectFileAsync(path));
            await Assert.ThrowsAsync<NotSupportedException>(() =>
                new CertificateInspectorService().InspectFileAsync(Path.ChangeExtension(path, ".pfx")));
        }
        finally
        {
            DeleteTestDirectory(directory);
        }
    }

    [Fact]
    public async Task ReportsMalformedCertificateContentInsteadOfReturningEmptyDetails()
    {
        var (directory, path) = CreateCertificatePath(".cer");
        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, "not a certificate");

            await Assert.ThrowsAsync<CryptographicException>(() =>
                new CertificateInspectorService().InspectFileAsync(path));
        }
        finally
        {
            DeleteTestDirectory(directory);
        }
    }

    private static X509Certificate2 CreateCertificate(
        DateTimeOffset notBefore,
        DateTimeOffset notAfter,
        bool includeSubjectAlternativeNames)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=localhost",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            critical: false));
        if (includeSubjectAlternativeNames)
        {
            var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
            subjectAlternativeNames.AddDnsName("localhost");
            subjectAlternativeNames.AddIpAddress(IPAddress.Loopback);
            request.CertificateExtensions.Add(subjectAlternativeNames.Build());
        }

        return request.CreateSelfSigned(notBefore, notAfter);
    }

    private static (string Directory, string Path) CreateCertificatePath(string extension)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"TasklyCertificateTest-{Guid.NewGuid():N}");
        return (directory, Path.Combine(directory, $"certificate{extension}"));
    }

    private static void DeleteTestDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
