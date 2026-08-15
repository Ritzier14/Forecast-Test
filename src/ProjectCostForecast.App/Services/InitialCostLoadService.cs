using System.IO;
using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App.Services;

/// <summary>
/// Builds the first project state from a raw accounting export.  It deliberately
/// creates a resource/task line for every imported actual so all initial costs
/// remain visible and explainable in the CTC and resource drill-down views.
/// </summary>
public sealed class InitialCostLoadService
{
    private readonly CsvTransactionService _transactionService = new();
    private readonly CalculationService _calculationService = new();
    private readonly DateOnly _asOfDate;

    public InitialCostLoadService(DateOnly? asOfDate = null)
    {
        _asOfDate = asOfDate ?? DateOnly.FromDateTime(DateTime.Today);
    }

    public ProjectDataset Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var transactions = _transactionService.Import(path, 1);
        return BuildDataset(transactions, Path.GetFileName(path));
    }

    public ProjectDataset BuildDataset(IEnumerable<CostTransaction> sourceTransactions, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(sourceTransactions);

        var transactions = sourceTransactions
            .Where(IsValidTransaction)
            .Select(CloneAndResolveResource)
            .OrderBy(transaction => FiscalPeriod.SortKey(transaction.FyPeriod))
            .ThenBy(transaction => transaction.DocDate)
            .ThenBy(transaction => transaction.RowNumber)
            .ToList();
        for (var index = 0; index < transactions.Count; index++)
        {
            transactions[index].RowNumber = index + 1;
        }

        if (transactions.Count == 0)
        {
            throw new InvalidOperationException("The initial cost-load workbook did not contain any valid transaction rows.");
        }

        var periods = transactions
            .GroupBy(transaction => transaction.FyPeriod, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => FiscalPeriod.SortKey(group.Key))
            .Select(group => new ForecastPeriod
            {
                Label = group.Key,
                StartDate = FiscalPeriod.TryGetCalendarMonthStart(group.Key, out var calendarMonthStart)
                    ? calendarMonthStart
                    : group
                        .Where(transaction => transaction.DocDate.HasValue)
                        .Select(transaction => transaction.DocDate!.Value)
                        .OrderBy(date => date)
                        .Select(date => new DateOnly(date.Year, date.Month, 1))
                        .FirstOrDefault()
            })
            .ToList();
        var workingMonthStart = new DateOnly(_asOfDate.Year, _asOfDate.Month, 1).AddMonths(-1);
        EnsureForecastPeriod(periods, workingMonthStart);
        EnsureForecastPeriod(periods, workingMonthStart.AddMonths(1));
        EnsureContinuousForecastPeriods(periods);
        periods = periods.OrderBy(period => period.StartDate ?? DateOnly.MaxValue).ToList();
        var currentPeriod = FiscalPeriod.LabelFromCalendarMonth(workingMonthStart);
        var projectCodes = transactions
            .Select(transaction => transaction.ProjectCode)
            .Where(projectCode => !string.IsNullOrWhiteSpace(projectCode))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(projectCode => projectCode)
            .ToList();

        var dataset = new ProjectDataset
        {
            Header = new ProjectHeader
            {
                ProjectTitle = projectCodes.Count == 0
                    ? "Initial cost load"
                    : $"{string.Join(", ", projectCodes)} - Initial cost load",
                ReportTitle = "Project Cost Forecast",
                CurrentPeriod = currentPeriod,
                SourceWorkbook = sourceName,
                ImportNotes = "Initial actual-cost load packaged with the application. One cumulative saved month has been created for each period in the source data."
            },
            ForecastPeriods = periods,
            Transactions = transactions,
            ForecastLines = BuildForecastLines(transactions, periods),
            AuditEvents =
            [
                new AuditEvent
                {
                    EntityType = "TransactionImport",
                    EntityId = sourceName,
                    FieldName = "InitialCostLoad",
                    OldValue = "0",
                    NewValue = transactions.Count.ToString(),
                    Reason = "Loaded packaged example accounting export"
                }
            ]
        };

        _calculationService.Recalculate(dataset);
        dataset.SavedMonthSnapshots = BuildMonthlySnapshots(dataset);
        return dataset;
    }

    private static bool IsValidTransaction(CostTransaction transaction)
    {
        return !string.IsNullOrWhiteSpace(transaction.TaskNumber)
            && FiscalPeriod.TryParseLabel(transaction.FyPeriod, out _, out _);
    }

    private static void EnsureForecastPeriod(ICollection<ForecastPeriod> periods, DateOnly monthStart)
    {
        monthStart = new DateOnly(monthStart.Year, monthStart.Month, 1);
        var label = FiscalPeriod.LabelFromCalendarMonth(monthStart);
        if (periods.Any(period => string.Equals(period.Label, label, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        periods.Add(new ForecastPeriod
        {
            Label = label,
            StartDate = monthStart
        });
    }

    private static void EnsureContinuousForecastPeriods(ICollection<ForecastPeriod> periods)
    {
        var datedPeriods = periods
            .Where(period => period.StartDate.HasValue)
            .Select(period => new DateOnly(period.StartDate!.Value.Year, period.StartDate.Value.Month, 1))
            .OrderBy(date => date)
            .ToList();
        if (datedPeriods.Count == 0)
        {
            return;
        }

        var start = datedPeriods.First();
        var end = datedPeriods.Last();
        for (var monthStart = start; monthStart.DayNumber <= end.DayNumber; monthStart = monthStart.AddMonths(1))
        {
            var label = FiscalPeriod.LabelFromCalendarMonth(monthStart);
            if (periods.Any(period => string.Equals(period.Label, label, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            periods.Add(new ForecastPeriod
            {
                Label = label,
                StartDate = monthStart
            });
        }
    }

    private static CostTransaction CloneAndResolveResource(CostTransaction transaction)
    {
        var resolvedResourceName = FirstNonBlank(transaction.ManualName, transaction.Who, transaction.ResourceDescription);
        return new CostTransaction
        {
            RowNumber = transaction.RowNumber,
            FyPeriod = transaction.FyPeriod.Trim(),
            TaskNumber = transaction.TaskNumber.Trim(),
            Period = transaction.Period,
            DocDate = transaction.DocDate,
            Units = transaction.Units,
            UnitRate = transaction.UnitRate,
            Amount = transaction.Amount,
            CostLedger = transaction.CostLedger,
            CostAccount = transaction.CostAccount,
            ProjectCode = transaction.ProjectCode,
            ParentProjectCode = transaction.ParentProjectCode,
            ResourceCode = transaction.ResourceCode,
            ResourceDescription = transaction.ResourceDescription,
            Source = transaction.Source,
            PoNumber = transaction.PoNumber,
            PoComments = transaction.PoComments,
            SupplierName = transaction.SupplierName,
            Narrative1 = transaction.Narrative1,
            Narrative2 = transaction.Narrative2,
            Narrative3 = transaction.Narrative3,
            Who = transaction.Who,
            EcmNumber = transaction.EcmNumber,
            ManualName = resolvedResourceName
        };
    }

    private static List<ForecastLine> BuildForecastLines(
        IEnumerable<CostTransaction> transactions,
        IReadOnlyCollection<ForecastPeriod> periods)
    {
        return transactions
            .GroupBy(
                transaction => string.Join(
                    "\u001F",
                    CalculationService.Normalise(transaction.TaskNumber),
                    CalculationService.Normalise(transaction.LedgerResourceName),
                    CalculationService.Normalise(transaction.ProjectCode)),
                StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select((group, index) =>
            {
                var representative = group.First();
                var transactionProjectCode = CalculationService.Normalise(representative.ProjectCode);
                return new ForecastLine
                {
                    RowNumber = index + 1,
                    TaskNumber = CalculationService.Normalise(representative.TaskNumber),
                    ResourceName = CalculationService.Normalise(representative.LedgerResourceName),
                    ProjectCode = string.IsNullOrWhiteSpace(transactionProjectCode) ? "Unassigned" : transactionProjectCode,
                    TransactionProjectCode = transactionProjectCode,
                    UseLedgerResourceMatchOnly = true,
                    MonthlyForecasts = periods.Select(period => new MonthlyForecast
                    {
                        PeriodLabel = period.Label,
                        PeriodStartDate = period.StartDate,
                        Amount = 0m
                    }).ToList()
                };
            })
            .ToList();
    }

    private List<SavedMonthSnapshot> BuildMonthlySnapshots(ProjectDataset dataset)
    {
        var allTransactions = dataset.Transactions;
        var currentPeriod = dataset.Header.CurrentPeriod;
        var snapshots = new List<SavedMonthSnapshot>();

        foreach (var month in allTransactions
                     .GroupBy(transaction => transaction.FyPeriod, StringComparer.OrdinalIgnoreCase)
                     .Where(group => FiscalPeriod.SortKey(group.Key) != int.MaxValue)
                     .OrderBy(group => FiscalPeriod.SortKey(group.Key)))
        {
            var period = month.Key;
            var cutoff = FiscalPeriod.SortKey(period);
            dataset.Transactions = allTransactions
                .Where(transaction => FiscalPeriod.SortKey(transaction.FyPeriod) <= cutoff)
                .ToList();
            dataset.Header.CurrentPeriod = period;
            _calculationService.Recalculate(dataset);

            var savedAt = month
                .Where(transaction => transaction.DocDate.HasValue)
                .Select(transaction => transaction.DocDate!.Value.ToDateTime(TimeOnly.MaxValue))
                .DefaultIfEmpty(DateTime.Now)
                .Max();
            snapshots.Add(CreateSnapshot(dataset, period, savedAt));
        }

        dataset.Transactions = allTransactions;
        dataset.Header.CurrentPeriod = currentPeriod;
        _calculationService.Recalculate(dataset);
        return snapshots.OrderByDescending(snapshot => FiscalPeriod.SortKey(snapshot.Period)).ToList();
    }

    private static SavedMonthSnapshot CreateSnapshot(ProjectDataset dataset, string period, DateTime savedAt)
    {
        var lines = dataset.ForecastLines.Select(line => new SavedMonthForecastLine
        {
            RowNumber = line.RowNumber,
            TaskNumber = line.TaskNumber,
            ResourceName = line.ResourceName,
            ProjectCode = line.ProjectCode,
            CostToDate = line.CostToDateSummary,
            CurrentPeriodForecast = line.MonthForecast,
            CostToComplete = line.TotalForecastCtc,
            FinalForecast = line.PlannedCostFcc,
            Budget = line.Budget,
            TotalBudgetVariance = line.TotalBudgetVariance,
            VarianceFromPreviousMonth = line.VarianceLastMonthToDate,
            MonthlyForecasts = line.MonthlyForecasts.Select(forecast => new SavedMonthPeriodAmount
            {
                PeriodLabel = forecast.PeriodLabel,
                PeriodStartDate = forecast.PeriodStartDate,
                Amount = forecast.Amount
            }).ToList()
        }).ToList();

        return new SavedMonthSnapshot
        {
            Period = period,
            SavedAt = savedAt,
            CostToDate = lines.Sum(line => line.CostToDate),
            CostToComplete = lines.Sum(line => line.CostToComplete),
            FinalForecast = lines.Sum(line => line.FinalForecast),
            TotalBudgetVariance = lines.Sum(line => line.TotalBudgetVariance),
            ForecastLines = lines
        };
    }

    private static string FirstNonBlank(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "Unassigned resource";
    }
}
