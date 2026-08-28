using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App.Services;

public enum NewMonthPreparationStatus
{
    Ready,
    MissingCurrentPeriod,
    AlreadySaved
}

public sealed record NewMonthPreparation(
    NewMonthPreparationStatus Status,
    ProjectDataset? StagedDataset,
    string CurrentPeriod,
    string NextPeriod,
    string Message)
{
    public bool IsReady => Status == NewMonthPreparationStatus.Ready && StagedDataset is not null;
}

public sealed class NewMonthOperation
{
    private readonly CalculationService _calculationService;
    private readonly ProjectDatasetCloner _datasetCloner;
    private readonly IClock _clock;

    public NewMonthOperation(
        CalculationService calculationService,
        ProjectDatasetCloner datasetCloner,
        IClock? clock = null)
    {
        _calculationService = calculationService ?? throw new ArgumentNullException(nameof(calculationService));
        _datasetCloner = datasetCloner ?? throw new ArgumentNullException(nameof(datasetCloner));
        _clock = clock ?? SystemClock.Instance;
    }

    public NewMonthPreparation Prepare(ProjectDataset source, DateTime? savedAt = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        var timestamp = savedAt is { } legacyTimestamp
            ? DateTimeContract.FromDateTime(legacyTimestamp)
            : _clock.UtcNow;

        return PrepareAt(source, timestamp);
    }

    public SavedMonthSnapshot BuildSnapshot(
        string period,
        IEnumerable<ForecastLine> forecastLines)
    {
        return BuildSavedMonthSnapshotAt(period, forecastLines, _clock.UtcNow);
    }

    private NewMonthPreparation PrepareAt(ProjectDataset source, DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(source);

        var staged = _datasetCloner.Clone(source);
        staged.Header ??= new ProjectHeader();
        staged.ForecastLines ??= [];
        staged.SavedMonthSnapshots ??= [];
        staged.AuditEvents ??= [];

        var currentPeriod = staged.Header.CurrentPeriod;
        if (string.IsNullOrWhiteSpace(currentPeriod))
        {
            return new(
                NewMonthPreparationStatus.MissingCurrentPeriod,
                null,
                string.Empty,
                string.Empty,
                "Set a current period before creating a new month baseline.");
        }

        if (staged.SavedMonthSnapshots.Any(snapshot =>
                string.Equals(snapshot.Period, currentPeriod, StringComparison.OrdinalIgnoreCase)))
        {
            return new(
                NewMonthPreparationStatus.AlreadySaved,
                null,
                currentPeriod,
                string.Empty,
                $"A baseline for {currentPeriod} already exists; no duplicate baseline was created.");
        }

        _calculationService.Recalculate(staged);
        var nextPeriod = GetNextForecastPeriod(staged, currentPeriod);
        var snapshot = BuildSavedMonthSnapshotAt(currentPeriod, staged.ForecastLines, timestamp);
        staged.SavedMonthSnapshots.Insert(0, snapshot);

        foreach (var line in staged.ForecastLines)
        {
            line.LastMonthPlannedCost = line.PlannedCostFcc;
            line.LastMonthForecast = line.MonthForecast;
        }

        if (!string.IsNullOrWhiteSpace(nextPeriod))
        {
            staged.Header.CurrentPeriod = nextPeriod;
        }

        var auditTimestamp = DateTimeContract.NormalizeUtc(timestamp);
        AddAuditEvent(
            staged,
            new AuditEvent
            {
                EntityType = "SavedMonth",
                EntityId = currentPeriod,
                FieldName = "Baseline",
                NewValue = DateTimeContract.FormatUtc(snapshot.SavedAt),
                ChangedAt = auditTimestamp,
                Reason = "Created new month baseline"
            });
        AddAuditEvent(
            staged,
            new AuditEvent
            {
                EntityType = "SavedMonth",
                EntityId = currentPeriod,
                FieldName = "FutureAction",
                NewValue = "UnlockOpenSavedMonth",
                ChangedAt = auditTimestamp,
                Reason = "Future unlock-open-saved-month action recorded"
            });

        _calculationService.Recalculate(staged);
        return new(
            NewMonthPreparationStatus.Ready,
            staged,
            currentPeriod,
            nextPeriod,
            string.IsNullOrWhiteSpace(nextPeriod)
                ? $"Saved {currentPeriod} baseline"
                : $"Saved {currentPeriod} baseline and moved to {nextPeriod}");
    }

    public static SavedMonthSnapshot BuildSavedMonthSnapshot(
        string period,
        IEnumerable<ForecastLine> forecastLines,
        DateTime? savedAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(period);
        ArgumentNullException.ThrowIfNull(forecastLines);

        var timestamp = savedAt is { } legacyTimestamp
            ? DateTimeContract.FromDateTime(legacyTimestamp)
            : SystemClock.Instance.UtcNow;
        return BuildSavedMonthSnapshotAt(period, forecastLines, timestamp);
    }

    public static SavedMonthSnapshot BuildSavedMonthSnapshotAt(
        string period,
        IEnumerable<ForecastLine> forecastLines,
        DateTimeOffset savedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(period);
        ArgumentNullException.ThrowIfNull(forecastLines);

        var lines = forecastLines.Select(line => new SavedMonthForecastLine
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
            SavedAt = DateTimeContract.NormalizeUtc(savedAt),
            CostToDate = lines.Sum(line => line.CostToDate),
            CostToComplete = lines.Sum(line => line.CostToComplete),
            FinalForecast = lines.Sum(line => line.FinalForecast),
            TotalBudgetVariance = lines.Sum(line => line.TotalBudgetVariance),
            ForecastLines = lines
        };
    }

    public static string GetNextForecastPeriod(ProjectDataset dataset, string currentPeriod)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentPeriod);

        var periods = dataset.ForecastPeriods
            .Where(period => !string.IsNullOrWhiteSpace(period.Label))
            .OrderBy(period => period.StartDate ?? DateOnly.MaxValue)
            .ThenBy(period => period.Label)
            .Select(period => period.Label)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var index = periods.FindIndex(period => string.Equals(period, currentPeriod, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < periods.Count ? periods[index + 1] : string.Empty;
    }

    private static void AddAuditEvent(ProjectDataset dataset, AuditEvent auditEvent)
    {
        dataset.AuditEvents.Insert(0, auditEvent);
    }
}
