using System.Windows;

namespace TasklyDev.Services;

/// <summary>Uses a Windows confirmation dialog before clearing clipboard history.</summary>
public sealed class WpfClipboardHistoryConfirmationService : IClipboardHistoryConfirmationService
{
    /// <inheritdoc />
    public bool ConfirmClearHistory() =>
        MessageBox.Show(
            "Delete all clipboard history entries? This cannot be undone.",
            "Clear clipboard history",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
}
