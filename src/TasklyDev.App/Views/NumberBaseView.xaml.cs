using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace TasklyDev.Views;

public partial class NumberBaseView : UserControl
{
    private bool _editorResizePending;

    public NumberBaseView()
    {
        InitializeComponent();
        QueueEditorHeightUpdate();
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e) =>
        QueueEditorHeightUpdate();

    private void NumberBaseView_Loaded(object sender, RoutedEventArgs e) =>
        QueueEditorHeightUpdate();

    private void NumberBaseView_SizeChanged(object sender, SizeChangedEventArgs e) =>
        QueueEditorHeightUpdate();

    private void QueueEditorHeightUpdate()
    {
        if (_editorResizePending)
        {
            return;
        }

        _editorResizePending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(UpdateEditorHeights));
    }

    private void UpdateEditorHeights()
    {
        _editorResizePending = false;
        if (!IsLoaded || InputEditor.ActualWidth <= 0 || OutputEditor.ActualWidth <= 0)
        {
            return;
        }

        InputEditor.Height = double.NaN;
        OutputEditor.Height = double.NaN;
        InputEditor.Measure(new Size(InputEditor.ActualWidth, double.PositiveInfinity));
        OutputEditor.Measure(new Size(OutputEditor.ActualWidth, double.PositiveInfinity));
        var height = Math.Max(InputEditor.DesiredSize.Height, OutputEditor.DesiredSize.Height);
        InputEditor.Height = height;
        OutputEditor.Height = height;
    }
}
