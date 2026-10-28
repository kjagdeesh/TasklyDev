using System.Windows;
using System.Windows.Controls;
using TasklyDev.ViewModels;

namespace TasklyDev.Views;

/// <summary>Displays operating-system, hardware, storage, uptime, and runtime information.</summary>
public partial class SystemInformationView : UserControl
{
    /// <summary>Creates the System Information view.</summary>
    public SystemInformationView()
    {
        InitializeComponent();
    }

    /// <summary>Refreshes the system snapshot when the screen becomes visible.</summary>
    private void SystemInformationView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && DataContext is SystemInformationViewModel viewModel)
        {
            viewModel.RefreshCommand.Execute(null);
        }
    }
}
