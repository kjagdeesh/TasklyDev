using System.Windows;
using TasklyDev.Views;

namespace TasklyDev.Services;

/// <summary>Uses a Windows confirmation dialog before clearing clipboard history.</summary>
public sealed class WpfClipboardHistoryConfirmationService : IClipboardHistoryConfirmationService
{
    /// <inheritdoc />
    public bool ConfirmDeleteSelectedEntry() =>
        ShowDialog(new ClearClipboardHistoryDialog(
            "Delete this clipboard entry?",
            "This action cannot be undone.",
            "The selected clipboard entry will be permanently deleted.",
            "Other entries and your capture settings will remain unchanged.",
            "Delete entry",
            "Confirm deleting selected clipboard entry"));

    /// <inheritdoc />
    public bool ConfirmClearHistory()
        => ShowDialog(new ClearClipboardHistoryDialog());

    private static bool ShowDialog(ClearClipboardHistoryDialog dialog)
    {
        if (Application.Current?.MainWindow is { } owner)
        {
            dialog.Owner = owner;
        }

        return dialog.ShowDialog() == true;
    }
}
