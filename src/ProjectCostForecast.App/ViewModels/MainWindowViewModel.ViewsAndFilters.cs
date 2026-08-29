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
    private void RecalculateAndRefresh(
        bool markDirty,
        string reason,
        bool rebuildFilterLists = true,
        bool includeRawTransactionsPivot = false)
    {
        var projections =
            ViewRefreshProjections
            | RefreshProjection.CalculatedViews
            | RefreshProjection.Totals
            | RefreshProjection.Ledger;
        if (includeRawTransactionsPivot)
        {
            projections |= RefreshProjection.RawTransactionsPivot;
        }

        RequestRefresh(new RefreshRequest(
            projections,
            reason,
            Recalculate: true,
            RebuildFilterLists: rebuildFilterLists,
            MarkDirty: markDirty));
    }

    private void RebuildCalculatedViews(bool rebuildFilterLists, bool rebuildRawTransactionsPivot = true)
    {
        _refreshCoordinator.Measure(
            RefreshPhase.CalculatedViews,
            () => RebuildCalculatedViewsCore(rebuildFilterLists, rebuildRawTransactionsPivot));
    }

    private void RebuildCalculatedViewsCore(bool rebuildFilterLists, bool rebuildRawTransactionsPivot)
    {
        var selectedResourceName = SelectedResourceSummary?.ResourceName;
        RebuildForecastLineLookups();
        RebuildTaskCodeReviewRows();
        InvalidatePivotFilterValues();
        ReplaceCollection(CategorySummaries, _dataset.CategorySummaries);
        ReplaceCollection(ResourceSummaries, BuildResourceSummaries(_dataset.Transactions, _dataset.ForecastLines));
        RestoreSelectedResourceSummary(selectedResourceName);
        ReplaceCollection(FiscalYearReportLines, _calculationService.BuildFiscalYearReport(_dataset));
        ReplaceCollection(ActualsPeriodSummaries, _calculationService.BuildActualsPeriodSummaries(_dataset.Transactions));
        RebuildMonthlyPivotTables(rebuildRawTransactionsPivot);
        RebuildCustomPivot();
        RebuildMonthlyReport();
        ApplyForecastPeriodLockStates();
        if (rebuildFilterLists)
        {
            _refreshCoordinator.Measure(RefreshPhase.FilterLists, RebuildFilterLists);
        }

        RefreshValidation(syncDataset: false);
    }

    private void RestoreSelectedResourceSummary(string? resourceName)
    {
        if (string.IsNullOrWhiteSpace(resourceName))
        {
            if (_selectedResourceSummary is not null)
            {
                _selectedResourceSummary = null;
                OnPropertyChanged(nameof(SelectedResourceSummary));
            }

            return;
        }

        var restored = ResourceSummaries.FirstOrDefault(summary =>
            string.Equals(summary.ResourceName, resourceName, StringComparison.OrdinalIgnoreCase));
        if (ReferenceEquals(_selectedResourceSummary, restored))
        {
            return;
        }

        _selectedResourceSummary = restored;
        OnPropertyChanged(nameof(SelectedResourceSummary));
    }

    private bool IsForecastEditTransactionActive()
    {
        return ForecastLinesView is IEditableCollectionView editableView
            && (editableView.IsAddingNew || editableView.IsEditingItem);
    }

    private void QueueDeferredViewRefresh()
    {
        RequestRefresh(new RefreshRequest(ViewRefreshProjections | RefreshProjection.RawTransactionsPivot));
    }

    private void RefreshForecastAndTransactionViews()
    {
        if (_suppressFilterRefresh)
        {
            return;
        }

        RequestRefresh(new RefreshRequest(
            RefreshProjection.ForecastLinesView
            | RefreshProjection.RawTransactionsView
            | RefreshProjection.RawTransactionsPivot));
    }

    private void RefreshSearchViews()
    {
        if (_suppressFilterRefresh)
        {
            return;
        }

        RequestRefresh(new RefreshRequest(
            RefreshProjection.ForecastLinesView
            | RefreshProjection.RawTransactionsView
            | RefreshProjection.RawTransactionsPivot));
    }

    private void RefreshForecastLinesView()
    {
        if (!_suppressFilterRefresh)
        {
            RequestRefresh(new RefreshRequest(RefreshProjection.ForecastLinesView));
        }
    }

    private void RefreshRawTransactionsView()
    {
        if (!_suppressFilterRefresh)
        {
            RequestRefresh(new RefreshRequest(
                RefreshProjection.RawTransactionsView
                | RefreshProjection.RawTransactionsPivot));
        }
    }

    private static void RefreshViews(params ICollectionView[] views)
    {
        foreach (var view in views)
        {
            RefreshView(view);
        }
    }

    private static void RefreshView(ICollectionView view)
    {
        view.Refresh();
    }

    private string? _searchTermSource;
    private string _searchTermNormalised = string.Empty;

    private string GetNormalisedSearchTerm()
    {
        if (!ReferenceEquals(_searchTermSource, SearchText))
        {
            _searchTermSource = SearchText;
            _searchTermNormalised = CalculationService.Normalise(SearchText);
        }

        return _searchTermNormalised;
    }

    private bool FilterForecastLine(object item)
    {
        if (item is not ForecastLine line)
        {
            return false;
        }

        if (ShowOnlyLinesWithActualCost && line.CostToDate <= 0)
        {
            return false;
        }

        if (ShowCostThisMonthOnly && line.CurrentMonthCost == 0)
        {
            return false;
        }

        if (ShowOnlyLinesWithRemainingForecast && line.TotalForecastCtc <= 0)
        {
            return false;
        }

        if (SelectedMonthlyVarianceFilter == "Negative only" && line.VarianceLastMonthToDate >= 0)
        {
            return false;
        }

        if (SelectedMonthlyVarianceFilter == "Positive only" && line.VarianceLastMonthToDate <= 0)
        {
            return false;
        }

        if (SelectedMonthlyVarianceFilter == "Any variance" && line.VarianceLastMonthToDate == 0)
        {
            return false;
        }

        if (SelectedBudgetVarianceFilter == "Over budget" && line.TotalBudgetVariance >= 0)
        {
            return false;
        }

        if (SelectedBudgetVarianceFilter == "Under budget" && line.TotalBudgetVariance <= 0)
        {
            return false;
        }

        if (SelectedBudgetVarianceFilter == "Any variance" && line.TotalBudgetVariance == 0)
        {
            return false;
        }

        if (SelectedProjectCode != "All" && !string.Equals(line.ProjectCode, SelectedProjectCode, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        var term = GetNormalisedSearchTerm();
        return CalculationService.Normalise(line.TaskNumber).Contains(term, StringComparison.OrdinalIgnoreCase)
            || CalculationService.Normalise(line.ResourceName).Contains(term, StringComparison.OrdinalIgnoreCase)
            || CalculationService.Normalise(line.ProjectCode).Contains(term, StringComparison.OrdinalIgnoreCase)
            || CalculationService.Normalise(line.ReportingCategory).Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private bool FilterTransaction(object item)
    {
        if (item is not CostTransaction tx)
        {
            return false;
        }

        if (SelectedProjectCode != "All" && !TransactionMatchesSelectedProject(tx))
        {
            return false;
        }

        if (ShowCostThisMonthOnly && !string.Equals(
                CalculationService.Normalise(tx.FyPeriod),
                CalculationService.Normalise(Header.CurrentPeriod),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (SelectedPeriod != "All" && !string.Equals(tx.FyPeriod, SelectedPeriod, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        var term = GetNormalisedSearchTerm();
        return CalculationService.Normalise(tx.TaskNumber).Contains(term, StringComparison.OrdinalIgnoreCase)
            || CalculationService.Normalise(tx.LedgerResourceName).Contains(term, StringComparison.OrdinalIgnoreCase)
            || CalculationService.Normalise(tx.ResourceCode).Contains(term, StringComparison.OrdinalIgnoreCase)
            || CalculationService.Normalise(tx.Narrative1).Contains(term, StringComparison.OrdinalIgnoreCase)
            || CalculationService.Normalise(tx.Narrative2).Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private bool TransactionMatchesSelectedProject(CostTransaction transaction)
    {
        var selectedProjectCode = CalculationService.Normalise(SelectedProjectCode);
        if (string.IsNullOrWhiteSpace(selectedProjectCode))
        {
            return true;
        }

        if (string.Equals(CalculationService.Normalise(transaction.ProjectCode), selectedProjectCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(CalculationService.Normalise(transaction.TaskNumber), selectedProjectCode, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var taskNumber = CalculationService.Normalise(transaction.TaskNumber);
        return _taskNumbersByProjectCode.TryGetValue(selectedProjectCode, out var taskNumbers)
            && taskNumbers.Contains(taskNumber);
    }

    private void ClearFilters()
    {
        _searchRefreshTimer.Stop();
        _suppressFilterRefresh = true;
        try
        {
            SearchText = string.Empty;
            SelectedProjectCode = "All";
            SelectedPeriod = "All";
            ShowOnlyLinesWithActualCost = false;
            ShowCostThisMonthOnly = false;
            ShowOnlyLinesWithRemainingForecast = false;
            SelectedMonthlyVarianceFilter = "All";
            SelectedBudgetVarianceFilter = "All";
        }
        finally
        {
            _suppressFilterRefresh = false;
        }

        RequestRefresh(new RefreshRequest(
            RefreshProjection.ForecastLinesView
            | RefreshProjection.RawTransactionsView
            | RefreshProjection.RawTransactionsPivot));
    }

    private void ClearAllRecords(bool newProject = false)
    {
        var message = newProject
            ? "Start a new blank project? Current project records will be cleared. This cannot be undone unless you reopen or restore from a saved file."
            : "Clear all current forecast lines, transactions, contingency items, saved month snapshots, and audit history? This cannot be undone unless you reopen or restore from a saved file.";
        var title = newProject ? "New project" : "Clear all records";
        if (MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        var clearedDataset = new ProjectDataset
        {
            Header = new ProjectHeader
            {
                ProjectTitle = newProject ? "New project" : Header.ProjectTitle,
                ReportTitle = newProject ? "Project Cost Forecast" : Header.ReportTitle,
                CurrentPeriod = Header.CurrentPeriod,
                SourceWorkbook = newProject ? string.Empty : Header.SourceWorkbook,
                ImportNotes = newProject ? string.Empty : Header.ImportNotes
            },
            Phases = newProject
                ? []
                : _dataset.Phases.Select(phase => new PhaseItem
            {
                Name = phase.Name,
                Start = phase.Start,
                End = phase.End
            }).ToList(),
            ForecastPeriods = _dataset.ForecastPeriods.Select(period => new ForecastPeriod
            {
                Column = period.Column,
                Label = period.Label,
                StartDate = period.StartDate
            }).ToList(),
            FiscalYearBudgets = newProject
                ? []
                : _dataset.FiscalYearBudgets.Select(budget => new FiscalYearBudget
            {
                FiscalYear = budget.FiscalYear,
                Budget = budget.Budget
            }).ToList()
        };

        LoadDataset(clearedDataset, markDirty: true);
        if (newProject)
        {
            ProjectFilePath = string.Empty;
            _projectFileRevision = null;
        }

        AddAuditEvent(
            "Project",
            Header.ProjectTitle,
            newProject ? "NewProject" : "ClearAll",
            "Existing records",
            "Cleared",
            newProject ? "Created a new blank project" : "Cleared all working records for a fresh import");
        StatusText = newProject
            ? "New project created. You can now import a new data sheet."
            : "Cleared all current records. You can now import a new data sheet.";
    }

    private static IEnumerable<ResourceSummary> BuildResourceSummaries(IEnumerable<CostTransaction> transactions, IEnumerable<ForecastLine> forecastLines)
    {
        var projectCodesByTask = forecastLines
            .Where(line => !string.IsNullOrWhiteSpace(line.TaskNumber))
            .GroupBy(line => CalculationService.Normalise(line.TaskNumber))
            .ToDictionary(
                group => group.Key,
                group => string.Join(", ", group.Select(line => line.ProjectCode).Where(code => !string.IsNullOrWhiteSpace(code)).Distinct().OrderBy(code => code)),
                StringComparer.OrdinalIgnoreCase);

        return transactions
            .GroupBy(t => CalculationService.Normalise(t.LedgerResourceName))
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .Select(g => new ResourceSummary
            {
                ResourceName = g.First().LedgerResourceName,
                ProjectCodeList = string.Join(", ", g.Select(x => projectCodesByTask.GetValueOrDefault(CalculationService.Normalise(x.TaskNumber))).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x)),
                ResourceCodeList = string.Join(", ", g.Select(x => x.ResourceCode).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x)),
                TaskNumberList = string.Join(", ", g.Select(x => x.TaskNumber).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x)),
                SourceList = string.Join(", ", g.Select(x => x.Source).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x)),
                TransactionCount = g.Count(),
                Units = g.Sum(x => x.Units),
                Amount = g.Sum(x => x.Amount)
            })
            .OrderByDescending(x => x.Amount)
            .ThenBy(x => x.ResourceName);
    }

    private void ApplyForecastGrouping()
    {
        if (IsForecastEditTransactionActive())
        {
            QueueDeferredForecastGrouping();
            return;
        }

        ApplyForecastGroupingCore();
    }

    private void ApplyForecastGroupingCore()
    {
        _refreshCoordinator.Measure(RefreshPhase.ForecastGrouping, () =>
        {
            using (ForecastLinesView.DeferRefresh())
            {
                ForecastLinesView.GroupDescriptions.Clear();
                switch (ForecastGroupByKey)
                {
                    case ForecastGroupByTaskKey:
                        ForecastLinesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ForecastLine.TaskNumber)));
                        break;
                    case ForecastGroupByResourceKey:
                        ForecastLinesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ForecastLine.ResourceName)));
                        break;
                    case ForecastGroupByCategoryKey:
                        ForecastLinesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ForecastLine.ReportingCategory)));
                        break;
                }
            }
        });
    }

    private void QueueDeferredForecastGrouping()
    {
        RequestRefresh(new RefreshRequest(RefreshProjection.ForecastGrouping));
    }

    private void ApplyCategorySorting()
    {
        using (CategorySummariesView.DeferRefresh())
        {
            CategorySummariesView.SortDescriptions.Clear();

            if (SelectedCategorySortOption is null)
            {
                return;
            }

            switch (SelectedCategorySortOption.Key)
            {
                case "TotalCost":
                    CategorySummariesView.SortDescriptions.Add(new SortDescription(nameof(CategorySummary.CostToDate), ListSortDirection.Descending));
                    CategorySummariesView.SortDescriptions.Add(new SortDescription(nameof(CategorySummary.ProjectCode), ListSortDirection.Ascending));
                    break;
                case "MonthCost":
                    CategorySummariesView.SortDescriptions.Add(new SortDescription(nameof(CategorySummary.CurrentMonthCost), ListSortDirection.Descending));
                    CategorySummariesView.SortDescriptions.Add(new SortDescription(nameof(CategorySummary.ProjectCode), ListSortDirection.Ascending));
                    break;
                default:
                    CategorySummariesView.SortDescriptions.Add(new SortDescription(nameof(CategorySummary.ProjectCode), ListSortDirection.Ascending));
                    break;
            }
        }
    }

    public bool TryMoveForecastLineWithinProjectCode(ForecastLine sourceLine, ForecastLine targetLine)
    {
        if (sourceLine == targetLine)
        {
            return false;
        }

        if (!string.Equals(sourceLine.ProjectCode, targetLine.ProjectCode, StringComparison.OrdinalIgnoreCase))
        {
            StatusText = "You can only move a forecast line within the same cost code.";
            return false;
        }

        var sourceIndex = ForecastLines.IndexOf(sourceLine);
        var targetIndex = ForecastLines.IndexOf(targetLine);
        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
        {
            return false;
        }

        ForecastLines.Move(sourceIndex, targetIndex);
        SelectedForecastLine = sourceLine;
        SyncDatasetFromCollections();
        RefreshView(ForecastLinesView);
        IsDirty = true;
        AddAuditEvent("ForecastLine", sourceLine.RowNumber.ToString(), "Moved", sourceIndex.ToString(), targetIndex.ToString(), "Moved forecast line within cost code");
        StatusText = $"Moved '{sourceLine.ResourceName}' within cost code {sourceLine.ProjectCode}.";
        return true;
    }

    public void SaveForecastLineCommentEditor(
        ForecastLine line,
        IEnumerable<ResourceCommentMetricPreference> metrics,
        string totalBudgetVarianceComment,
        string monthBudgetVarianceComment,
        string forecastVarianceComment)
    {
        line.ResourceCommentMetrics = metrics
            .OrderBy(metric => metric.DisplayOrder)
            .Select(metric => new ResourceCommentMetricPreference
            {
                Key = metric.Key,
                Label = metric.Label,
                IsVisible = metric.IsVisible,
                DisplayOrder = metric.DisplayOrder
            })
            .ToList();
        var previousComments = CombineVarianceComments(
            line.CommentsOnTotalBudgetVariance,
            line.CommentsOnMonthBudgetVariance,
            line.CommentsOnMonthForecastVariance);
        line.CommentsOnTotalBudgetVariance = totalBudgetVarianceComment.Trim();
        line.CommentsOnMonthBudgetVariance = monthBudgetVarianceComment.Trim();
        line.CommentsOnMonthForecastVariance = forecastVarianceComment.Trim();
        line.EnsureResourceCommentMetrics();
        var currentComments = CombineVarianceComments(
            line.CommentsOnTotalBudgetVariance,
            line.CommentsOnMonthBudgetVariance,
            line.CommentsOnMonthForecastVariance);
        RecordMonthlyCommentHistory(line, previousComments, currentComments);

        SyncDatasetFromCollections();
        RebuildMonthlyReport();
        RefreshViewsAndTotals();
        IsDirty = true;
        AddAuditEvent("ForecastLine", line.RowNumber.ToString(), "CommentEditor", string.Empty, line.ResourceName, "Updated forecast line comments and pill layout");
        StatusText = $"{line.ResourceName}: comments updated.";
    }

    private void RecordMonthlyCommentHistory(ForecastLine line, string previousComment, string newComment)
    {
        if (string.IsNullOrWhiteSpace(newComment)
            || string.Equals(previousComment?.Trim(), newComment, StringComparison.Ordinal))
        {
            return;
        }

        var periodLabel = CalculationService.Normalise(Header.CurrentPeriod);
        var monthLabel = _dataset.ForecastPeriods
            .FirstOrDefault(period => string.Equals(
                CalculationService.Normalise(period.Label),
                periodLabel,
                StringComparison.OrdinalIgnoreCase))
            ?.StartDate?.ToString("MMM yy") ?? string.Empty;

        // One entry per period: re-saving a comment in the same month updates that month's entry.
        var entry = line.MonthlyCommentHistory.FirstOrDefault(comment =>
            string.Equals(comment.PeriodLabel, periodLabel, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            entry = new ForecastMonthlyComment { PeriodLabel = periodLabel, MonthLabel = monthLabel };
            line.MonthlyCommentHistory.Add(entry);
        }

        entry.ResourceName = line.ResourceName;
        entry.Text = newComment;
        entry.RecordedAt = _clock.UtcNow;
        line.NotifyAllMonthCommentsChanged();
    }

    private static string CombineVarianceComments(params string[] comments)
    {
        return string.Join("; ", comments
            .Where(comment => !string.IsNullOrWhiteSpace(comment))
            .Select(comment => comment.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    public void SaveManualForecastComment(ForecastLine line, string comment)
    {
        line.ManualAllMonthComment = comment.Trim();
        line.UseManualAllMonthComment = !string.IsNullOrWhiteSpace(line.ManualAllMonthComment);
        line.ManualCommentPeriodLabel = CalculationService.Normalise(Header.CurrentPeriod);
        line.ManualCommentMonthLabel = _dataset.ForecastPeriods
            .FirstOrDefault(period => string.Equals(CalculationService.Normalise(period.Label), line.ManualCommentPeriodLabel, StringComparison.OrdinalIgnoreCase))
            ?.StartDate?.ToString("MMM yy") ?? string.Empty;
        line.ManualCommentRecordedAt = _clock.UtcNow;
        line.NotifyAllMonthCommentsChanged();
        SyncDatasetFromCollections();
        RebuildMonthlyReport();
        IsDirty = true;
        StatusText = $"{line.ResourceName}: manual comment override saved.";
    }

    public void SetForecastCommentMode(ForecastLine line, bool useManual)
    {
        line.UseManualAllMonthComment = useManual && line.HasManualAllMonthComment;
        line.NotifyAllMonthCommentsChanged();
        SyncDatasetFromCollections();
        RebuildMonthlyReport();
        IsDirty = true;
    }
}
