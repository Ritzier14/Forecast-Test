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
    private void RebuildMonthlyPivotTables()
    {
        var periods = _dataset.ForecastPeriods
            .Select(period => period.Label)
            .Concat(Transactions.Select(transaction => transaction.FyPeriod))
            .Concat(ForecastLines.SelectMany(line => line.MonthlyForecasts.Select(forecast => forecast.PeriodLabel)))
            .Where(period => !string.IsNullOrWhiteSpace(period))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(FiscalPeriod.SortKey)
            .ToList();

        ReplaceCollection(MonthlyPivotPeriods, periods);

        var actualRows = Transactions
            .GroupBy(transaction => new
            {
                Task = CalculationService.Normalise(transaction.TaskNumber),
                Resource = CalculationService.Normalise(transaction.LedgerResourceName)
            })
            .Where(group => !string.IsNullOrWhiteSpace(group.Key.Task) || !string.IsNullOrWhiteSpace(group.Key.Resource))
            .Select(group => new MonthlyPivotRow(group
                .Where(transaction => !string.IsNullOrWhiteSpace(transaction.FyPeriod))
                .GroupBy(transaction => transaction.FyPeriod)
                .ToDictionary(periodGroup => periodGroup.Key, periodGroup => periodGroup.Sum(transaction => transaction.Amount), StringComparer.OrdinalIgnoreCase))
            {
                TaskNumber = group.Key.Task,
                ResourceName = group.Key.Resource,
                ProjectCode = FindProjectCodeForTask(group.Key.Task)
            })
            .OrderBy(row => row.TaskNumber)
            .ThenBy(row => row.ResourceName)
            .ToList();

        var forecastRows = ForecastLines
            .Select(line => new MonthlyPivotRow(line.MonthlyForecasts
                .Where(forecast => !string.IsNullOrWhiteSpace(forecast.PeriodLabel))
                .GroupBy(forecast => forecast.PeriodLabel)
                .ToDictionary(periodGroup => periodGroup.Key, periodGroup => periodGroup.Sum(forecast => forecast.Amount), StringComparer.OrdinalIgnoreCase))
            {
                TaskNumber = line.TaskNumber,
                ResourceName = line.ResourceName,
                ProjectCode = line.ProjectCode
            })
            .Where(row => row.Total != 0)
            .OrderBy(row => row.TaskNumber)
            .ThenBy(row => row.ResourceName)
            .ToList();

        ReplaceCollection(ActualsMonthlyPivotRows, actualRows);
        ReplaceCollection(ForecastMonthlyPivotRows, forecastRows);
        ReplaceCollection(CategoryMonthlyPivotRows, ForecastLines
            .Where(line => !string.IsNullOrWhiteSpace(line.ProjectCode))
            .GroupBy(line => line.ProjectCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => new MonthlyPivotRow(group
                .SelectMany(line => line.MonthlyForecasts)
                .Where(forecast => !string.IsNullOrWhiteSpace(forecast.PeriodLabel))
                .GroupBy(forecast => forecast.PeriodLabel)
                .ToDictionary(periodGroup => periodGroup.Key, periodGroup => periodGroup.Sum(forecast => forecast.Amount), StringComparer.OrdinalIgnoreCase))
            {
                ProjectCode = group.Key
            })
            .OrderBy(row => row.ProjectCode)
            .ToList());
        RebuildRawTransactionsPivotTable();
    }

    private void RebuildLedgerTransactionViews()
    {
        var ledgerTransactions = LedgerTransactions
            .OrderBy(transaction => FiscalPeriod.SortKey(transaction.FyPeriod))
            .ThenBy(transaction => transaction.DocDate)
            .ThenBy(transaction => transaction.TaskNumber)
            .ThenBy(transaction => transaction.ResourceCode)
            .ToList();

        ReplaceCollection(LedgerTransactionRows, ledgerTransactions);

        var periods = BuildActiveTransactionPeriods(ledgerTransactions);
        ReplaceCollection(LedgerMonthlyPivotPeriods, periods);

        var pivotRows = ledgerTransactions
            .GroupBy(transaction => new
            {
                Task = CalculationService.Normalise(transaction.TaskNumber),
                Resource = CalculationService.Normalise(transaction.LedgerResourceName),
                Group = CalculationService.Normalise(transaction.ProjectCode)
            })
            .Where(group => !string.IsNullOrWhiteSpace(group.Key.Task) || !string.IsNullOrWhiteSpace(group.Key.Resource))
            .Select(group => new MonthlyPivotRow(group
                .Where(transaction => !string.IsNullOrWhiteSpace(transaction.FyPeriod))
                .GroupBy(transaction => transaction.FyPeriod)
                .ToDictionary(periodGroup => periodGroup.Key, periodGroup => periodGroup.Sum(transaction => transaction.Amount), StringComparer.OrdinalIgnoreCase))
            {
                TaskNumber = group.First().TaskNumber,
                ResourceName = group.First().LedgerResourceName,
                ProjectCode = group.First().ProjectCode
            })
            .OrderBy(row => row.TaskNumber)
            .ThenBy(row => row.ResourceName)
            .ToList();

        ReplaceCollection(LedgerMonthlyPivotRows, pivotRows);
        ApplyLedgerTransactionGrouping();
    }

    private void RebuildRawTransactionsPivotTable()
    {
        var visibleTransactions = RawTransactionsView.Cast<object>()
            .OfType<CostTransaction>()
            .OrderBy(transaction => FiscalPeriod.SortKey(transaction.FyPeriod))
            .ThenBy(transaction => transaction.DocDate)
            .ThenBy(transaction => transaction.TaskNumber)
            .ThenBy(transaction => transaction.ResourceCode)
            .ToList();

        ReplaceCollection(RawTransactionsMonthlyPivotPeriods, BuildActiveTransactionPeriods(visibleTransactions));

        var pivotRows = visibleTransactions
            .GroupBy(transaction => new
            {
                Task = CalculationService.Normalise(transaction.TaskNumber),
                Resource = CalculationService.Normalise(transaction.LedgerResourceName),
                Group = CalculationService.Normalise(transaction.ProjectCode)
            })
            .Where(group => !string.IsNullOrWhiteSpace(group.Key.Task) || !string.IsNullOrWhiteSpace(group.Key.Resource))
            .Select(group => new MonthlyPivotRow(group
                .Where(transaction => !string.IsNullOrWhiteSpace(transaction.FyPeriod))
                .GroupBy(transaction => transaction.FyPeriod)
                .ToDictionary(periodGroup => periodGroup.Key, periodGroup => periodGroup.Sum(transaction => transaction.Amount), StringComparer.OrdinalIgnoreCase))
            {
                TaskNumber = group.First().TaskNumber,
                ResourceName = group.First().LedgerResourceName,
                ProjectCode = group.First().ProjectCode
            })
            .OrderBy(row => row.TaskNumber)
            .ThenBy(row => row.ResourceName)
            .ToList();

        ReplaceCollection(RawTransactionsMonthlyPivotRows, pivotRows);
        ApplyRawTransactionGrouping();
    }

    private void ApplyRawTransactionGrouping()
    {
        using (RawTransactionsView.DeferRefresh())
        {
            RawTransactionsView.GroupDescriptions.Clear();

            if (ShowRawTransactionsGroupedByMonth)
            {
                RawTransactionsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(CostTransaction.FyPeriod)));
            }
        }
    }

    private void ApplyLedgerTransactionGrouping()
    {
        using (LedgerTransactionsView.DeferRefresh())
        {
            LedgerTransactionsView.GroupDescriptions.Clear();

            if (string.Equals(ActiveDetailWorkspaceKey, "Ledger Costs", StringComparison.OrdinalIgnoreCase)
                && string.Equals(SelectedDetailWorkspaceView?.ContentKey, "GroupByMonth", StringComparison.OrdinalIgnoreCase))
            {
                LedgerTransactionsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(CostTransaction.FyPeriod)));
            }
        }
    }

    private List<string> BuildActiveTransactionPeriods(IEnumerable<CostTransaction> transactions)
    {
        var periodsWithCost = transactions
            .Select(transaction => FiscalPeriod.NormaliseLabel(transaction.FyPeriod))
            .Where(period => !string.IsNullOrWhiteSpace(period))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(FiscalPeriod.SortKey)
            .ToList();

        if (periodsWithCost.Count == 0)
        {
            return [];
        }

        var firstPeriod = periodsWithCost[0];
        var endPeriod = FiscalPeriod.NormaliseLabel(Header.CurrentPeriod);
        if (string.IsNullOrWhiteSpace(endPeriod))
        {
            endPeriod = periodsWithCost[^1];
        }

        if (FiscalPeriod.TryParseLabel(firstPeriod, out var startYear, out var startMonth)
            && FiscalPeriod.TryParseLabel(endPeriod, out var endYear, out var endMonth))
        {
            var startIndex = (startYear * 12) + startMonth;
            var endIndex = (endYear * 12) + endMonth;
            if (endIndex < startIndex)
            {
                return periodsWithCost;
            }

            return FiscalPeriod.BuildContinuousRange(startYear, startMonth, endYear, endMonth);
        }

        return periodsWithCost;
    }

}
