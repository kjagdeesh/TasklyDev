namespace TasklyDev.Core.Hashing;

public sealed record HashDigest(HashAlgorithmKind Algorithm, string HexValue);
