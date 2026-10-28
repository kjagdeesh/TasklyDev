namespace TasklyDev.Core.Hashing;

/// <summary>Identifies a digest algorithm supported by the hash calculator.</summary>
public enum HashAlgorithmKind
{
    /// <summary>MD5, available only for legacy checksum compatibility.</summary>
    MD5,
    /// <summary>SHA-256.</summary>
    SHA256,
    /// <summary>SHA-512.</summary>
    SHA512
}
