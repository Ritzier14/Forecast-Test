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
    private int _pendingSpreadsheetAuditChangeCount;
    private bool _spreadsheetRefreshQueued;
    private string _pendingSpreadsheetRefreshStatus = string.Empty;
    private bool _pendingSpreadsheetRebuildFilterLists;

    private void MonthlyForecastAmountChanged(object? sender, ValueChangedEventArgs<decimal> e)
    {
        if (sender is not MonthlyForecast forecast)
        {
            return;
        }

        if (_spreadsheetEditBatchDepth > 0)
        {
            _pendingSpreadsheetAuditChangeCount++;
            _spreadsheetEditBatchChanged = true;
            return;
        }

        _forecastLineByMonthlyForecast.TryGetValue(forecast, out var line);
        if (line is not null)
        {
            RecalculateForecastLinesForSpreadsheetEdit([line]);
        }

        AddAuditEvent(new AuditEvent
        {
            EntityType = "MonthlyForecast",
            EntityId = line?.RowNumber.ToString() ?? string.Empty,
            FieldName = forecast.PeriodLabel,
            OldValue = e.OldValue.ToString("0.##"),
            NewValue = e.NewValue.ToString("0.##"),
            Reason = "Edited monthly forecast"
        });
        RecalculateAndRefresh(markDirty: true, reason: "Monthly forecast edited", rebuildFilterLists: false);
    }

    private void AddAuditEvent(string entityType, string entityId, string fieldName, string oldValue, string newValue, string reason)
    {
        AddAuditEvent(new AuditEvent
        {
            EntityType = entityType,
            EntityId = entityId,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            Reason = reason
        });
    }

    private void AddAuditEvent(AuditEvent auditEvent)
    {
        AuditEvents.Insert(0, auditEvent);
        _dataset.AuditEvents.Insert(0, auditEvent);
        OnPropertyChanged(nameof(AuditEvents));
    }

    private void RefreshValidation(bool syncDataset = true)
    {
        if (syncDataset)
        {
            SyncDatasetFromCollections();
        }

        ReplaceCollection(ValidationIssues, _validationService.Validate(_dataset));
        OnPropertyChanged(nameof(ValidationIssueCount));
    }

    private void RefreshViewsAndTotals()
    {
        if (IsForecastEditTransactionActive())
        {
            QueueDeferredViewRefresh();
        }
        else
        {
            RefreshViews(ForecastLinesView, RawTransactionsView, ResourceSummariesView);
            RebuildRawTransactionsPivotTable();
        }

        NotifyTotalsChanged();
        NotifyLedgerChanged();
        OnPropertyChanged(nameof(TransactionCount));
        OnPropertyChanged(nameof(ForecastLineCount));
        CommandManager.InvalidateRequerySuggested();
    }

    public void RecalculateForecastLinesForSpreadsheetEdit(IEnumerable<ForecastLine> lines)
    {
        var changedLines = new List<ForecastLine>();
        foreach (var line in lines)
        {
            if (line is not null && !changedLines.Any(existing => ReferenceEquals(existing, line)))
            {
                changedLines.Add(line);
            }
        }

        if (changedLines.Count == 0)
        {
            return;
        }

        foreach (var line in changedLines)
        {
            _calculationService.RecalculateForecastLine(line, _dataset.Transactions, _dataset.Header.CurrentPeriod);
        }

        _dataset.CategorySummaries = _calculationService.RecalculateCategorySummaries(_dataset.ForecastLines);
        ReplaceCollection(CategorySummaries, _dataset.CategorySummaries);
        NotifyTotalsChanged();
    }

    private void NotifyTotalsChanged()
    {
        RecalculateTotals();
        OnPropertyChanged(nameof(TotalForecastCtc));
        OnPropertyChanged(nameof(TotalCostToDate));
        OnPropertyChanged(nameof(PlannedCostFcc));
        OnPropertyChanged(nameof(TotalBudget));
        OnPropertyChanged(nameof(TotalBudgetVariance));
        OnPropertyChanged(nameof(CurrentMonthCostTotal));
        OnPropertyChanged(nameof(RemainingForecastTotal));
        OnPropertyChanged(nameof(MonthlyVarianceTotal));
        OnPropertyChanged(nameof(TotalContingencyRemaining));
        RefreshKpiPills();
        OnPropertyChanged(nameof(FiscalReportSpentTotal));
        OnPropertyChanged(nameof(FiscalReportCostToCompleteTotal));
        OnPropertyChanged(nameof(FiscalReportPlannedCostTotal));
        OnPropertyChanged(nameof(FiscalReportBudgetTotal));
        OnPropertyChanged(nameof(FiscalReportVarianceTotal));
        OnPropertyChanged(nameof(ProjectContingencyTotal));
        OnPropertyChanged(nameof(ContingencyExpendedTotal));
        OnPropertyChanged(nameof(ContingencyProposedTotal));
        OnPropertyChanged(nameof(ContingencyRemainingTotal));
    }

    public void BeginSpreadsheetEditBatch()
    {
        _spreadsheetEditBatchDepth++;
    }

    public void EndSpreadsheetEditBatch(string status, bool changed, bool rebuildFilterLists = true)
    {
        _spreadsheetEditBatchChanged |= changed;
        if (_spreadsheetEditBatchDepth > 0)
        {
            _spreadsheetEditBatchDepth--;
        }

        if (_spreadsheetEditBatchDepth == 0 && _spreadsheetEditBatchChanged)
        {
            _spreadsheetEditBatchChanged = false;
            if (_pendingSpreadsheetAuditChangeCount > 0)
            {
                AddAuditEvent(
                    "MonthlyForecast",
                    "Bulk edit",
                    "Cells changed",
                    string.Empty,
                    _pendingSpreadsheetAuditChangeCount.ToString(),
                    status);
                _pendingSpreadsheetAuditChangeCount = 0;
            }

            QueueSpreadsheetRefresh(status, rebuildFilterLists);
        }
    }

    private void QueueSpreadsheetRefresh(string status, bool rebuildFilterLists)
    {
        _pendingSpreadsheetRefreshStatus = status;
        _pendingSpreadsheetRebuildFilterLists |= rebuildFilterLists;
        IsDirty = true;
        StatusText = status;
        if (_spreadsheetRefreshQueued)
        {
            return;
        }

        _spreadsheetRefreshQueued = true;
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _spreadsheetRefreshQueued = false;
            var pendingStatus = _pendingSpreadsheetRefreshStatus;
            var pendingRebuild = _pendingSpreadsheetRebuildFilterLists;
            _pendingSpreadsheetRefreshStatus = string.Empty;
            _pendingSpreadsheetRebuildFilterLists = false;
            RecalculateAndRefresh(markDirty: true, reason: pendingStatus, pendingRebuild);
        }, DispatcherPriority.ApplicationIdle);
    }

    private void RecalculateTotals()
    {
        var forecastTotals = new AppTotals();
        foreach (var line in ForecastLines)
        {
            forecastTotals.TotalForecastCtc += line.TotalForecastCtc;
            forecastTotals.PlannedCostFcc += line.PlannedCostFcc;
            forecastTotals.TotalBudget += line.Budget;
            forecastTotals.TotalBudgetVariance += line.TotalBudgetVariance;
            forecastTotals.CurrentMonthCostTotal += line.CurrentMonthCost;
            forecastTotals.RemainingForecastTotal += line.TotalForecastCtc;
            forecastTotals.MonthlyVarianceTotal += line.VarianceLastMonthToDate;
        }

        foreach (var transaction in Transactions)
        {
            forecastTotals.TotalCostToDate += transaction.Amount;
        }

        foreach (var fiscalLine in FiscalYearReportLines)
        {
            forecastTotals.FiscalReportSpentTotal += fiscalLine.SpentToDate;
            forecastTotals.FiscalReportCostToCompleteTotal += fiscalLine.CostToComplete;
            forecastTotals.FiscalReportPlannedCostTotal += fiscalLine.PlannedCost;
            forecastTotals.FiscalReportBudgetTotal += fiscalLine.Budget;
            forecastTotals.FiscalReportVarianceTotal += fiscalLine.Variance;
        }

        foreach (var summary in CategorySummaries)
        {
            if (CalculationService.Normalise(summary.ProjectCode).Contains("Contig", StringComparison.OrdinalIgnoreCase))
            {
                forecastTotals.ProjectContingencyTotal += summary.PlannedCost;
            }
        }

        forecastTotals.TotalContingencyRemaining = ContingencyEntries.LastOrDefault()?.RemainingContingency ?? 0;
        foreach (var entry in ContingencyEntries)
        {
            forecastTotals.ContingencyExpendedTotal += entry.ContingencyExpended;
            forecastTotals.ContingencyProposedTotal += entry.ProposedExpenditure;
        }

        forecastTotals.ContingencyRemainingTotal = forecastTotals.ProjectContingencyTotal
            - forecastTotals.ContingencyExpendedTotal
            - forecastTotals.ContingencyProposedTotal;
        _totals = forecastTotals;
    }

    private void NotifyLedgerChanged()
    {
        RefreshLedgerSelectionSnapshots();
        RebuildLedgerTransactionViews();
        OnPropertyChanged(nameof(SelectedMonthlyForecasts));
        OnPropertyChanged(nameof(LedgerTransactions));
        OnPropertyChanged(nameof(LedgerTitle));
        OnPropertyChanged(nameof(LedgerTransactionCount));
        OnPropertyChanged(nameof(LedgerTransactionTotal));
        OnPropertyChanged(nameof(LedgerUnitsTotal));
        OnPropertyChanged(nameof(LedgerAverageRate));
        OnPropertyChanged(nameof(LedgerForecastTotal));
        OnPropertyChanged(nameof(LedgerProjectedTotal));
        RebuildLedgerChart();
    }

    private void QueueLedgerChanged()
    {
        if (_ledgerRefreshQueued)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted)
        {
            NotifyLedgerChanged();
            return;
        }

        _ledgerRefreshQueued = true;
        dispatcher.BeginInvoke(() =>
        {
            _ledgerRefreshQueued = false;
            NotifyLedgerChanged();
        }, DispatcherPriority.Background);
    }

    private void RebuildFilterLists()
    {
        var selectedProjectCode = SelectedProjectCode;
        var selectedPeriod = SelectedPeriod;

        ReplaceCollection(AvailableProjectCodes, new[] { "All" }.Concat(ForecastLines.Select(x => x.ProjectCode).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x)));
        ReplaceCollection(AvailablePeriods, new[] { "All" }.Concat(Transactions.Select(x => x.FyPeriod).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x)));

        _suppressFilterRefresh = true;
        try
        {
            SelectedProjectCode = AvailableProjectCodes.Contains(selectedProjectCode, StringComparer.OrdinalIgnoreCase)
                ? selectedProjectCode
                : "All";
            SelectedPeriod = AvailablePeriods.Contains(selectedPeriod, StringComparer.OrdinalIgnoreCase)
                ? selectedPeriod
                : "All";
        }
        finally
        {
            _suppressFilterRefresh = false;
        }
    }

    private void SyncDatasetFromCollections()
    {
        _dataset.ForecastLines = ForecastLines.ToList();
        _dataset.ManagementResources = ManagementResources.ToList();
        _dataset.Transactions = Transactions.ToList();
        _dataset.CategorySummaries = CategorySummaries.ToList();
        _dataset.ContingencyEntries = ContingencyEntries.ToList();
        _dataset.Phases = Phases.ToList();
        _dataset.SavedMonthSnapshots = SavedMonthSnapshots.OrderBy(snapshot => snapshot.SavedAt).ToList();
        _dataset.UnmatchedImportCombinations = UnmatchedImportCombinations.OrderByDescending(item => item.RecordedAt).ToList();
        _dataset.AuditEvents = AuditEvents.ToList();
        _dataset.WorkspaceViews = BuildWorkspaceViewLayouts();
        _dataset.SelectedCtcMonthForecastYears = _selectedCtcMonthForecastYears
            .OrderBy(year => year)
            .ToList();
        _dataset.ShowCtcMonthForecastYearTotals = ShowCtcMonthForecastYearTotals;
        SyncScheduleToDataset();
    }

    private void ApplyUserPreferences()
    {
        _suppressPreferenceSave = true;
        try
        {
            ShowOnlyLinesWithActualCost = _userPreferences.ShowOnlyLinesWithActualCost;
            ShowCostThisMonthOnly = _userPreferences.ShowCostThisMonthOnly;
            ShowOnlyLinesWithRemainingForecast = _userPreferences.ShowOnlyLinesWithRemainingForecast;
            SelectedMonthlyVarianceFilter = MonthlyVarianceFilters.Contains(_userPreferences.SelectedMonthlyVarianceFilter)
                ? _userPreferences.SelectedMonthlyVarianceFilter
                : "All";
            SelectedBudgetVarianceFilter = BudgetVarianceFilters.Contains(_userPreferences.SelectedBudgetVarianceFilter)
                ? _userPreferences.SelectedBudgetVarianceFilter
                : "All";
            ShowCtcMonthForecastColumns = _userPreferences.ShowCtcMonthForecastColumns;
            ShowMonthNameAboveFiscalPeriod = _userPreferences.ShowMonthNameAboveFiscalPeriod;
            ShowCtcMonthForecastYearTotals = _userPreferences.ShowCtcMonthForecastYearTotals;
            ForecastFreezeColumnKey = _userPreferences.ForecastFreezeColumnKey;
            KeepColumnHighlightsAcrossTabs = _userPreferences.KeepColumnHighlightsAcrossTabs;
            ShowVarianceIndicators = _userPreferences.ShowVarianceIndicators;
            SelectedProjectCode = AvailableProjectCodes.Contains(_userPreferences.SelectedProjectCode, StringComparer.OrdinalIgnoreCase)
                ? _userPreferences.SelectedProjectCode
                : "All";
            SelectedPeriod = AvailablePeriods.Contains(_userPreferences.SelectedPeriod, StringComparer.OrdinalIgnoreCase)
                ? _userPreferences.SelectedPeriod
                : "All";

            var categorySort = CategorySortOptions.FirstOrDefault(option =>
                string.Equals(option.Key, _userPreferences.SelectedCategorySortOptionKey, StringComparison.OrdinalIgnoreCase));
            if (categorySort is not null)
            {
                SelectedCategorySortOption = categorySort;
            }

            var ledgerRange = LedgerChartRangeOptions.FirstOrDefault(option =>
                string.Equals(option.Key, _userPreferences.SelectedLedgerChartRangeKey, StringComparison.OrdinalIgnoreCase));
            if (ledgerRange is not null)
            {
                SelectedLedgerChartRangeOption = ledgerRange;
            }

            var preferredYears = (_userPreferences.SelectedCtcMonthForecastYears ?? [])
                .Where(AvailableCtcMonthForecastYears.Contains)
                .Distinct()
                .OrderBy(year => year)
                .ToList();

            if (preferredYears.Count > 0)
            {
                _selectedCtcMonthForecastYears.Clear();
                foreach (var year in preferredYears)
                {
                    _selectedCtcMonthForecastYears.Add(year);
                }

                _selectedCtcMonthForecastYear = preferredYears[0];
                OnPropertyChanged(nameof(SelectedCtcMonthForecastYear));
                OnPropertyChanged(nameof(SelectedCtcMonthForecastYears));
                RebuildCtcMonthForecastColumns();
            }
        }
        finally
        {
            _suppressPreferenceSave = false;
        }
    }

    private void SaveUserPreferences()
    {
        if (_suppressPreferenceSave)
        {
            return;
        }

        _userPreferences.SelectedProjectCode = SelectedProjectCode;
        _userPreferences.SelectedPeriod = SelectedPeriod;
        _userPreferences.ShowOnlyLinesWithActualCost = ShowOnlyLinesWithActualCost;
        _userPreferences.ShowCostThisMonthOnly = ShowCostThisMonthOnly;
        _userPreferences.ShowOnlyLinesWithRemainingForecast = ShowOnlyLinesWithRemainingForecast;
        _userPreferences.SelectedMonthlyVarianceFilter = SelectedMonthlyVarianceFilter;
        _userPreferences.SelectedBudgetVarianceFilter = SelectedBudgetVarianceFilter;
        _userPreferences.ShowCtcMonthForecastColumns = ShowCtcMonthForecastColumns;
        _userPreferences.ShowMonthNameAboveFiscalPeriod = ShowMonthNameAboveFiscalPeriod;
        _userPreferences.ShowCtcMonthForecastYearTotals = ShowCtcMonthForecastYearTotals;
        _userPreferences.SelectedCtcMonthForecastYears = _selectedCtcMonthForecastYears.OrderBy(year => year).ToList();
        _userPreferences.ForecastFreezeColumnKey = ForecastFreezeColumnKey;
        _userPreferences.KeepColumnHighlightsAcrossTabs = KeepColumnHighlightsAcrossTabs;
        _userPreferences.ShowVarianceIndicators = ShowVarianceIndicators;
        _userPreferences.SelectedCategorySortOptionKey = SelectedCategorySortOption?.Key ?? "Alphabetical";
        _userPreferences.SelectedLedgerChartRangeKey = SelectedLedgerChartRangeOption?.Key ?? "Last24";
        _userPreferencesService.Save(_userPreferences);
    }

    private List<WorkspaceViewLayout> BuildWorkspaceViewLayouts()
    {
        return _workspaceViews
            .Concat(_detailWorkspaceViews)
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .SelectMany(pair => pair.Value.Select(view => new WorkspaceViewLayout
            {
                WorkspaceKey = NormaliseWorkspaceKey(view.WorkspaceKey),
                ContentKey = view.ContentKey,
                Name = view.Name,
                GroupForecastLinesByTask = view.GroupForecastLinesByTask,
                ForecastGroupByKey = NormalizeForecastGroupByKey(view.ForecastGroupByKey),
                HiddenColumnKeys = view.HiddenColumnKeys
                    .Where(key => !string.IsNullOrWhiteSpace(key))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            }))
            .ToList();
    }

    private static WorkspaceViewTab CreateWorkspaceViewTab(WorkspaceViewLayout layout)
    {
        var groupByKey = NormalizeForecastGroupByKey(layout.ForecastGroupByKey);
        if (string.Equals(groupByKey, ForecastGroupByNoneKey, StringComparison.OrdinalIgnoreCase) && layout.GroupForecastLinesByTask)
        {
            groupByKey = ForecastGroupByTaskKey;
        }

        if (string.Equals(NormaliseWorkspaceKey(layout.WorkspaceKey), "CTC Forecast", StringComparison.OrdinalIgnoreCase)
            && string.Equals(layout.Name, "Default", StringComparison.OrdinalIgnoreCase)
            && string.Equals(groupByKey, ForecastGroupByNoneKey, StringComparison.OrdinalIgnoreCase))
        {
            groupByKey = ForecastGroupByTaskKey;
        }

        return new WorkspaceViewTab
        {
            WorkspaceKey = NormaliseWorkspaceKey(layout.WorkspaceKey),
            ContentKey = layout.ContentKey,
            Name = string.IsNullOrWhiteSpace(layout.Name) ? "View" : layout.Name,
            GroupForecastLinesByTask = string.Equals(groupByKey, ForecastGroupByTaskKey, StringComparison.OrdinalIgnoreCase),
            ForecastGroupByKey = groupByKey,
            HiddenColumnKeys = layout.HiddenColumnKeys?.ToList() ?? []
        };
    }

    private static List<WorkspaceViewLayout> GetDefaultWorkspaceViewLayouts()
    {
        return
        [
            new WorkspaceViewLayout { WorkspaceKey = "CTC Forecast", ContentKey = "Default", Name = "Default", GroupForecastLinesByTask = true, ForecastGroupByKey = ForecastGroupByTaskKey },
            new WorkspaceViewLayout { WorkspaceKey = "Resources", ContentKey = "Default", Name = "Default" },
            new WorkspaceViewLayout { WorkspaceKey = "Raw Transactions", ContentKey = "Default", Name = "Default" },
            new WorkspaceViewLayout { WorkspaceKey = "Summary View", ContentKey = "Default", Name = "Default" },
            new WorkspaceViewLayout { WorkspaceKey = "Monthly Report", ContentKey = "Default", Name = "Default" },
            new WorkspaceViewLayout { WorkspaceKey = "Pivot Builder", ContentKey = "Default", Name = "Default" },
            new WorkspaceViewLayout { WorkspaceKey = "Contingency", ContentKey = "Default", Name = "Default" },
            new WorkspaceViewLayout { WorkspaceKey = "Audit", ContentKey = "Default", Name = "Default" },
            new WorkspaceViewLayout { WorkspaceKey = "Ledger Costs", ContentKey = "Default", Name = "Default" },
            new WorkspaceViewLayout { WorkspaceKey = "Ledger Monthly Forecast", ContentKey = "Default", Name = "Default" },
            new WorkspaceViewLayout { WorkspaceKey = "Ledger Spend Curve", ContentKey = "Default", Name = "Default" }
        ];
    }

    private static string NormalizeForecastGroupByKey(string? groupByKey)
    {
        if (string.Equals(groupByKey, ForecastGroupByTaskKey, StringComparison.OrdinalIgnoreCase))
        {
            return ForecastGroupByTaskKey;
        }

        if (string.Equals(groupByKey, ForecastGroupByResourceKey, StringComparison.OrdinalIgnoreCase))
        {
            return ForecastGroupByResourceKey;
        }

        if (string.Equals(groupByKey, ForecastGroupByCategoryKey, StringComparison.OrdinalIgnoreCase))
        {
            return ForecastGroupByCategoryKey;
        }

        return ForecastGroupByNoneKey;
    }

    private static string NormaliseWorkspaceKey(string? workspaceKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return string.Empty;
        }

        return workspaceKey.Trim() switch
        {
            "Category Report" => "Summary View",
            _ => workspaceKey.Trim()
        };
    }

    private static bool IsDetailWorkspaceKey(string workspaceKey)
    {
        return workspaceKey.StartsWith("Ledger ", StringComparison.OrdinalIgnoreCase);
    }

    private void SubscribeMonthlyForecastEvents()
    {
        _forecastLineByMonthlyForecast.Clear();
        foreach (var line in ForecastLines)
        {
            SubscribeMonthlyForecastEvents(line);
        }
    }

    private void UnsubscribeMonthlyForecastEvents()
    {
        foreach (var line in ForecastLines)
        {
            UnsubscribeMonthlyForecastEvents(line);
        }

        _forecastLineByMonthlyForecast.Clear();
    }

    private void SubscribeMonthlyForecastEvents(ForecastLine line)
    {
        foreach (var forecast in line.MonthlyForecasts)
        {
            forecast.AmountChanged -= MonthlyForecastAmountChanged;
            forecast.AmountChanged += MonthlyForecastAmountChanged;
            _forecastLineByMonthlyForecast[forecast] = line;
        }
    }

    private void UnsubscribeMonthlyForecastEvents(ForecastLine line)
    {
        foreach (var forecast in line.MonthlyForecasts)
        {
            forecast.AmountChanged -= MonthlyForecastAmountChanged;
            _forecastLineByMonthlyForecast.Remove(forecast);
        }
    }

    private bool ConfirmDiscardUnsavedChanges()
    {
        if (!IsDirty)
        {
            return true;
        }

        return MessageBox.Show("There are unsaved changes. Continue without saving?", "Unsaved changes", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        if (target is BatchObservableCollection<T> batchCollection)
        {
            batchCollection.ReplaceWith(source);
            return;
        }

        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private static void AddItems<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        if (target is BatchObservableCollection<T> batchCollection)
        {
            batchCollection.AddRange(source);
            return;
        }

        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private static BatchObservableCollection<T> CreateCollection<T>() => new();

    private static BatchObservableCollection<T> CreateCollection<T>(IEnumerable<T> items) => new(items);
}
