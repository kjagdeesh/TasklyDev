using System.Text.RegularExpressions;
using TasklyDev.Core.RegularExpressions;
using Xunit;

namespace TasklyDev.Tests;

public sealed class RegexTesterServiceTests
{
    [Fact]
    public void ReturnsMatchPositionsAndNamedAndNumberedGroups()
    {
        var result = new RegexTesterService().Test(
            @"(?<user>\w+)@(\w+\.\w+)",
            "Email: support@example.com",
            ignoreCase: false,
            multiline: false,
            singleline: false);

        var match = Assert.Single(result.Matches);
        Assert.Equal(7, match.Index);
        Assert.Equal("support@example.com", match.Value);
        Assert.Contains(match.Groups, group => group.Name == "user" && group.Value == "support");
        Assert.Contains(match.Groups, group => group.Name == "1" && group.Value == "example.com");
        Assert.False(result.IsTruncated);
    }

    [Fact]
    public void AppliesIgnoreCaseOption()
    {
        var service = new RegexTesterService();

        Assert.Empty(service.Test("abc", "ABC", false, false, false).Matches);
        Assert.Single(service.Test("abc", "ABC", true, false, false).Matches);
    }

    [Fact]
    public void AppliesMultilineAndSinglelineOptions()
    {
        var service = new RegexTesterService();

        Assert.Empty(service.Test("^target$", "first\ntarget\nlast", false, false, false).Matches);
        Assert.Single(service.Test("^target$", "first\ntarget\nlast", false, true, false).Matches);
        Assert.Empty(service.Test("first.*last", "first\ntarget\nlast", false, false, false).Matches);
        Assert.Single(service.Test("first.*last", "first\ntarget\nlast", false, false, true).Matches);
    }

    [Fact]
    public void InvalidPatternIsSurfaced()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new RegexTesterService().Test("(", "text", false, false, false));
    }

    [Fact]
    public void PathologicalPatternTimesOutWithinFiniteLimit()
    {
        var service = new RegexTesterService(TimeSpan.FromMilliseconds(20));
        var input = string.Concat(new string('a', 50_000), "!");

        Assert.ThrowsAny<TimeoutException>(() =>
            service.Test("^(a+)+$", input, false, false, false));
    }

    [Fact]
    public void CapsDisplayedMatchesForLargeInputs()
    {
        var result = new RegexTesterService().Test(
            @"\d+",
            string.Join(' ', Enumerable.Repeat("12", 200)),
            ignoreCase: false,
            multiline: false,
            singleline: false);

        Assert.Equal(RegexTesterService.MaximumDisplayedMatches, result.Matches.Count);
        Assert.True(result.IsTruncated);
    }
}
