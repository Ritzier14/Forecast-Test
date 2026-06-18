using System.Globalization;
using System.Windows.Data;

namespace ProjectCostForecast.App.Models;

public sealed class ForecastGroupSummaryConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not IEnumerable<object> items)
        {
            return string.Empty;
        }

        var lines = items.OfType<ForecastLine>().ToList();
        if (lines.Count == 0)
        {
            return string.Empty;
        }

        var total = parameter?.ToString() switch
        {
            nameof(ForecastLine.CostToDateSummary) => lines.Sum(line => line.CostToDateSummary),
            nameof(ForecastLine.CurrentMonthCost) => lines.Sum(line => line.CurrentMonthCost),
            nameof(ForecastLine.LastMonthForecast) => lines.Sum(line => line.LastMonthForecast),
            nameof(ForecastLine.VarianceLastMonthToDate) => lines.Sum(line => line.VarianceLastMonthToDate),
            nameof(ForecastLine.TotalForecastCtc) => lines.Sum(line => line.TotalForecastCtc),
            nameof(ForecastLine.PlannedCostFcc) => lines.Sum(line => line.PlannedCostFcc),
            nameof(ForecastLine.Budget) => lines.Sum(line => line.Budget),
            nameof(ForecastLine.TotalBudgetVariance) => lines.Sum(line => line.TotalBudgetVariance),
            _ => 0
        };

        return total.ToString("C0", culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public sealed class ForecastGroupMonthSummaryConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2
            || values[0] is not IEnumerable<object> items
            || values[1] is not ForecastMonthColumnDefinition column)
        {
            return string.Empty;
        }

        var total = items.OfType<ForecastLine>().Sum(line => line[column.Key]);
        return total == 0 ? "-" : total.ToString("C0", culture);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
