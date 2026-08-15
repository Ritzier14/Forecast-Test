using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.ViewModels;

namespace ProjectCostForecast.App;

public partial class MainWindow
{
    private static string GetReportDataSetTitle(string dataSetKey)
        => dataSetKey switch
        {
            "CostCodeSummary" => "Cost code summary",
            "TotalBudget" => "Total budget",
            "TotalSpend" => "Total spend",
            "TotalForecast" => "Total forecast",
            _ => "Data set"
        };

    private FrameworkElement CreateReportDataSetTable(ReportCanvasObjectLayout layout)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return new TextBlock { Text = "No data available.", Margin = new Thickness(12) };
        }

        var rows = viewModel.ForecastLines
            .GroupBy(line => string.IsNullOrWhiteSpace(line.ProjectCode) ? "(Unassigned)" : line.ProjectCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new ReportDataSetSummaryRow
            {
                Group = group.Key,
                TotalBudget = group.Sum(line => line.Budget),
                TotalSpend = group.Sum(line => line.CostToDateSummary),
                TotalForecast = group.Sum(line => line.TotalForecastCtc)
            })
            .OrderBy(row => row.Group, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (rows.Count > 0)
        {
            rows.Add(new ReportDataSetSummaryRow
            {
                Group = "Total",
                TotalBudget = rows.Sum(row => row.TotalBudget),
                TotalSpend = rows.Sum(row => row.TotalSpend),
                TotalForecast = rows.Sum(row => row.TotalForecast),
                IsTotal = true
            });
        }

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            RowHeight = 26,
            Margin = new Thickness(4),
            Background = System.Windows.Media.Brushes.White,
            ItemsSource = rows
        };
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Cost code",
            Binding = new Binding(nameof(ReportDataSetSummaryRow.Group)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });
        grid.Columns.Add(CreateReportDataSetMoneyColumn("Total budget", nameof(ReportDataSetSummaryRow.TotalBudget)));
        grid.Columns.Add(CreateReportDataSetMoneyColumn("Total spend", nameof(ReportDataSetSummaryRow.TotalSpend)));
        grid.Columns.Add(CreateReportDataSetMoneyColumn("Total forecast", nameof(ReportDataSetSummaryRow.TotalForecast)));
        return grid;
    }

    private FrameworkElement CreateReportDataSetMetric(ReportCanvasObjectLayout layout)
    {
        var value = layout.DataSetKey switch
        {
            "TotalBudget" => DataContext is MainWindowViewModel viewModel ? viewModel.TotalBudget : 0,
            "TotalSpend" => DataContext is MainWindowViewModel viewModel ? viewModel.TotalCostToDate : 0,
            "TotalForecast" => DataContext is MainWindowViewModel viewModel ? viewModel.TotalForecastCtc : 0,
            _ => 0
        };
        var panel = new StackPanel { Margin = new Thickness(12), VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new TextBlock
        {
            Text = GetReportDataSetTitle(layout.DataSetKey),
            Foreground = BrushFactory.Frozen("#64748B"),
            FontSize = 12
        });
        panel.Children.Add(new TextBlock
        {
            Text = AccountingNoDecimalsConverter.FormatAccounting(value, System.Globalization.CultureInfo.CurrentCulture),
            Foreground = BrushFactory.Frozen("#0F172A"),
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 5, 0, 0)
        });
        return new Border
        {
            Background = BrushFactory.Frozen("#EEF4FF"),
            BorderBrush = BrushFactory.Frozen("#D8E6FF"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = panel
        };
    }

    private static DataGridTextColumn CreateReportDataSetMoneyColumn(string header, string propertyName)
        => new()
        {
            Header = header,
            Binding = new Binding(propertyName) { Converter = new AccountingNoDecimalsConverter() },
            Width = new DataGridLength(135)
        };
}

internal sealed class ReportDataSetSummaryRow
{
    public string Group { get; set; } = string.Empty;
    public decimal TotalBudget { get; set; }
    public decimal TotalSpend { get; set; }
    public decimal TotalForecast { get; set; }
    public bool IsTotal { get; set; }
}
