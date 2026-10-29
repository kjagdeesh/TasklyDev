using System.Windows;
using TasklyDev.Views;

namespace TasklyDev.Services;

/// <summary>Uses a Windows confirmation dialog before clearing clipboard history.</summary>
public sealed class WpfClipboardHistoryConfirmationService : IClipboardHistoryConfirmationService
{
    /// <inheritdoc />
    public bool ConfirmClearHistory()
    {
        var dialog = new ClearClipboardHistoryDialog();
        if (Application.Current?.MainWindow is { } owner)
        {
            dialog.Owner = owner;
        }

        return dialog.ShowDialog() == true;
    }
}
