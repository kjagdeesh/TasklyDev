namespace TasklyDev.Core.RegularExpressions;

/// <summary>Tests regular-expression patterns against input text.</summary>
public interface IRegexTesterService
{
    /// <summary>Finds matches and capture groups using the selected .NET regex options.</summary>
    /// <param name="pattern">Regular-expression pattern to evaluate.</param>
    /// <param name="input">Text to search.</param>
    /// <param name="ignoreCase">Whether matching ignores letter case.</param>
    /// <param name="multiline">Whether line anchors match at each line boundary.</param>
    /// <param name="singleline">Whether the dot metacharacter matches newline characters.</param>
    /// <returns>Match details, limited to the configured display maximum.</returns>
    RegexTestResult Test(string pattern, string input, bool ignoreCase, bool multiline, bool singleline);
}
