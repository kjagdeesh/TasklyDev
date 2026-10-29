using System.Windows;
using System.Windows.Input;

namespace TasklyDev.Views;

public partial class ClearClipboardHistoryDialog : Window
{
    public ClearClipboardHistoryDialog()
    {
        InitializeComponent();
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
