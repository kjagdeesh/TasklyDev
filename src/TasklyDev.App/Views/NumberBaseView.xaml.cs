using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace TasklyDev.Views;

/// <summary>Renders the number-base converter and synchronizes its editor heights.</summary>
public partial class NumberBaseView : UserControl
{
    private bool _editorResizePending;

    /// <summary>Creates the converter controls and schedules the initial editor layout.</summary>
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

    /// <summary>Coalesces layout events into one deferred editor-height measurement.</summary>
    private void QueueEditorHeightUpdate()
    {
        if (_editorResizePending)
        {
            return;
        }

        _editorResizePending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(UpdateEditorHeights));
    }

    /// <summary>Measures both editors and assigns them the same content-aware height.</summary>
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
