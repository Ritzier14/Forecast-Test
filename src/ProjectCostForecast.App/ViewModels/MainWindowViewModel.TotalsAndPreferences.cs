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
        if (IsViewingSavedMonth)
        {
            SavedMonthForecastAmountChanged(sender, e);
            return;
        }

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
        if (auditEvent.ChangedAt == DateTimeOffset.UnixEpoch)
        {
            auditEvent.ChangedAt = _clock.UtcNow;
        }

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
        OnPropertyChanged(nameof(ValidationSummaryText));
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
        var changedLines = new HashSet<ForecastLine>(ReferenceEqualityComparer.Instance);
        foreach (var line in lines)
        {
            if (line is not null)
            {
                changedLines.Add(line);
            }
        }

        if (changedLines.Count == 0)
        {
            return;
        }

        if (IsViewingSavedMonth && _viewedSavedMonthSnapshot is { } snapshot)
        {
            foreach (var displayLine in changedLines)
            {
                var savedLine = snapshot.ForecastLines.FirstOrDefault(line => line.RowNumber == displayLine.RowNumber);
                if (savedLine is not null)
                {
                    RecalculateSavedMonthDisplayLine(savedLine, displayLine, snapshot.Period);
                }
            }

            RecalculateSavedMonthSnapshotTotals(snapshot);
            NotifyTotalsChanged();
            IsDirty = true;
            return;
        }

        _calculationService.RecalculateForecastLines(changedLines, _dataset.Transactions, _dataset.Header.CurrentPeriod);

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
        RebuildBudgetChart();
    }

    public void BeginSpreadsheetEditBatch()
    {
        _spreadsheetEditBatchDepth++;
    }

    public void EndSpreadsheetEditBatch(string status, bool changed, bool rebuildFilterLists = true)
    {
        if (IsViewingSavedMonth)
        {
            if (_spreadsheetEditBatchDepth > 0)
            {
                _spreadsheetEditBatchDepth--;
            }

            if (changed)
            {
                IsDirty = true;
                StatusText = status;
            }

            _spreadsheetEditBatchChanged = false;
            _pendingSpreadsheetAuditChangeCount = 0;
            return;
        }

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
            if (pendingRebuild)
            {
                RecalculateAndRefresh(markDirty: true, reason: pendingStatus, pendingRebuild);
            }
            else
            {
                RefreshSpreadsheetEditDependents(pendingStatus);
            }
        }, DispatcherPriority.ApplicationIdle);
    }

    private void RefreshSpreadsheetEditDependents(string status)
    {
        SyncDatasetFromCollections();
        ApplyClosedForecastPeriodRule();
        _calculationService.Recalculate(_dataset);
        ReplaceCollection(CategorySummaries, _dataset.CategorySummaries);
        RebuildMonthlyPivotTables();
        RebuildCustomPivot();
        RebuildMonthlyReport();
        RefreshValidation(syncDataset: false);
        NotifyTotalsChanged();
        IsDirty = true;
        StatusText = $"{status}. {ValidationIssueCount} validation issue(s).";
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
        RebuildMonthlyForecastPresentationRows();
        OnPropertyChanged(nameof(SelectedMonthlyForecasts));
        OnPropertyChanged(nameof(MonthlyForecastAcrossRows));
        OnPropertyChanged(nameof(LedgerTransactions));
        OnPropertyChanged(nameof(LedgerTitle));
        OnPropertyChanged(nameof(LedgerTransactionCount));
        OnPropertyChanged(nameof(LedgerTransactionTotal));
        OnPropertyChanged(nameof(LedgerUnitsTotal));
        OnPropertyChanged(nameof(LedgerAverageRate));
        OnPropertyChanged(nameof(LedgerForecastTotal));
        OnPropertyChanged(nameof(LedgerProjectedTotal));
        OnPropertyChanged(nameof(LedgerBudgetTotal));
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
        _dataset.ProjectTaskCodes = ProjectTaskCodes
            .OrderBy(task => task.DisplayOrder)
            .ToList();
        _dataset.ProjectCategories = ProjectCategories
            .OrderBy(category => category.DisplayOrder)
            .ToList();
        _dataset.ManagementResources = ManagementResources.ToList();
        _dataset.ContingencyEntries = ContingencyEntries.ToList();
        _dataset.Phases = Phases.ToList();
        _dataset.UnmatchedImportCombinations = UnmatchedImportCombinations.OrderByDescending(item => item.RecordedAt).ToList();
        _dataset.AuditEvents = AuditEvents.ToList();
        _dataset.WorkspaceViews.ReplaceWith(BuildWorkspaceViewLayouts());
        _dataset.SelectedCtcMonthForecastYears = _selectedCtcMonthForecastYears
            .OrderBy(year => year)
            .ToList();
        _dataset.ShowCtcMonthForecastYearTotals = ShowCtcMonthForecastYearTotals;
        SyncBudgetLinesToDataset();
        SyncScheduleToDataset();
    }

    private void ApplyUserPreferences()
    {
        _suppressPreferenceSave = true;
        try
        {
            var preferredKpiKeys = (_userPreferences.KpiPillKeys ?? [])
                .Where(key => KpiOptions.Any(option => string.Equals(option.Key, key, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (preferredKpiKeys.Count > 0)
            {
                KpiPills.Clear();
                foreach (var key in preferredKpiKeys)
                {
                    AddKpiPill(key);
                }
            }

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
            ShowActualCostInMonthCells = _userPreferences.ShowActualCostInMonthCells;
            IsBudgetColumnUnlocked = _userPreferences.IsBudgetColumnUnlocked;
            ShowCurrencySymbols = _userPreferences.ShowCurrencySymbols;
            ForecastMonthMillionDecimals = _userPreferences.ForecastMonthMillionDecimals < 0
                ? 2
                : _userPreferences.ForecastMonthMillionDecimals;
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

            LedgerChartTimeScale = Enum.TryParse<LedgerChartTimeScale>(
                _userPreferences.SelectedLedgerChartTimeScaleKey,
                ignoreCase: true,
                out var savedTimeScale)
                ? savedTimeScale
                : LedgerChartTimeScale.Month;
            _showLedgerActualSeries = _userPreferences.ShowLedgerActualSeries;
            _showLedgerForecastSeries = _userPreferences.ShowLedgerForecastSeries;
            _showLedgerBudgetSeries = _userPreferences.ShowLedgerBudgetSeries;
            OnPropertyChanged(nameof(ShowLedgerActualSeries));
            OnPropertyChanged(nameof(ShowLedgerForecastSeries));
            OnPropertyChanged(nameof(ShowLedgerBudgetSeries));

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
        _userPreferences.ShowActualCostInMonthCells = ShowActualCostInMonthCells;
        _userPreferences.IsBudgetColumnUnlocked = IsBudgetColumnUnlocked;
        _userPreferences.ShowCurrencySymbols = ShowCurrencySymbols;
        _userPreferences.ForecastMonthMillionDecimals = ForecastMonthMillionDecimals;
        _userPreferences.SelectedCtcMonthForecastYears = _selectedCtcMonthForecastYears.OrderBy(year => year).ToList();
        _userPreferences.ForecastFreezeColumnKey = ForecastFreezeColumnKey;
        _userPreferences.KeepColumnHighlightsAcrossTabs = KeepColumnHighlightsAcrossTabs;
        _userPreferences.ShowVarianceIndicators = ShowVarianceIndicators;
        _userPreferences.SelectedCategorySortOptionKey = SelectedCategorySortOption?.Key ?? "Alphabetical";
        _userPreferences.SelectedLedgerChartRangeKey = SelectedLedgerChartRangeOption?.Key ?? "Last24";
        _userPreferences.SelectedLedgerChartTimeScaleKey = LedgerChartTimeScale.ToString();
        _userPreferences.ShowLedgerActualSeries = ShowLedgerActualSeries;
        _userPreferences.ShowLedgerForecastSeries = ShowLedgerForecastSeries;
        _userPreferences.ShowLedgerBudgetSeries = ShowLedgerBudgetSeries;
        _userPreferences.KpiPillKeys = KpiPills.Select(pill => pill.Key).ToList();
        _userPreferences.ForecastCurvePresets = UserForecastCurvePresets
            .Select(CloneForecastCurvePreset)
            .ToList();
        _preferenceSaveTimer.Stop();
        _preferenceSaveTimer.Start();
    }

    public void FlushUserPreferences()
    {
        _preferenceSaveTimer.Stop();
        PersistUserPreferences();
    }

    private void PersistUserPreferences()
    {
        if (_suppressPreferenceSave)
        {
            return;
        }

        try
        {
            _userPreferencesService.Save(_userPreferences);
        }
        catch (Exception exception)
        {
            StatusText = $"Could not save user preferences: {exception.Message}";
        }
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
                IconKey = view.IconKey,
                IconColorHex = view.IconColorHex,
                GroupForecastLinesByTask = view.GroupForecastLinesByTask,
                ForecastGroupByKey = NormalizeForecastGroupByKey(view.ForecastGroupByKey),
                ShowZeroAsBlank = view.ShowZeroAsBlank,
                ReportCanvasInitialized = view.ReportCanvasInitialized,
                ReportCanvasPageSize = view.ReportCanvasPageSize,
                ReportCanvasOrientation = view.ReportCanvasOrientation,
                ReportCanvasObjects = view.ReportCanvasObjects.Select(item => new ReportCanvasObjectLayout
                {
                    Id = item.Id,
                    ObjectType = item.ObjectType,
                    X = item.X,
                    Y = item.Y,
                    Width = item.Width,
                    Height = item.Height,
                    Text = item.Text,
                    StyleKey = item.StyleKey,
                    ChartKind = item.ChartKind,
                    Grouping = item.Grouping,
                    DataSetKey = item.DataSetKey,
                    ReportChartCostCodeFilterEnabled = item.ReportChartCostCodeFilterEnabled,
                    ReportChartCostCodeFilter = item.ReportChartCostCodeFilter,
                    ReportChartCostCodeFilters = item.ReportChartCostCodeFilters?.ToList() ?? [],
                    ReportChartHeadingFilterEnabled = item.ReportChartHeadingFilterEnabled,
                    ReportChartSubHeadingFilterEnabled = item.ReportChartSubHeadingFilterEnabled,
                    ReportChartValueKeys = item.ReportChartValueKeys?.ToList() ?? [],
                    FromDate = item.FromDate,
                    ToDate = item.ToDate,
                    XAxisTickFrequency = item.XAxisTickFrequency
                }).ToList(),
                HiddenColumnKeys = view.HiddenColumnKeys
                    .Where(key => !string.IsNullOrWhiteSpace(key))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                ColumnLayouts = view.ColumnLayouts
                    .Where(layout => !string.IsNullOrWhiteSpace(layout.Key))
                    .GroupBy(layout => layout.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .OrderBy(layout => layout.DisplayIndex)
                    .Select(layout => new WorkspaceColumnLayout
                    {
                        Key = layout.Key,
                        Width = layout.Width,
                        DisplayIndex = layout.DisplayIndex
                    })
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
            IconKey = layout.IconKey,
            IconColorHex = layout.IconColorHex,
            EditName = string.IsNullOrWhiteSpace(layout.Name) ? "View" : layout.Name,
            DefaultName = string.IsNullOrWhiteSpace(layout.Name) ? "View" : layout.Name,
            RenameRestoreName = string.IsNullOrWhiteSpace(layout.Name) ? "View" : layout.Name,
            GroupForecastLinesByTask = string.Equals(groupByKey, ForecastGroupByTaskKey, StringComparison.OrdinalIgnoreCase),
            ForecastGroupByKey = groupByKey,
            HiddenColumnKeys = layout.HiddenColumnKeys?.ToList() ?? [],
            ColumnLayouts = layout.ColumnLayouts?
                .Where(item => !string.IsNullOrWhiteSpace(item.Key))
                .Select(item => new WorkspaceColumnLayout
                {
                    Key = item.Key,
                    Width = item.Width,
                    DisplayIndex = item.DisplayIndex
                })
                .ToList() ?? [],
            ShowZeroAsBlank = layout.ShowZeroAsBlank,
            ReportCanvasInitialized = layout.ReportCanvasInitialized,
            ReportCanvasPageSize = layout.ReportCanvasPageSize,
            ReportCanvasOrientation = layout.ReportCanvasOrientation,
            ReportCanvasObjects = layout.ReportCanvasObjects?.Select(item => new ReportCanvasObjectLayout
            {
                Id = item.Id,
                ObjectType = item.ObjectType,
                X = item.X,
                Y = item.Y,
                Width = item.Width,
                Height = item.Height,
                Text = item.Text,
                StyleKey = item.StyleKey,
                ChartKind = item.ChartKind,
                Grouping = item.Grouping,
                DataSetKey = item.DataSetKey,
                ReportChartCostCodeFilterEnabled = item.ReportChartCostCodeFilterEnabled,
                ReportChartCostCodeFilter = item.ReportChartCostCodeFilter,
                ReportChartCostCodeFilters = item.ReportChartCostCodeFilters?.ToList() ?? [],
                ReportChartHeadingFilterEnabled = item.ReportChartHeadingFilterEnabled,
                ReportChartSubHeadingFilterEnabled = item.ReportChartSubHeadingFilterEnabled,
                ReportChartValueKeys = item.ReportChartValueKeys?.ToList() ?? [],
                FromDate = item.FromDate,
                ToDate = item.ToDate,
                XAxisTickFrequency = item.XAxisTickFrequency
            }).ToList() ?? []
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
            new WorkspaceViewLayout { WorkspaceKey = "Budget", ContentKey = "Default", Name = "Default" },
            new WorkspaceViewLayout { WorkspaceKey = "Ledger Costs", ContentKey = "Default", Name = "Default" },
            new WorkspaceViewLayout { WorkspaceKey = "Ledger Monthly Forecast", ContentKey = "MonthsDown", Name = "Months Down" },
            new WorkspaceViewLayout { WorkspaceKey = "Ledger Monthly Forecast", ContentKey = "MonthsAcross", Name = "Months Across" },
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
