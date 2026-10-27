using System.Diagnostics;
using System.Text.RegularExpressions;

namespace TasklyDev.Core.RegularExpressions;

public sealed class RegexTesterService : IRegexTesterService
{
    public const int MaximumDisplayedMatches = 100;
    private static readonly TimeSpan DefaultMatchTimeout = TimeSpan.FromMilliseconds(250);
    private readonly TimeSpan _matchTimeout;

    public RegexTesterService(TimeSpan? matchTimeout = null)
    {
        _matchTimeout = matchTimeout ?? DefaultMatchTimeout;
        if (_matchTimeout <= TimeSpan.Zero || _matchTimeout == Regex.InfiniteMatchTimeout)
        {
            throw new ArgumentOutOfRangeException(nameof(matchTimeout), "A finite positive regex timeout is required.");
        }
    }

    public RegexTestResult Test(string pattern, string input, bool ignoreCase, bool multiline, bool singleline)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(input);

        var options = RegexOptions.CultureInvariant;
        if (ignoreCase)
        {
            options |= RegexOptions.IgnoreCase;
        }

        if (multiline)
        {
            options |= RegexOptions.Multiline;
        }

        if (singleline)
        {
            options |= RegexOptions.Singleline;
        }

        var regex = new Regex(pattern, options, _matchTimeout);
        var elapsed = Stopwatch.StartNew();
        var matches = new List<RegexMatchResult>();
        var match = regex.Match(input);
        EnsureWithinTotalTimeout(elapsed);
        while (match.Success && matches.Count < MaximumDisplayedMatches)
        {
            var groups = new List<RegexCaptureGroup>(match.Groups.Count);
            for (var index = 0; index < match.Groups.Count; index++)
            {
                var group = match.Groups[index];
                if (!group.Success)
                {
                    continue;
                }

                groups.Add(new RegexCaptureGroup(
                    regex.GroupNameFromNumber(index),
                    group.Index,
                    group.Length,
                    group.Value));
            }

            matches.Add(new RegexMatchResult(match.Index, match.Length, match.Value, groups));
            match = match.NextMatch();
            EnsureWithinTotalTimeout(elapsed);
        }

        return new RegexTestResult(matches, match.Success);
    }

    private void EnsureWithinTotalTimeout(Stopwatch elapsed)
    {
        if (elapsed.Elapsed > _matchTimeout)
        {
            throw new TimeoutException("Regex evaluation exceeded the total processing time limit.");
        }
    }
}
