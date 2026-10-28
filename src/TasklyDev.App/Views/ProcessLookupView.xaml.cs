using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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

    private void ProcessGridHeader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button header ||
            header.Tag is not string sortProperty ||
            DataContext is not ProcessLookupViewModel viewModel)
        {
            return;
        }

        var view = CollectionViewSource.GetDefaultView(viewModel.Processes);
        var isDescending = view.SortDescriptions.Count > 0 &&
                           view.SortDescriptions[0].PropertyName == sortProperty &&
                           view.SortDescriptions[0].Direction == ListSortDirection.Ascending;
        var direction = isDescending ? ListSortDirection.Descending : ListSortDirection.Ascending;

        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new SortDescription(sortProperty, direction));

        foreach (var button in new[] { ProcessNameHeader, ProcessIdHeader, ProcessStartHeader, ProcessPathHeader })
        {
            button.Content = $"{SortHeaderText(button.Tag as string)}  ↕";
        }

        header.Content = $"{SortHeaderText(sortProperty)}  {(isDescending ? '↓' : '↑')}";
    }

    private static string SortHeaderText(string? propertyName) => propertyName switch
    {
        "Name" => "Process name",
        "Id" => "PID",
        "StartTime" => "Start time",
        "ExecutablePath" => "Executable path",
        _ => string.Empty
    };
}
