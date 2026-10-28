namespace TasklyDev.Core.Conversion;

/// <summary>Converts signed integers between supported positional bases.</summary>
public interface INumberBaseService
{
    /// <summary>Converts an integer string from one base to another.</summary>
    /// <param name="input">Signed integer using digits valid for <paramref name="sourceBase"/>.</param>
    /// <param name="sourceBase">Base of the input, from 2 through 36.</param>
    /// <param name="targetBase">Base of the result, from 2 through 36.</param>
    /// <returns>The converted integer, formatted with uppercase letters when needed.</returns>
    string Convert(string input, int sourceBase, int targetBase);
}
