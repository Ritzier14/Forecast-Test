using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App.Services;

public sealed class ProjectFileFormatException : Exception
{
    public ProjectFileFormatException(string message)
        : base(message)
    {
    }

    public ProjectFileFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed record ProjectDatasetMigrationResult(
    ProjectDataset Dataset,
    int SourceVersion,
    bool DataChanged)
{
    public bool WasMigrated => SourceVersion != ProjectDatasetMigrationPipeline.CurrentVersion || DataChanged;
}

/// <summary>
/// Owns the project-file format boundary. It deliberately contains no WPF or
/// view-model concerns so old files can be migrated and checked headlessly.
/// </summary>
public sealed class ProjectDatasetMigrationPipeline
{
    public const int LegacyUnversionedVersion = 0;
    public const int CurrentVersion = 1;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        WriteIndented = true
    };

    public ProjectDatasetMigrationPipeline()
    {
        DateTimeContract.AddJsonConverters(_jsonOptions);
    }

    public ProjectDatasetMigrationResult Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new ProjectFileFormatException(
                    "The project file must contain a JSON object; a JSON null or another root value is not a project file.");
            }

            var sourceVersion = ReadSourceVersion(root);
            if (sourceVersion > CurrentVersion)
            {
                throw new ProjectFileFormatException(
                    $"The project file uses format version {sourceVersion}, but this application supports up to version {CurrentVersion}. Update the application before opening this file.");
            }

            var dataset = root.Deserialize<ProjectDataset>(_jsonOptions)
                ?? throw new ProjectFileFormatException("The project file did not contain a project dataset.");

            return Migrate(dataset, sourceVersion);
        }
        catch (ProjectFileFormatException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            throw new ProjectFileFormatException(
                "The project file is malformed or contains an invalid value.",
                ex);
        }
        catch (NotSupportedException ex)
        {
            throw new ProjectFileFormatException(
                "The project file contains a value that this application cannot read.",
                ex);
        }
    }

    public ProjectDatasetMigrationResult Normalize(ProjectDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        return Migrate(dataset, CurrentVersion);
    }

    public ProjectDatasetMigrationResult PrepareForSave(ProjectDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        return Migrate(dataset, CurrentVersion);
    }

    private static int ReadSourceVersion(JsonElement root)
    {
        var found = false;
        var version = LegacyUnversionedVersion;
        foreach (var property in root.EnumerateObject())
        {
            if (!string.Equals(property.Name, nameof(ProjectDataset.FormatVersion), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (found)
            {
                throw new ProjectFileFormatException("The project file contains the format version more than once.");
            }

            found = true;
            if (property.Value.ValueKind != JsonValueKind.Number
                || !property.Value.TryGetInt32(out version)
                || version < LegacyUnversionedVersion)
            {
                throw new ProjectFileFormatException(
                    $"The project file format version must be a non-negative integer; received {property.Value}." );
            }
        }

        return version;
    }

    private static ProjectDatasetMigrationResult Migrate(ProjectDataset dataset, int sourceVersion)
    {
        var dataChanged = NormalizeDataset(dataset);
        dataset.FormatVersion = CurrentVersion;
        return new ProjectDatasetMigrationResult(dataset, sourceVersion, dataChanged);
    }

    private static bool NormalizeDataset(ProjectDataset dataset)
    {
        var changed = false;

        if (dataset.Header is null)
        {
            dataset.Header = new ProjectHeader();
            changed = true;
        }

        dataset.Phases = EnsureList(dataset.Phases, ref changed);
        dataset.ForecastPeriods = EnsureList(dataset.ForecastPeriods, ref changed);
        dataset.FiscalYearBudgets = EnsureList(dataset.FiscalYearBudgets, ref changed);
        dataset.BudgetLines = EnsureList(dataset.BudgetLines, ref changed);
        dataset.ForecastLines = EnsureList(dataset.ForecastLines, ref changed);
        dataset.ProjectTaskCodes = EnsureList(dataset.ProjectTaskCodes, ref changed);
        dataset.ProjectCategories = EnsureList(dataset.ProjectCategories, ref changed);
        dataset.ManagementResources = EnsureList(dataset.ManagementResources, ref changed);
        dataset.Transactions = EnsureList(dataset.Transactions, ref changed);
        dataset.UnmatchedImportCombinations = EnsureList(dataset.UnmatchedImportCombinations, ref changed);
        dataset.ContingencyEntries = EnsureList(dataset.ContingencyEntries, ref changed);
        dataset.CategorySummaries = EnsureList(dataset.CategorySummaries, ref changed);
        dataset.CostCenterNameMappings = EnsureList(dataset.CostCenterNameMappings, ref changed);
        dataset.SavedMonthSnapshots = EnsureList(dataset.SavedMonthSnapshots, ref changed);
        dataset.AuditEvents = EnsureList(dataset.AuditEvents, ref changed);
        dataset.WorkspaceViews = EnsureList(dataset.WorkspaceViews, ref changed);
        dataset.WorkspaceTabOrder = EnsureList(dataset.WorkspaceTabOrder, ref changed);
        dataset.DetailWorkspaceTabOrder = EnsureList(dataset.DetailWorkspaceTabOrder, ref changed);
        dataset.SelectedCtcMonthForecastYears = EnsureList(dataset.SelectedCtcMonthForecastYears, ref changed);

        if (dataset.ForecastGroupHeaderIconKeys is null)
        {
            dataset.ForecastGroupHeaderIconKeys = new(StringComparer.OrdinalIgnoreCase);
            changed = true;
        }

        if (dataset.ForecastGroupHeaderIconColorHexes is null)
        {
            dataset.ForecastGroupHeaderIconColorHexes = new(StringComparer.OrdinalIgnoreCase);
            changed = true;
        }

        if (dataset.ForecastCalendarYearHeaderColorHexes is null)
        {
            dataset.ForecastCalendarYearHeaderColorHexes = new(StringComparer.OrdinalIgnoreCase);
            changed = true;
        }

        if (dataset.ForecastFiscalYearHeaderColorHexes is null)
        {
            dataset.ForecastFiscalYearHeaderColorHexes = new(StringComparer.OrdinalIgnoreCase);
            changed = true;
        }

        if (dataset.ForecastGroupHeaderColorHexes is null)
        {
            dataset.ForecastGroupHeaderColorHexes = new(StringComparer.OrdinalIgnoreCase);
            changed = true;
        }

        if (dataset.Schedule is null)
        {
            dataset.Schedule = new ScheduleData();
            changed = true;
        }

        foreach (var budgetLine in dataset.BudgetLines)
        {
            budgetLine.Amounts = EnsureList(budgetLine.Amounts, ref changed);
        }

        foreach (var line in dataset.ForecastLines)
        {
            line.ResourceCommentMetrics = EnsureList(line.ResourceCommentMetrics, ref changed);
            line.MonthlyCommentHistory = EnsureList(line.MonthlyCommentHistory, ref changed);
            line.MonthlyForecasts = EnsureList(line.MonthlyForecasts, ref changed);
            line.TaskPhases = EnsureList(line.TaskPhases, ref changed);
            line.TaskCostLines = EnsureList(line.TaskCostLines, ref changed);

            var metricKeysBefore = string.Join(
                "\u001F",
                line.ResourceCommentMetrics.Select(metric => $"{metric.Key}:{metric.Label}:{metric.IsVisible}:{metric.DisplayOrder}"));
            line.EnsureResourceCommentMetrics();
            var metricKeysAfter = string.Join(
                "\u001F",
                line.ResourceCommentMetrics.Select(metric => $"{metric.Key}:{metric.Label}:{metric.IsVisible}:{metric.DisplayOrder}"));
            changed |= !string.Equals(metricKeysBefore, metricKeysAfter, StringComparison.Ordinal);
        }

        foreach (var resource in dataset.ManagementResources)
        {
            resource.MonthlyAllocations = EnsureList(resource.MonthlyAllocations, ref changed);
        }

        foreach (var snapshot in dataset.SavedMonthSnapshots)
        {
            snapshot.ForecastLines = EnsureList(snapshot.ForecastLines, ref changed);
            foreach (var line in snapshot.ForecastLines)
            {
                line.MonthlyForecasts = EnsureList(line.MonthlyForecasts, ref changed);
            }
        }

        foreach (var workspace in dataset.WorkspaceViews)
        {
            workspace.HiddenColumnKeys = EnsureList(workspace.HiddenColumnKeys, ref changed);
            workspace.ColumnLayouts = EnsureList(workspace.ColumnLayouts, ref changed);
            workspace.ReportCanvasObjects = EnsureList(workspace.ReportCanvasObjects, ref changed);
            foreach (var canvasObject in workspace.ReportCanvasObjects)
            {
                canvasObject.ReportChartCostCodeFilters = EnsureList(canvasObject.ReportChartCostCodeFilters, ref changed);
                canvasObject.ReportChartValueKeys = EnsureList(canvasObject.ReportChartValueKeys, ref changed);
            }
        }

        dataset.Schedule.Calendars = EnsureList(dataset.Schedule.Calendars, ref changed);
        dataset.Schedule.Activities = EnsureList(dataset.Schedule.Activities, ref changed);
        dataset.Schedule.Links = EnsureList(dataset.Schedule.Links, ref changed);
        dataset.Schedule.Baselines = EnsureList(dataset.Schedule.Baselines, ref changed);
        foreach (var calendar in dataset.Schedule.Calendars)
        {
            if (calendar.WorkingDays is null)
            {
                calendar.WorkingDays = [false, true, true, true, true, true, false];
                changed = true;
            }

            calendar.Holidays = EnsureList(calendar.Holidays, ref changed);
            calendar.ExtraWorkDays = EnsureList(calendar.ExtraWorkDays, ref changed);
        }

        foreach (var baseline in dataset.Schedule.Baselines)
        {
            baseline.Entries = EnsureList(baseline.Entries, ref changed);
        }

        changed |= NormalizeDurableTimestamps(dataset);
        changed |= NormaliseForecastPeriodDates(dataset);
        return changed;
    }

    private static bool NormalizeDurableTimestamps(ProjectDataset dataset)
    {
        var changed = false;

        foreach (var auditEvent in dataset.AuditEvents)
        {
            changed |= NormalizeTimestamp(
                auditEvent.ChangedAt,
                value => auditEvent.ChangedAt = value);
        }

        foreach (var line in dataset.ForecastLines)
        {
            if (line.ManualCommentRecordedAt is { } manualCommentRecordedAt)
            {
                changed |= NormalizeTimestamp(
                    manualCommentRecordedAt,
                    value => line.ManualCommentRecordedAt = value);
            }

            foreach (var comment in line.MonthlyCommentHistory)
            {
                changed |= NormalizeTimestamp(
                    comment.RecordedAt,
                    value => comment.RecordedAt = value);
            }
        }

        foreach (var mapping in dataset.CostCenterNameMappings)
        {
            changed |= NormalizeTimestamp(
                mapping.LastUsedAt,
                value => mapping.LastUsedAt = value);
        }

        foreach (var unmatchedImport in dataset.UnmatchedImportCombinations)
        {
            changed |= NormalizeTimestamp(
                unmatchedImport.RecordedAt,
                value => unmatchedImport.RecordedAt = value);
        }

        foreach (var snapshot in dataset.SavedMonthSnapshots)
        {
            changed |= NormalizeTimestamp(
                snapshot.SavedAt,
                value => snapshot.SavedAt = value);
        }

        foreach (var baseline in dataset.Schedule.Baselines)
        {
            changed |= NormalizeTimestamp(
                baseline.CapturedAt,
                value => baseline.CapturedAt = value);
        }

        return changed;
    }

    private static bool NormalizeTimestamp(
        DateTimeOffset timestamp,
        Action<DateTimeOffset> setTimestamp)
    {
        if (timestamp.Offset == TimeSpan.Zero)
        {
            return false;
        }

        setTimestamp(DateTimeContract.NormalizeUtc(timestamp));
        return true;
    }

    private static List<T> EnsureList<T>(List<T>? values, ref bool changed)
    {
        if (values is not null)
        {
            return values;
        }

        changed = true;
        return [];
    }

    private static bool NormaliseForecastPeriodDates(ProjectDataset dataset)
    {
        var datesByPeriod = new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase);
        var changed = false;

        foreach (var period in dataset.ForecastPeriods)
        {
            var normalizedLabel = FiscalPeriod.NormaliseLabel(period.Label);
            if (!string.Equals(period.Label, normalizedLabel, StringComparison.Ordinal))
            {
                period.Label = normalizedLabel;
                changed = true;
            }

            if (!FiscalPeriod.TryGetCalendarMonthStart(period.Label, out var calendarMonthStart))
            {
                continue;
            }

            if (period.StartDate != calendarMonthStart)
            {
                period.StartDate = calendarMonthStart;
                changed = true;
            }

            datesByPeriod[period.Label] = calendarMonthStart;
        }

        if (datesByPeriod.Count > 0)
        {
            var firstMonth = datesByPeriod.Values.Min();
            var lastMonth = datesByPeriod.Values.Max();
            for (var monthStart = firstMonth; monthStart.DayNumber <= lastMonth.DayNumber; monthStart = monthStart.AddMonths(1))
            {
                var periodLabel = FiscalPeriod.LabelFromCalendarMonth(monthStart);
                if (datesByPeriod.ContainsKey(periodLabel))
                {
                    continue;
                }

                dataset.ForecastPeriods.Add(new ForecastPeriod
                {
                    Label = periodLabel,
                    StartDate = monthStart
                });
                datesByPeriod[periodLabel] = monthStart;
                changed = true;
            }
        }

        dataset.ForecastPeriods = dataset.ForecastPeriods
            .OrderBy(period => period.StartDate ?? DateOnly.MaxValue)
            .ThenBy(period => FiscalPeriod.SortKey(period.Label))
            .ToList();

        datesByPeriod = dataset.ForecastPeriods
            .Where(period => FiscalPeriod.TryGetCalendarMonthStart(period.Label, out _))
            .GroupBy(period => period.Label, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().StartDate!.Value,
                StringComparer.OrdinalIgnoreCase);

        foreach (var line in dataset.ForecastLines)
        {
            changed |= EnsureForecastPeriods(line, datesByPeriod);
        }

        foreach (var resource in dataset.ManagementResources)
        {
            changed |= EnsureManagementPeriods(resource, datesByPeriod);
        }

        foreach (var snapshot in dataset.SavedMonthSnapshots)
        {
            foreach (var line in snapshot.ForecastLines)
            {
                changed |= EnsureSavedMonthPeriods(line, datesByPeriod);
            }
        }

        return changed;
    }

    private static bool EnsureForecastPeriods(
        ForecastLine line,
        IReadOnlyDictionary<string, DateOnly> datesByPeriod)
    {
        var changed = false;
        foreach (var forecast in line.MonthlyForecasts)
        {
            changed |= NormalizePeriodLabel(
                forecast.PeriodLabel,
                label => forecast.PeriodLabel = label);
        }

        foreach (var period in datesByPeriod)
        {
            var forecast = line.MonthlyForecasts.FirstOrDefault(item =>
                string.Equals(item.PeriodLabel, period.Key, StringComparison.OrdinalIgnoreCase));
            if (forecast is null)
            {
                line.MonthlyForecasts.Add(new MonthlyForecast
                {
                    PeriodLabel = period.Key,
                    PeriodStartDate = period.Value
                });
                changed = true;
                continue;
            }

            changed |= SetCanonicalPeriodDate(
                forecast.PeriodLabel,
                datesByPeriod,
                date => forecast.PeriodStartDate = date,
                forecast.PeriodStartDate);
        }

        return changed;
    }

    private static bool EnsureManagementPeriods(
        ManagementResource resource,
        IReadOnlyDictionary<string, DateOnly> datesByPeriod)
    {
        var changed = false;
        foreach (var allocation in resource.MonthlyAllocations)
        {
            changed |= NormalizePeriodLabel(
                allocation.PeriodLabel,
                label => allocation.PeriodLabel = label);
        }

        foreach (var period in datesByPeriod)
        {
            var allocation = resource.MonthlyAllocations.FirstOrDefault(item =>
                string.Equals(item.PeriodLabel, period.Key, StringComparison.OrdinalIgnoreCase));
            if (allocation is null)
            {
                resource.MonthlyAllocations.Add(new ManagementResourceAllocation
                {
                    PeriodLabel = period.Key,
                    PeriodStartDate = period.Value
                });
                changed = true;
                continue;
            }

            changed |= SetCanonicalPeriodDate(
                allocation.PeriodLabel,
                datesByPeriod,
                date => allocation.PeriodStartDate = date,
                allocation.PeriodStartDate);
        }

        return changed;
    }

    private static bool EnsureSavedMonthPeriods(
        SavedMonthForecastLine line,
        IReadOnlyDictionary<string, DateOnly> datesByPeriod)
    {
        var changed = false;
        foreach (var forecast in line.MonthlyForecasts)
        {
            changed |= NormalizePeriodLabel(
                forecast.PeriodLabel,
                label => forecast.PeriodLabel = label);
        }

        foreach (var period in datesByPeriod)
        {
            var forecast = line.MonthlyForecasts.FirstOrDefault(item =>
                string.Equals(item.PeriodLabel, period.Key, StringComparison.OrdinalIgnoreCase));
            if (forecast is null)
            {
                line.MonthlyForecasts.Add(new SavedMonthPeriodAmount
                {
                    PeriodLabel = period.Key,
                    PeriodStartDate = period.Value
                });
                changed = true;
                continue;
            }

            changed |= SetCanonicalPeriodDate(
                forecast.PeriodLabel,
                datesByPeriod,
                date => forecast.PeriodStartDate = date,
                forecast.PeriodStartDate);
        }

        return changed;
    }

    private static bool NormalizePeriodLabel(string? value, Action<string> setLabel)
    {
        var normalized = FiscalPeriod.NormaliseLabel(value);
        if (string.Equals(value, normalized, StringComparison.Ordinal))
        {
            return false;
        }

        setLabel(normalized);
        return true;
    }

    private static bool SetCanonicalPeriodDate(
        string? periodLabel,
        IReadOnlyDictionary<string, DateOnly> datesByPeriod,
        Action<DateOnly> setDate,
        DateOnly? currentDate)
    {
        if (string.IsNullOrWhiteSpace(periodLabel)
            || !datesByPeriod.TryGetValue(periodLabel, out var canonicalDate))
        {
            return false;
        }

        if (currentDate == canonicalDate)
        {
            return false;
        }

        setDate(canonicalDate);
        return true;
    }
}
