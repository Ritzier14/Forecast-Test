using ProjectCostForecast.App;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using ProjectCostForecast.App.ViewModels;
using ClosedXML.Excel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

var root = FindRepositoryRoot();
var dataPath = Path.Combine(root, "src", "ProjectCostForecast.App", "Data", "SampleData.json");
var initialCostLoadPath = Path.Combine(root, "src", "ProjectCostForecast.App", "Data", "data_anonymised.xlsx");
var dataset = new ProjectFileService().Load(dataPath);
var calculationService = new CalculationService();
calculationService.Recalculate(dataset);

var initialCostLoadDataset = new InitialCostLoadService(new DateOnly(2026, 7, 18)).Load(initialCostLoadPath);
AssertTrue(initialCostLoadDataset.Transactions.Count > 1000, "Initial cost-load workbook imports its transaction rows");
AssertTrue(initialCostLoadDataset.Transactions.All(transaction => !string.IsNullOrWhiteSpace(transaction.ManualName)), "Initial cost-load resolves a resource name for every transaction");
AssertTrue(initialCostLoadDataset.ForecastLines.Count > 0, "Initial cost-load creates CTC lines for imported resources");
var initialCostLoadPeriods = initialCostLoadDataset.Transactions
    .Select(transaction => transaction.FyPeriod)
    .Where(period => FiscalPeriod.SortKey(period) != int.MaxValue)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToList();
AssertEqual(initialCostLoadPeriods.Count, initialCostLoadDataset.SavedMonthSnapshots.Count, "Initial cost-load creates one saved month per source period");
AssertTrue(initialCostLoadDataset.SavedMonthSnapshots.All(snapshot => FiscalPeriod.TryParseLabel(snapshot.Period, out _, out _)), "Initial saved months use valid FY-period labels");
AssertEqual(initialCostLoadDataset.Transactions.Sum(transaction => transaction.Amount), initialCostLoadDataset.SavedMonthSnapshots.First().CostToDate, "Latest initial saved month includes all imported actuals");
AssertEqual("26-12", initialCostLoadDataset.Header.CurrentPeriod, "Fresh startup defaults to the previous calendar month working period");
AssertTrue(initialCostLoadDataset.ForecastPeriods.Any(period => period.Label == "26-12"), "Fresh startup includes the default working month");
AssertTrue(initialCostLoadDataset.ForecastPeriods.Any(period => period.Label == "27-01"), "Fresh startup includes the next month for roll-forward");
AssertTrue(initialCostLoadDataset.SavedMonthSnapshots.All(snapshot => snapshot.Period != initialCostLoadDataset.Header.CurrentPeriod), "Fresh startup does not treat the unsaved working month as already rolled forward");

AssertEqual(new DateOnly(2026, 1, 1), GetCalendarMonthStart("26-07"), "FY26-07 maps to January 2026");
AssertEqual(new DateOnly(2026, 7, 1), GetCalendarMonthStart("27-01"), "FY27-01 maps to July 2026");
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
AssertTrue(
    outOfOrderPeriodsDataset.ForecastPeriods.Take(2).Select(period => period.Label).SequenceEqual(["24-07", "24-08"]),
    "Initial cost-load orders periods by fiscal calendar rather than document posting date");
AssertEqual(new DateOnly(2024, 1, 1), outOfOrderPeriodsDataset.ForecastPeriods.Single(period => period.Label == "24-07").StartDate, "FY24-07 keeps its canonical calendar start");
AssertEqual(new DateOnly(2024, 2, 1), outOfOrderPeriodsDataset.ForecastPeriods.Single(period => period.Label == "24-08").StartDate, "FY24-08 keeps its canonical calendar start");
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
AssertTrue(
    calendar2022Labels.SequenceEqual(FiscalPeriod.BuildContinuousRange(22, 7, 23, 6)),
    "Calendar 2022 spans fiscal periods 22-07 through 23-06");
AssertEqual(12, calendar2022Labels.Count, "Calendar 2022 includes every fiscal month");
AssertTrue(
    sparseCalendarYearDataset.ForecastLines.Single().MonthlyForecasts.Any(forecast => forecast.PeriodLabel == "22-08" && forecast.Amount == 0m),
    "Missing fiscal months receive zero forecast entries");

var clipboardRows = SpreadsheetClipboardService.Parse("A\tB\r\nC\tD");
AssertEqual(2, clipboardRows.Count, "Clipboard parser row count");
AssertEqual("D", clipboardRows[1][1], "Clipboard parser preserves matrix values");
AssertEqual("A\tB" + Environment.NewLine + "C\tD", SpreadsheetClipboardService.Serialize(clipboardRows), "Clipboard serializer emits Excel-compatible text");
var appliedCells = new Dictionary<(int Row, int Column), string>();
var appliedCount = SpreadsheetClipboardService.Apply(
    clipboardRows,
    2,
    3,
    (row, column) => !(row == 2 && column == 4),
    (row, column, value) => appliedCells[(row, column)] = value);
AssertEqual(3, appliedCount, "Clipboard apply skips read-only destinations");
AssertTrue(!appliedCells.ContainsKey((2, 4)), "Clipboard apply leaves read-only destination unchanged");
AssertEqual("D", appliedCells[(3, 4)], "Clipboard apply offsets the pasted matrix");
var originalCulture = CultureInfo.CurrentCulture;
try
{
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
    AssertTrue(
        SpreadsheetClipboardService.TryConvert("1,5", typeof(decimal), out var commaDecimalValue)
        && commaDecimalValue is 1.5m,
        "Clipboard conversion respects comma decimal cultures");
}
finally
{
    CultureInfo.CurrentCulture = originalCulture;
}
AssertEqual("1,260,000", AccountingNoDecimalsConverter.FormatAccounting(1260000m, CultureInfo.CurrentCulture), "Accounting formatter omits dollar symbols by default");
AssertEqual("(1,260,000)", AccountingNoDecimalsConverter.FormatAccounting(-1260000m, CultureInfo.CurrentCulture), "Accounting formatter uses brackets for negatives");
AssertEqual("1.26m", AccountingNoDecimalsConverter.FormatAccounting(1260000m, CultureInfo.CurrentCulture, compactMillions: true, compactMillionDecimals: 2), "Accounting formatter compacts millions for forecast cells");
AssertTrue(AccountingNoDecimalsConverter.TryParseForecastMonthInput("1.26", CultureInfo.CurrentCulture, out var compactMillionAmount) && compactMillionAmount == 1260000m, "Forecast month parser expands bare decimal million input");
AssertTrue(AccountingNoDecimalsConverter.TryParseForecastMonthInput("1.25m", CultureInfo.CurrentCulture, out var suffixedMillionAmount) && suffixedMillionAmount == 1250000m, "Forecast month parser expands suffixed million input");

AssertTrue(KpiComparisonFormatter.Format(108.7m, 100m).StartsWith("↑ 8.7%", StringComparison.Ordinal), "KPI comparison formats an increase");
AssertTrue(KpiComparisonFormatter.Format(90m, 100m).StartsWith("↓ 10%", StringComparison.Ordinal), "KPI comparison formats a decrease");
AssertTrue(KpiComparisonFormatter.Format(100m, 100m).StartsWith("→ 0%", StringComparison.Ordinal), "KPI comparison formats an unchanged value");
AssertEqual(string.Empty, KpiComparisonFormatter.Format(100m, 0m), "KPI comparison hides a zero baseline");
AssertEqual("Up", KpiComparisonFormatter.GetDirection(101m, 100m), "KPI comparison direction reports increase");

AssertEqual(63, dataset.Transactions.Count, "Raw transaction count");
AssertEqual(27695m, dataset.Transactions.Sum(t => t.Amount), "Raw transaction total");

var stanleyLine = FindForecastLine(dataset, "WA57102001", "Stanley Drake");
var stanleyTransactions = dataset.Transactions.Where(t => CalculationService.MatchesForecastLine(t, stanleyLine)).ToList();
AssertEqual(39, stanleyTransactions.Count, "Stanley Drake transaction count");
AssertEqual(15000m, stanleyTransactions.Sum(t => t.Amount), "Stanley Drake transaction total");
AssertEqual(15000m, stanleyLine.CostToDate, "Stanley Drake recalculated CTD");

var flexLine = FindForecastLine(dataset, "WA57102001", "Flex Projects L");
var flexTransactions = dataset.Transactions.Where(t => CalculationService.MatchesForecastLine(t, flexLine)).ToList();
AssertEqual(4, flexTransactions.Count, "Flex Projects L AP transaction count");
AssertEqual(7420m, flexTransactions.Sum(t => t.Amount), "Flex Projects L AP transaction total");
AssertEqual(7420m, flexLine.CostToDate, "Flex Projects L recalculated CTD");

var resourceGroups = dataset.Transactions
    .GroupBy(t => CalculationService.Normalise(t.LedgerResourceName))
    .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount), StringComparer.OrdinalIgnoreCase);
AssertTrue(resourceGroups.ContainsKey("Flex Projects L"), "Resource grouping uses Manual Name for AP rows");
AssertTrue(!resourceGroups.ContainsKey("Contractors Payments"), "AP contractor payments are not grouped under the generic description");

AssertTrue(dataset.CategorySummaries.Count > 0, "Category summaries are recalculated");
var seedValidationIssues = new ValidationService().Validate(dataset);
AssertEqual(0, seedValidationIssues.Count, "Seed data has no validation issues");
AssertTrue(seedValidationIssues.All(issue => issue.Severity != "Error"), "Seed data has no validation errors");
AssertTrue(
    dataset.ForecastLines.All(line => line.TotalBudgetVariance == line.Budget - line.PlannedCostFcc),
    "Budget variance uses budget minus planned cost consistently");

var fiscalReport = calculationService.BuildFiscalYearReport(dataset);
var fy26 = fiscalReport.Single(line => line.FiscalYear == "FY26");
var fy27 = fiscalReport.Single(line => line.FiscalYear == "FY27");
var fy28 = fiscalReport.Single(line => line.FiscalYear == "FY28");
AssertEqual(27695m, fy26.SpentToDate, "FY26 spent to date matches workbook report");
AssertEqual(279308.64m, fy26.CostToComplete, "FY26 CTC matches workbook report");
AssertEqual(1508914.85m, fy27.CostToComplete, "FY27 CTC matches workbook report");
AssertEqual(2450076.82m, fy28.CostToComplete, "FY28 CTC matches workbook report");
AssertEqual(4265995.31m, fiscalReport.Sum(line => line.PlannedCost), "FY planned cost total matches workbook report");
AssertEqual(933563.56m, fiscalReport.Sum(line => line.Budget), "AP/LTP budget total matches workbook report");

var actualsPivot = calculationService.BuildActualsPeriodSummaries(dataset.Transactions);
AssertEqual(5100m, actualsPivot.Single(row => row.TaskNumber == "WA57102001" && row.ResourceName == "Stanley Drake" && row.FyPeriod == "26-09").Amount, "Pivot Stanley Drake 26-09 amount");
AssertEqual(280m, actualsPivot.Single(row => row.TaskNumber == "WA57102001" && row.ResourceName == "Flex Projects L" && row.FyPeriod == "26-09").Amount, "Pivot Flex Projects L 26-09 amount");
AssertEqual(27695m, actualsPivot.Sum(row => row.Amount), "Pivot total amount matches raw data");
AssertTrue(SpreadsheetClipboardService.TryConvert(string.Empty, typeof(decimal), out var blankForecastValue) && blankForecastValue is 0m, "Blank forecast cells convert to zero");

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
AssertEqual(100m, projectALine.CostToDate, "Project-scoped forecast line receives only its own actuals");
AssertEqual(200m, projectBLine.CostToDate, "Second project-scoped forecast line receives only its own actuals");
AssertEqual(300m, projectScopedActuals.CategorySummaries.Sum(summary => summary.CostToDate), "Cross-project actuals are not double counted");
AssertTrue(CalculationService.MatchesForecastLine(projectScopedActuals.Transactions[0], projectALine), "Project-scoped matcher accepts the matching project");
AssertTrue(!CalculationService.MatchesForecastLine(projectScopedActuals.Transactions[1], projectALine), "Project-scoped matcher rejects a different project");

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
AssertEqual(300m, legacyAggregateLine.CostToDate, "Legacy forecast line without transaction project keeps aggregate actuals");
AssertTrue(projectScopedActuals.Transactions.All(transaction => CalculationService.MatchesForecastLine(transaction, legacyAggregateLine)), "Legacy matcher keeps task-resource fallback across projects");

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
AssertEqual(75m, unassignedProjectActuals.ForecastLines[0].CostToDate, "Project-scoped unassigned line receives only unassigned actuals");
AssertEqual(200m, unassignedProjectActuals.ForecastLines[1].CostToDate, "Null legacy attribution retains the aggregate fallback");

const int calculationPerformanceLineCount = 500;
const int calculationPerformanceTransactionCount = 20000;
var calculationPerformanceLines = Enumerable.Range(0, calculationPerformanceLineCount)
    .Select(index => new ForecastLine
    {
        TaskNumber = $"PERF-{index}",
        ResourceName = $"Resource-{index}",
        TransactionProjectCode = $"Project-{index % 5}"
    })
    .ToList();
var calculationPerformanceTransactions = Enumerable.Range(0, calculationPerformanceTransactionCount)
    .Select(index => new CostTransaction
    {
        TaskNumber = $"PERF-{index % calculationPerformanceLineCount}",
        ManualName = $"Resource-{index % calculationPerformanceLineCount}",
        ProjectCode = $"Project-{index % 5}",
        FyPeriod = "26-09",
        Amount = 1m
    })
    .ToList();
var calculationStopwatch = Stopwatch.StartNew();
calculationService.RecalculateForecastLines(
    calculationPerformanceLines,
    calculationPerformanceTransactions,
    "26-09");
calculationStopwatch.Stop();
AssertTrue(
    calculationPerformanceLines.All(line => line.CostToDate == 40m),
    "Batch forecast recalculation attributes the large transaction set correctly");
AssertTrue(
    calculationStopwatch.Elapsed < TimeSpan.FromSeconds(5),
    $"Batch forecast recalculation completes within 5 seconds ({calculationStopwatch.ElapsedMilliseconds} ms)");

var deletePersistenceDataset = new ProjectFileService().Load(dataPath);
var deletePersistenceLine = FindForecastLine(deletePersistenceDataset, "WA57102001", "Flex Projects L");
var deletedForecast = deletePersistenceLine.MonthlyForecasts.Single(forecast => forecast.PeriodLabel == "26-10");
var originalDeleteLineCtc = deletePersistenceLine.TotalForecastCtc;
AssertEqual(3000m, deletedForecast.Amount, "Delete persistence test starts with a user forecast value");
deletedForecast.Amount = 0m;
calculationService.Recalculate(deletePersistenceDataset);
AssertEqual(originalDeleteLineCtc - 3000m, deletePersistenceLine.TotalForecastCtc, "Clearing a monthly forecast recalculates line CTC");
AssertEqual(deletePersistenceLine.TotalForecastCtc + deletePersistenceLine.CostToDate, deletePersistenceLine.PlannedCostFcc, "Clearing a monthly forecast recalculates FCC");
AssertTrue(deletePersistenceDataset.CategorySummaries.Single(summary => summary.ProjectCode == "Project Management").TotalForecast < 651820m, "Clearing a monthly forecast recalculates category summaries");
var deletePersistencePath = Path.Combine(Path.GetTempPath(), $"project-cost-delete-{Guid.NewGuid():N}.json");
try
{
    new ProjectFileService().Save(deletePersistencePath, deletePersistenceDataset);
    var reloadedDeleteDataset = new ProjectFileService().Load(deletePersistencePath);
    calculationService.Recalculate(reloadedDeleteDataset);
    var reloadedDeleteLine = FindForecastLine(reloadedDeleteDataset, "WA57102001", "Flex Projects L");
    AssertEqual(0m, reloadedDeleteLine.MonthlyForecasts.Single(forecast => forecast.PeriodLabel == "26-10").Amount, "Cleared monthly forecast persists as zero");
    AssertEqual(deletePersistenceLine.TotalForecastCtc, reloadedDeleteLine.TotalForecastCtc, "Reopened project keeps recalculated CTC after delete");
}
finally
{
    File.Delete(deletePersistencePath);
}

var projectFileService = new ProjectFileService();
var projectFileTestDirectory = Path.Combine(Path.GetTempPath(), $"project-cost-file-service-{Guid.NewGuid():N}");
var projectFileTestPath = Path.Combine(projectFileTestDirectory, "atomic-project.json");
try
{
    var atomicDataset = new ProjectDataset
    {
        Header = new ProjectHeader { ProjectTitle = "Atomic version 1" },
        ForecastLines =
        [
            new ForecastLine { TaskNumber = "SCOPED", TransactionProjectCode = "PROJECT-PERSISTED" },
            new ForecastLine { TaskNumber = "UNASSIGNED", TransactionProjectCode = string.Empty },
            new ForecastLine { TaskNumber = "LEGACY", TransactionProjectCode = null }
        ]
    };
    projectFileService.Save(projectFileTestPath, atomicDataset);
    AssertEqual("Atomic version 1", projectFileService.Load(projectFileTestPath).Header.ProjectTitle, "Atomic project save round-trips a new file");

    atomicDataset.Header.ProjectTitle = "Atomic version 2";
    projectFileService.Save(projectFileTestPath, atomicDataset);
    var overwrittenAtomicDataset = projectFileService.Load(projectFileTestPath);
    AssertEqual("Atomic version 2", overwrittenAtomicDataset.Header.ProjectTitle, "Atomic project save replaces an existing file");
    AssertEqual("PROJECT-PERSISTED", overwrittenAtomicDataset.ForecastLines.Single(line => line.TaskNumber == "SCOPED").TransactionProjectCode, "Transaction project attribution persists in project files");
    AssertEqual(string.Empty, overwrittenAtomicDataset.ForecastLines.Single(line => line.TaskNumber == "UNASSIGNED").TransactionProjectCode, "Explicit unassigned project scope persists distinctly");
    AssertTrue(overwrittenAtomicDataset.ForecastLines.Single(line => line.TaskNumber == "LEGACY").TransactionProjectCode is null, "Legacy aggregate attribution remains null after persistence");

    var firstBackup = projectFileService.CreateBackup(projectFileTestPath);
    var secondBackup = projectFileService.CreateBackup(projectFileTestPath);
    AssertTrue(!string.Equals(firstBackup, secondBackup, StringComparison.OrdinalIgnoreCase), "Rapid project backups use distinct paths");
    AssertTrue(File.Exists(firstBackup) && File.Exists(secondBackup), "Rapid project backups both exist");
}
finally
{
    if (Directory.Exists(projectFileTestDirectory))
    {
        Directory.Delete(projectFileTestDirectory, recursive: true);
    }
}

var closedPeriod = MainWindowViewModel.DetermineClosedForecastPeriod(dataset.ForecastPeriods, new DateOnly(2026, 5, 17));
AssertEqual("26-10", closedPeriod?.Label, "Current closed forecast period is previous calendar month");
var expectedWorkingPeriod = MainWindowViewModel.DetermineExpectedWorkingPeriod(dataset.ForecastPeriods, new DateOnly(2026, 6, 17));
AssertEqual("26-11", expectedWorkingPeriod?.Label, "Expected working period is the previous calendar month period");
AssertEqual(0, MainWindowViewModel.BuildActivePeriodWarnings(dataset.ForecastPeriods, "26-11", new DateOnly(2026, 6, 17)).Count, "Accepted working period has no active warning");
AssertEqual(1, MainWindowViewModel.BuildActivePeriodWarnings(dataset.ForecastPeriods, "26-10", new DateOnly(2026, 6, 17)).Count, "Too-old saved period has an active warning");
AssertEqual(1, MainWindowViewModel.BuildActivePeriodWarnings(dataset.ForecastPeriods, "26-12", new DateOnly(2026, 6, 17)).Count, "Current-month saved period has an active warning");
AssertEqual(1, MainWindowViewModel.BuildActivePeriodWarnings(dataset.ForecastPeriods, "99-99", new DateOnly(2026, 6, 17)).Count, "Unknown saved period has an active warning");

var viewModel = CreateViewModel();
AssertEqual("26-09", viewModel.Header.CurrentPeriod, "View model uses saved current period without calendar overwrite");
AssertEqual(2026, viewModel.SelectedCtcMonthForecastYear, "Forecast grid defaults to the saved current forecast year");
var currentPeriodForecast = viewModel.ForecastLines
    .Single(line => line.TaskNumber == "WA57102001" && line.ResourceName == "Stanley Drake")
    .MonthlyForecasts
    .Single(forecast => forecast.PeriodLabel == viewModel.Header.CurrentPeriod);
var previousPeriodForecast = viewModel.ForecastLines
    .Single(line => line.TaskNumber == "WA57102001" && line.ResourceName == "Stanley Drake")
    .MonthlyForecasts
    .Single(forecast => forecast.PeriodLabel == "26-08");
AssertTrue(!currentPeriodForecast.IsLocked, "Saved current period remains editable");
AssertTrue(previousPeriodForecast.IsLocked, "Periods before the saved current period are locked");
var workingForecastLine = viewModel.ForecastLines.First();
var workingForecastAmount = workingForecastLine.MonthlyForecasts.First().Amount;
var historicalSnapshot = new SavedMonthSnapshot
{
    Period = "26-08",
    SavedAt = new DateTime(2026, 4, 1, 8, 0, 0),
    ForecastLines =
    [
        new SavedMonthForecastLine
        {
            RowNumber = 9001,
            TaskNumber = "HIST-1",
            ResourceName = "Historical resource",
            ProjectCode = "Historical category",
            CostToDate = 100m,
            CurrentPeriodForecast = 10m,
            CostToComplete = 30m,
            FinalForecast = 130m,
            Budget = 200m,
            TotalBudgetVariance = 70m,
            VarianceFromPreviousMonth = 5m,
            MonthlyForecasts =
            [
                new SavedMonthPeriodAmount { PeriodLabel = "26-08", PeriodStartDate = new DateOnly(2026, 3, 1), Amount = 10m },
                new SavedMonthPeriodAmount { PeriodLabel = "26-09", PeriodStartDate = new DateOnly(2026, 4, 1), Amount = 20m }
            ]
        }
    ]
};
viewModel.SearchText = "filter that matches nothing";
viewModel.ShowOnlyLinesWithRemainingForecast = true;
viewModel.ForecastLinesView.Refresh();
AssertEqual(0, viewModel.ForecastLinesView.Cast<object>().Count(), "Active CTC filters can hide all current rows");
viewModel.ViewSavedMonthSnapshot(historicalSnapshot);
AssertTrue(viewModel.IsViewingSavedMonth, "Opening a saved month enters historical viewing mode");
AssertTrue(viewModel.IsSavedMonthViewLocked, "Opened saved month is locked by default");
AssertEqual(1, viewModel.ForecastLinesView.Cast<object>().Count(), "Opening a saved month clears filters that would leave the CTC grid blank");
AssertTrue(!viewModel.ShowOnlyLinesWithRemainingForecast && string.IsNullOrEmpty(viewModel.SearchText), "Saved-month CTC view starts with neutral filters");
AssertTrue(viewModel.ForecastLines.SelectMany(line => line.MonthlyForecasts).All(month => month.IsLocked), "Every historical forecast month is locked by default");
AssertEqual("26-09", viewModel.Header.CurrentPeriod, "Viewing history does not change the saved working period");
AssertTrue(viewModel.SetSavedMonthViewLocked(false, confirmUnlock: false), "Historical month can be explicitly unlocked");
AssertTrue(viewModel.ForecastLines.SelectMany(line => line.MonthlyForecasts).All(month => !month.IsLocked), "Explicit unlock makes historical forecast cells editable");
viewModel.ForecastLines.Single().MonthlyForecasts.First().Amount = 15m;
AssertEqual(15m, historicalSnapshot.ForecastLines.Single().MonthlyForecasts.First().Amount, "Editing an unlocked historical month updates its snapshot");
AssertEqual(35m, historicalSnapshot.CostToComplete, "Historical snapshot totals recalculate after editing");
AssertEqual(135m, historicalSnapshot.FinalForecast, "Historical final forecast recalculates after editing");
viewModel.CloseSavedMonthView();
AssertTrue(!viewModel.IsViewingSavedMonth, "Returning closes historical viewing mode");
AssertTrue(viewModel.ShowOnlyLinesWithRemainingForecast && viewModel.SearchText == "filter that matches nothing", "Returning to the current month restores the previous CTC filters");
AssertTrue(ReferenceEquals(workingForecastLine, viewModel.ForecastLines.First()), "Returning restores the original working forecast objects");
AssertEqual(workingForecastAmount, viewModel.ForecastLines.First().MonthlyForecasts.First().Amount, "Historical edits do not alter the current working forecast");
viewModel.SearchText = string.Empty;
viewModel.ShowOnlyLinesWithRemainingForecast = false;

AssertEqual(2, viewModel.BudgetLines.Count, "Budget workspace provides P3M and LTP/AP lines");
AssertTrue(viewModel.BudgetFiscalYears.Count > 0, "Budget workspace derives fiscal years from current project data");
var p3mBudgetLine = viewModel.BudgetLines.Single(line => line.Key == MainWindowViewModel.P3mBudgetLineKey);
var ltpApBudgetLine = viewModel.BudgetLines.Single(line => line.Key == MainWindowViewModel.LtpApBudgetLineKey);
AssertTrue(ltpApBudgetLine.IsActive, "Legacy AP/LTP budget is active by default");
AssertEqual(dataset.FiscalYearBudgets.Sum(budget => budget.Budget), ltpApBudgetLine.Total, "Legacy fiscal budgets migrate into the LTP/AP line");
var firstBudgetYear = p3mBudgetLine.Amounts.First();
firstBudgetYear.Amount = 123456m;
AssertEqual(123456m, p3mBudgetLine.Total, "Budget line total updates after an FY entry");
viewModel.SetActiveBudgetLine(p3mBudgetLine);
AssertTrue(p3mBudgetLine.IsActive && !ltpApBudgetLine.IsActive, "Right-click budget action can switch the active line");
AssertEqual(123456m, viewModel.FiscalYearReportLines.Single(line => line.FiscalYear == firstBudgetYear.FiscalYear).Budget, "Fiscal reporting uses the selected active budget line");
AssertTrue(viewModel.BudgetActualChartGeometry != System.Windows.Media.Geometry.Empty, "Budget chart plots current spend to date");
AssertTrue(viewModel.BudgetForecastChartGeometry != System.Windows.Media.Geometry.Empty, "Budget chart plots spend plus forecast");
AssertTrue(viewModel.BudgetPlanChartGeometry != System.Windows.Media.Geometry.Empty, "Budget chart plots the active budget");
AssertTrue(!string.IsNullOrWhiteSpace(viewModel.ForecastFreezeColumnKey), "Forecast grid freeze key is initialized");

var chartViewModel = CreateViewModel();
chartViewModel.SelectedForecastLine = chartViewModel.ForecastLines.First();
var monthlyChartTickCount = chartViewModel.LedgerChartXAxisLabels.Count;
chartViewModel.ZoomLedgerChart(zoomIn: false);
AssertEqual(LedgerChartTimeScale.Quarter, chartViewModel.LedgerChartTimeScale, "Spend curve zoom moves from months to quarters");
AssertTrue(chartViewModel.LedgerChartXAxisLabels.Count <= monthlyChartTickCount, "Spend curve aggregation reduces physical time buckets");
chartViewModel.ShowLedgerActualSeries = false;
chartViewModel.ShowLedgerForecastSeries = false;
chartViewModel.ShowLedgerBudgetSeries = true;
AssertTrue(!chartViewModel.ShowLedgerActualSeries && !chartViewModel.ShowLedgerForecastSeries && chartViewModel.ShowLedgerBudgetSeries, "Spend curve series visibility toggles independently");

var presentationViewModel = CreateViewModel();
presentationViewModel.ActiveDetailWorkspaceKey = "Ledger Monthly Forecast";
AssertTrue(presentationViewModel.CurrentDetailWorkspaceViews.Any(view => view.ContentKey == "MonthsDown"), "Monthly Forecast has a Months Down default view");
AssertTrue(presentationViewModel.CurrentDetailWorkspaceViews.Any(view => view.ContentKey == "MonthsAcross"), "Monthly Forecast has a Months Across default view");
presentationViewModel.SelectedForecastLine = presentationViewModel.ForecastLines.First();
presentationViewModel.ToggleMonthlyForecastOrientation();
AssertTrue(presentationViewModel.IsMonthlyForecastMonthsAcross, "Monthly Forecast Flip Table switches to months across");
AssertTrue(presentationViewModel.MonthlyForecastAcrossRows.Count > 0, "Months Across exposes presentation rows without duplicating forecast data");
AssertTrue(presentationViewModel.MonthlyForecastAcrossRows.All(row => row.Values.Count > 0), "Months Across exposes month columns for each presentation row");
var acrossForecastRow = presentationViewModel.MonthlyForecastAcrossRows.First(row => row.Metric == "Forecast");
var acrossPeriod = acrossForecastRow.Values.Keys.First();
AssertEqual(
    presentationViewModel.SelectedForecastLine.MonthlyForecasts.Single(forecast => forecast.PeriodLabel == acrossPeriod).Amount,
    acrossForecastRow[acrossPeriod],
    "Months Across reads the same underlying forecast amount");
presentationViewModel.ToggleMonthlyForecastOrientation();
AssertTrue(!presentationViewModel.IsMonthlyForecastMonthsAcross, "Monthly Forecast Flip Table returns to months down");
presentationViewModel.TaskCodeReviewDisplayMode = "Both";
AssertTrue(presentationViewModel.TaskCodeReviewRows.Count > 0, "Task Code Review exposes task-code relationship rows");

var calendarYearViewModel = CreateViewModel();
var nextCalendarYear = calendarYearViewModel.AvailableCtcMonthForecastYears.Max() + 1;
var selectedCalendarYearsBeforeAdd = calendarYearViewModel.SelectedCtcMonthForecastYears.ToHashSet();
calendarYearViewModel.AddNewCalendarYear();
AssertTrue(calendarYearViewModel.AvailableCtcMonthForecastYears.Contains(nextCalendarYear), "Calendar year context action adds a new forecast year");
AssertEqual(nextCalendarYear, calendarYearViewModel.SelectedCtcMonthForecastYear, "New calendar year becomes the selected forecast year");
AssertTrue(selectedCalendarYearsBeforeAdd.All(calendarYearViewModel.IsCtcMonthForecastYearSelected), "Adding a calendar year preserves the years already shown");
AssertTrue(calendarYearViewModel.SelectedCtcMonthForecastYears.Contains(nextCalendarYear), "Adding a calendar year also shows the new year");

viewModel.ResetForecastFreezeColumn();
AssertEqual(MainWindowViewModel.DefaultForecastFreezeColumnKey, viewModel.ForecastFreezeColumnKey, "Forecast grid freeze reset returns to the forecast start boundary");
viewModel.SetForecastFreezeColumn("MONTH:26-11");
AssertEqual("MONTH:26-11", viewModel.ForecastFreezeColumnKey, "Forecast grid freeze column can be changed");
viewModel.ResetForecastFreezeColumn();
AssertEqual(MainWindowViewModel.DefaultForecastFreezeColumnKey, viewModel.ForecastFreezeColumnKey, "Forecast grid freeze reset returns to the forecast start boundary");
viewModel.SetDetailPanelRailWidth(72);
AssertEqual(72d, viewModel.DetailPanelRailWidth, "Collapsed detail rail width persists on the view model");
viewModel.SetDetailPanelRailWidth(8);
AssertEqual(36d, viewModel.DetailPanelRailWidth, "Collapsed detail rail width clamps to the minimum");
viewModel.SetDetailPanelRailWidth(160);
AssertEqual(92d, viewModel.DetailPanelRailWidth, "Collapsed detail rail width clamps to the maximum");
viewModel.SetDetailPanelPinned(true);
AssertTrue(viewModel.IsDetailPanelPinned, "Detail panel pin state can be enabled globally");
viewModel.SetDetailPanelPinned(false);
AssertTrue(!viewModel.IsDetailPanelPinned, "Detail panel pin state can be disabled globally");

var contingencyViewModel = CreateViewModel();
contingencyViewModel.ContingencyEntries.Clear();
contingencyViewModel.IsDirty = false;
var trackedContingency = new ContingencyEntry
{
    RemainingContingency = 500m,
    ContingencyExpended = 100m,
    ProposedExpenditure = 50m
};
contingencyViewModel.ContingencyEntries.Add(trackedContingency);
AssertTrue(contingencyViewModel.IsDirty, "Adding a contingency row marks the project dirty");
AssertEqual(500m, contingencyViewModel.TotalContingencyRemaining, "Adding a contingency row refreshes remaining contingency");
AssertEqual(100m, contingencyViewModel.ContingencyExpendedTotal, "Adding a contingency row refreshes expended contingency");
contingencyViewModel.IsDirty = false;
trackedContingency.RemainingContingency = 425m;
AssertTrue(contingencyViewModel.IsDirty, "Editing a contingency row marks the project dirty");
AssertEqual(425m, contingencyViewModel.TotalContingencyRemaining, "Editing a contingency row refreshes totals");
contingencyViewModel.IsDirty = false;
contingencyViewModel.ContingencyEntries.Remove(trackedContingency);
AssertTrue(contingencyViewModel.IsDirty, "Removing a contingency row marks the project dirty");
AssertEqual(0m, contingencyViewModel.TotalContingencyRemaining, "Removing a contingency row refreshes totals");

var metadataDataset = new ProjectDataset
{
    Header = new ProjectHeader { CurrentPeriod = "26-11" },
    ForecastPeriods = [new ForecastPeriod { Label = "26-11", StartDate = new DateOnly(2026, 6, 1) }],
    Transactions =
    [
        new CostTransaction { TaskNumber = "RAW-001", ManualName = "Imported", FyPeriod = "26-11", Amount = 10m }
    ],
    ForecastLines =
    [
        new ForecastLine { TaskNumber = "RAW-001", ResourceName = "Imported", ProjectCode = "Legacy Category" },
        new ForecastLine { TaskNumber = "MAN-001", ResourceName = "Manual", ProjectCode = string.Empty }
    ],
    ProjectTaskCodes =
    [
        new ProjectTaskCode { SystemCode = "RAW-001", TaskName = "Imported task", IsRawDataCode = true },
        new ProjectTaskCode { SystemCode = "MAN-001", TaskName = "Manual task", IsManualCode = true }
    ],
    ProjectCategories =
    [
        new ProjectCategory { Name = "Legacy Category" },
        new ProjectCategory { Name = "Manual Override" }
    ]
};
var metadataViewModel = CreateViewModel();
InvokeLoadDataset(metadataViewModel, metadataDataset);
AssertEqual(new DateOnly(2026, 5, 1), metadataDataset.ForecastPeriods.Single().StartDate, "Loading a project repairs legacy fiscal-period calendar dates");
var sparseLoadedDataset = new ProjectDataset
{
    Header = new ProjectHeader { CurrentPeriod = "23-06" },
    ForecastPeriods =
    [
        new ForecastPeriod { Label = "22-07", StartDate = new DateOnly(2022, 1, 1) },
        new ForecastPeriod { Label = "23-06", StartDate = new DateOnly(2022, 12, 1) }
    ],
    ForecastLines =
    [
        new ForecastLine
        {
            TaskNumber = "SPARSE-001",
            ResourceName = "Sparse",
            MonthlyForecasts =
            [
                new MonthlyForecast { PeriodLabel = "22-07", PeriodStartDate = new DateOnly(2022, 1, 1), Amount = 5m },
                new MonthlyForecast { PeriodLabel = "23-06", PeriodStartDate = new DateOnly(2022, 12, 1), Amount = 7m }
            ]
        }
    ]
};
var sparseLoadedViewModel = CreateViewModel();
InvokeLoadDataset(sparseLoadedViewModel, sparseLoadedDataset);
AssertTrue(
    sparseLoadedDataset.ForecastPeriods
        .Where(period => period.StartDate?.Year == 2022)
        .Select(period => period.Label)
        .SequenceEqual(FiscalPeriod.BuildContinuousRange(22, 7, 23, 6)),
    "Loading a sparse project fills the fiscal months inside each calendar year");
AssertTrue(
    sparseLoadedDataset.ForecastLines.Single().MonthlyForecasts.Any(forecast => forecast.PeriodLabel == "22-08" && forecast.Amount == 0m),
    "Loading a sparse project aligns zero forecast cells with filled months");
var migratedLine = metadataViewModel.ForecastLines.Single(line => line.TaskNumber == "RAW-001");
var fallbackLine = metadataViewModel.ForecastLines.Single(line => line.TaskNumber == "MAN-001");
AssertEqual("Legacy Category", migratedLine.ReportingCategoryOverride, "Legacy ProjectCode migrates into reporting category override");
AssertEqual("Imported task", migratedLine.ReportingCategory, "Migrated legacy ProjectCode falls back to task-name category until manually changed");
AssertEqual("Manual task", fallbackLine.ReportingCategory, "Task name is the default reporting category");
metadataViewModel.SetForecastLineReportingCategory(fallbackLine, "Manual Override");
AssertEqual("Manual Override", fallbackLine.ReportingCategory, "Typed row category override applies immediately");
metadataViewModel.DeleteProjectCategory(metadataViewModel.ProjectCategories.Single(category => category.Name == "Manual Override"));
AssertEqual("Manual task", fallbackLine.ReportingCategory, "Deleting a category clears overrides and falls back to task name");
metadataViewModel.ProjectTaskCodes.Add(new ProjectTaskCode { SystemCode = "MAN-002", TaskName = "Manual task", IsManualCode = true });
metadataViewModel.RefreshTaskCategoryMetadata();
AssertTrue(metadataViewModel.ProjectTaskCodes.Any(task => task.TaskName == "Manual task (1)"), "Duplicate task names get numeric suffixes");
AssertTrue(!metadataViewModel.ProjectTaskCodes.Single(task => task.SystemCode == "RAW-001").CanEditSystemCode, "Raw task system code is locked");
AssertTrue(metadataViewModel.ProjectTaskCodes.Single(task => task.SystemCode == "MAN-001").CanEditSystemCode, "Manual task system code remains editable");

var managementSourceLine = viewModel.ForecastLines.Single(line =>
    string.Equals(line.TaskNumber, "WA57102001", StringComparison.OrdinalIgnoreCase)
    && string.Equals(line.ResourceName, "Stanley Drake", StringComparison.OrdinalIgnoreCase));
var managementResource = viewModel.AddManagementResource(managementSourceLine);
var managementPeriod = managementResource.MonthlyAllocations.First().PeriodLabel;
managementResource.HourlyRate = 125m;
managementResource[managementPeriod] = 50m;
AssertEqual(1, viewModel.ManagementResources.Count, "Forecast resource can be added to management planning");
AssertEqual(50m, viewModel.ManagementResourceAllocationRows.Single()[managementPeriod], "Management allocation table stores percentage by month");
AssertEqual(80m, viewModel.ManagementResourceHoursRows.Single()[managementPeriod], "Management hours use 160 hours per month");
AssertEqual(10000m, viewModel.ManagementResourceCostRows.Single()[managementPeriod], "Management monthly cost uses hours times rate");
managementResource[managementPeriod] = 25m;
AssertEqual(5000m, managementSourceLine[managementPeriod], "Editing management percentage updates the matching forecast month value");
managementSourceLine[managementPeriod] = 2500m;
viewModel.SynchronizeManagementResourcesFromForecastLines([managementSourceLine]);
AssertEqual(12.5m, managementResource[managementPeriod], "Editing forecast month value recalculates management percentage");
viewModel.AddManagementResource(managementSourceLine);
AssertEqual(1, viewModel.ManagementResources.Count, "Management resource cannot be added twice");
var managementProjectPath = Path.Combine(Path.GetTempPath(), $"project-cost-management-{Guid.NewGuid():N}.json");
try
{
    var managementDataset = new ProjectDataset { ManagementResources = [managementResource] };
    new ProjectFileService().Save(managementProjectPath, managementDataset);
    var reloadedManagementResource = new ProjectFileService().Load(managementProjectPath).ManagementResources.Single();
    AssertEqual(12.5m, reloadedManagementResource[managementPeriod], "Management allocations persist in the project file");
    AssertEqual(125m, reloadedManagementResource.HourlyRate, "Management hourly rate persists in the project file");
}
finally
{
    File.Delete(managementProjectPath);
}
var rateViewModel = CreateViewModel();
var ratePeriods = rateViewModel.CtcMonthForecastColumns.Where(column => !column.IsTotal).Select(column => column.Key).TakeLast(2).ToArray();
var ratePreviousPeriod = ratePeriods[0];
var rateLatestPeriod = ratePeriods[1];
var rateLine = new ForecastLine { RowNumber = 900001, TaskNumber = "RATE-001", ResourceName = "Rate Person", ProjectCode = "Rate Test" };
rateLine.MonthlyForecasts.Add(new MonthlyForecast { PeriodLabel = rateLatestPeriod, Amount = 12000m });
rateViewModel.Transactions.Add(new CostTransaction { FyPeriod = ratePreviousPeriod, ManualName = "Rate Person", UnitRate = 150m });
rateViewModel.Transactions.Add(new CostTransaction { FyPeriod = ratePreviousPeriod, ManualName = "Rate Person", UnitRate = 150m });
rateViewModel.Transactions.Add(new CostTransaction { FyPeriod = ratePreviousPeriod, ManualName = "Rate Person", UnitRate = 999m });
rateViewModel.Transactions.Add(new CostTransaction { FyPeriod = rateLatestPeriod, ManualName = "Rate Person", UnitRate = 150m });
rateViewModel.Transactions.Add(new CostTransaction { FyPeriod = rateLatestPeriod, ManualName = "Rate Person", UnitRate = 175m });
AssertEqual(150m, rateViewModel.CalculateManagementResourceDefaultRate(rateLine), "Management default rate uses the most frequent exact raw rate from the last two periods");
var rateResource = rateViewModel.AddManagementResource(rateLine);
AssertEqual(150m, rateResource.HourlyRate, "Management resource starts on the calculated default rate");
AssertEqual(50m, rateResource[rateLatestPeriod], "Management allocation starts from existing forecast value divided by rate times monthly hours");
rateResource.OverrideHourlyRate(175m);
AssertTrue(rateResource.IsHourlyRateOverridden, "Management resource rate override is tracked");
rateViewModel.ResetManagementResourceRate(rateResource);
AssertEqual(150m, rateResource.HourlyRate, "Management resource rate can reset to calculated rate");
var tieLine = new ForecastLine { RowNumber = 900002, TaskNumber = "RATE-002", ResourceName = "Tie Person", ProjectCode = "Rate Test" };
rateViewModel.Transactions.Add(new CostTransaction { FyPeriod = ratePreviousPeriod, ManualName = "Tie Person", UnitRate = 200m });
rateViewModel.Transactions.Add(new CostTransaction { FyPeriod = ratePreviousPeriod, ManualName = "Tie Person", UnitRate = 200m });
rateViewModel.Transactions.Add(new CostTransaction { FyPeriod = rateLatestPeriod, ManualName = "Tie Person", UnitRate = 250m });
rateViewModel.Transactions.Add(new CostTransaction { FyPeriod = rateLatestPeriod, ManualName = "Tie Person", UnitRate = 250m });
AssertEqual(250m, rateViewModel.CalculateManagementResourceDefaultRate(tieLine), "Management default rate tie resolves to the rate most frequent in the latest period");
var batchedAllocationResource = new ManagementResource();
var batchedAllocationNotifications = 0;
batchedAllocationResource.PropertyChanged += (_, eventArgs) =>
{
    if (string.Equals(eventArgs.PropertyName, "Item[]", StringComparison.Ordinal))
    {
        batchedAllocationNotifications++;
    }
};
AssertTrue(
    batchedAllocationResource.SetAllocations(Enumerable.Range(1, 36)
        .Select(month => new KeyValuePair<string, decimal>($"PERIOD-{month}", month))),
    "Management allocation batch reports changed values");
AssertEqual(1, batchedAllocationNotifications, "Management allocation batch raises one indexer notification");

var noOpBatchCollection = new BatchObservableCollection<string>(["One", "Two"]);
var noOpBatchCollectionResets = 0;
noOpBatchCollection.CollectionChanged += (_, _) => noOpBatchCollectionResets++;
noOpBatchCollection.ReplaceWith(["One", "Two"]);
AssertEqual(0, noOpBatchCollectionResets, "Identical batch replacement skips the collection reset");
noOpBatchCollection.ReplaceWith(["One", "Changed"]);
AssertEqual(1, noOpBatchCollectionResets, "Changed batch replacement raises one collection reset");
var hoveredLine = viewModel.ForecastLines.Single(line =>
    string.Equals(line.TaskNumber, "WA57102001", StringComparison.OrdinalIgnoreCase)
    && string.Equals(line.ResourceName, "Flex Projects L", StringComparison.OrdinalIgnoreCase));
viewModel.SelectedForecastLine = viewModel.ForecastLines.Single(line =>
    string.Equals(line.TaskNumber, "WA57102001", StringComparison.OrdinalIgnoreCase)
    && string.Equals(line.ResourceName, "Stanley Drake", StringComparison.OrdinalIgnoreCase));
var commentLine = viewModel.SelectedForecastLine;
commentLine.MonthlyCommentHistory.Clear();
commentLine.MonthlyCommentHistory.Add(new ForecastMonthlyComment
{
    PeriodLabel = "26-08",
    MonthLabel = "Feb 26",
    ResourceName = commentLine.ResourceName,
    Text = "Earlier comment",
    RecordedAt = DateTime.Today.AddMonths(-1)
});
viewModel.SaveForecastLineCommentEditor(
    commentLine,
    ResourceCommentMetricPreference.CreateDefaults(),
    "additional cost due to more effort required",
    "month pressure",
    string.Empty);
var currentCommentMonthLabel = dataset.ForecastPeriods
    .First(period => string.Equals(period.Label, viewModel.Header.CurrentPeriod, StringComparison.OrdinalIgnoreCase))
    .StartDate
    ?.ToString("MMM yy") ?? string.Empty;
AssertTrue(commentLine.AllMonthComments.StartsWith($"{currentCommentMonthLabel} - FY {viewModel.Header.CurrentPeriod}: Stanley Drake:", StringComparison.Ordinal), "All-month comments show newest month first with fiscal period and resource");
AssertTrue(commentLine.AllMonthComments.Contains("additional cost due to more effort required; month pressure", StringComparison.Ordinal), "All variance comment types are captured in monthly history");
var commentReportRow = viewModel.MonthlyReportVarianceCommentRows.Single(row => string.Equals(row.ProjectCode, commentLine.ReportingCategory, StringComparison.OrdinalIgnoreCase));
AssertTrue(commentReportRow.TotalBudgetVarianceComment.Contains("Stanley Drake: additional cost due to more effort required", StringComparison.Ordinal), "Report total-budget comment identifies the resource");
AssertTrue(commentReportRow.MonthVarianceComment.Contains("Stanley Drake: month pressure", StringComparison.Ordinal), "Report month comment identifies the resource");
AssertTrue(commentReportRow.AllMonthComments.StartsWith($"{currentCommentMonthLabel} - FY {viewModel.Header.CurrentPeriod}: Stanley Drake:", StringComparison.Ordinal), "Report all-month comments show newest month first");
viewModel.SaveManualForecastComment(commentLine, "Manual forecast explanation");
AssertTrue(commentLine.UseManualAllMonthComment, "Saving a manual comment enables manual mode");
AssertTrue(commentLine.AllMonthComments.Contains("Stanley Drake: Manual forecast explanation", StringComparison.Ordinal), "Manual comment overrides pulled-through display text");
viewModel.SetForecastCommentMode(commentLine, false);
AssertTrue(!commentLine.UseManualAllMonthComment, "Comment mode can return to pulled-through comments");
AssertEqual("Manual forecast explanation", commentLine.ManualAllMonthComment, "Returning to auto mode retains the manual comment");
viewModel.SetForecastCommentMode(commentLine, true);
var frontHeavyCurve = ForecastCurvePresets.Apply(ForecastCurvePresets.FrontHeavy, [100m, 100m, 100m, 100m]);
AssertEqual(400m, frontHeavyCurve.Sum(), "Curve presets preserve the selected forecast total");
AssertTrue(frontHeavyCurve[0] > frontHeavyCurve[^1], "Front-heavy preset places more forecast in early months");
var backHeavyCurve = ForecastCurvePresets.Apply(ForecastCurvePresets.BackHeavy, [100m, 100m, 100m, 100m]);
AssertTrue(backHeavyCurve[0] < backHeavyCurve[^1], "Back-heavy preset places more forecast in later months");
var cumulativeCurve = ForecastCurveMath.BuildCumulative([6000m, 6000m, 7200m]);
AssertTrue(cumulativeCurve.SequenceEqual([6000m, 12000m, 19200m]), "Curve graph converts monthly values to cumulative values");
var movedCumulativeCurve = ForecastCurveMath.MoveCumulativePoint(cumulativeCurve, 1, 15000m);
var movedMonthlyCurve = ForecastCurveMath.ToMonthlyValues(movedCumulativeCurve);
AssertTrue(movedMonthlyCurve.SequenceEqual([6000m, 9000m, 4200m]), "Moving an interior cumulative marker adjusts adjacent monthly values");
AssertEqual(19200m, movedMonthlyCurve.Sum(), "Moving an interior curve marker preserves the later cumulative total");
var clampedCumulativeCurve = ForecastCurveMath.MoveCumulativePoint(cumulativeCurve, 1, 25000m);
AssertEqual(19200m, clampedCumulativeCurve[1], "Cumulative marker cannot move above the following month");
var smoothCurve = ForecastCurveMath.AdjustMonthlyCurve([100m, 100m, 100m, 100m, 100m, 100m], 2, 350m, 2);
AssertEqual(600m, smoothCurve.Sum(), "Smooth curve adjustment preserves the selected range total");
AssertTrue(smoothCurve[1] != 100m && smoothCurve[2] != 100m && smoothCurve[3] != 100m && smoothCurve[4] != 100m, "Nearby adjustment affects months on both sides of the marker");
AssertTrue(smoothCurve.All(value => value >= 0), "Smooth curve adjustment does not create negative months");
var wideCurve = ForecastCurveMath.AdjustMonthlyCurve([100m, 100m, 100m, 100m, 100m, 100m], 2, 350m, 4);
AssertTrue(wideCurve[0] != 100m && wideCurve[5] != 100m, "Wide adjustment reaches farther months");
var lockedCurve = ForecastCurveMath.AdjustMonthlyCurve([100m, 100m, 100m, 100m], 1, 260m, 4, [false, true, false, false]);
AssertEqual(100m, lockedCurve[1], "Locked curve months stay fixed during adjustment");
AssertEqual(400m, lockedCurve.Sum(), "Locked curve adjustment redistributes value across unlocked months");
var capturedCurveShape = ForecastCurvePresets.CaptureShape([10m, 30m, 60m]);
AssertEqual(1m, capturedCurveShape.Sum(), "User curve preset captures normalized shape only");
var userCurvePreset = new UserForecastCurvePreset { Name = "Test shape", MonthCount = 3, Weights = capturedCurveShape.ToList() };
var appliedUserCurve = ForecastCurvePresets.ApplyUserPreset(userCurvePreset, [100m, 100m, 100m]);
AssertEqual(300m, appliedUserCurve.Sum(), "User curve preset scales to the current selected total");
AssertTrue(appliedUserCurve[0] < appliedUserCurve[1] && appliedUserCurve[1] < appliedUserCurve[2], "User curve preset keeps the saved shape");
var savedUserPresetCount = viewModel.UserForecastCurvePresets.Count;
var savedUserPreset = viewModel.SaveForecastCurvePreset("Reusable curve", "test note", "Stanley Drake", 300m, capturedCurveShape);
AssertEqual(savedUserPresetCount + 1, viewModel.UserForecastCurvePresets.Count, "User curve preset is stored at app-preference level");
AssertEqual("Reusable curve", savedUserPreset.Name, "User curve preset keeps its name metadata");
viewModel.DeleteForecastCurvePreset(savedUserPreset);
AssertEqual(savedUserPresetCount, viewModel.UserForecastCurvePresets.Count, "User curve preset can be deleted");
viewModel.SetHoveredForecastLine(hoveredLine);
AssertEqual("Flex Projects L / WA57102001", viewModel.LedgerTitle, "Hovering a row previews that resource drilldown");
viewModel.ClearHoveredForecastLine();
AssertEqual("Stanley Drake / WA57102001", viewModel.LedgerTitle, "Clearing hover returns drilldown to the selected row");
AssertEqual("Oct 25\n26-04", viewModel.LedgerChartXAxisLabels.First().Text, "Spend curve starts at first project cost month");
AssertEqual("Oct 26\n27-04", viewModel.LedgerChartXAxisLabels.Last().Text, "Spend curve trims trailing zero forecast periods");
AssertTrue(viewModel.LedgerChartXAxisLabels.All(label => label.Text.Contains('\n')), "Spend curve labels include calendar month and FY period");
AssertEqual(13, viewModel.LedgerChartXAxisLabels.Count, "Spend curve shows one x-axis tick per month in range");
AssertTrue(viewModel.LedgerChartCanvasWidth > 800, "Spend curve widens beyond viewport so monthly labels can scroll");
AssertTrue(viewModel.LedgerChartXAxisLabels.All(label =>
{
    var calendarLabel = label.Text.Split('\n')[0];
    return !calendarLabel.EndsWith("27", StringComparison.OrdinalIgnoreCase)
        && !calendarLabel.EndsWith("28", StringComparison.OrdinalIgnoreCase);
}), "Spend curve axis excludes zero-only future years");

var autoImportPath = Path.Combine(Path.GetTempPath(), $"project-cost-forecast-auto-import-{Guid.NewGuid():N}.csv");
File.WriteAllText(
    autoImportPath,
    "FY-Period,Task Numb,Period,Doc Date,Units,Unit Rate,Amount,Cost Ledg,Cost Acco,Project Co,Parent Pro,Resource C,Resource D,Source,PO Numb,PO Comm,Supplier N,Narrative 1,Narrative 2,Narrative 3,Who,ECM Number" + Environment.NewLine
    + "26-11,WA57102001,11,2026-05-01,1,123,123,26PRJA,10WA402,WA571,WA57P,90001,Auto Match Person,TC,PO-AUTO,Comment Auto,Supplier Auto,AUTO/01/05/2026/1/1,AUTO COST POSTING,,Auto Match Person,ECM-AUTO-001");
var transactionCountBeforeAutoImport = viewModel.Transactions.Count;
viewModel.ImportTransactionFile(autoImportPath);
File.Delete(autoImportPath);
var autoImportedTransaction = viewModel.Transactions.Single(t => string.Equals(t.EcmNumber, "ECM-AUTO-001", StringComparison.OrdinalIgnoreCase));
AssertEqual(transactionCountBeforeAutoImport + 1, viewModel.Transactions.Count, "Auto import adds the matching Who/resource transaction");
AssertEqual("Auto Match Person", autoImportedTransaction.ManualName, "Who/resource match auto-populates manual name during import");
AssertEqual("Auto Match Person", autoImportedTransaction.LedgerResourceName, "Auto import uses the Who/resource match as the ledger resource name");
var autoCreatedForecastLine = viewModel.ForecastLines.Single(line =>
    string.Equals(line.TaskNumber, autoImportedTransaction.TaskNumber, StringComparison.OrdinalIgnoreCase)
    && string.Equals(line.ResourceName, autoImportedTransaction.LedgerResourceName, StringComparison.OrdinalIgnoreCase));
AssertEqual("WA571", autoCreatedForecastLine.TransactionProjectCode, "Import-created forecast line records its transaction project");
var anchoredManualLine = viewModel.InsertForecastLine(autoCreatedForecastLine, below: true);
AssertEqual(autoCreatedForecastLine.TransactionProjectCode, anchoredManualLine.TransactionProjectCode, "Manual line anchored to a forecast line copies transaction project attribution");
viewModel.DeleteForecastLine(anchoredManualLine);
var legacyAnchor = viewModel.ForecastLines.First(line => line.TransactionProjectCode is null);
var legacyAnchoredManualLine = viewModel.InsertForecastLine(legacyAnchor, below: true);
AssertTrue(legacyAnchoredManualLine.TransactionProjectCode is null, "Manual line anchored to a legacy line preserves aggregate attribution");
viewModel.DeleteForecastLine(legacyAnchoredManualLine);
var unanchoredManualLine = viewModel.InsertForecastLine(null, below: true);
AssertTrue(unanchoredManualLine.TransactionProjectCode is null, "Unanchored manual line starts with legacy aggregate attribution");
viewModel.DeleteForecastLine(unanchoredManualLine);
var autoCreatePreviewRows = viewModel.BuildForecastLineAutoCreatePreviewItems(
[
    new CostTransaction
    {
        TaskNumber = "WA57109999",
        ProjectCode = "WA571",
        ManualName = "Preview Person",
        Source = "TC",
        Amount = 200m
    },
    new CostTransaction
    {
        TaskNumber = "WA57109999",
        ProjectCode = "WA571",
        ManualName = "Preview Person",
        Source = "AP",
        Amount = 300m
    }
]);
AssertEqual(1, autoCreatePreviewRows.Count, "Preview groups new transactions by task, manual name, and project");
AssertEqual("WA57109999", autoCreatePreviewRows[0].TaskNumber, "Preview row shows the task code");
AssertEqual("Preview Person", autoCreatePreviewRows[0].ManualName, "Preview row shows the manual name");
AssertEqual("WA571", autoCreatePreviewRows[0].ProjectCode, "Preview row shows the project code");
AssertEqual(500m, autoCreatePreviewRows[0].Amount, "Preview row totals the grouped amount");
AssertEqual(2, autoCreatePreviewRows[0].TransactionCount, "Preview row counts grouped transactions");
AssertTrue(autoCreatePreviewRows[0].Source.Contains("AP", StringComparison.Ordinal) && autoCreatePreviewRows[0].Source.Contains("TC", StringComparison.Ordinal), "Preview row combines grouped sources");
var unmatchedImportPath = Path.Combine(Path.GetTempPath(), $"project-cost-unmatched-{Guid.NewGuid():N}.json");
try
{
    var unmatchedDataset = new ProjectDataset
    {
        UnmatchedImportCombinations =
        [
            new UnmatchedImportCombination
            {
                TaskNumber = "WA57109999",
                ManualName = "Preview Person",
                ProjectCode = "WA571",
                Category = "WA571",
                Source = "AP, TC",
                Amount = 500m,
                TransactionCount = 2
            }
        ]
    };
    new ProjectFileService().Save(unmatchedImportPath, unmatchedDataset);
    var reloadedUnmatched = new ProjectFileService().Load(unmatchedImportPath);
    AssertEqual(1, reloadedUnmatched.UnmatchedImportCombinations.Count, "Unmatched import combinations persist in the project file");
    AssertEqual("Preview Person", reloadedUnmatched.UnmatchedImportCombinations[0].ManualName, "Unmatched import combination keeps its manual name");
}
finally
{
    File.Delete(unmatchedImportPath);
}

viewModel.ActiveWorkspaceKey = "Resources";
var defaultResourceView = viewModel.SelectedWorkspaceView!;
viewModel.SetSelectedWorkspaceHiddenColumnKeys(["Units", "Amount"]);
viewModel.SetSelectedWorkspaceColumnLayouts([
    new WorkspaceColumnLayout { Key = "Resource", Width = 215, DisplayIndex = 0 },
    new WorkspaceColumnLayout { Key = "Amount", Width = 124, DisplayIndex = 1 }
]);
AssertTrue(defaultResourceView.HiddenColumnKeys.SequenceEqual(["Amount", "Units"], StringComparer.OrdinalIgnoreCase), "Workspace view stores its hidden column list");
AssertEqual(215d, defaultResourceView.ColumnLayouts.Single(layout => layout.Key == "Resource").Width, "Workspace view stores column width");
AssertEqual(1, defaultResourceView.ColumnLayouts.Single(layout => layout.Key == "Amount").DisplayIndex, "Workspace view stores column order");
viewModel.AddWorkspaceViewCommand.Execute(null);
var customResourceView = viewModel.SelectedWorkspaceView!;
AssertTrue(customResourceView.HiddenColumnKeys.SequenceEqual(defaultResourceView.HiddenColumnKeys, StringComparer.OrdinalIgnoreCase), "New workspace view inherits the current column layout");
AssertTrue(customResourceView.ColumnLayouts.Select(layout => layout.Key).SequenceEqual(defaultResourceView.ColumnLayouts.Select(layout => layout.Key), StringComparer.OrdinalIgnoreCase), "New workspace view inherits column order and widths");
viewModel.SetSelectedWorkspaceHiddenColumnKeys(["Tasks"]);
viewModel.SetSelectedWorkspaceColumnLayouts([
    new WorkspaceColumnLayout { Key = "Tasks", Width = 301, DisplayIndex = 0 }
]);
viewModel.SelectedWorkspaceView = defaultResourceView;
AssertTrue(defaultResourceView.HiddenColumnKeys.SequenceEqual(["Amount", "Units"], StringComparer.OrdinalIgnoreCase), "Original workspace view keeps its own hidden columns");
AssertTrue(defaultResourceView.ColumnLayouts.Any(layout => layout.Key == "Resource" && Math.Abs(layout.Width - 215d) < 0.01), "Original workspace view keeps its own column layout");
viewModel.SelectedWorkspaceView = customResourceView;
AssertTrue(customResourceView.HiddenColumnKeys.SequenceEqual(["Tasks"], StringComparer.OrdinalIgnoreCase), "Custom workspace view keeps an independent hidden column layout");
AssertTrue(customResourceView.ColumnLayouts.Any(layout => layout.Key == "Tasks" && Math.Abs(layout.Width - 301d) < 0.01), "Custom workspace view keeps an independent column width");

var headerColorPersistencePath = Path.Combine(Path.GetTempPath(), $"project-cost-header-colours-{Guid.NewGuid():N}.json");
try
{
    var headerColorDataset = new ProjectDataset();
    headerColorDataset.ForecastCalendarYearHeaderColorHexes["Calendar year 2026"] = "#CFE5FA";
    headerColorDataset.ForecastFiscalYearHeaderColorHexes["FY27"] = "#F0D37A";
    headerColorDataset.ForecastGroupHeaderColorHexes["Project Management"] = "#D7ECCF";
    new ProjectFileService().Save(headerColorPersistencePath, headerColorDataset);
    var reloadedHeaderColorDataset = new ProjectFileService().Load(headerColorPersistencePath);
    AssertEqual("#CFE5FA", reloadedHeaderColorDataset.ForecastCalendarYearHeaderColorHexes["Calendar year 2026"], "Calendar year header colour persists in the project file");
    AssertEqual("#F0D37A", reloadedHeaderColorDataset.ForecastFiscalYearHeaderColorHexes["FY27"], "Fiscal year header colour persists in the project file");
    AssertEqual("#D7ECCF", reloadedHeaderColorDataset.ForecastGroupHeaderColorHexes["Project Management"], "Forecast group header colour persists in the project file");
}
finally
{
    File.Delete(headerColorPersistencePath);
}

var migratedMonthWidth = InvokeForecastWidthMigration(savedWidth: 112d, currentWidth: 78d, minWidth: 70d, isTotal: false);
AssertNearlyEqual(78d, migratedMonthWidth, 0.01, "Legacy saved forecast month width migrates to the new default width");
var migratedTotalMonthWidth = InvokeForecastWidthMigration(savedWidth: 120d, currentWidth: 96d, minWidth: 84d, isTotal: true);
AssertNearlyEqual(96d, migratedTotalMonthWidth, 0.01, "Legacy saved forecast total width migrates to the new default width");
var preservedCustomMonthWidth = InvokeForecastWidthMigration(savedWidth: 143d, currentWidth: 78d, minWidth: 70d, isTotal: false);
AssertNearlyEqual(143d, preservedCustomMonthWidth, 0.01, "User-resized forecast month widths are preserved during migration");
var resetWidthColumn = new DataGridTextColumn { Header = "Reset width test", Width = 137d };
InvokeEnsureColumnPresentation(resetWidthColumn);
resetWidthColumn.Width = 243d;
AssertTrue(InvokeResetColumnWidthToDefault(resetWidthColumn), "Resizable column can reset to its configured default width");
AssertNearlyEqual(137d, resetWidthColumn.Width.DisplayValue, 0.01, "Column width reset restores the configured default width");

var forecastVirtualization = GetForecastGridVirtualizationSettings();
AssertTrue(forecastVirtualization.Rows, "Forecast grid keeps row virtualization enabled");
AssertTrue(forecastVirtualization.Columns, "Forecast grid keeps column virtualization enabled");
AssertTrue(forecastVirtualization.Panel, "Forecast grid uses a virtualizing items panel");
AssertTrue(forecastVirtualization.Grouping, "Grouped forecast rows remain virtualized");

RunSharedGridControlRegressionTests(root);

var resizableForecastLine = new ForecastLine();
AssertTrue(!resizableForecastLine.HasCustomRowHeight, "Forecast rows use the grid default height until resized");
resizableForecastLine.SetRowDisplayHeight(96);
AssertTrue(resizableForecastLine.HasCustomRowHeight, "Resizing records a row-specific height");
AssertNearlyEqual(96d, resizableForecastLine.RowDisplayHeight, 0.01, "Row-specific height is stored on the row data");
resizableForecastLine.SetRowDisplayHeight(1);
AssertNearlyEqual(30d, resizableForecastLine.RowDisplayHeight, 0.01, "Row resize clamps to the usable minimum height");
resizableForecastLine.SetRowDisplayHeight(999);
AssertNearlyEqual(600d, resizableForecastLine.RowDisplayHeight, 0.01, "Row resize clamps to the usable maximum height");

viewModel.ActiveWorkspaceKey = "CTC Forecast";
var defaultForecastView = viewModel.SelectedWorkspaceView!;
viewModel.GroupForecastLinesByTask = true;
viewModel.SetSelectedForecastShowZeroAsBlank(false);
AssertTrue(defaultForecastView.GroupForecastLinesByTask, "Forecast view stores group by task per view");
AssertTrue(!defaultForecastView.ShowZeroAsBlank, "Forecast view stores show-zero-as-blank per view");
viewModel.AddWorkspaceViewCommand.Execute(null);
var customForecastView = viewModel.SelectedWorkspaceView!;
AssertTrue(customForecastView.GroupForecastLinesByTask, "New forecast view inherits group by task from its source view");
AssertTrue(!customForecastView.ShowZeroAsBlank, "New forecast view inherits show-zero-as-blank from its source view");
viewModel.GroupForecastLinesByTask = false;
viewModel.SetSelectedForecastShowZeroAsBlank(true);
viewModel.SelectedWorkspaceView = defaultForecastView;
AssertTrue(viewModel.GroupForecastLinesByTask, "Returning to the original forecast view restores its grouping state");
AssertTrue(!viewModel.ShowForecastZeroAsBlank, "Returning to the original forecast view restores zero display state");
viewModel.SelectedWorkspaceView = customForecastView;
AssertTrue(!viewModel.GroupForecastLinesByTask, "New forecast view keeps an independent grouping state");
AssertTrue(viewModel.ShowForecastZeroAsBlank, "New forecast view keeps an independent zero display state");

viewModel.ActiveDetailWorkspaceKey = "Ledger Costs";
var defaultLedgerCostView = viewModel.SelectedDetailWorkspaceView!;
viewModel.SetSelectedDetailWorkspaceHiddenColumnKeys(["Supplier", "Narrative 2"]);
AssertTrue(defaultLedgerCostView.HiddenColumnKeys.SequenceEqual(["Narrative 2", "Supplier"], StringComparer.OrdinalIgnoreCase), "Detail workspace view stores its hidden column list");
viewModel.AddDetailWorkspaceViewCommand.Execute(null);
var customLedgerCostView = viewModel.SelectedDetailWorkspaceView!;
AssertTrue(customLedgerCostView.HiddenColumnKeys.SequenceEqual(defaultLedgerCostView.HiddenColumnKeys, StringComparer.OrdinalIgnoreCase), "New detail workspace view inherits the current column layout");
viewModel.SetSelectedDetailWorkspaceHiddenColumnKeys(["ECM Number"]);
viewModel.SelectedDetailWorkspaceView = defaultLedgerCostView;
AssertTrue(defaultLedgerCostView.HiddenColumnKeys.SequenceEqual(["Narrative 2", "Supplier"], StringComparer.OrdinalIgnoreCase), "Original detail workspace view keeps its own hidden columns");
viewModel.SelectedDetailWorkspaceView = customLedgerCostView;
AssertTrue(customLedgerCostView.HiddenColumnKeys.SequenceEqual(["ECM Number"], StringComparer.OrdinalIgnoreCase), "Custom detail workspace view keeps an independent hidden column layout");
viewModel.SelectedForecastLine = viewModel.ForecastLines.Single(line =>
    string.Equals(line.TaskNumber, "WA57102001", StringComparison.OrdinalIgnoreCase)
    && string.Equals(line.ResourceName, "Stanley Drake", StringComparison.OrdinalIgnoreCase));
viewModel.SetSelectedDetailWorkspaceContentKey("PivotByMonth");
AssertTrue(viewModel.ShowLedgerCostsPivotByMonth, "Detail workspace can switch to pivot-by-month mode");
AssertEqual("26-07", viewModel.LedgerMonthlyPivotPeriods.First(), "Ledger pivot range starts at Stanley Drake's first cost month");
AssertTrue(viewModel.LedgerMonthlyPivotPeriods.Contains("26-08"), "Ledger pivot range includes Stanley Drake's August costs");
AssertTrue(viewModel.LedgerMonthlyPivotPeriods.Contains("26-09"), "Ledger pivot range includes Stanley Drake's September costs");
AssertEqual(viewModel.Header.CurrentPeriod, viewModel.LedgerMonthlyPivotPeriods.Last(), "Ledger pivot range extends through the current period");
AssertEqual(15000m, viewModel.LedgerMonthlyPivotRows.Single(row =>
    string.Equals(row.TaskNumber, "WA57102001", StringComparison.OrdinalIgnoreCase)
    && string.Equals(row.ResourceName, "Stanley Drake", StringComparison.OrdinalIgnoreCase)).Total, "Ledger pivot keeps the Stanley Drake total");
viewModel.ActiveWorkspaceKey = "Raw Transactions";
viewModel.SetSelectedWorkspaceContentKey("PivotByMonth");
AssertTrue(viewModel.ShowRawTransactionsPivotByMonth, "Raw transactions workspace can switch to pivot-by-month mode");
AssertTrue(viewModel.RawTransactionsMonthlyPivotPeriods.Contains("26-08"), "Raw transactions pivot includes August costs");
AssertTrue(viewModel.RawTransactionsMonthlyPivotPeriods.Contains("26-09"), "Raw transactions pivot includes September costs");
AssertEqual(viewModel.Header.CurrentPeriod, viewModel.RawTransactionsMonthlyPivotPeriods.Last(), "Raw transactions pivot range extends through the current period");
viewModel.SetSelectedWorkspaceContentKey("GroupByMonth");
AssertTrue(viewModel.ShowRawTransactionsGroupedByMonth, "Raw transactions workspace can switch to group-by-month mode");
AssertTrue(!viewModel.ShowRawTransactionsPivotByMonth, "Raw transactions group-by-month mode exits pivot mode");
var pivotBuilderViewModel = CreateViewModel();
var projectCodePivotField = pivotBuilderViewModel.PivotFields.Single(field => field.Key == "ProjectCode");
AssertTrue(!pivotBuilderViewModel.TryAddPivotFieldToArea("Filters", projectCodePivotField), "Pivot builder rejects duplicate fields across areas");
var projectCodeRowField = pivotBuilderViewModel.PivotRowFields.Single(field => field.Key == "ProjectCode");
AssertTrue(pivotBuilderViewModel.TryMovePivotFieldToArea("Filters", projectCodeRowField), "Pivot builder moves fields between areas");
AssertTrue(pivotBuilderViewModel.PivotFilterFields.Any(field => field.Key == "ProjectCode"), "Moved pivot field lands in the target area");
AssertTrue(!pivotBuilderViewModel.PivotRowFields.Any(field => field.Key == "ProjectCode"), "Moved pivot field leaves the source area");
AssertEqual("Sum of Amount", pivotBuilderViewModel.PivotValueFields.Single(field => field.Key == "Amount").DisplayName, "Pivot value fields default to Sum");

var rawImportPath = Path.Combine(Path.GetTempPath(), $"project-cost-forecast-import-{Guid.NewGuid():N}.csv");
File.WriteAllText(
    rawImportPath,
    "FY-Period,Task Numb,Period,Doc Date,Units,Unit Rate,Amount,Cost Ledg,Cost Acco,Project Co,Parent Pro,Resource C,Resource D,Source,PO Numb,PO Comm,Supplier N,Narrative 1,Narrative 2,Narrative 3,Who,ECM Number" + Environment.NewLine
    + "26-08,WA57102001,8,2026-02-08,3,150,450,26PRJA,10WA402,WA571,WA57P,10732,Stanley Drake,TC,PO-1,Comment A,Supplier A,10732/08/02/2026/13/1,DETAILED COST POSTING,,Stanley Drake,7597308");
var importedRows = new CsvTransactionService().Import(rawImportPath, 42);
File.Delete(rawImportPath);
var imported = importedRows.Single();
AssertEqual(42, imported.RowNumber, "Raw import starting row number");
AssertEqual("WA57102001", imported.TaskNumber, "Raw import truncated task header");
AssertEqual("WA571", imported.ProjectCode, "Raw import truncated project header");
AssertEqual("WA57P", imported.ParentProjectCode, "Raw import truncated parent project header");
AssertEqual("10732", imported.ResourceCode, "Raw import resource code header");
AssertEqual("Stanley Drake", imported.ResourceDescription, "Raw import resource description header");
AssertEqual("Supplier A", imported.SupplierName, "Raw import supplier header");
AssertEqual("DETAILED COST POSTING", imported.Narrative2, "Raw import narrative 2 header");
AssertEqual("7597308", imported.EcmNumber, "Raw import ECM number header");

var multilineCsvPath = Path.Combine(Path.GetTempPath(), $"project-cost-forecast-multiline-{Guid.NewGuid():N}.csv");
var multilineComment = "First line, with comma" + Environment.NewLine + "Second line with \"quoted text\"";
try
{
    var csvService = new CsvTransactionService();
    csvService.ExportTransactions(
        multilineCsvPath,
        [new CostTransaction { TaskNumber = "CSV-1", ProjectCode = "CSV-PROJECT", ManualName = "CSV Resource", PoComments = multilineComment, Amount = 42m }]);
    var multilineRoundTrip = csvService.Import(multilineCsvPath, 1).Single();
    AssertEqual(multilineComment, multilineRoundTrip.PoComments, "CSV import preserves quoted multiline fields");
    AssertEqual("CSV Resource", multilineRoundTrip.ManualName, "CSV multiline record keeps following columns aligned");
    AssertEqual(42m, multilineRoundTrip.Amount, "CSV multiline record keeps numeric columns aligned");
}
finally
{
    File.Delete(multilineCsvPath);
}

var excelImportPath = Path.Combine(Path.GetTempPath(), $"project-cost-forecast-import-{Guid.NewGuid():N}.xlsx");
using (var workbook = new XLWorkbook())
{
    var sheet = workbook.Worksheets.Add("Transactions");
    sheet.Cell(1, 1).Value = "FY-Period";
    sheet.Cell(1, 2).Value = "Task Numb";
    sheet.Cell(1, 3).Value = "Period";
    sheet.Cell(1, 4).Value = "Doc Date";
    sheet.Cell(1, 5).Value = "Units";
    sheet.Cell(1, 6).Value = "Unit Rate";
    sheet.Cell(1, 7).Value = "Amount";
    sheet.Cell(1, 8).Value = "Cost Ledg";
    sheet.Cell(1, 9).Value = "Cost Acco";
    sheet.Cell(1, 10).Value = "Project Co";
    sheet.Cell(1, 11).Value = "Parent Pro";
    sheet.Cell(1, 12).Value = "Resource C";
    sheet.Cell(1, 13).Value = "Resource D";
    sheet.Cell(1, 14).Value = "Source";
    sheet.Cell(1, 15).Value = "PO Numb";
    sheet.Cell(1, 16).Value = "PO Comm";
    sheet.Cell(1, 17).Value = "Supplier N";
    sheet.Cell(1, 18).Value = "Narrative 1";
    sheet.Cell(1, 19).Value = "Narrative 2";
    sheet.Cell(1, 20).Value = "Narrative 3";
    sheet.Cell(1, 21).Value = "Who";
    sheet.Cell(1, 22).Value = "ECM Number";
    sheet.Cell(2, 1).Value = "26-08";
    sheet.Cell(2, 2).Value = "WA57102001";
    sheet.Cell(2, 3).Value = 8;
    sheet.Cell(2, 4).Value = new DateTime(2026, 2, 8);
    sheet.Cell(2, 4).Style.DateFormat.Format = "yyyy-MM-dd";
    sheet.Cell(2, 5).Value = 3;
    sheet.Cell(2, 6).Value = 150;
    sheet.Cell(2, 7).Value = 450;
    sheet.Cell(2, 8).Value = "26PRJA";
    sheet.Cell(2, 9).Value = "10WA402";
    sheet.Cell(2, 10).Value = "WA571";
    sheet.Cell(2, 11).Value = "WA57P";
    sheet.Cell(2, 12).Value = "10732";
    sheet.Cell(2, 13).Value = "Stanley Drake";
    sheet.Cell(2, 14).Value = "TC";
    sheet.Cell(2, 15).Value = "PO-1";
    sheet.Cell(2, 16).Value = "Comment A";
    sheet.Cell(2, 17).Value = "Supplier A";
    sheet.Cell(2, 18).Value = "10732/08/02/2026/13/1";
    sheet.Cell(2, 19).Value = "DETAILED COST POSTING";
    sheet.Cell(2, 20).Value = string.Empty;
    sheet.Cell(2, 21).Value = "Stanley Drake";
    sheet.Cell(2, 22).Value = "7597308";
    workbook.SaveAs(excelImportPath);
}

var importedWorkbookRows = new CsvTransactionService().Import(excelImportPath, 77);
File.Delete(excelImportPath);
var importedWorkbookRow = importedWorkbookRows.Single();
AssertEqual(77, importedWorkbookRow.RowNumber, "Excel import starting row number");
AssertEqual(imported.TaskNumber, importedWorkbookRow.TaskNumber, "Excel import task number");
AssertEqual(imported.ProjectCode, importedWorkbookRow.ProjectCode, "Excel import project code");
AssertEqual(imported.ParentProjectCode, importedWorkbookRow.ParentProjectCode, "Excel import parent project code");
AssertEqual(imported.ResourceCode, importedWorkbookRow.ResourceCode, "Excel import resource code");
AssertEqual(imported.ResourceDescription, importedWorkbookRow.ResourceDescription, "Excel import resource description");
AssertEqual(imported.SupplierName, importedWorkbookRow.SupplierName, "Excel import supplier name");
AssertEqual(imported.Narrative2, importedWorkbookRow.Narrative2, "Excel import narrative 2");
AssertEqual(imported.EcmNumber, importedWorkbookRow.EcmNumber, "Excel import ECM number");

var sameNamedCost = new CostTransaction
{
    FyPeriod = imported.FyPeriod,
    TaskNumber = imported.TaskNumber,
    Period = imported.Period,
    DocDate = imported.DocDate,
    Units = imported.Units,
    UnitRate = imported.UnitRate,
    Amount = imported.Amount,
    CostLedger = imported.CostLedger,
    CostAccount = imported.CostAccount,
    ProjectCode = imported.ProjectCode,
    ParentProjectCode = imported.ParentProjectCode,
    ResourceCode = imported.ResourceCode,
    ResourceDescription = imported.ResourceDescription,
    Source = imported.Source,
    PoNumber = imported.PoNumber,
    PoComments = imported.PoComments,
    SupplierName = imported.SupplierName,
    Narrative1 = imported.Narrative1,
    Narrative2 = imported.Narrative2,
    Narrative3 = imported.Narrative3,
    Who = imported.Who,
    EcmNumber = "DIFFERENT"
};
AssertEqual(CsvTransactionService.BuildNameMappingKey(imported), CsvTransactionService.BuildNameMappingKey(sameNamedCost), "Cost centre name mapping ignores invoice-only fields");
AssertTrue(!string.Equals(CsvTransactionService.BuildDuplicateKey(imported), CsvTransactionService.BuildDuplicateKey(sameNamedCost), StringComparison.OrdinalIgnoreCase), "Duplicate key separates different raw costs");

var differentSupplierSamePerson = new CostTransaction
{
    ResourceDescription = imported.ResourceDescription,
    SupplierName = "Different supplier",
    Narrative1 = imported.Narrative1,
    Narrative2 = imported.Narrative2,
    Narrative3 = imported.Narrative3,
    Who = imported.Who
};
AssertEqual(CsvTransactionService.BuildNameMappingKey(imported), CsvTransactionService.BuildNameMappingKey(differentSupplierSamePerson), "Cost centre name mapping uses resource description and narratives instead of supplier");

var differentResourceDescription = new CostTransaction
{
    ResourceDescription = "Someone Else",
    Narrative1 = imported.Narrative1,
    Narrative2 = imported.Narrative2,
    Narrative3 = imported.Narrative3,
    Who = imported.Who
};
AssertTrue(!string.Equals(CsvTransactionService.BuildNameMappingKey(imported), CsvTransactionService.BuildNameMappingKey(differentResourceDescription), StringComparison.OrdinalIgnoreCase), "Cost centre name mapping separates different resource descriptions");

var differentNarrative1SamePerson = new CostTransaction
{
    ResourceDescription = imported.ResourceDescription,
    Narrative1 = "1406/30/07/2017/4/1",
    Narrative2 = imported.Narrative2,
    Narrative3 = imported.Narrative3,
    Who = imported.Who
};
AssertEqual(CsvTransactionService.BuildNameMappingKey(imported), CsvTransactionService.BuildNameMappingKey(differentNarrative1SamePerson), "Cost centre name mapping ignores Narrative 1 changes");

var dashPlaceholderRow = new CostTransaction
{
    ResourceDescription = imported.ResourceDescription,
    Narrative1 = imported.Narrative1,
    Narrative2 = imported.Narrative2,
    Narrative3 = "-",
    Who = imported.Who
};
var blankPlaceholderRow = new CostTransaction
{
    ResourceDescription = imported.ResourceDescription,
    Narrative1 = imported.Narrative1,
    Narrative2 = imported.Narrative2,
    Narrative3 = string.Empty,
    Who = imported.Who
};
AssertEqual(CsvTransactionService.BuildNameMappingKey(dashPlaceholderRow), CsvTransactionService.BuildNameMappingKey(blankPlaceholderRow), "Cost centre name mapping treats dash placeholders the same as blanks");

var scoreMethod = typeof(MainWindowViewModel).GetMethod("ScoreCostCenterAssociation", BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new InvalidOperationException("Could not find ScoreCostCenterAssociation.");
var candidateMethod = typeof(MainWindowViewModel).GetMethod("BuildCostCenterNameCandidates", BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new InvalidOperationException("Could not find BuildCostCenterNameCandidates.");

var paulaAnchor = new CostCenterNameMapping
{
    ManualName = "Paula Wright",
    Who = "Paula Wright",
    Narrative2 = "Pwright",
    Narrative1 = "WrightP"
};
var paulaNarrative2Only = new CostTransaction
{
    Narrative2 = "P Wright"
};
var paulaNarrative1Only = new CostTransaction
{
    Narrative1 = "WrightP"
};
var genericNarrativeOnly = new CostTransaction
{
    Narrative2 = "DETAILED COST POSTING"
};
var unrelatedKatie = new CostCenterNameMapping
{
    ManualName = "Katie Armstrong",
    Who = "Katie Armstrong",
    Narrative2 = "Karmstrong",
    Narrative1 = "ArmstrongK"
};

var narrative2AssociationScore = (int)(scoreMethod.Invoke(null, [paulaNarrative2Only, paulaAnchor]) ?? 0);
var narrative1AssociationScore = (int)(scoreMethod.Invoke(null, [paulaNarrative1Only, paulaAnchor]) ?? 0);
var genericNarrativeScore = (int)(scoreMethod.Invoke(null, [genericNarrativeOnly, paulaAnchor]) ?? 0);
var unrelatedKatieScore = (int)(scoreMethod.Invoke(null, [paulaNarrative2Only, unrelatedKatie]) ?? 0);

AssertTrue(narrative2AssociationScore > 0, "Narrative 2 association can suggest an existing mapped name");
AssertTrue(narrative1AssociationScore > 0, "Narrative 1 association can suggest an existing mapped name");
AssertEqual(0, genericNarrativeScore, "Generic narrative labels do not create person associations");
AssertEqual(0, unrelatedKatieScore, "Unrelated names are not suggested without a real shared association");
var accrualSuggestionRow = new CostTransaction
{
    Narrative2 = "HM Based on Actual Claim",
    Narrative3 = "FY19 - November 2018 Accrual"
};
var accrualCandidates = (candidateMethod.Invoke(CreateViewModel(), [accrualSuggestionRow, null]) as IEnumerable<CostCenterNameOption>)?.ToList()
    ?? throw new InvalidOperationException("Expected accrual candidates.");
AssertEqual("Accrual", accrualCandidates[0].RawName, "Rows mentioning accrual suggest Accrual first");
var opusSuggestionViewModel = CreateViewModel();
var opusForecastLine = new ForecastLine
{
    TaskNumber = "WW32203002",
    ResourceName = "OPUS",
    ProjectCode = "WW322"
};
opusSuggestionViewModel.ForecastLines.Add(opusForecastLine);
var opusSuggestionRow = new CostTransaction
{
    ResourceCode = "255",
    ResourceDescription = "Contractors Payments",
    Narrative1 = "WSP OPUS",
    Narrative2 = "Annual Pain Gain",
    Narrative3 = "YTD May 2020"
};
var opusCandidates = (candidateMethod.Invoke(opusSuggestionViewModel, [opusSuggestionRow, null]) as IEnumerable<CostCenterNameOption>)?.ToList()
    ?? throw new InvalidOperationException("Expected OPUS candidates.");
AssertEqual("OPUS", opusCandidates[0].RawName, "Rows mentioning an existing name suggest that name before generic codes");

// --- Schedule / Gantt CPM engine checks ---
var parsedPredecessors = SchedulingService.ParsePredecessors("A1 SS+3, A2 FF-2; A3", out var parseErrors);
AssertEqual(0, parseErrors.Count, "Predecessor parser accepts FS/SS/FF tokens");
AssertEqual(3, parsedPredecessors.Count, "Predecessor parser reads all tokens");
AssertEqual(ActivityLinkType.StartToStart, parsedPredecessors[0].Type, "Predecessor parser reads SS type");
AssertEqual(3, parsedPredecessors[0].LagDays, "Predecessor parser reads positive lag");
AssertEqual(-2, parsedPredecessors[1].LagDays, "Predecessor parser reads negative lag");
AssertEqual(ActivityLinkType.FinishToStart, parsedPredecessors[2].Type, "Predecessor parser defaults to FS");

var fiveDayCalendar = new ScheduleCalendar { Id = "CAL5", Name = "Standard 5 day" };
fiveDayCalendar.Holidays.Add(new DateOnly(2026, 7, 8));
AssertEqual(
    0,
    SchedulingService.CalculateFinishToStartLagFromDrop(fiveDayCalendar, new DateOnly(2026, 7, 6), new DateOnly(2026, 7, 6)),
    "Dropping a Gantt link on the target start creates FS without lag");
AssertEqual(
    2,
    SchedulingService.CalculateFinishToStartLagFromDrop(fiveDayCalendar, new DateOnly(2026, 7, 6), new DateOnly(2026, 7, 9)),
    "Dropping inside a target bar calculates working-day lag");
AssertEqual(
    -1,
    SchedulingService.CalculateFinishToStartLagFromDrop(fiveDayCalendar, new DateOnly(2026, 7, 6), new DateOnly(2026, 7, 3)),
    "Dropping before a target bar calculates working-day lead");
var sevenDayCalendar = new ScheduleCalendar
{
    Id = "CAL7",
    Name = "7 day",
    WorkingDays = [true, true, true, true, true, true, true]
};

var schedule = new ScheduleData
{
    ProjectStart = new DateOnly(2026, 7, 6), // Monday
    DefaultCalendarId = "CAL5",
    Calendars = [fiveDayCalendar, sevenDayCalendar],
    ActiveBaselineName = "BL1",
    Baselines =
    [
        new ScheduleBaseline
        {
            Name = "BL1",
            Entries = [new ScheduleBaselineEntry { ActivityId = "A2", Start = new DateOnly(2026, 7, 10), Finish = new DateOnly(2026, 7, 13) }]
        }
    ],
    Activities =
    [
        new ScheduleActivity { Id = "H1", Name = "Stage 1", Kind = ScheduleActivityKind.Heading, OutlineLevel = 0 },
        new ScheduleActivity { Id = "A1", Name = "Dig", Kind = ScheduleActivityKind.Task, DurationDays = 3, CalendarId = "CAL5", OutlineLevel = 1 },
        new ScheduleActivity { Id = "A2", Name = "Pour", Kind = ScheduleActivityKind.Task, DurationDays = 2, CalendarId = "CAL5", OutlineLevel = 1, PredecessorText = "A1 FS+1" },
        new ScheduleActivity { Id = "A3", Name = "Cure", Kind = ScheduleActivityKind.Task, DurationDays = 4, CalendarId = "CAL7", OutlineLevel = 1, PredecessorText = "A2" },
        new ScheduleActivity { Id = "M1", Name = "Stage complete", Kind = ScheduleActivityKind.Milestone, CalendarId = "CAL7", OutlineLevel = 1, PredecessorText = "A3" },
        new ScheduleActivity { Id = "HAM", Name = "Site overheads", Kind = ScheduleActivityKind.Hammock, HammockMemberText = "A1, A3" },
        new ScheduleActivity { Id = "A4", Name = "Parallel works", Kind = ScheduleActivityKind.Task, DurationDays = 2, CalendarId = "CAL5", PredecessorText = "A1" },
        new ScheduleActivity { Id = "A5", Name = "Constrained start", Kind = ScheduleActivityKind.Task, DurationDays = 2, CalendarId = "CAL5", ConstraintType = ScheduleConstraintType.StartOnOrAfter, ConstraintDate = new DateOnly(2026, 7, 13) }
    ]
};

new SchedulingService().Recalculate(schedule);
ScheduleActivity ScheduleActivityById(string id) => schedule.Activities.Single(a => a.Id == id);
var dig = ScheduleActivityById("A1");
var pour = ScheduleActivityById("A2");
var cure = ScheduleActivityById("A3");
var stageMilestone = ScheduleActivityById("M1");
var hammock = ScheduleActivityById("HAM");
var parallel = ScheduleActivityById("A4");
var constrained = ScheduleActivityById("A5");
var stageHeading = ScheduleActivityById("H1");

AssertEqual(4, schedule.Links.Count, "Schedule links are rebuilt from predecessor text");
AssertEqual((DateOnly?)new DateOnly(2026, 7, 6), dig.EarlyStart, "CPM starts first activity at project start");
AssertEqual((DateOnly?)new DateOnly(2026, 7, 9), dig.EarlyFinish, "Calendar holiday pushes the early finish");
AssertEqual((DateOnly?)new DateOnly(2026, 7, 13), pour.EarlyStart, "FS+1 lag schedules successor across the weekend");
AssertEqual((DateOnly?)new DateOnly(2026, 7, 14), pour.EarlyFinish, "Successor finish respects working days");
AssertEqual((DateOnly?)new DateOnly(2026, 7, 18), cure.EarlyFinish, "Seven-day calendar activity works the weekend");
AssertEqual((DateOnly?)new DateOnly(2026, 7, 19), stageMilestone.EarlyStart, "Milestone lands on next seven-day working day");
AssertEqual((DateOnly?)new DateOnly(2026, 7, 6), dig.LateStart, "Backward pass returns matching late start on the critical path");
AssertEqual(0, dig.TotalFloatDays ?? -1, "Critical chain has zero total float");
AssertTrue(dig.IsCritical && pour.IsCritical && cure.IsCritical && stageMilestone.IsCritical, "Critical path is flagged through the chain");
AssertEqual(4, parallel.TotalFloatDays ?? -1, "Parallel activity carries total float");
AssertTrue(!parallel.IsCritical, "Activity with float is not critical");
AssertEqual((DateOnly?)new DateOnly(2026, 7, 13), constrained.EarlyStart, "Start-on-or-after constraint holds the early start");
AssertEqual((DateOnly?)new DateOnly(2026, 7, 6), stageHeading.EarlyStart, "Heading rolls up the earliest child start");
AssertEqual((DateOnly?)new DateOnly(2026, 7, 19), stageHeading.EarlyFinish, "Heading rolls up the latest child finish");
AssertEqual((DateOnly?)new DateOnly(2026, 7, 6), hammock.EarlyStart, "Hammock spans from its earliest member");
AssertEqual((DateOnly?)new DateOnly(2026, 7, 18), hammock.EarlyFinish, "Hammock spans to its latest member");
AssertEqual((DateOnly?)new DateOnly(2026, 7, 13), pour.BaselineFinish, "Active baseline supplies baseline dates");
AssertEqual(1, pour.SlipDays ?? -1, "Slip is measured in working days against the baseline");

var deadlineCalendar = new ScheduleCalendar { Id = "DEADLINE-CAL", Name = "Deadline calendar" };
var deadlineSchedule = new ScheduleData
{
    ProjectStart = new DateOnly(2026, 7, 6),
    MustFinishBy = new DateOnly(2026, 7, 8),
    DefaultCalendarId = deadlineCalendar.Id,
    Calendars = [deadlineCalendar],
    Activities = [new ScheduleActivity { Id = "D1", Name = "Deadline task", DurationDays = 5, CalendarId = deadlineCalendar.Id }]
};
new SchedulingService().Recalculate(deadlineSchedule);
var deadlineActivity = deadlineSchedule.Activities.Single();
AssertEqual((DateOnly?)new DateOnly(2026, 7, 10), deadlineActivity.EarlyFinish, "Deadline does not rewrite the calculated early finish");
AssertEqual((DateOnly?)new DateOnly(2026, 7, 8), deadlineActivity.LateFinish, "Earlier must-finish deadline drives the backward pass");
AssertEqual(-2, deadlineActivity.TotalFloatDays ?? 0, "Missed must-finish deadline produces negative float");
AssertTrue(deadlineActivity.IsCritical, "Negative-float deadline activity is critical");

var scheduleEditor = CreateViewModel();
var originalScheduleCount = scheduleEditor.ScheduleActivities.Count;
scheduleEditor.SelectedScheduleActivity = scheduleEditor.ScheduleActivities[1];
var insertedAbove = scheduleEditor.AddScheduleActivityRelative(ScheduleActivityKind.Task, above: true);
AssertEqual(1, scheduleEditor.ScheduleActivities.IndexOf(insertedAbove), "Schedule activity can be inserted above the selected row");
var insertedBelow = scheduleEditor.AddScheduleActivityRelative(ScheduleActivityKind.Task, above: false);
AssertEqual(2, scheduleEditor.ScheduleActivities.IndexOf(insertedBelow), "Schedule activity can be inserted below the selected row");
AssertEqual(originalScheduleCount + 2, scheduleEditor.ScheduleActivities.Count, "Schedule insertion adds rows without replacing activities");
scheduleEditor.SelectedScheduleActivity = insertedAbove;
scheduleEditor.ConvertSelectedScheduleActivityToMilestone();
AssertEqual(ScheduleActivityKind.Milestone, insertedAbove.Kind, "Activity can be converted to a milestone");
AssertEqual(0, insertedAbove.DurationDays, "Milestone conversion clears duration");
scheduleEditor.SetSelectedScheduleProgress(75);
AssertEqual(75d, insertedAbove.PercentComplete, "Progress shortcut updates activity completion");
insertedAbove.Kind = ScheduleActivityKind.Task;
insertedAbove.DurationDays = 2;
insertedAbove.PredecessorText = string.Empty;
insertedBelow.PredecessorText = string.Empty;
scheduleEditor.RecalculateSchedule();
AssertTrue(scheduleEditor.TryCreateScheduleLink(insertedAbove, insertedBelow, ActivityLinkType.StartToStart, 2), "Advanced relationship can be added with type and lag");
var addedRelationship = SchedulingService.ParsePredecessors(insertedBelow.PredecessorText, out _).Single();
AssertEqual(ActivityLinkType.StartToStart, addedRelationship.Type, "Added relationship keeps its SS type");
AssertEqual(2, addedRelationship.LagDays, "Added relationship keeps its lag");
scheduleEditor.UpdateScheduleLink(insertedBelow, insertedAbove.Id, ActivityLinkType.FinishToFinish, -1);
var movedRelationship = SchedulingService.ParsePredecessors(insertedBelow.PredecessorText, out _).Single();
AssertEqual(ActivityLinkType.FinishToFinish, movedRelationship.Type, "Existing relationship type can be changed");
AssertEqual(-1, movedRelationship.LagDays, "Existing relationship lag can be changed");
scheduleEditor.CopyScheduleLinkSource(insertedAbove);
scheduleEditor.CopyScheduleLinkSource(insertedAbove);
AssertEqual(2, scheduleEditor.ScheduleLinkClipboardActivities.Count, "Link clipboard permits repeated source entries");
var linkClipboardTarget = scheduleEditor.AddScheduleActivityAt(ScheduleActivityKind.Task, scheduleEditor.ScheduleActivities.Count);
AssertTrue(scheduleEditor.PasteScheduleLinkTo(linkClipboardTarget), "Link clipboard connects a distant activity");
AssertEqual(1, scheduleEditor.ScheduleLinkClipboardActivities.Count, "Using one link clipboard entry removes only that entry");
var secondLinkClipboardTarget = scheduleEditor.AddScheduleActivityAt(ScheduleActivityKind.Task, scheduleEditor.ScheduleActivities.Count);
AssertTrue(scheduleEditor.PasteScheduleLinkTo(secondLinkClipboardTarget), "Link clipboard source remains available for another target");
AssertEqual(0, scheduleEditor.ScheduleLinkClipboardActivities.Count, "Link clipboard is empty after the second repeated entry is used");
var successorClipboardSource = scheduleEditor.AddScheduleActivityAt(ScheduleActivityKind.Task, scheduleEditor.ScheduleActivities.Count);
var successorClipboardTarget = scheduleEditor.AddScheduleActivityAt(ScheduleActivityKind.Task, scheduleEditor.ScheduleActivities.Count);
scheduleEditor.CopyScheduleLinkSource(successorClipboardTarget);
AssertTrue(scheduleEditor.PasteScheduleSuccessorFromClipboard(successorClipboardSource), "Activity panel can add a successor from the link clipboard");
AssertEqual(successorClipboardSource.Id, SchedulingService.ParsePredecessors(successorClipboardTarget.PredecessorText, out _).Single().PredecessorId, "Successor clipboard paste links the selected activity as predecessor");
AssertEqual(0, scheduleEditor.ScheduleLinkClipboardActivities.Count, "Successor clipboard paste consumes one link clipboard entry");
scheduleEditor.RecalculateSchedule();
AssertTrue(!scheduleEditor.TryCreateScheduleLink(insertedAbove, secondLinkClipboardTarget), "Duplicate schedule links are ignored");
AssertTrue(!scheduleEditor.TryCreateScheduleLink(linkClipboardTarget, insertedAbove), "Link command prevents circular relationships");
scheduleEditor.BreakScheduleLink(insertedBelow, insertedAbove.Id);
AssertEqual(0, SchedulingService.ParsePredecessors(insertedBelow.PredecessorText, out _).Count, "Individual relationship can be broken");
var oldRow = scheduleEditor.ScheduleActivities.IndexOf(insertedAbove);
scheduleEditor.MoveScheduleActivity(insertedAbove, Math.Min(oldRow + 3, scheduleEditor.ScheduleActivities.Count - 1));
AssertTrue(scheduleEditor.ScheduleActivities.IndexOf(insertedAbove) != oldRow, "Schedule activity can be reordered by target row");
scheduleEditor.CaptureScheduleBaseline("Editable baseline");
scheduleEditor.ScheduleEditMode = ScheduleEditMode.SelectedBaseline;
var editedBaselineDate = insertedAbove.EarlyStart?.AddDays(2) ?? DateOnly.FromDateTime(DateTime.Today);
insertedAbove.BaselineStart = editedBaselineDate;
AssertEqual(editedBaselineDate, scheduleEditor.ScheduleDataRef.ActiveBaseline?.FindEntry(insertedAbove.Id)?.Start, "Selected baseline dates can be edited");
AssertTrue(scheduleEditor.ScheduleDataRef.Baselines.Count >= 2, "Multiple schedule baselines are retained");

var performanceCalendar = new ScheduleCalendar { Id = "PERF", Name = "Performance", WorkingDays = [true, true, true, true, true, true, true] };
var performanceSchedule = new ScheduleData
{
    ProjectStart = new DateOnly(2026, 1, 1),
    DefaultCalendarId = performanceCalendar.Id,
    Calendars = [performanceCalendar]
};
const int performanceActivityCount = 2500;
for (var index = 0; index < performanceActivityCount; index++)
{
    performanceSchedule.Activities.Add(new ScheduleActivity
    {
        Id = $"P{index:0000}",
        Name = $"Performance activity {index}",
        Kind = ScheduleActivityKind.Task,
        DurationDays = 1,
        CalendarId = performanceCalendar.Id,
        PredecessorText = index == 0 ? string.Empty : $"P{index - 1:0000}"
    });
}

var scheduleStopwatch = Stopwatch.StartNew();
new SchedulingService().Recalculate(performanceSchedule);
scheduleStopwatch.Stop();
AssertEqual(performanceActivityCount - 1, performanceSchedule.Links.Count, "Large schedule rebuilds every relationship");
AssertTrue(scheduleStopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Large schedule recalculates within 5 seconds ({scheduleStopwatch.ElapsedMilliseconds} ms)");

// --- Forecast curve engine checks ---
var linearSpread = ForecastCurveService.Distribute(1000m, 4, ForecastCurveProfile.Linear);
AssertEqual(4, linearSpread.Count, "Linear curve fills every period");
AssertEqual(1000m, linearSpread.Sum(), "Linear curve sums exactly to the total");
AssertEqual(250m, linearSpread[0], "Linear curve spreads evenly");

var frontLoaded = ForecastCurveService.Distribute(1200m, 6, ForecastCurveProfile.FrontLoaded);
AssertEqual(1200m, frontLoaded.Sum(), "Front loaded curve sums exactly to the total");
AssertTrue(frontLoaded[0] > frontLoaded[5], "Front loaded curve is heaviest at the start");

var backLoaded = ForecastCurveService.Distribute(1200m, 6, ForecastCurveProfile.BackLoaded);
AssertTrue(backLoaded[5] > backLoaded[0], "Back loaded curve is heaviest at the end");

var sCurve = ForecastCurveService.Distribute(10000m, 10, ForecastCurveProfile.SCurve);
AssertEqual(10000m, sCurve.Sum(), "S-curve sums exactly to the total");
AssertTrue(sCurve[4] > sCurve[0] && sCurve[4] > sCurve[9], "S-curve peaks in the middle periods");
AssertTrue(Math.Abs(sCurve[0] - sCurve[9]) < 50m, "S-curve tails are roughly symmetric");

var bell = ForecastCurveService.Distribute(999.99m, 5, ForecastCurveProfile.Bell);
AssertEqual(999.99m, bell.Sum(), "Bell curve folds rounding residue back into the total");
AssertTrue(bell[2] > bell[0], "Bell curve peaks in the middle");

var singlePeriod = ForecastCurveService.Distribute(500m, 1, ForecastCurveProfile.SCurve);
AssertEqual(500m, singlePeriod[0], "Single period takes the whole total");
AssertEqual(0, ForecastCurveService.Distribute(500m, 0, ForecastCurveProfile.Linear).Count, "Zero periods yields an empty spread");

var curveLine = new ForecastLine { RowNumber = 999, ResourceName = "Curve test" };
curveLine.MonthlyForecasts.Add(new MonthlyForecast { PeriodLabel = "26-11", Amount = 1m });
curveLine.MonthlyForecasts.Add(new MonthlyForecast { PeriodLabel = "26-12", Amount = 2m, IsLocked = true });
curveLine.MonthlyForecasts.Add(new MonthlyForecast { PeriodLabel = "27-01", Amount = 3m });
var curveService = new ForecastCurveService();
var applied = curveService.ApplyCurve(curveLine, curveLine.MonthlyForecasts, 600m, ForecastCurveProfile.Linear);
AssertEqual(2, applied, "Curve apply skips locked months");
AssertEqual(2m, curveLine.MonthlyForecasts[1].Amount, "Locked month amount is untouched by curve");
AssertEqual(600m, curveLine.MonthlyForecasts[0].Amount + curveLine.MonthlyForecasts[2].Amount, "Curve apply spreads the full total over open months");

Console.WriteLine("All Project Cost Forecast checks passed.");

static ForecastLine FindForecastLine(ProjectDataset dataset, string taskNumber, string resourceName)
{
    return dataset.ForecastLines.Single(line =>
        string.Equals(line.TaskNumber, taskNumber, StringComparison.OrdinalIgnoreCase)
        && string.Equals(line.ResourceName, resourceName, StringComparison.OrdinalIgnoreCase));
}

static DateOnly GetCalendarMonthStart(string periodLabel)
{
    if (!FiscalPeriod.TryGetCalendarMonthStart(periodLabel, out var calendarMonthStart))
    {
        throw new InvalidOperationException($"Could not map fiscal period {periodLabel} to a calendar month.");
    }

    return calendarMonthStart;
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "ProjectCostForecast.sln")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new DirectoryNotFoundException("Could not locate ProjectCostForecast.sln.");
}

static void AssertEqual<T>(T expected, T actual, string description)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{description}: expected {expected}, actual {actual}.");
    }

    Console.WriteLine($"PASS: {description}");
}

static void AssertTrue(bool condition, string description)
{
    if (!condition)
    {
        throw new InvalidOperationException(description);
    }

    Console.WriteLine($"PASS: {description}");
}

static void AssertNearlyEqual(double expected, double actual, double tolerance, string description)
{
    if (Math.Abs(expected - actual) > tolerance)
    {
        throw new InvalidOperationException($"{description}: expected {expected}, actual {actual}, tolerance {tolerance}.");
    }

    Console.WriteLine($"PASS: {description}");
}

static double InvokeForecastWidthMigration(double savedWidth, double currentWidth, double minWidth, bool isTotal)
{
    var method = typeof(MainWindow).GetMethod("GetAppliedLayoutWidth", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new MissingMethodException(typeof(MainWindow).FullName, "GetAppliedLayoutWidth");
    var column = new DataGridTextColumn
    {
        Header = new ForecastMonthColumnDefinition
        {
            Key = isTotal ? "TOTAL:26-11" : "MONTH:26-11",
            IsTotal = isTotal
        },
        Width = new DataGridLength(currentWidth),
        MinWidth = minWidth
    };

    return (double)(method.Invoke(null, [column, savedWidth])
        ?? throw new InvalidOperationException("Forecast width migration returned null."));
}

static void InvokeEnsureColumnPresentation(DataGridColumn column)
{
    var method = typeof(MainWindow).GetMethod("EnsureColumnPresentation", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new MissingMethodException(typeof(MainWindow).FullName, "EnsureColumnPresentation");
    method.Invoke(null, [column]);
}

static bool InvokeResetColumnWidthToDefault(DataGridColumn column)
{
    var method = typeof(MainWindow).GetMethod("ResetColumnWidthToDefault", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new MissingMethodException(typeof(MainWindow).FullName, "ResetColumnWidthToDefault");
    return (bool)(method.Invoke(null, [column])
        ?? throw new InvalidOperationException("Column width reset returned null."));
}

static (bool Rows, bool Columns, bool Panel, bool Grouping) GetForecastGridVirtualizationSettings()
{
    (bool Rows, bool Columns, bool Panel, bool Grouping) result = default;
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            var grid = new DataGrid { Name = "ForecastLinesGrid" };
            var method = typeof(MainWindow).GetMethod("ConfigureForecastGridPerformance", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(MainWindow).FullName, "ConfigureForecastGridPerformance");
            method.Invoke(null, [grid]);
            result = (
                grid.EnableRowVirtualization,
                grid.EnableColumnVirtualization,
                VirtualizingPanel.GetIsVirtualizing(grid),
                VirtualizingPanel.GetIsVirtualizingWhenGrouping(grid));
        }
        catch (Exception exception)
        {
            failure = exception;
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();
    if (failure is not null)
    {
        throw new InvalidOperationException("Could not inspect forecast grid virtualization settings.", failure);
    }

    return result;
}

static void RunSharedGridControlRegressionTests(string repositoryRoot)
{
    var appSourceRoot = Path.Combine(repositoryRoot, "src", "ProjectCostForecast.App");
    var sourceFiles = Directory.GetFiles(appSourceRoot, "*.*", SearchOption.AllDirectories)
        .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
        .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}artifacts{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        .ToList();
    var rawGridDeclarations = sourceFiles
        .SelectMany(path => Regex.Matches(
                File.ReadAllText(path),
                path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)
                    ? @"<DataGrid(?=[\s>])"
                    : @"\bnew\s+DataGrid\s*(?=\{)")
            .Select(_ => path))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
    AssertEqual(0, rawGridDeclarations.Count, "Every application table is constructed as ProjectDataGrid");

    var forecastInteractionSource = File.ReadAllText(Path.Combine(appSourceRoot, "MainWindow.ForecastGridInteraction.cs"));
    var ganttSource = File.ReadAllText(Path.Combine(appSourceRoot, "MainWindow.Gantt.cs"));
    var mainWindowXaml = File.ReadAllText(Path.Combine(appSourceRoot, "MainWindow.xaml"));
    var gridBuilderSource = File.ReadAllText(Path.Combine(appSourceRoot, "MainWindow.GridBuilders.cs"));
    AssertTrue(!forecastInteractionSource.Contains("ApplyLiveForecastRowHeight", StringComparison.Ordinal), "Forecast no longer owns a separate row resize implementation");
    AssertTrue(!mainWindowXaml.Contains("<Setter Property=\"Height\" Value=\"{Binding RowDisplayHeight}\"", StringComparison.Ordinal), "Forecast XAML never binds cell or row container height independently");
    AssertTrue(!gridBuilderSource.Contains("FrameworkElement.HeightProperty,\r\n            new Binding(nameof(ForecastLine.RowDisplayHeight))", StringComparison.Ordinal), "Generated forecast columns never own cell height");
    AssertTrue(
        ganttSource.Contains("ScheduleGrid.EnableRowVirtualization = false;", StringComparison.Ordinal)
        && ganttSource.Contains("VirtualizingPanel.SetIsVirtualizing(ScheduleGrid, false);", StringComparison.Ordinal)
        && ganttSource.Contains("ScrollViewer.SetCanContentScroll(ScheduleGrid, false);", StringComparison.Ordinal),
        "Schedule keeps exact non-virtualized pixel offsets for its variable-height Gantt rows");

    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            var profiles = Enum.GetValues<ProjectDataGridProfile>();
            foreach (var profile in profiles)
            {
                var profileGrid = new ProjectDataGrid { Profile = profile };
                ProjectDataGridProfiles.Apply(profileGrid, profile);
                AssertEqual(profile, ProjectDataGridProfiles.GetProfile(profileGrid), $"{profile} uses one shared profile dependency property");
                AssertEqual(DataGridSelectionMode.Extended, profileGrid.SelectionMode, $"{profile} supports Shift/Ctrl extended selection");
                AssertTrue(profileGrid.CanUserResizeRows, $"{profile} exposes shared row resizing");
                AssertTrue(profileGrid.ModifierClickSelectsWholeRows, $"{profile} uses shared whole-row Shift/Ctrl selection");
                if (profile == ProjectDataGridProfile.Schedule)
                {
                    AssertEqual(DataGridSelectionUnit.FullRow, profileGrid.SelectionUnit, "Schedule keeps whole-row range selection");
                }
            }

            var pixelScrollGrid = new ProjectDataGrid();
            pixelScrollGrid.EnableRowVirtualization = false;
            VirtualizingPanel.SetIsVirtualizing(pixelScrollGrid, false);
            ScrollViewer.SetCanContentScroll(pixelScrollGrid, false);
            ProjectDataGridProfiles.Apply(pixelScrollGrid, ProjectDataGridProfile.Schedule);
            AssertTrue(!ScrollViewer.GetCanContentScroll(pixelScrollGrid), "Schedule preserves its explicit pixel scrolling contract");
            AssertTrue(!pixelScrollGrid.EnableRowVirtualization && !VirtualizingPanel.GetIsVirtualizing(pixelScrollGrid), "Schedule preserves its exact row layout contract");

            var rows = Enumerable.Range(1, 8)
                .Select(index => new SharedGridTestRow($"Row {index}", index * 10, index <= 4 ? "A" : "B"))
                .ToList();
            var centeredTextStyle = new Style(typeof(TextBlock));
            centeredTextStyle.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
            centeredTextStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(6, 2, 6, 2)));
            var grid = new ProjectDataGrid
            {
                Profile = ProjectDataGridProfile.Forecast,
                ItemsSource = rows,
                AutoGenerateColumns = false,
                Width = 420,
                Height = 250,
                RowHeight = 30,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                EnableRowVirtualization = true,
                EnableColumnVirtualization = true
            };
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Name",
                Binding = new System.Windows.Data.Binding(nameof(SharedGridTestRow.Name)),
                ElementStyle = centeredTextStyle,
                Width = 220
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Value",
                Binding = new System.Windows.Data.Binding(nameof(SharedGridTestRow.Value)),
                ElementStyle = centeredTextStyle,
                Width = 120
            });
            var constrainedRowStyle = new Style(typeof(DataGridRow));
            var constrainedRowTrigger = new DataTrigger
            {
                Binding = new System.Windows.Data.Binding(nameof(SharedGridTestRow.Name)),
                Value = rows[^1].Name
            };
            constrainedRowTrigger.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 72d));
            constrainedRowStyle.Triggers.Add(constrainedRowTrigger);
            grid.RowStyle = constrainedRowStyle;

            // Store this while no row containers exist. On realization the
            // control must normalize it against the row style's MinHeight.
            grid.SetRowHeight(rows[^1], 30d);
            using var presentationSource = new HwndSource(new HwndSourceParameters("SharedGridRegressionHost")
            {
                Width = 420,
                Height = 250,
                WindowStyle = unchecked((int)0x80000000)
            });
            presentationSource.RootVisual = grid;
            ArrangeSharedGrid(grid);
            grid.ScrollIntoView(rows[0]);
            ArrangeSharedGrid(grid);

            var row = grid.ItemContainerGenerator.ContainerFromItem(rows[0]) as DataGridRow
                ?? throw new InvalidOperationException("Shared grid did not realize its first row.");
            foreach (var targetHeight in new[] { 30d, 31d, 42d, 63d, 96d, 137d, 300d, 600d })
            {
                grid.SetRowHeight(rows[0], targetHeight);
                ArrangeSharedGrid(grid);
                AssertNearlyEqual(targetHeight, row.ActualHeight, 0.75, $"Shared row reaches {targetHeight:0}px");

                var cells = FindVisualDescendantsForTest<DataGridCell>(row).ToList();
                AssertTrue(cells.Count >= 2, $"Shared row realizes its cells at {targetHeight:0}px");
                AssertTrue(
                    cells.All(cell => cell.ReadLocalValue(FrameworkElement.HeightProperty) == DependencyProperty.UnsetValue),
                    $"Cells have no independent Height owner at {targetHeight:0}px");
                AssertTrue(
                    cells.All(cell => Math.Abs(cell.ActualHeight - row.ActualHeight) <= 1d),
                    $"Every cell and border spans the complete {targetHeight:0}px row " +
                    $"(row={row.ActualHeight:0.##}, cells={string.Join(",", cells.Select(cell => cell.ActualHeight.ToString("0.##", CultureInfo.InvariantCulture)))})");

                var text = FindVisualDescendantsForTest<TextBlock>(cells[0]).FirstOrDefault()
                    ?? throw new InvalidOperationException("Shared grid cell did not create its text content.");
                var textTop = text.TranslatePoint(new Point(0, 0), row).Y;
                var textCenter = textTop + (text.ActualHeight / 2d);
                AssertNearlyEqual(row.ActualHeight / 2d, textCenter, 1d, $"Cell text stays vertically centered at {targetHeight:0}px");
            }

            grid.ResetRowHeight(rows[0]);
            ArrangeSharedGrid(grid);
            AssertNearlyEqual(30d, row.ActualHeight, 0.75, "Double-click/reset returns a row to the grid's current default height");
            AssertTrue(grid.GetRowHeight(rows[0]) is null, "Reset removes the per-item row-height override");

            grid.ScrollIntoView(rows[^1]);
            ArrangeSharedGrid(grid);
            var constrainedRow = grid.ItemContainerGenerator.ContainerFromItem(rows[^1]) as DataGridRow
                ?? throw new InvalidOperationException("Shared grid did not realize its constrained row.");
            AssertNearlyEqual(72d, constrainedRow.ActualHeight, 0.75, "A virtualized override is normalized to the realized row style minimum");
            AssertNearlyEqual(72d, grid.GetRowHeight(rows[^1]) ?? 0d, 0.01, "The normalized row height is retained for aligned dependent views");
            AssertTrue(
                FindVisualDescendantsForTest<DataGridCell>(constrainedRow)
                    .All(cell => Math.Abs(cell.ActualHeight - constrainedRow.ActualHeight) <= 1d),
                "A constrained realized row keeps every cell and border at the normalized height");

            var groupedRowsView = new System.Windows.Data.ListCollectionView(rows);
            groupedRowsView.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription(nameof(SharedGridTestRow.Group)));
            grid.ItemsSource = groupedRowsView;
            ArrangeSharedGrid(grid);
            AssertTrue(groupedRowsView.Groups is { Count: > 0 }, "Shared selection test exercises grouped rows");

            var selectRowsMethod = typeof(ProjectDataGrid).GetMethod(
                "SelectWholeRowsFromModifierClick",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(ProjectDataGrid).FullName, "SelectWholeRowsFromModifierClick");
            var selectionAnchorField = typeof(ProjectDataGrid).GetField(
                "_rowSelectionAnchor",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(typeof(ProjectDataGrid).FullName, "_rowSelectionAnchor");
            object? modifierCurrentItem = null;
            grid.ModifierRowSelectionCompleted += (_, eventArgs) => modifierCurrentItem = eventArgs.CurrentItem;
            selectionAnchorField.SetValue(grid, rows[1]);
            selectRowsMethod.Invoke(grid, [rows[4], ModifierKeys.Shift]);
            var selectedRangeItems = grid.SelectedCells.Select(cell => cell.Item).Distinct(ReferenceEqualityComparer.Instance).ToList();
            AssertTrue(selectedRangeItems.SequenceEqual(rows.Skip(1).Take(4)), "Shared Shift selection includes the forward row range in view order");
            AssertTrue(ReferenceEquals(grid.CurrentCell.Item, rows[4]) && ReferenceEquals(modifierCurrentItem, rows[4]), "Forward Shift selection keeps the clicked end row current");

            selectionAnchorField.SetValue(grid, rows[5]);
            selectRowsMethod.Invoke(grid, [rows[3], ModifierKeys.Shift]);
            selectedRangeItems = grid.SelectedCells.Select(cell => cell.Item).Distinct(ReferenceEqualityComparer.Instance).ToList();
            AssertTrue(selectedRangeItems.SequenceEqual(rows.Skip(3).Take(3)), "Shared Shift selection supports reverse row ranges");
            AssertTrue(ReferenceEquals(grid.CurrentCell.Item, rows[3]) && ReferenceEquals(modifierCurrentItem, rows[3]), "Reverse Shift selection keeps the clicked end row current");

            selectRowsMethod.Invoke(grid, [rows[4], ModifierKeys.Control]);
            selectedRangeItems = grid.SelectedCells.Select(cell => cell.Item).Distinct(ReferenceEqualityComparer.Instance).ToList();
            AssertTrue(!selectedRangeItems.Contains(rows[4]) && selectedRangeItems.Contains(rows[3]) && selectedRangeItems.Contains(rows[5]), "Shared Ctrl selection toggles a complete row");
            AssertTrue(ReferenceEquals(grid.CurrentCell.Item, rows[3]) && ReferenceEquals(modifierCurrentItem, rows[3]), "Ctrl deselection moves current context to the first remaining selected row");

            grid.SelectedCells.Clear();
            grid.SelectionUnit = DataGridSelectionUnit.FullRow;
            selectionAnchorField.SetValue(grid, rows[1]);
            selectRowsMethod.Invoke(grid, [rows[4], ModifierKeys.Shift]);
            var selectedFullRows = grid.SelectedItems.Cast<object>().ToList();
            AssertTrue(selectedFullRows.SequenceEqual(rows.Skip(1).Take(4)), "Shared Shift selection supports full-row tables");
            AssertTrue(ReferenceEquals(grid.CurrentCell.Item, rows[4]) && ReferenceEquals(modifierCurrentItem, rows[4]), "Full-row Shift selection keeps the clicked end row current");
            selectRowsMethod.Invoke(grid, [rows[3], ModifierKeys.Control]);
            selectedFullRows = grid.SelectedItems.Cast<object>().ToList();
            AssertTrue(!selectedFullRows.Contains(rows[3]) && selectedFullRows.Contains(rows[1]) && selectedFullRows.Contains(rows[4]), "Shared Ctrl selection toggles full-row tables");
            AssertTrue(ReferenceEquals(grid.CurrentCell.Item, rows[1]) && ReferenceEquals(modifierCurrentItem, rows[1]), "Full-row Ctrl deselection keeps a remaining selected row current");
        }
        catch (Exception exception)
        {
            failure = exception;
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();
    if (failure is not null)
    {
        throw new InvalidOperationException("Shared grid regression checks failed.", failure);
    }
}

static void ArrangeSharedGrid(ProjectDataGrid grid)
{
    var size = new Size(grid.Width, grid.Height);
    grid.ApplyTemplate();
    grid.Measure(size);
    grid.Arrange(new Rect(size));
    grid.UpdateLayout();
}

static IEnumerable<T> FindVisualDescendantsForTest<T>(DependencyObject root) where T : DependencyObject
{
    for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
    {
        var child = VisualTreeHelper.GetChild(root, index);
        if (child is T match)
        {
            yield return match;
        }

        foreach (var descendant in FindVisualDescendantsForTest<T>(child))
        {
            yield return descendant;
        }
    }
}

static void InvokeLoadDataset(MainWindowViewModel viewModel, ProjectDataset dataset)
{
    var method = typeof(MainWindowViewModel).GetMethod("LoadDataset", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new MissingMethodException(typeof(MainWindowViewModel).FullName, "LoadDataset");
    method.Invoke(viewModel, [dataset, false]);
}

static MainWindowViewModel CreateViewModel()
{
    return new MainWindowViewModel(new MainWindowViewModelDependencies
    {
        UserPreferencesService = new InMemoryUserPreferencesService(),
        // The UI-behaviour fixtures below target the legacy workbook scenario.
        // Startup loading of the packaged accounting export is covered above.
        InitialDatasetFactory = () => new ProjectFileService().Load(Path.Combine(
            FindRepositoryRoot(), "src", "ProjectCostForecast.App", "Data", "SampleData.json"))
    });
}

sealed class InMemoryUserPreferencesService : IUserPreferencesService
{
    private AppUserPreferences _preferences = new();

    public AppUserPreferences Load() => _preferences;

    public void Save(AppUserPreferences preferences)
    {
        _preferences = preferences;
    }
}

sealed record SharedGridTestRow(string Name, int Value, string Group);
