using System.Security.Cryptography;
using System.Text;

namespace TasklyDev.Core.Hashing;

/// <summary>Computes MD5, SHA-256, or SHA-512 digests for text and byte streams.</summary>
public sealed class HashService : IHashService
{
    private const int BufferSize = 81920;

    /// <inheritdoc />
    public IReadOnlyList<HashDigest> ComputeText(string text, IReadOnlyCollection<HashAlgorithmKind> algorithms)
    {
        ArgumentNullException.ThrowIfNull(text);
        var selectedAlgorithms = ValidateAlgorithms(algorithms);
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);

        return selectedAlgorithms
            .Select(algorithm => new HashDigest(algorithm, ToLowerHex(HashData(algorithm, bytes))))
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HashDigest>> ComputeFileAsync(
        string filePath,
        IReadOnlyCollection<HashAlgorithmKind> algorithms,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return await ComputeStreamAsync(stream, algorithms, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HashDigest>> ComputeStreamAsync(
        Stream input,
        IReadOnlyCollection<HashAlgorithmKind> algorithms,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.CanRead)
        {
            throw new ArgumentException("The input stream must be readable.", nameof(input));
        }

        var selectedAlgorithms = ValidateAlgorithms(algorithms);
        var activeHashes = selectedAlgorithms
            .Select(algorithm => (Algorithm: algorithm, Hash: IncrementalHash.CreateHash(GetAlgorithmName(algorithm))))
            .ToArray();

        try
        {
            var buffer = new byte[BufferSize];
            int bytesRead;
            while ((bytesRead = await input.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
            {
                foreach (var item in activeHashes)
                {
                    item.Hash.AppendData(buffer, 0, bytesRead);
                }
            }

            return activeHashes
                .Select(item => new HashDigest(item.Algorithm, ToLowerHex(item.Hash.GetHashAndReset())))
                .ToArray();
        }
        finally
        {
            foreach (var item in activeHashes)
            {
                item.Hash.Dispose();
            }
        }
    }

    private static HashAlgorithmName GetAlgorithmName(HashAlgorithmKind algorithm) =>
        algorithm switch
        {
            HashAlgorithmKind.MD5 => HashAlgorithmName.MD5,
            HashAlgorithmKind.SHA256 => HashAlgorithmName.SHA256,
            HashAlgorithmKind.SHA512 => HashAlgorithmName.SHA512,
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unsupported hash algorithm.")
        };

    private static byte[] HashData(HashAlgorithmKind algorithm, byte[] data) =>
        algorithm switch
        {
            HashAlgorithmKind.MD5 => MD5.HashData(data),
            HashAlgorithmKind.SHA256 => SHA256.HashData(data),
            HashAlgorithmKind.SHA512 => SHA512.HashData(data),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unsupported hash algorithm.")
        };

    private static HashAlgorithmKind[] ValidateAlgorithms(IReadOnlyCollection<HashAlgorithmKind> algorithms)
    {
        ArgumentNullException.ThrowIfNull(algorithms);
        if (algorithms.Count == 0)
        {
            throw new ArgumentException("Select at least one hash algorithm.", nameof(algorithms));
        }

        var selectedAlgorithms = algorithms.Distinct().ToArray();
        foreach (var algorithm in selectedAlgorithms)
        {
            _ = GetAlgorithmName(algorithm);
        }

        return selectedAlgorithms;
    }

    private static string ToLowerHex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
}
