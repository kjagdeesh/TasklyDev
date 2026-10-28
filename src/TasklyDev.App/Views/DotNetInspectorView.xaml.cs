using System.Windows;
using System.Windows.Controls;
using TasklyDev.ViewModels;

namespace TasklyDev.Views;

/// <summary>Displays read-only machine-wide .NET SDK, runtime, workload, and host information.</summary>
public partial class DotNetInspectorView : UserControl
{
    /// <summary>Creates the .NET inspector view.</summary>
    public DotNetInspectorView()
    {
        InitializeComponent();
    }

    /// <summary>Refreshes machine discovery when the screen becomes visible.</summary>
    private void DotNetInspectorView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && DataContext is DotNetInspectorViewModel viewModel)
        {
            viewModel.RefreshCommand.Execute(null);
        }
    }
}
