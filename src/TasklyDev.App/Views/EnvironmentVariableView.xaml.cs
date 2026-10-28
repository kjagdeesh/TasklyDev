using System.Windows;
using System.Windows.Controls;
using TasklyDev.ViewModels;

namespace TasklyDev.Views;

public partial class EnvironmentVariableView : UserControl
{
    public EnvironmentVariableView()
    {
        InitializeComponent();
    }

    private void EnvironmentVariableView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && DataContext is EnvironmentVariableViewModel viewModel)
        {
            viewModel.RefreshCommand.Execute(null);
        }
    }
}
