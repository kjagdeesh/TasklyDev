namespace TasklyDev.Core.Identifiers;

public interface IGuidService
{
    IReadOnlyList<string> Generate(int quantity, GuidOutputFormat format, bool uppercase = false);
}
