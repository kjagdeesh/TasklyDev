namespace TasklyDev.Core.Identifiers;

/// <summary>Generates RFC-compatible GUID values in supported .NET string formats.</summary>
public sealed class GuidService : IGuidService
{
    /// <summary>The maximum number of GUIDs that can be generated in one batch.</summary>
    public const int MaximumBatchSize = 500;

    /// <inheritdoc />
    public IReadOnlyList<string> Generate(int quantity, GuidOutputFormat format, bool uppercase = false)
    {
        if (quantity is < 1 or > MaximumBatchSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                quantity,
                $"Quantity must be between 1 and {MaximumBatchSize}.");
        }

        if (!Enum.IsDefined(format))
        {
            throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported GUID output format.");
        }

        var values = new string[quantity];
        var formatSpecifier = format.ToString();
        for (var index = 0; index < values.Length; index++)
        {
            var value = Guid.NewGuid().ToString(formatSpecifier);
            values[index] = uppercase ? value.ToUpperInvariant() : value.ToLowerInvariant();
        }

        return values;
    }
}
