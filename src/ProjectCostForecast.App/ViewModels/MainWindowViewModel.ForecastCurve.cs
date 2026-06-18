using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;

namespace ProjectCostForecast.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private readonly ForecastCurveService _forecastCurveService = new();

    public IReadOnlyList<MonthlyForecast> GetAdjustableForecastMonths(ForecastLine line)
    {
        return line.MonthlyForecasts
            .Where(month => !month.IsLocked)
            .OrderBy(month => FiscalPeriod.SortKey(month.PeriodLabel))
            .ToList();
    }

    public bool ApplyForecastCurve(
        ForecastLine line,
        ForecastCurveProfile profile,
        decimal total,
        string? startPeriodLabel = null,
        string? endPeriodLabel = null)
    {
        var months = GetAdjustableForecastMonths(line).AsEnumerable();
        if (!string.IsNullOrWhiteSpace(startPeriodLabel))
        {
            var startKey = FiscalPeriod.SortKey(startPeriodLabel);
            months = months.Where(month => FiscalPeriod.SortKey(month.PeriodLabel) >= startKey);
        }

        if (!string.IsNullOrWhiteSpace(endPeriodLabel))
        {
            var endKey = FiscalPeriod.SortKey(endPeriodLabel);
            months = months.Where(month => FiscalPeriod.SortKey(month.PeriodLabel) <= endKey);
        }

        var targetMonths = months.ToList();
        if (targetMonths.Count == 0)
        {
            StatusText = "No open forecast months in the selected range to adjust.";
            return false;
        }

        BeginSpreadsheetEditBatch();
        int changed;
        try
        {
            changed = _forecastCurveService.ApplyCurve(line, targetMonths, total, profile);
        }
        finally
        {
            EndSpreadsheetEditBatch(
                $"Applied {profile} curve of {total:C0} across {targetMonths.Count} months for {line.ResourceName}",
                changed: true,
                rebuildFilterLists: false);
        }

        AddAuditEvent(
            "ForecastLine",
            line.RowNumber.ToString(),
            "AdjustCurve",
            string.Empty,
            $"{profile} {total:0.##} over {targetMonths.Count} months",
            "Adjusted forecast curve");
        StatusText = $"{line.ResourceName}: spread {total:C0} across {changed} months as {ForecastCurveService.DescribeProfile(profile)}.";
        return true;
    }
}
