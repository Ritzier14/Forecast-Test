using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App;

public sealed class UnmatchedImportWindow : Window
{
    public UnmatchedImportWindow(IEnumerable<UnmatchedImportCombination> items)
    {
        Title = "Unmatched import combinations";
        Width = 980;
        Height = 600;
        MinWidth = 700;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var root = new DockPanel
        {
            Margin = new Thickness(14)
        };

        var closeButton = new Button
        {
            Content = "Close",
            MinWidth = 90,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        closeButton.Click += (_, _) => Close();
        DockPanel.SetDock(closeButton, Dock.Bottom);
        root.Children.Add(closeButton);

        var intro = new TextBlock
        {
            Text = "These new task/resource combinations were not imported because auto-create was cancelled.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        };
        DockPanel.SetDock(intro, Dock.Top);
        root.Children.Add(intro);

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            CanUserResizeRows = false,
            IsReadOnly = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            RowHeaderWidth = 0,
            ItemsSource = items.OrderByDescending(item => item.RecordedAt).ToList()
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Recorded", Binding = new Binding(nameof(UnmatchedImportCombination.RecordedAt)) { StringFormat = "g" }, Width = 130 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Task code", Binding = new Binding(nameof(UnmatchedImportCombination.TaskNumber)), Width = 110 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Manual Name", Binding = new Binding(nameof(UnmatchedImportCombination.ManualName)), Width = 220 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Project code", Binding = new Binding(nameof(UnmatchedImportCombination.ProjectCode)), Width = 110 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Amount", Binding = new Binding(nameof(UnmatchedImportCombination.Amount)) { StringFormat = "{0:C0}" }, Width = 100 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Transactions", Binding = new Binding(nameof(UnmatchedImportCombination.TransactionCount)), Width = 95 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Category", Binding = new Binding(nameof(UnmatchedImportCombination.Category)), Width = 170 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Source", Binding = new Binding(nameof(UnmatchedImportCombination.Source)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });

        root.Children.Add(SpreadsheetReadOnlyGridBehavior.Attach(grid));
        Content = root;
    }
}
