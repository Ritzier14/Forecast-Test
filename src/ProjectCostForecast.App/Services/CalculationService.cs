using System.Text.RegularExpressions;
using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App.Services;

public sealed class CalculationService
{
    public void Recalculate(ProjectDataset dataset)
    {
        var transactionTotals = BuildForecastTransactionTotals(dataset.Transactions);
        foreach (var line in dataset.ForecastLines)
        {
            RecalculateForecastLine(line, transactionTotals, dataset.Header.CurrentPeriod);
        }

        dataset.CategorySummaries = RecalculateCategorySummaries(dataset.ForecastLines);
    }

    public void RecalculateForecastLine(ForecastLine line, IEnumerable<CostTransaction> transactions, string currentPeriod)
    {
        var transactionTotals = BuildForecastTransactionTotals(transactions);
        RecalculateForecastLine(line, transactionTotals, currentPeriod);
    }

    private static void RecalculateForecastLine(ForecastLine line, IReadOnlyDictionary<ForecastMatchKey, ForecastTransactionTotals> transactionTotals, string currentPeriod)
    {
        var key = CreateForecastMatchKey(line.TaskNumber, line.ResourceName);
        transactionTotals.TryGetValue(key, out var matchingTransactions);

        line.CostToDate = matchingTransactions?.Amount ?? 0m;
        line.CostToDateSummary = line.CostToDate;
        line.CurrentMonthCost = matchingTransactions?.AmountByPeriod.GetValueOrDefault(Normalise(currentPeriod)) ?? 0m;
        line.TotalForecastCtc = line.MonthlyForecasts.Sum(m => m.Amount);
        line.MonthForecast = line.MonthlyForecasts
            .Where(m => string.Equals(m.PeriodLabel, currentPeriod, StringComparison.OrdinalIgnoreCase))
            .Sum(m => m.Amount);
        line.PlannedCostFcc = line.TotalForecastCtc + line.CostToDate;
        line.VarianceLastMonthToDate = line.LastMonthPlannedCost - line.PlannedCostFcc;
        line.MonthForecastVariance = line.LastMonthForecast - line.CurrentMonthCost;
        line.TotalBudgetVariance = line.Budget - line.PlannedCostFcc;
        line.NotifyMonthForecastValuesChanged();
    }

    private static Dictionary<ForecastMatchKey, ForecastTransactionTotals> BuildForecastTransactionTotals(IEnumerable<CostTransaction> transactions)
    {
        var totalsByKey = new Dictionary<ForecastMatchKey, ForecastTransactionTotals>();

        foreach (var transaction in transactions)
        {
            if (string.IsNullOrWhiteSpace(transaction.TaskNumber))
            {
                continue;
            }

            foreach (var name in GetDistinctForecastMatchNames(transaction))
            {
                var key = CreateForecastMatchKey(transaction.TaskNumber, name);
                if (!totalsByKey.TryGetValue(key, out var totals))
                {
                    totals = new ForecastTransactionTotals();
                    totalsByKey[key] = totals;
                }

                totals.Amount += transaction.Amount;
                var period = Normalise(transaction.FyPeriod);
                if (!string.IsNullOrWhiteSpace(period))
                {
                    totals.AmountByPeriod[period] = totals.AmountByPeriod.GetValueOrDefault(period) + transaction.Amount;
                }
            }
        }

        return totalsByKey;
    }

    private static IEnumerable<string> GetDistinctForecastMatchNames(CostTransaction transaction)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddMatchName(names, transaction.ManualName);
        AddMatchName(names, transaction.Who);
        AddMatchName(names, transaction.ResourceDescription);
        AddMatchName(names, transaction.LedgerResourceName);
        return names;
    }

    private static void AddMatchName(ISet<string> names, string? value)
    {
        var name = Normalise(value);
        if (!string.IsNullOrWhiteSpace(name))
        {
            names.Add(name);
        }
    }

    private static ForecastMatchKey CreateForecastMatchKey(string? taskNumber, string? resourceName)
    {
        return new ForecastMatchKey(
            Normalise(taskNumber).ToUpperInvariant(),
            Normalise(resourceName).ToUpperInvariant());
    }

    public List<CategorySummary> RecalculateCategorySummaries(IEnumerable<ForecastLine> forecastLines)
    {
        return forecastLines
            .GroupBy(line =>
            {
                var reportingCategory = Normalise(line.ReportingCategory);
                return string.IsNullOrWhiteSpace(reportingCategory)
                    ? Normalise(line.ProjectCode)
                    : reportingCategory;
            })
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .Select(group => new CategorySummary
            {
                ProjectCode = group.Key,
                TotalForecast = group.Sum(line => line.TotalForecastCtc),
                CostToDate = group.Sum(line => line.CostToDateSummary),
                CurrentMonthCost = group.Sum(line => line.CurrentMonthCost),
                PlannedCost = group.Sum(line => line.PlannedCostFcc),
                Budget = group.Sum(line => line.Budget),
                TotalBudgetVariance = group.Sum(line => line.TotalBudgetVariance),
                MonthForecastVariance = group.Sum(line => line.MonthForecastVariance)
            })
            .OrderBy(summary => summary.ProjectCode)
            .ToList();
    }

    public List<FiscalYearReportLine> BuildFiscalYearReport(ProjectDataset dataset)
    {
        var budgetByFiscalYear = dataset.FiscalYearBudgets
            .Where(budget => !string.IsNullOrWhiteSpace(budget.FiscalYear))
            .GroupBy(budget => NormaliseFiscalYear(budget.FiscalYear))
            .ToDictionary(group => group.Key, group => group.Sum(x => x.Budget), StringComparer.OrdinalIgnoreCase);
        var spentByFiscalYear = dataset.Transactions
            .Select(transaction => new
            {
                FiscalYear = FiscalPeriod.FiscalYearFromPeriodLabel(transaction.FyPeriod),
                transaction.Amount
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.FiscalYear))
            .GroupBy(item => item.FiscalYear, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Amount), StringComparer.OrdinalIgnoreCase);
        var costToCompleteByFiscalYear = dataset.ForecastLines
            .SelectMany(line => line.MonthlyForecasts)
            .Select(forecast => new
            {
                FiscalYear = FiscalPeriod.FiscalYearFromPeriodLabel(forecast.PeriodLabel),
                forecast.Amount
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.FiscalYear))
            .GroupBy(item => item.FiscalYear, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Amount), StringComparer.OrdinalIgnoreCase);

        var fiscalYears = dataset.ForecastPeriods
            .Select(period => FiscalPeriod.FiscalYearFromPeriodLabel(period.Label))
            .Concat(spentByFiscalYear.Keys)
            .Concat(costToCompleteByFiscalYear.Keys)
            .Concat(budgetByFiscalYear.Keys)
            .Where(fiscalYear => !string.IsNullOrWhiteSpace(fiscalYear))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(fiscalYear => fiscalYear)
            .ToList();

        return fiscalYears
            .Select(fiscalYear =>
            {
                var spentToDate = spentByFiscalYear.GetValueOrDefault(fiscalYear);
                var costToComplete = costToCompleteByFiscalYear.GetValueOrDefault(fiscalYear);
                spentToDate = RoundCurrency(spentToDate);
                costToComplete = RoundCurrency(costToComplete);
                var plannedCost = RoundCurrency(spentToDate + costToComplete);
                var budget = RoundCurrency(budgetByFiscalYear.GetValueOrDefault(fiscalYear));

                return new FiscalYearReportLine
                {
                    FiscalYear = fiscalYear,
                    SpentToDate = spentToDate,
                    CostToComplete = costToComplete,
                    PlannedCost = plannedCost,
                    Budget = budget,
                    Variance = RoundCurrency(budget - plannedCost)
                };
            })
            .Where(line => line.SpentToDate != 0 || line.CostToComplete != 0 || line.Budget != 0)
            .ToList();
    }

    public List<ActualsPeriodSummary> BuildActualsPeriodSummaries(IEnumerable<CostTransaction> transactions)
    {
        return transactions
            .GroupBy(transaction => new
            {
                Task = Normalise(transaction.TaskNumber),
                Resource = Normalise(transaction.LedgerResourceName),
                Period = Normalise(transaction.FyPeriod)
            })
            .Where(group => !string.IsNullOrWhiteSpace(group.Key.Task) || !string.IsNullOrWhiteSpace(group.Key.Resource))
            .Select(group => new ActualsPeriodSummary
            {
                TaskNumber = group.Key.Task,
                ResourceName = group.Key.Resource,
                FyPeriod = group.Key.Period,
                TransactionCount = group.Count(),
                Units = group.Sum(x => x.Units),
                Amount = group.Sum(x => x.Amount)
            })
            .OrderBy(summary => summary.TaskNumber)
            .ThenBy(summary => summary.ResourceName)
            .ThenBy(summary => summary.FyPeriod)
            .ToList();
    }

    public static bool MatchesForecastLine(CostTransaction transaction, ForecastLine line)
    {
        if (!string.Equals(Normalise(transaction.TaskNumber), Normalise(line.TaskNumber), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return MatchesForecastResource(transaction, line);
    }

    public static bool MatchesForecastResource(CostTransaction transaction, ForecastLine line)
    {
        var transactionNames = new[]
        {
            transaction.ManualName,
            transaction.Who,
            transaction.ResourceDescription,
            transaction.LedgerResourceName
        };

        return transactionNames.Any(name =>
            string.Equals(Normalise(name), Normalise(line.ResourceName), StringComparison.OrdinalIgnoreCase));
    }

    private static readonly Regex WhitespaceRunRegex = new(@"\s+", RegexOptions.Compiled);

    public static string Normalise(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return NeedsWhitespaceCollapse(trimmed)
            ? WhitespaceRunRegex.Replace(trimmed, " ")
            : trimmed;
    }

    private static bool NeedsWhitespaceCollapse(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            if (!char.IsWhiteSpace(current))
            {
                continue;
            }

            if (current != ' ' || (i + 1 < value.Length && char.IsWhiteSpace(value[i + 1])))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormaliseFiscalYear(string fiscalYear)
    {
        var normalised = Normalise(fiscalYear).ToUpperInvariant();
        return normalised.StartsWith("FY", StringComparison.OrdinalIgnoreCase) ? normalised : $"FY{normalised}";
    }

    private static decimal RoundCurrency(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private readonly record struct ForecastMatchKey(string TaskNumber, string ResourceName);

    private sealed class ForecastTransactionTotals
    {
        public decimal Amount { get; set; }
        public Dictionary<string, decimal> AmountByPeriod { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
