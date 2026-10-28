namespace TasklyDev.Core.Identifiers;

/// <summary>Generates batches of GUID strings.</summary>
public interface IGuidService
{
    /// <summary>Generates GUIDs using the requested representation and letter casing.</summary>
    /// <param name="quantity">Number of GUIDs to generate; must be between 1 and 500.</param>
    /// <param name="format">String representation to use for each GUID.</param>
    /// <param name="uppercase">Whether hexadecimal letters should be uppercase.</param>
    /// <returns>The generated GUID strings.</returns>
    IReadOnlyList<string> Generate(int quantity, GuidOutputFormat format, bool uppercase = false);
}
