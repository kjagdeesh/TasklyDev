using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Formats.Asn1;
using System.Net;
using TasklyDev.Core.Certificates;

namespace TasklyDev.Windows.Certificates;

/// <summary>Reads public certificate metadata and builds trust chains without network revocation checks.</summary>
public sealed class CertificateInspectorService : ICertificateInspectorService
{
    private const long MaximumCertificateFileBytes = 10 * 1024 * 1024;
    private static readonly string[] SupportedExtensions = [".cer", ".crt", ".der", ".pem"];

    /// <inheritdoc />
    public Task<CertificateInspectionResult> InspectFileAsync(
        string filePath,
        CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var certificate = LoadPublicCertificate(filePath);
            return InspectCertificate(certificate, Path.GetFullPath(filePath));
        }, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<CertificateListItem>> GetStoreCertificatesAsync(
        CertificateStoreScope scope,
        CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<CertificateListItem>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var store = OpenStore(scope);
            var certificates = new List<CertificateListItem>(store.Certificates.Count);
            foreach (X509Certificate2 certificate in store.Certificates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (certificate)
                {
                    certificates.Add(CreateListItem(certificate));
                }
            }

            return certificates
                .OrderBy(item => item.Subject, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Thumbprint, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }, cancellationToken);

    /// <inheritdoc />
    public Task<CertificateInspectionResult> InspectStoreCertificateAsync(
        CertificateStoreScope scope,
        string thumbprint,
        CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(thumbprint);
            cancellationToken.ThrowIfCancellationRequested();
            using var store = OpenStore(scope);
            var matches = store.Certificates.Find(
                X509FindType.FindByThumbprint,
                thumbprint,
                validOnly: false);
            if (matches.Count == 0)
            {
                throw new FileNotFoundException("The selected certificate is no longer available in that store.");
            }

            using var certificate = matches[0];
            var source = scope == CertificateStoreScope.CurrentUser
                ? "Windows Current User personal store"
                : "Windows Local Machine personal store";
            return InspectCertificate(certificate, source);
        }, cancellationToken);

    private static X509Store OpenStore(CertificateStoreScope scope)
    {
        var location = scope switch
        {
            CertificateStoreScope.CurrentUser => StoreLocation.CurrentUser,
            CertificateStoreScope.LocalMachine => StoreLocation.LocalMachine,
            _ => throw new ArgumentOutOfRangeException(nameof(scope))
        };
        var store = new X509Store(StoreName.My, location);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
        return store;
    }

    private static X509Certificate2 LoadPublicCertificate(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var extension = Path.GetExtension(filePath);
        if (!SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Choose a public certificate file with a .cer, .crt, .der, or .pem extension.");
        }

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaximumCertificateFileBytes)
        {
            throw new InvalidDataException("Certificate files larger than 10 MB are not supported.");
        }

        var contents = new byte[checked((int)stream.Length)];
        stream.ReadExactly(contents);
        if (contents.AsSpan().IndexOf("-----BEGIN"u8) >= 0 &&
            contents.AsSpan().IndexOf("PRIVATE KEY-----"u8) >= 0)
        {
            throw new NotSupportedException("Private-key-bearing PEM files are not accepted. Select a public certificate only.");
        }

        var certificate = X509CertificateLoader.LoadCertificate(contents);
        if (certificate.HasPrivateKey)
        {
            certificate.Dispose();
            throw new NotSupportedException("Private-key-bearing certificate files are not accepted.");
        }

        return certificate;
    }

    private static CertificateListItem CreateListItem(X509Certificate2 certificate) =>
        new(
            certificate.Thumbprint ?? string.Empty,
            GetSimpleName(certificate),
            certificate.Issuer,
            new DateTimeOffset(certificate.NotBefore),
            new DateTimeOffset(certificate.NotAfter));

    private static CertificateInspectionResult InspectCertificate(X509Certificate2 certificate, string source)
    {
        var notBefore = new DateTimeOffset(certificate.NotBefore);
        var notAfter = new DateTimeOffset(certificate.NotAfter);
        var now = DateTimeOffset.Now;
        var isWithinValidityPeriod = now >= notBefore && now <= notAfter;
        var dateValidityStatus = now < notBefore
            ? "Not yet valid"
            : now > notAfter
                ? "Expired"
                : "Within validity dates";

        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
        chain.ChainPolicy.DisableCertificateDownloads = true;
        var isTrusted = chain.Build(certificate);
        var chainStatuses = chain.ChainStatus
            .Select(status => $"{status.Status}: {status.StatusInformation.Trim()}")
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (chainStatuses.Length == 0 && !isTrusted)
        {
            chainStatuses = ["Chain construction failed without additional status details."];
        }

        return new CertificateInspectionResult(
            source,
            GetSimpleName(certificate),
            certificate.Issuer,
            certificate.Thumbprint ?? string.Empty,
            notBefore,
            notAfter,
            isWithinValidityPeriod,
            dateValidityStatus,
            isTrusted ? "Trusted by local Windows chain policy" : "Not trusted by local Windows chain policy",
            chainStatuses,
            chain.ChainElements
                .Cast<X509ChainElement>()
                .Select(element => GetSimpleName(element.Certificate))
                .ToArray(),
            "Not checked (network revocation checks are disabled).",
            GetSubjectAlternativeNames(certificate),
            GetKeyUsages(certificate),
            FormatAlgorithm(certificate.PublicKey.Oid),
            FormatAlgorithm(certificate.SignatureAlgorithm));
    }

    private static string GetSimpleName(X509Certificate2 certificate)
    {
        var simpleName = certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
        return string.IsNullOrWhiteSpace(simpleName) ? certificate.Subject : simpleName;
    }

    private static IReadOnlyList<string> GetSubjectAlternativeNames(X509Certificate2 certificate)
    {
        var extension = certificate.Extensions
            .Cast<X509Extension>()
            .FirstOrDefault(item => item.Oid?.Value == "2.5.29.17");
        if (extension is null)
        {
            return ["No Subject Alternative Name extension is present."];
        }

        var reader = new AsnReader(extension.RawData, AsnEncodingRules.DER);
        var sequence = reader.ReadSequence();
        var names = new List<string>();
        while (sequence.HasData)
        {
            var tag = sequence.PeekTag();
            if (tag.TagClass != TagClass.ContextSpecific)
            {
                sequence.ReadEncodedValue();
                continue;
            }

            var generalNameTag = new Asn1Tag(TagClass.ContextSpecific, tag.TagValue);
            switch (tag.TagValue)
            {
                case 1:
                    names.Add($"Email: {sequence.ReadCharacterString(UniversalTagNumber.IA5String, generalNameTag)}");
                    break;
                case 2:
                    names.Add($"DNS: {sequence.ReadCharacterString(UniversalTagNumber.IA5String, generalNameTag)}");
                    break;
                case 6:
                    names.Add($"URI: {sequence.ReadCharacterString(UniversalTagNumber.IA5String, generalNameTag)}");
                    break;
                case 7:
                    names.Add($"IP: {new IPAddress(sequence.ReadOctetString(generalNameTag))}");
                    break;
                default:
                    names.Add($"GeneralName tag [{tag.TagValue}] is present but is not decoded.");
                    sequence.ReadEncodedValue();
                    break;
            }
        }

        reader.ThrowIfNotEmpty();
        return names.Count == 0
            ? ["Subject Alternative Name extension is present but contains no supported name types."]
            : names;
    }

    private static IReadOnlyList<string> GetKeyUsages(X509Certificate2 certificate)
    {
        var extension = certificate.Extensions
            .Cast<X509Extension>()
            .FirstOrDefault(item => item.Oid?.Value == "2.5.29.15");
        if (extension is null)
        {
            return ["No Key Usage extension is present."];
        }

        var keyUsage = new X509KeyUsageExtension(extension, extension.Critical);
        var usages = Enum.GetValues<X509KeyUsageFlags>()
            .Where(flag => flag != X509KeyUsageFlags.None && (keyUsage.KeyUsages & flag) == flag)
            .Select(flag => flag.ToString())
            .ToArray();
        return usages.Length == 0 ? ["No key usages are set."] : usages;
    }

    private static string FormatAlgorithm(Oid? oid)
    {
        return string.IsNullOrWhiteSpace(oid?.FriendlyName)
            ? oid?.Value ?? "Unknown"
            : $"{oid.FriendlyName} ({oid.Value})";
    }
}
