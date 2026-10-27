using System.Text.RegularExpressions;
using TasklyDev.Core.Identifiers;
using Xunit;

namespace TasklyDev.Tests;

public sealed class GuidServiceTests
{
    private readonly GuidService _service = new();

    [Theory]
    [InlineData(GuidOutputFormat.D, "^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$")]
    [InlineData(GuidOutputFormat.N, "^[0-9a-f]{32}$")]
    [InlineData(GuidOutputFormat.B, "^\\{[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}\\}$")]
    [InlineData(GuidOutputFormat.P, "^\\([0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}\\)$")]
    [InlineData(GuidOutputFormat.X, "^\\{0x[0-9a-f]{8},0x[0-9a-f]{4},0x[0-9a-f]{4},\\{0x[0-9a-f]{2}(,0x[0-9a-f]{2}){7}\\}\\}$")]
    public void GeneratesValidGuidRepresentation(GuidOutputFormat format, string pattern)
    {
        var result = Assert.Single(_service.Generate(1, format));

        Assert.Matches(new Regex(pattern, RegexOptions.CultureInvariant), result);
    }

    [Fact]
    public void GeneratesUniqueBatchWithinMaximumSize()
    {
        var values = _service.Generate(GuidService.MaximumBatchSize, GuidOutputFormat.D);

        Assert.Equal(GuidService.MaximumBatchSize, values.Count);
        Assert.Equal(values.Count, values.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(501)]
    public void RejectsQuantityOutsideOneThroughFiveHundred(int quantity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.Generate(quantity, GuidOutputFormat.D));
    }

    [Fact]
    public void UppercaseOptionChangesOnlyCharacterCasing()
    {
        var uppercase = Assert.Single(_service.Generate(1, GuidOutputFormat.D, uppercase: true));

        Assert.Equal(uppercase, uppercase.ToUpperInvariant());
        Assert.Matches("^[0-9A-F-]+$", uppercase);
    }
}
