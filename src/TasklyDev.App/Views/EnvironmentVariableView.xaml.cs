using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TasklyDev.ViewModels;

namespace TasklyDev.Views;

/// <summary>Renders, refreshes, and sorts environment-variable results.</summary>
public partial class EnvironmentVariableView : UserControl
{
    /// <summary>Creates the environment-variable browser controls.</summary>
    public EnvironmentVariableView()
    {
        InitializeComponent();
    }

    /// <summary>Reloads variables when the screen becomes visible.</summary>
    private void EnvironmentVariableView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && DataContext is EnvironmentVariableViewModel viewModel)
        {
            viewModel.RefreshCommand.Execute(null);
        }
    }

    /// <summary>Sorts the environment-variable rows by the clicked table column.</summary>
    private void EnvironmentGridHeader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button header ||
            header.Tag is not string sortProperty ||
            DataContext is not EnvironmentVariableViewModel viewModel)
        {
            return;
        }

        var view = CollectionViewSource.GetDefaultView(viewModel.Variables);
        var isDescending = view.SortDescriptions.Count > 0 &&
                           view.SortDescriptions[0].PropertyName == sortProperty &&
                           view.SortDescriptions[0].Direction == ListSortDirection.Ascending;
        var direction = isDescending ? ListSortDirection.Descending : ListSortDirection.Ascending;

        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new SortDescription(sortProperty, direction));

        foreach (var button in new[] { EnvironmentNameHeader, EnvironmentValueHeader })
        {
            button.Content = $"{SortHeaderText(button.Tag as string)}  ↕";
        }

        header.Content = $"{SortHeaderText(sortProperty)}  {(isDescending ? '↓' : '↑')}";
    }

    /// <summary>Maps an environment-variable property name to its table-header label.</summary>
    private static string SortHeaderText(string? propertyName) => propertyName switch
    {
        "Name" => "Name",
        "Value" => "Value (preview)",
        _ => string.Empty
    };
}
