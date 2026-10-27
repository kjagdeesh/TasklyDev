using System.Security.Cryptography;
using System.Text;
using TasklyDev.Core.Hashing;
using Xunit;

namespace TasklyDev.Tests;

public sealed class HashServiceTests
{
    private readonly HashService _service = new();

    [Theory]
    [InlineData(HashAlgorithmKind.MD5, "9e107d9d372bb6826bd81d3542a419d6")]
    [InlineData(HashAlgorithmKind.SHA256, "d7a8fbb307d7809469ca9abcb0082e4f8d5651e46d3cdb762d02d0bf37c9e592")]
    [InlineData(HashAlgorithmKind.SHA512, "07e547d9586f6a73f73fbac0435ed76951218fb7d0c8d788a309d785436bbb642e93a252a954f23912547d1e8a3b5ed6e1bfd7097821233fa0538f3db854fee6")]
    public void ComputesKnownTextDigest(HashAlgorithmKind algorithm, string expected)
    {
        var digest = Assert.Single(_service.ComputeText("The quick brown fox jumps over the lazy dog", [algorithm]));

        Assert.Equal(expected, digest.HexValue);
        Assert.Equal(digest.HexValue.ToLowerInvariant(), digest.HexValue);
    }

    [Fact]
    public void ComputesAllAlgorithmsForUtf8TextIncludingEmptyInput()
    {
        var results = _service.ComputeText("你好 🌱", [HashAlgorithmKind.MD5, HashAlgorithmKind.SHA256, HashAlgorithmKind.SHA512]);

        Assert.Equal(3, results.Count);
        Assert.All(results, result => Assert.False(string.IsNullOrWhiteSpace(result.HexValue)));
        Assert.Equal(32, results.Single(result => result.Algorithm == HashAlgorithmKind.MD5).HexValue.Length);

        var emptyDigest = Assert.Single(_service.ComputeText(string.Empty, [HashAlgorithmKind.SHA256]));
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", emptyDigest.HexValue);
    }

    [Fact]
    public async Task StreamHashMatchesTextHashAcrossBufferBoundaries()
    {
        var bytes = Encoding.UTF8.GetBytes(new string('x', 200_000));
        using var stream = new MemoryStream(bytes);

        var actual = await _service.ComputeStreamAsync(stream, [HashAlgorithmKind.SHA256, HashAlgorithmKind.MD5]);
        var expectedSha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var expectedMd5 = Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();

        Assert.Equal(expectedSha, actual.Single(result => result.Algorithm == HashAlgorithmKind.SHA256).HexValue);
        Assert.Equal(expectedMd5, actual.Single(result => result.Algorithm == HashAlgorithmKind.MD5).HexValue);
    }

    [Fact]
    public async Task FileHashReadsTheSelectedFile()
    {
        var filePath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(filePath, "file payload");
            var result = Assert.Single(await _service.ComputeFileAsync(filePath, [HashAlgorithmKind.SHA256]));

            Assert.Equal(
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("file payload"))).ToLowerInvariant(),
                result.HexValue);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task StreamHashHonorsCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("payload"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.ComputeStreamAsync(stream, [HashAlgorithmKind.SHA256], cancellationSource.Token));
    }

    [Fact]
    public void RejectsEmptyOrUnsupportedAlgorithmSelection()
    {
        Assert.Throws<ArgumentException>(() => _service.ComputeText("value", []));
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.ComputeText("value", [(HashAlgorithmKind)99]));
    }
}
