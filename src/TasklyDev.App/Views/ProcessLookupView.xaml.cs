using System.Windows;
using System.Windows.Controls;
using TasklyDev.ViewModels;

namespace TasklyDev.Views;

public partial class ProcessLookupView : UserControl
{
    public ProcessLookupView()
    {
        InitializeComponent();
    }

    private void ProcessLookupView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && DataContext is ProcessLookupViewModel viewModel)
        {
            viewModel.RefreshCommand.Execute(null);
        }
    }
}
