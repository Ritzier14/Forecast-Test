using System.Diagnostics;
using System.Globalization;
using ProjectCostForecast.App;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna11CalculationCoverageTests
{
    [Fact]
    public void Initial_cost_load_builds_the_deterministic_startup_dataset_from_the_packaged_workbook()
    {
        var initialCostLoadPath = Path.Combine(
            Luna11TestSupport.RepositoryRoot,
            "src",
            "ProjectCostForecast.App",
            "Data",
            "data_anonymised.xlsx");
        var dataset = new InitialCostLoadService(new DateOnly(2026, 7, 18)).Load(initialCostLoadPath);

        Assert.True(dataset.Transactions.Count > 1000);
        Assert.All(dataset.Transactions, transaction => Assert.False(string.IsNullOrWhiteSpace(transaction.ManualName)));
        Assert.True(dataset.ForecastLines.Count > 0);
        var sourcePeriods = dataset.Transactions
            .Select(transaction => transaction.FyPeriod)
            .Where(period => FiscalPeriod.SortKey(period) != int.MaxValue)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        Assert.Equal(sourcePeriods.Count, dataset.SavedMonthSnapshots.Count);
        Assert.All(dataset.SavedMonthSnapshots, snapshot => Assert.True(FiscalPeriod.TryParseLabel(snapshot.Period, out _, out _)));
        Assert.Equal(dataset.Transactions.Sum(transaction => transaction.Amount), dataset.SavedMonthSnapshots.First().CostToDate);
        Assert.Equal("26-12", dataset.Header.CurrentPeriod);
        Assert.Contains(dataset.ForecastPeriods, period => period.Label == "26-12");
        Assert.Contains(dataset.ForecastPeriods, period => period.Label == "27-01");
        Assert.All(dataset.SavedMonthSnapshots, snapshot => Assert.NotEqual(dataset.Header.CurrentPeriod, snapshot.Period));

        Assert.Equal(new DateOnly(2026, 1, 1), Luna11TestSupport.GetCalendarMonthStart("26-07"));
        Assert.Equal(new DateOnly(2026, 7, 1), Luna11TestSupport.GetCalendarMonthStart("27-01"));

        var outOfOrderPeriodsDataset = new InitialCostLoadService(new DateOnly(2026, 7, 18)).BuildDataset(
        [
            new CostTransaction
            {
                RowNumber = 1,
                FyPeriod = "24-08",
                TaskNumber = "DATE-001",
                DocDate = new DateOnly(2023, 11, 21),
                Amount = 10m,
                ManualName = "Date fixture"
            },
            new CostTransaction
            {
                RowNumber = 2,
                FyPeriod = "24-07",
                TaskNumber = "DATE-001",
                DocDate = new DateOnly(2024, 1, 7),
                Amount = 20m,
                ManualName = "Date fixture"
            }
        ], "date-fixture.xlsx");

        Assert.True(
            outOfOrderPeriodsDataset.ForecastPeriods.Take(2).Select(period => period.Label).SequenceEqual(["24-07", "24-08"]));
        Assert.Equal(new DateOnly(2024, 1, 1), outOfOrderPeriodsDataset.ForecastPeriods.Single(period => period.Label == "24-07").StartDate);
        Assert.Equal(new DateOnly(2024, 2, 1), outOfOrderPeriodsDataset.ForecastPeriods.Single(period => period.Label == "24-08").StartDate);

        var sparseCalendarYearDataset = new InitialCostLoadService(new DateOnly(2023, 7, 18)).BuildDataset(
        [
            new CostTransaction
            {
                RowNumber = 1,
                FyPeriod = "22-07",
                TaskNumber = "CAL-001",
                DocDate = new DateOnly(2022, 1, 15),
                Amount = 10m,
                ManualName = "Calendar fixture"
            },
            new CostTransaction
            {
                RowNumber = 2,
                FyPeriod = "23-06",
                TaskNumber = "CAL-001",
                DocDate = new DateOnly(2022, 12, 15),
                Amount = 20m,
                ManualName = "Calendar fixture"
            }
        ], "calendar-fixture.xlsx");
        var calendar2022Labels = sparseCalendarYearDataset.ForecastPeriods
            .Where(period => period.StartDate?.Year == 2022)
            .Select(period => period.Label)
            .ToList();

        Assert.True(calendar2022Labels.SequenceEqual(FiscalPeriod.BuildContinuousRange(22, 7, 23, 6)));
        Assert.Equal(12, calendar2022Labels.Count);
        Assert.Contains(
            sparseCalendarYearDataset.ForecastLines.Single().MonthlyForecasts,
            forecast => forecast.PeriodLabel == "22-08" && forecast.Amount == 0m);
    }

    [Fact]
    public void Clipboard_accounting_and_kpi_formatters_preserve_the_workbook_display_contract()
    {
        var clipboardRows = SpreadsheetClipboardService.Parse("A\tB\r\nC\tD");
        Assert.Equal(2, clipboardRows.Count);
        Assert.Equal("D", clipboardRows[1][1]);
        Assert.Equal("A\tB" + Environment.NewLine + "C\tD", SpreadsheetClipboardService.Serialize(clipboardRows));

        var appliedCells = new Dictionary<(int Row, int Column), string>();
        var appliedCount = SpreadsheetClipboardService.Apply(
            clipboardRows,
            2,
            3,
            (row, column) => !(row == 2 && column == 4),
            (row, column, value) => appliedCells[(row, column)] = value);
        Assert.Equal(3, appliedCount);
        Assert.DoesNotContain((2, 4), appliedCells.Keys);
        Assert.Equal("D", appliedCells[(3, 4)]);

        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.True(
                SpreadsheetClipboardService.TryConvert("1,5", typeof(decimal), out var commaDecimalValue)
                && commaDecimalValue is 1.5m);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }

        Assert.Equal("1,260,000", AccountingNoDecimalsConverter.FormatAccounting(1260000m, CultureInfo.CurrentCulture));
        Assert.Equal("(1,260,000)", AccountingNoDecimalsConverter.FormatAccounting(-1260000m, CultureInfo.CurrentCulture));
        Assert.Equal(
            "1.26m",
            AccountingNoDecimalsConverter.FormatAccounting(
                1260000m,
                CultureInfo.CurrentCulture,
                compactMillions: true,
                compactMillionDecimals: 2));
        Assert.True(
            AccountingNoDecimalsConverter.TryParseForecastMonthInput("1.26", CultureInfo.CurrentCulture, out var compactMillionAmount)
            && compactMillionAmount == 1260000m);
        Assert.True(
            AccountingNoDecimalsConverter.TryParseForecastMonthInput("1.25m", CultureInfo.CurrentCulture, out var suffixedMillionAmount)
            && suffixedMillionAmount == 1250000m);

        Assert.StartsWith("↑ 8.7%", KpiComparisonFormatter.Format(108.7m, 100m));
        Assert.StartsWith("↓ 10%", KpiComparisonFormatter.Format(90m, 100m));
        Assert.StartsWith("→ 0%", KpiComparisonFormatter.Format(100m, 100m));
        Assert.Equal(string.Empty, KpiComparisonFormatter.Format(100m, 0m));
        Assert.Equal("Up", KpiComparisonFormatter.GetDirection(101m, 100m));
    }

    [Fact]
    public void Seed_calculation_resource_drilldown_reports_and_project_attribution_remain_consistent()
    {
        var dataset = Luna11TestSupport.LoadSeedDataset();
        var calculationService = new CalculationService();
        calculationService.Recalculate(dataset);

        Assert.Equal(63, dataset.Transactions.Count);
        Assert.Equal(27695m, dataset.Transactions.Sum(transaction => transaction.Amount));

        var stanleyLine = Luna11TestSupport.FindForecastLine(dataset, "WA57102001", "Stanley Drake");
        var stanleyTransactions = dataset.Transactions
            .Where(transaction => CalculationService.MatchesForecastLine(transaction, stanleyLine))
            .ToList();
        Assert.Equal(39, stanleyTransactions.Count);
        Assert.Equal(15000m, stanleyTransactions.Sum(transaction => transaction.Amount));
        Assert.Equal(15000m, stanleyLine.CostToDate);

        var flexLine = Luna11TestSupport.FindForecastLine(dataset, "WA57102001", "Flex Projects L");
        var flexTransactions = dataset.Transactions
            .Where(transaction => CalculationService.MatchesForecastLine(transaction, flexLine))
            .ToList();
        Assert.Equal(4, flexTransactions.Count);
        Assert.Equal(7420m, flexTransactions.Sum(transaction => transaction.Amount));
        Assert.Equal(7420m, flexLine.CostToDate);

        var resourceGroups = dataset.Transactions
            .GroupBy(transaction => CalculationService.Normalise(transaction.LedgerResourceName))
            .ToDictionary(group => group.Key, group => group.Sum(transaction => transaction.Amount), StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Flex Projects L", resourceGroups.Keys);
        Assert.DoesNotContain("Contractors Payments", resourceGroups.Keys);
        Assert.NotEmpty(dataset.CategorySummaries);

        var seedValidationIssues = new ValidationService().Validate(dataset);
        Assert.Empty(seedValidationIssues);
        Assert.DoesNotContain(seedValidationIssues, issue => issue.Severity == "Error");
        Assert.All(
            dataset.ForecastLines,
            line => Assert.Equal(line.Budget - line.PlannedCostFcc, line.TotalBudgetVariance));

        var fiscalReport = calculationService.BuildFiscalYearReport(dataset);
        var fy26 = fiscalReport.Single(line => line.FiscalYear == "FY26");
        var fy27 = fiscalReport.Single(line => line.FiscalYear == "FY27");
        var fy28 = fiscalReport.Single(line => line.FiscalYear == "FY28");
        Assert.Equal(27695m, fy26.SpentToDate);
        Assert.Equal(279308.64m, fy26.CostToComplete);
        Assert.Equal(1508914.85m, fy27.CostToComplete);
        Assert.Equal(2450076.82m, fy28.CostToComplete);
        Assert.Equal(4265995.31m, fiscalReport.Sum(line => line.PlannedCost));
        Assert.Equal(933563.56m, fiscalReport.Sum(line => line.Budget));

        var actualsPivot = calculationService.BuildActualsPeriodSummaries(dataset.Transactions);
        Assert.Equal(
            5100m,
            actualsPivot.Single(row => row.TaskNumber == "WA57102001" && row.ResourceName == "Stanley Drake" && row.FyPeriod == "26-09").Amount);
        Assert.Equal(
            280m,
            actualsPivot.Single(row => row.TaskNumber == "WA57102001" && row.ResourceName == "Flex Projects L" && row.FyPeriod == "26-09").Amount);
        Assert.Equal(27695m, actualsPivot.Sum(row => row.Amount));
        Assert.True(
            SpreadsheetClipboardService.TryConvert(string.Empty, typeof(decimal), out var blankForecastValue)
            && blankForecastValue is 0m);

        var projectScopedActuals = new ProjectDataset
        {
            Header = new ProjectHeader { CurrentPeriod = "26-09" },
            Transactions =
            [
                new CostTransaction { TaskNumber = "TASK-1", ManualName = "Shared Resource", ProjectCode = "PROJECT-A", FyPeriod = "26-09", Amount = 100m },
                new CostTransaction { TaskNumber = "TASK-1", ManualName = "Shared Resource", ProjectCode = "PROJECT-B", FyPeriod = "26-09", Amount = 200m }
            ],
            ForecastLines =
            [
                new ForecastLine { TaskNumber = "TASK-1", ResourceName = "Shared Resource", ProjectCode = "Reporting A", TransactionProjectCode = "PROJECT-A" },
                new ForecastLine { TaskNumber = "TASK-1", ResourceName = "Shared Resource", ProjectCode = "Reporting B", TransactionProjectCode = "PROJECT-B" }
            ]
        };
        calculationService.Recalculate(projectScopedActuals);
        var projectALine = projectScopedActuals.ForecastLines[0];
        var projectBLine = projectScopedActuals.ForecastLines[1];
        Assert.Equal(100m, projectALine.CostToDate);
        Assert.Equal(200m, projectBLine.CostToDate);
        Assert.Equal(300m, projectScopedActuals.CategorySummaries.Sum(summary => summary.CostToDate));
        Assert.True(CalculationService.MatchesForecastLine(projectScopedActuals.Transactions[0], projectALine));
        Assert.False(CalculationService.MatchesForecastLine(projectScopedActuals.Transactions[1], projectALine));

        var legacyAggregateLine = new ForecastLine
        {
            TaskNumber = "TASK-1",
            ResourceName = "Shared Resource",
            ProjectCode = "Legacy reporting category"
        };
        calculationService.RecalculateForecastLines(
            [legacyAggregateLine, legacyAggregateLine],
            projectScopedActuals.Transactions,
            "26-09");
        Assert.Equal(300m, legacyAggregateLine.CostToDate);
        Assert.All(
            projectScopedActuals.Transactions,
            transaction => Assert.True(CalculationService.MatchesForecastLine(transaction, legacyAggregateLine)));

        var unassignedProjectActuals = new ProjectDataset
        {
            Header = new ProjectHeader { CurrentPeriod = "26-09" },
            Transactions =
            [
                new CostTransaction { TaskNumber = "UNASSIGNED-1", ManualName = "Shared Unassigned", ProjectCode = string.Empty, Amount = 75m },
                new CostTransaction { TaskNumber = "UNASSIGNED-1", ManualName = "Shared Unassigned", ProjectCode = "PROJECT-X", Amount = 125m }
            ],
            ForecastLines =
            [
                new ForecastLine { TaskNumber = "UNASSIGNED-1", ResourceName = "Shared Unassigned", TransactionProjectCode = string.Empty },
                new ForecastLine { TaskNumber = "UNASSIGNED-1", ResourceName = "Shared Unassigned" }
            ]
        };
        calculationService.Recalculate(unassignedProjectActuals);
        Assert.Equal(75m, unassignedProjectActuals.ForecastLines[0].CostToDate);
        Assert.Equal(200m, unassignedProjectActuals.ForecastLines[1].CostToDate);
    }

    [Fact]
    public void Batch_forecast_recalculation_keeps_large_transaction_work_within_the_legacy_budget()
    {
        const int lineCount = 500;
        const int transactionCount = 20000;
        var lines = Enumerable.Range(0, lineCount)
            .Select(index => new ForecastLine
            {
                TaskNumber = $"PERF-{index}",
                ResourceName = $"Resource-{index}",
                TransactionProjectCode = $"Project-{index % 5}"
            })
            .ToList();
        var transactions = Enumerable.Range(0, transactionCount)
            .Select(index => new CostTransaction
            {
                TaskNumber = $"PERF-{index % lineCount}",
                ManualName = $"Resource-{index % lineCount}",
                ProjectCode = $"Project-{index % 5}",
                FyPeriod = "26-09",
                Amount = 1m
            })
            .ToList();

        var stopwatch = Stopwatch.StartNew();
        new CalculationService().RecalculateForecastLines(lines, transactions, "26-09");
        stopwatch.Stop();

        Assert.All(lines, line => Assert.Equal(40m, line.CostToDate));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Elapsed {stopwatch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Clearing_a_monthly_forecast_recalculates_and_round_trips_derived_totals()
    {
        var dataset = Luna11TestSupport.LoadSeedDataset();
        var calculationService = new CalculationService();
        var line = Luna11TestSupport.FindForecastLine(dataset, "WA57102001", "Flex Projects L");
        var forecast = line.MonthlyForecasts.Single(month => month.PeriodLabel == "26-10");
        var originalCtc = line.TotalForecastCtc;
        Assert.Equal(3000m, forecast.Amount);

        forecast.Amount = 0m;
        calculationService.Recalculate(dataset);
        Assert.Equal(originalCtc - 3000m, line.TotalForecastCtc);
        Assert.Equal(line.TotalForecastCtc + line.CostToDate, line.PlannedCostFcc);
        Assert.True(dataset.CategorySummaries.Single(summary => summary.ProjectCode == "Project Management").TotalForecast < 651820m);

        using var directory = new Luna11TemporaryDirectory();
        var path = Path.Combine(directory.Root, "deleted-forecast.json");
        new ProjectFileService().Save(path, dataset);
        var reopened = new ProjectFileService().Load(path);
        calculationService.Recalculate(reopened);
        var reopenedLine = Luna11TestSupport.FindForecastLine(reopened, "WA57102001", "Flex Projects L");
        Assert.Equal(0m, reopenedLine.MonthlyForecasts.Single(month => month.PeriodLabel == "26-10").Amount);
        Assert.Equal(line.TotalForecastCtc, reopenedLine.TotalForecastCtc);
    }

    [Fact]
    public void Atomic_project_save_preserves_attribution_and_rapid_backup_identity()
    {
        using var directory = new Luna11TemporaryDirectory();
        var path = Path.Combine(directory.Root, "atomic-project.json");
        var service = new ProjectFileService();
        var dataset = new ProjectDataset
        {
            Header = new ProjectHeader { ProjectTitle = "Atomic version 1", CurrentPeriod = "26-09" },
            ForecastPeriods =
            [
                new ForecastPeriod { Label = "26-09", StartDate = new DateOnly(2026, 3, 1) }
            ],
            ForecastLines =
            [
                new ForecastLine { TaskNumber = "SCOPED", ResourceName = "Scoped resource", ProjectCode = "Scoped category", TransactionProjectCode = "PROJECT-PERSISTED" },
                new ForecastLine { TaskNumber = "UNASSIGNED", ResourceName = "Unassigned resource", ProjectCode = "Unassigned category", TransactionProjectCode = string.Empty },
                new ForecastLine { TaskNumber = "LEGACY", ResourceName = "Legacy resource", ProjectCode = "Legacy category", TransactionProjectCode = null }
            ]
        };

        service.Save(path, dataset);
        Assert.Equal("Atomic version 1", service.Load(path).Header.ProjectTitle);
        dataset.Header.ProjectTitle = "Atomic version 2";
        service.Save(path, dataset);
        var reopened = service.Load(path);
        Assert.Equal("Atomic version 2", reopened.Header.ProjectTitle);
        Assert.Equal("PROJECT-PERSISTED", reopened.ForecastLines.Single(line => line.TaskNumber == "SCOPED").TransactionProjectCode);
        Assert.Equal(string.Empty, reopened.ForecastLines.Single(line => line.TaskNumber == "UNASSIGNED").TransactionProjectCode);
        Assert.Null(reopened.ForecastLines.Single(line => line.TaskNumber == "LEGACY").TransactionProjectCode);

        var firstBackup = service.CreateBackup(path);
        var secondBackup = service.CreateBackup(path);
        Assert.NotEqual(firstBackup, secondBackup);
        Assert.True(File.Exists(firstBackup) && File.Exists(secondBackup));
    }
}
