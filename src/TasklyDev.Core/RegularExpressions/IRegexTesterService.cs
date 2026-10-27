namespace TasklyDev.Core.RegularExpressions;

public interface IRegexTesterService
{
    RegexTestResult Test(string pattern, string input, bool ignoreCase, bool multiline, bool singleline);
}
