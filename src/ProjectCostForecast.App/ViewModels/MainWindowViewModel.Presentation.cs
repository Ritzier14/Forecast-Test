using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;

namespace ProjectCostForecast.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    public void AddNewCalendarYear()
    {
        var selectedYearsBeforeAdd = _selectedCtcMonthForecastYears.ToHashSet();
        var existingYears = _dataset.ForecastPeriods
            .Where(period => period.StartDate.HasValue)
            .Select(period => period.StartDate!.Value.Year)
            .ToList();
        var nextYear = (existingYears.Count == 0 ? _clock.TodayInNewZealand.Year : existingYears.Max()) + 1;

        for (var month = 1; month <= 12; month++)
        {
            var start = new DateOnly(nextYear, month, 1);
            var label = FiscalPeriod.LabelFromCalendarMonth(start);
            if (_dataset.ForecastPeriods.Any(period => string.Equals(period.Label, label, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            _dataset.ForecastPeriods.Add(new ForecastPeriod
            {
                Label = label,
                Column = label,
                StartDate = start
            });
        }

        _projectDatasetMigrationPipeline.Normalize(_dataset);
        RebuildCtcMonthForecastYearOptions();

        // Rebuilding the available-year list can restore the persisted
        // selection, but the add action must never replace the years the user
        // was already viewing. Keep the pre-add selection and include the new
        // year alongside it.
        var selectedYears = selectedYearsBeforeAdd
            .Where(year => AvailableCtcMonthForecastYears.Contains(year))
            .ToHashSet();
        foreach (var year in _selectedCtcMonthForecastYears)
        {
            selectedYears.Add(year);
        }

        selectedYears.Add(nextYear);
        _selectedCtcMonthForecastYears.Clear();
        foreach (var year in selectedYears.OrderBy(year => year))
        {
            _selectedCtcMonthForecastYears.Add(year);
        }

        _selectedCtcMonthForecastYear = nextYear;
        _dataset.SelectedCtcMonthForecastYears = _selectedCtcMonthForecastYears.OrderBy(year => year).ToList();
        OnPropertyChanged(nameof(SelectedCtcMonthForecastYear));
        OnPropertyChanged(nameof(SelectedCtcMonthForecastYears));
        RebuildCtcMonthForecastColumns();
        RebuildMonthlyForecastPresentationRows();
        RecalculateAndRefresh(markDirty: true, reason: $"Added calendar year {nextYear}", rebuildFilterLists: false);
    }

    public bool IsMonthlyForecastMonthsAcross =>
        string.Equals(ActiveDetailWorkspaceKey, "Ledger Monthly Forecast", StringComparison.OrdinalIgnoreCase)
        && (string.Equals(SelectedDetailWorkspaceView?.ContentKey, "MonthsAcross", StringComparison.OrdinalIgnoreCase)
            || string.Equals(SelectedDetailWorkspaceView?.ContentKey, "Across", StringComparison.OrdinalIgnoreCase));

    public string TaskCodeReviewDisplayMode
    {
        get => _taskCodeReviewDisplayMode;
        set
        {
            var next = TaskCodeReviewDisplayModes.Contains(value)
                ? value
                : "Assigned Name";
            if (SetProperty(ref _taskCodeReviewDisplayMode, next))
            {
                OnPropertyChanged(nameof(TaskCodeReviewRows));
            }
        }
    }

    public void ToggleMonthlyForecastOrientation()
    {
        if (!string.Equals(ActiveDetailWorkspaceKey, "Ledger Monthly Forecast", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SetSelectedDetailWorkspaceContentKey(IsMonthlyForecastMonthsAcross ? "MonthsDown" : "MonthsAcross");
    }

    public string? GetWorkspaceViewIconKey(WorkspaceViewTab? view)
    {
        return view?.IconKey;
    }

    public string? GetWorkspaceViewIconColorHex(WorkspaceViewTab? view)
    {
        return view?.IconColorHex;
    }

    public void SetWorkspaceViewIcon(WorkspaceViewTab? view, string? iconKey, string? iconColorHex)
    {
        if (view is null)
        {
            return;
        }

        view.IconKey = iconKey?.Trim() ?? string.Empty;
        view.IconColorHex = iconColorHex?.Trim() ?? string.Empty;
        IsDirty = true;
    }

    private void RebuildMonthlyForecastPresentationRows()
    {
        if (!IsMonthlyForecastMonthsAcross)
        {
            ReplaceCollection(MonthlyForecastAcrossRows, []);
            return;
        }

        var lines = GetMonthlyForecastPresentationLines().ToList();
        var periods = CtcMonthForecastColumns
            .Where(column => !column.IsTotal && !string.IsNullOrWhiteSpace(column.Key))
            .Select(column => column.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (periods.Count == 0)
        {
            periods = lines
                .SelectMany(line => line.MonthlyForecasts)
                .Where(forecast => !string.IsNullOrWhiteSpace(forecast.PeriodLabel))
                .OrderBy(forecast => forecast.PeriodStartDate ?? DateOnly.MaxValue)
                .ThenBy(forecast => forecast.PeriodLabel)
                .Select(forecast => forecast.PeriodLabel)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var rows = new List<MonthlyForecastAcrossRow>();
        foreach (var line in lines)
        {
            var forecasts = line.MonthlyForecasts
                .Where(forecast => !string.IsNullOrWhiteSpace(forecast.PeriodLabel))
                .ToDictionary(forecast => forecast.PeriodLabel, StringComparer.OrdinalIgnoreCase);
            rows.Add(new MonthlyForecastAcrossRow(
                line.ResourceName,
                line.TaskNumber,
                "Forecast",
                periods.Select(period => new KeyValuePair<string, decimal>(
                    period,
                    forecasts.GetValueOrDefault(period)?.Amount ?? 0m)),
                (period, value) => SetMonthlyForecastAcrossValue(line, period, value)));

            rows.Add(new MonthlyForecastAcrossRow(
                line.ResourceName,
                line.TaskNumber,
                "Actual cost",
                periods.Select(period => new KeyValuePair<string, decimal>(
                    period,
                    forecasts.GetValueOrDefault(period)?.ActualCostAmount ?? 0m))));
        }

        ReplaceCollection(MonthlyForecastAcrossRows, rows);
    }

    private IEnumerable<ForecastLine> GetMonthlyForecastPresentationLines()
    {
        var activeLine = GetActiveLedgerForecastLine();
        if (activeLine is not null)
        {
            return [activeLine];
        }

        if (SelectedResourceSummary is not null)
        {
            var resource = CalculationService.Normalise(SelectedResourceSummary.ResourceName);
            return ForecastLines.Where(line => string.Equals(
                CalculationService.Normalise(line.ResourceName),
                resource,
                StringComparison.OrdinalIgnoreCase));
        }

        return [];
    }

    private void SetMonthlyForecastAcrossValue(ForecastLine line, string periodLabel, decimal value)
    {
        var forecast = line.MonthlyForecasts.FirstOrDefault(item =>
            string.Equals(item.PeriodLabel, periodLabel, StringComparison.OrdinalIgnoreCase));
        if (forecast is null || forecast.IsLocked || IsViewingSavedMonth)
        {
            return;
        }

        if (forecast.Amount == value)
        {
            return;
        }

        BeginSpreadsheetEditBatch();
        forecast.Amount = value;
        line.NotifyMonthForecastValuesChanged();
        EndSpreadsheetEditBatch("Monthly forecast edited", changed: true, rebuildFilterLists: false);
    }

    private void RebuildTaskCodeReviewRows()
    {
        var taskCodes = ProjectTaskCodes
            .Where(task => !string.IsNullOrWhiteSpace(task.SystemCode))
            .ToDictionary(task => task.SystemCode, StringComparer.OrdinalIgnoreCase);

        var linesByTask = ForecastLines
            .Where(line => !string.IsNullOrWhiteSpace(line.TaskNumber))
            .GroupBy(line => line.TaskNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var keys = taskCodes.Keys
            .Concat(linesByTask.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase);

        var rows = keys.Select(taskCode =>
        {
            taskCodes.TryGetValue(taskCode, out var task);
            var lines = linesByTask.GetValueOrDefault(taskCode) ?? [];
            var assignedName = task is not null && !string.IsNullOrWhiteSpace(task.TaskName)
                ? task.TaskName
                : lines.Select(line => line.TaskName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? "Unnamed task";
            var categories = lines
                .Select(line => string.IsNullOrWhiteSpace(line.ReportingCategory) ? line.ProjectCode : line.ReportingCategory)
                .Where(category => !string.IsNullOrWhiteSpace(category))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(category => category, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new TaskCodeReviewRow
            {
                TaskCode = taskCode,
                AssignedName = assignedName,
                Category = categories.Count == 0 ? assignedName : string.Join(", ", categories)
            };
        }).ToList();

        ReplaceCollection(TaskCodeReviewRows, rows);
        OnPropertyChanged(nameof(TaskCodeReviewRows));
    }
}
