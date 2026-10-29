using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;

namespace TasklyDev.Views;

public partial class ClearClipboardHistoryDialog : Window
{
    public ClearClipboardHistoryDialog()
        : this(
            "Clear clipboard history?",
            "Please confirm this permanent change.",
            "All saved clipboard entries on this device will be permanently deleted.",
            "Clipboard capture will remain in its current state.",
            "Clear history",
            "Confirm clearing clipboard history")
    {
    }

    public ClearClipboardHistoryDialog(
        string title,
        string subtitle,
        string message,
        string detail,
        string confirmButtonText,
        string confirmAutomationName)
    {
        InitializeComponent();
        ConfirmationTitle.Text = title;
        ConfirmationSubtitle.Text = subtitle;
        ConfirmationMessage.Text = message;
        ConfirmationDetail.Text = detail;
        ConfirmButton.Content = confirmButtonText;
        AutomationProperties.SetName(ConfirmButton, confirmAutomationName);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) =>
        DialogResult = false;

    private void Confirm_Click(object sender, RoutedEventArgs e) =>
        DialogResult = true;

    private void CloseWindow_Click(object sender, RoutedEventArgs e) =>
        DialogResult = false;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
