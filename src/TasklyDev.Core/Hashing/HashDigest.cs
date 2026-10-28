namespace TasklyDev.Core.Hashing;

/// <summary>Stores an algorithm and its lowercase hexadecimal digest.</summary>
public sealed record HashDigest(HashAlgorithmKind Algorithm, string HexValue);
