using System.Numerics;

namespace TasklyDev.Core.Conversion;

public sealed class NumberBaseService : INumberBaseService
{
    private const string Digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public string Convert(string input, int sourceBase, int targetBase)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidateBase(sourceBase, nameof(sourceBase));
        ValidateBase(targetBase, nameof(targetBase));

        var value = Parse(input, sourceBase);
        return Format(value, targetBase);
    }

    private static BigInteger Parse(string input, int numberBase)
    {
        var text = input.Trim();
        if (text.Length == 0)
        {
            throw new FormatException("Enter an integer to convert.");
        }

        var isNegative = text[0] == '-';
        var startIndex = isNegative || text[0] == '+' ? 1 : 0;
        if (startIndex == text.Length)
        {
            throw new FormatException("Enter at least one digit after the sign.");
        }

        var result = BigInteger.Zero;
        for (var index = startIndex; index < text.Length; index++)
        {
            var character = char.ToUpperInvariant(text[index]);
            var digit = Digits.IndexOf(character);
            if (digit < 0 || digit >= numberBase)
            {
                throw new FormatException($"'{text[index]}' is not a valid base-{numberBase} digit.");
            }

            result = (result * numberBase) + digit;
        }

        return isNegative ? BigInteger.Negate(result) : result;
    }

    private static string Format(BigInteger value, int numberBase)
    {
        if (value.IsZero)
        {
            return "0";
        }

        var isNegative = value.Sign < 0;
        var remaining = BigInteger.Abs(value);
        var output = new List<char>();

        while (remaining > BigInteger.Zero)
        {
            remaining = BigInteger.DivRem(remaining, numberBase, out var remainder);
            output.Add(Digits[(int)remainder]);
        }

        if (isNegative)
        {
            output.Add('-');
        }

        output.Reverse();
        return new string(output.ToArray());
    }

    private static void ValidateBase(int numberBase, string parameterName)
    {
        if (numberBase is < 2 or > 36)
        {
            throw new ArgumentOutOfRangeException(parameterName, numberBase, "Base must be between 2 and 36.");
        }
    }
}
