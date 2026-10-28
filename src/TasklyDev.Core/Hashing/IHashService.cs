namespace TasklyDev.Core.Hashing;

/// <summary>Computes selected message digests for text, files, and readable streams.</summary>
public interface IHashService
{
    /// <summary>Computes digests for the UTF-8 bytes of a string.</summary>
    /// <param name="text">Text to hash.</param>
    /// <param name="algorithms">Algorithms to calculate.</param>
    /// <returns>One digest per distinct selected algorithm.</returns>
    IReadOnlyList<HashDigest> ComputeText(string text, IReadOnlyCollection<HashAlgorithmKind> algorithms);

    /// <summary>Asynchronously computes digests for a file using buffered reads.</summary>
    /// <param name="filePath">Path of the file to read.</param>
    /// <param name="algorithms">Algorithms to calculate.</param>
    /// <param name="cancellationToken">Token used to cancel file reading.</param>
    /// <returns>One digest per distinct selected algorithm.</returns>
    Task<IReadOnlyList<HashDigest>> ComputeFileAsync(
        string filePath,
        IReadOnlyCollection<HashAlgorithmKind> algorithms,
        CancellationToken cancellationToken = default);
    /// <summary>Asynchronously computes digests for all bytes read from a stream.</summary>
    /// <param name="input">Readable input stream; the caller retains ownership.</param>
    /// <param name="algorithms">Algorithms to calculate.</param>
    /// <param name="cancellationToken">Token used to cancel stream reading.</param>
    /// <returns>One digest per distinct selected algorithm.</returns>
    Task<IReadOnlyList<HashDigest>> ComputeStreamAsync(
        Stream input,
        IReadOnlyCollection<HashAlgorithmKind> algorithms,
        CancellationToken cancellationToken = default);
}
