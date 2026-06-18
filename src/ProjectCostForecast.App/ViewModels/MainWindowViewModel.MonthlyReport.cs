using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ProjectCostForecast.App;
using Microsoft.Win32;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;

namespace ProjectCostForecast.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void RebuildMonthlyReport()
    {
        var fiscalWindow = BuildFiscalYearWindow();
        MonthlyReportFiscalYear1Label = fiscalWindow.ElementAtOrDefault(0) ?? "FY 1";
        MonthlyReportFiscalYear2Label = fiscalWindow.ElementAtOrDefault(1) ?? "FY 2";
        MonthlyReportFiscalYear3Label = fiscalWindow.ElementAtOrDefault(2) ?? "FY 3";

        var fiscalLookup = FiscalYearReportLines
            .ToDictionary(line => line.FiscalYear, line => line, StringComparer.OrdinalIgnoreCase);

        var summaryTotals = GetMonthlyReportTotalFiscalLines();
        var summaryTotalValues = CalculateFiscalYearReportTotals(summaryTotals);
        var year1 = fiscalLookup.GetValueOrDefault(MonthlyReportFiscalYear1Label);
        var year2 = fiscalLookup.GetValueOrDefault(MonthlyReportFiscalYear2Label);
        var year3 = fiscalLookup.GetValueOrDefault(MonthlyReportFiscalYear3Label);

        ReplaceCollection(MonthlyReportFiscalSummaryRows, new[]
        {
            BuildFiscalSummaryRow("Spent to date", year1?.SpentToDate ?? 0, year2?.SpentToDate ?? 0, year3?.SpentToDate ?? 0, summaryTotalValues.SpentToDate),
            BuildFiscalSummaryRow("Cost to complete", year1?.CostToComplete ?? 0, year2?.CostToComplete ?? 0, year3?.CostToComplete ?? 0, summaryTotalValues.CostToComplete),
            BuildFiscalSummaryRow("Planned cost", year1?.PlannedCost ?? 0, year2?.PlannedCost ?? 0, year3?.PlannedCost ?? 0, summaryTotalValues.PlannedCost),
            BuildFiscalSummaryRow("Budget", year1?.Budget ?? 0, year2?.Budget ?? 0, year3?.Budget ?? 0, summaryTotalValues.Budget),
            BuildFiscalSummaryRow("Variance", year1?.Variance ?? 0, year2?.Variance ?? 0, year3?.Variance ?? 0, summaryTotalValues.Variance)
        });

        var categoryGroups = ForecastLines
            .Where(line => !string.IsNullOrWhiteSpace(line.ProjectCode))
            .GroupBy(line => line.ProjectCode)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var lines = group.ToList();
                return new MonthlyReportCategoryGroup(group.Key, lines, CalculateMonthlyReportLineTotals(lines));
            })
            .ToList();

        var categoryRows = categoryGroups
            .Select(group =>
            {
                return new MonthlyReportCategoryRow
                {
                    ProjectCode = group.ProjectCode,
                    TotalForecastDisplay = FormatReportCurrency(group.Totals.TotalForecast),
                    CostToDateDisplay = FormatReportCurrency(group.Totals.CostToDate),
                    PlannedCostDisplay = FormatReportCurrency(group.Totals.PlannedCost),
                    InitialBudgetDisplay = FormatReportCurrency(group.Totals.Budget),
                    TotalBudgetVarianceDisplay = FormatReportCurrency(group.Totals.TotalBudgetVariance),
                    LastMonthPlannedCostDisplay = FormatReportCurrency(group.Totals.LastMonthPlannedCost),
                    VarianceFromLastMonthDisplay = FormatReportCurrency(group.Totals.VarianceFromLastMonth)
                };
            })
            .ToList();

        if (categoryRows.Count > 0)
        {
            var totalCategoryTotals = CalculateMonthlyReportLineTotals(ForecastLines);
            categoryRows.Add(new MonthlyReportCategoryRow
            {
                ProjectCode = "Total",
                TotalForecastDisplay = FormatReportCurrency(totalCategoryTotals.TotalForecast),
                CostToDateDisplay = FormatReportCurrency(totalCategoryTotals.CostToDate),
                PlannedCostDisplay = FormatReportCurrency(totalCategoryTotals.PlannedCost),
                InitialBudgetDisplay = FormatReportCurrency(totalCategoryTotals.Budget),
                TotalBudgetVarianceDisplay = FormatReportCurrency(totalCategoryTotals.TotalBudgetVariance),
                LastMonthPlannedCostDisplay = FormatReportCurrency(totalCategoryTotals.LastMonthPlannedCost),
                VarianceFromLastMonthDisplay = FormatReportCurrency(totalCategoryTotals.VarianceFromLastMonth)
            });
        }

        ReplaceCollection(MonthlyReportCategoryRows, categoryRows);

        var varianceRows = categoryGroups
            .Select(group =>
            {
                return new MonthlyReportVarianceCommentRow
                {
                    ProjectCode = group.ProjectCode,
                    TotalBudgetVarianceDisplay = FormatReportCurrency(group.Totals.TotalBudgetVariance),
                    VarianceLastMonthDisplay = FormatReportCurrency(group.Totals.VarianceFromLastMonth),
                    MonthVarianceComment = JoinCurrentResourceComments(group.Lines, line => CombineVarianceComments(
                        line.CommentsOnMonthForecastVariance,
                        line.CommentsOnMonthBudgetVariance)),
                    TotalBudgetVarianceComment = JoinCurrentResourceComments(group.Lines, line => line.CommentsOnTotalBudgetVariance),
                    AllMonthComments = BuildAllMonthComments(group.Lines)
                };
            })
            .ToList();

        if (varianceRows.Count > 0)
        {
            var totalVarianceTotals = CalculateMonthlyReportLineTotals(ForecastLines);
            varianceRows.Add(new MonthlyReportVarianceCommentRow
            {
                ProjectCode = "Total",
                TotalBudgetVarianceDisplay = FormatReportCurrency(totalVarianceTotals.TotalBudgetVariance),
                VarianceLastMonthDisplay = FormatReportCurrency(totalVarianceTotals.VarianceFromLastMonth),
                MonthVarianceComment = string.Empty,
                TotalBudgetVarianceComment = string.Empty,
                AllMonthComments = string.Empty
            });
        }

        ReplaceCollection(MonthlyReportVarianceCommentRows, varianceRows);
        ReplaceCollection(MonthlyReportRiskItems, []);
        ReplaceCollection(MonthlyReportPluggedRateItems, []);
        ActiveRiskStatusText = MonthlyReportRiskItems.Count == 0
            ? "No active risks / financial events captured in this project file yet."
            : string.Empty;
        PluggedRatesStatusText = MonthlyReportPluggedRateItems.Count == 0
            ? "No plugged rates captured in this project file yet."
            : string.Empty;
    }

    private MonthlyReportFiscalSummaryRow BuildFiscalSummaryRow(string label, decimal year1, decimal year2, decimal year3, decimal total)
    {
        return new MonthlyReportFiscalSummaryRow
        {
            Label = label,
            Year1Value = FormatReportCurrency(year1),
            Year2Value = FormatReportCurrency(year2),
            Year3Value = FormatReportCurrency(year3),
            TotalValue = FormatReportCurrency(total)
        };
    }

    private static MonthlyReportLineTotals CalculateMonthlyReportLineTotals(IEnumerable<ForecastLine> lines)
    {
        var totals = new MonthlyReportLineTotals();
        foreach (var line in lines)
        {
            totals.TotalForecast += line.TotalForecastCtc;
            totals.CostToDate += line.CostToDateSummary;
            totals.PlannedCost += line.PlannedCostFcc;
            totals.Budget += line.Budget;
            totals.TotalBudgetVariance += line.TotalBudgetVariance;
            totals.LastMonthPlannedCost += line.LastMonthPlannedCost;
            totals.VarianceFromLastMonth += line.VarianceLastMonthToDate;
        }

        return totals;
    }

    private static FiscalYearReportTotals CalculateFiscalYearReportTotals(IEnumerable<FiscalYearReportLine> lines)
    {
        var totals = new FiscalYearReportTotals();
        foreach (var line in lines)
        {
            totals.SpentToDate += line.SpentToDate;
            totals.CostToComplete += line.CostToComplete;
            totals.PlannedCost += line.PlannedCost;
            totals.Budget += line.Budget;
            totals.Variance += line.Variance;
        }

        return totals;
    }

    private List<string> BuildFiscalYearWindow()
    {
        var currentFiscalYear = FiscalPeriod.FiscalYearFromPeriodLabel(Header.CurrentPeriod);
        if (FiscalPeriod.TryParseFiscalYearNumber(currentFiscalYear, out var currentYear))
        {
            return Enumerable.Range(currentYear, 3)
                .Select(year => $"FY{year}")
                .ToList();
        }

        return FiscalYearReportLines
            .Select(line => line.FiscalYear)
            .Where(fiscalYear => !string.IsNullOrWhiteSpace(fiscalYear))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(fiscalYear => fiscalYear)
            .Take(3)
            .ToList();
    }

    private List<FiscalYearReportLine> GetMonthlyReportTotalFiscalLines()
    {
        var currentFiscalYear = FiscalPeriod.FiscalYearFromPeriodLabel(Header.CurrentPeriod);
        if (!FiscalPeriod.TryParseFiscalYearNumber(currentFiscalYear, out var currentYear))
        {
            return FiscalYearReportLines.ToList();
        }

        return FiscalYearReportLines
            .Where(line => FiscalPeriod.TryParseFiscalYearNumber(line.FiscalYear, out var year) && year >= currentYear)
            .ToList();
    }

    private static string JoinComments(IEnumerable<string> comments)
    {
        var values = comments
            .Where(comment => !string.IsNullOrWhiteSpace(comment))
            .Select(comment => comment.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return values.Count == 0 ? string.Empty : string.Join("; ", values);
    }

    private string JoinCurrentResourceComments(IEnumerable<ForecastLine> lines, Func<ForecastLine, string> selector)
    {
        var periodLabel = CalculationService.Normalise(Header.CurrentPeriod);
        var monthLabel = _dataset.ForecastPeriods
            .FirstOrDefault(period => string.Equals(CalculationService.Normalise(period.Label), periodLabel, StringComparison.OrdinalIgnoreCase))
            ?.StartDate?.ToString("MMM yy") ?? string.Empty;
        var dateLabel = string.IsNullOrWhiteSpace(monthLabel) ? $"FY {periodLabel}" : $"{monthLabel} - FY {periodLabel}";
        return string.Join(Environment.NewLine, lines.Select(line =>
            {
                var comment = selector(line);
                return string.IsNullOrWhiteSpace(comment) ? string.Empty : $"{dateLabel}: {line.ResourceName}: {comment.Trim()}";
            })
            .Where(comment => !string.IsNullOrWhiteSpace(comment))
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string BuildAllMonthComments(IEnumerable<ForecastLine> lines)
    {
        return string.Join(
            Environment.NewLine,
            lines.SelectMany(line => line.UseManualAllMonthComment && line.HasManualAllMonthComment
                    ? [new ForecastMonthlyComment
                    {
                        PeriodLabel = line.ManualCommentPeriodLabel,
                        MonthLabel = line.ManualCommentMonthLabel,
                        ResourceName = line.ResourceName,
                        Text = line.ManualAllMonthComment,
                        RecordedAt = line.ManualCommentRecordedAt ?? DateTime.MinValue
                    }]
                    : line.MonthlyCommentHistory)
                .Where(comment => !string.IsNullOrWhiteSpace(comment.Text))
                .OrderByDescending(comment => comment.PeriodSortKey)
                .ThenByDescending(comment => comment.RecordedAt)
                .Select(comment => comment.DisplayText)
                .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private sealed record MonthlyReportCategoryGroup(string ProjectCode, IReadOnlyList<ForecastLine> Lines, MonthlyReportLineTotals Totals);

    private sealed class MonthlyReportLineTotals
    {
        public decimal TotalForecast { get; set; }
        public decimal CostToDate { get; set; }
        public decimal PlannedCost { get; set; }
        public decimal Budget { get; set; }
        public decimal TotalBudgetVariance { get; set; }
        public decimal LastMonthPlannedCost { get; set; }
        public decimal VarianceFromLastMonth { get; set; }
    }

    private sealed class FiscalYearReportTotals
    {
        public decimal SpentToDate { get; set; }
        public decimal CostToComplete { get; set; }
        public decimal PlannedCost { get; set; }
        public decimal Budget { get; set; }
        public decimal Variance { get; set; }
    }

    private static string FormatReportCurrency(decimal value)
    {
        return decimal.Round(value, 0, MidpointRounding.AwayFromZero) == 0
            ? "-"
            : value.ToString("C0");
    }
}
