namespace TasklyDev.Services;

public interface IClipboardService
{
    bool TrySetText(string text);
}
