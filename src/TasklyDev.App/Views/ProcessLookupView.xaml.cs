using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TasklyDev.ViewModels;

namespace TasklyDev.Views;

/// <summary>Renders, refreshes, and sorts the process snapshot screen.</summary>
public partial class ProcessLookupView : UserControl
{
    /// <summary>Creates the process lookup controls and refreshes the snapshot when shown.</summary>
    public ProcessLookupView()
    {
        InitializeComponent();
    }

    /// <summary>Reloads process data when the screen becomes visible.</summary>
    private void ProcessLookupView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && DataContext is ProcessLookupViewModel viewModel)
        {
            viewModel.RefreshCommand.Execute(null);
        }
    }

    /// <summary>Sorts the process rows by the clicked table column.</summary>
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

    /// <summary>Maps a process property name to its table-header label.</summary>
    private static string SortHeaderText(string? propertyName) => propertyName switch
    {
        "Name" => "Process name",
        "Id" => "PID",
        "StartTime" => "Start time",
        "ExecutablePath" => "Executable path",
        _ => string.Empty
    };
}
