namespace TasklyDev.Services;

/// <summary>Confirms destructive clipboard-history operations.</summary>
public interface IClipboardHistoryConfirmationService
{
    /// <summary>Asks the user to confirm removal of every stored history entry.</summary>
    bool ConfirmClearHistory();
}
