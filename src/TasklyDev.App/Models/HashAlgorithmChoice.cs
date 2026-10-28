namespace TasklyDev.Models;

/// <summary>Identifies a hash algorithm offered by the calculator UI.</summary>
public enum HashAlgorithmChoice
{
    All,
    MD5,
    SHA256,
    SHA512
}

/// <summary>Specifies whether the hash calculator reads text or a file.</summary>
public enum HashInputMode
{
    Text,
    File
}
