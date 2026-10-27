namespace TasklyDev.Core.Hashing;

public interface IHashService
{
    IReadOnlyList<HashDigest> ComputeText(string text, IReadOnlyCollection<HashAlgorithmKind> algorithms);
    Task<IReadOnlyList<HashDigest>> ComputeFileAsync(
        string filePath,
        IReadOnlyCollection<HashAlgorithmKind> algorithms,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HashDigest>> ComputeStreamAsync(
        Stream input,
        IReadOnlyCollection<HashAlgorithmKind> algorithms,
        CancellationToken cancellationToken = default);
}
