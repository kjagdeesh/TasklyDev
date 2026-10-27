namespace TasklyDev.Core.Conversion;

public interface INumberBaseService
{
    string Convert(string input, int sourceBase, int targetBase);
}
