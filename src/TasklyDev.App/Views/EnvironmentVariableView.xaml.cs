using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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

    private static string SortHeaderText(string? propertyName) => propertyName switch
    {
        "Name" => "Name",
        "Value" => "Value (preview)",
        _ => string.Empty
    };
}
