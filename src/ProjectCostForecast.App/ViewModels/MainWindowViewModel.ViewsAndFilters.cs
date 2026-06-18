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
    private void RecalculateAndRefresh(bool markDirty, string reason, bool rebuildFilterLists = true)
    {
        SyncDatasetFromCollections();
        ApplyClosedForecastPeriodRule();
        _calculationService.Recalculate(_dataset);
        RebuildCalculatedViews(rebuildFilterLists);
        RefreshViewsAndTotals();
        IsDirty = markDirty || IsDirty;
        StatusText = $"{reason}. {ValidationIssueCount} validation issue(s).";
    }

    private void RebuildCalculatedViews(bool rebuildFilterLists)
    {
        RebuildForecastLineLookups();
        InvalidatePivotFilterValues();
        ReplaceCollection(CategorySummaries, _dataset.CategorySummaries);
        ReplaceCollection(ResourceSummaries, BuildResourceSummaries(_dataset.Transactions, _dataset.ForecastLines));
        ReplaceCollection(FiscalYearReportLines, _calculationService.BuildFiscalYearReport(_dataset));
        ReplaceCollection(ActualsPeriodSummaries, _calculationService.BuildActualsPeriodSummaries(_dataset.Transactions));
        RebuildMonthlyPivotTables();
        RebuildCustomPivot();
        RebuildMonthlyReport();
        ApplyForecastPeriodLockStates();
        if (rebuildFilterLists)
        {
            RebuildFilterLists();
        }

        RefreshValidation(syncDataset: false);
    }

    private bool IsForecastEditTransactionActive()
    {
        return ForecastLinesView is IEditableCollectionView editableView
            && (editableView.IsAddingNew || editableView.IsEditingItem);
    }

    private void QueueDeferredViewRefresh()
    {
        if (_viewRefreshQueued)
        {
            return;
        }

        _viewRefreshQueued = true;
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _viewRefreshQueued = false;
            if (IsForecastEditTransactionActive())
            {
                QueueDeferredViewRefresh();
                return;
            }

            RefreshViews(ForecastLinesView, RawTransactionsView, ResourceSummariesView);
            RebuildRawTransactionsPivotTable();
        }, DispatcherPriority.ApplicationIdle);
    }

    private void RefreshForecastAndTransactionViews()
    {
        if (_suppressFilterRefresh)
        {
            return;
        }

        RefreshViews(ForecastLinesView, RawTransactionsView);
        RebuildRawTransactionsPivotTable();
    }

    private void RefreshSearchViews()
    {
        if (_suppressFilterRefresh)
        {
            return;
        }

        RefreshViews(ForecastLinesView, RawTransactionsView);
    }

    private void RefreshForecastLinesView()
    {
        if (!_suppressFilterRefresh)
        {
            RefreshView(ForecastLinesView);
        }
    }

    private void RefreshRawTransactionsView()
    {
        if (!_suppressFilterRefresh)
        {
            RefreshView(RawTransactionsView);
            RebuildRawTransactionsPivotTable();
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
            || CalculationService.Normalise(line.ProjectCode).Contains(term, StringComparison.OrdinalIgnoreCase);
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

        RefreshViews(ForecastLinesView, RawTransactionsView);
    }

    private void ClearAllRecords()
    {
        const string message = "Clear all current forecast lines, transactions, contingency items, saved month snapshots, and audit history? This cannot be undone unless you reopen or restore from a saved file.";
        if (MessageBox.Show(message, "Clear all records", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        var clearedDataset = new ProjectDataset
        {
            Header = new ProjectHeader
            {
                ProjectTitle = Header.ProjectTitle,
                ReportTitle = Header.ReportTitle,
                CurrentPeriod = Header.CurrentPeriod,
                SourceWorkbook = Header.SourceWorkbook,
                ImportNotes = Header.ImportNotes
            },
            Phases = _dataset.Phases.Select(phase => new PhaseItem
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
            FiscalYearBudgets = _dataset.FiscalYearBudgets.Select(budget => new FiscalYearBudget
            {
                FiscalYear = budget.FiscalYear,
                Budget = budget.Budget
            }).ToList()
        };

        LoadDataset(clearedDataset, markDirty: true);
        AddAuditEvent("Project", Header.ProjectTitle, "ClearAll", "Existing records", "Cleared", "Cleared all working records for a fresh import");
        StatusText = "Cleared all current records. You can now import a new data sheet.";
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
                    ForecastLinesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ForecastLine.ProjectCode)));
                    break;
            }
        }
    }

    private void QueueDeferredForecastGrouping()
    {
        if (_forecastGroupingQueued)
        {
            return;
        }

        _forecastGroupingQueued = true;
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _forecastGroupingQueued = false;
            if (IsForecastEditTransactionActive())
            {
                QueueDeferredForecastGrouping();
                return;
            }

            ApplyForecastGroupingCore();
        }, DispatcherPriority.ApplicationIdle);
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
        entry.RecordedAt = DateTime.Now;
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
        line.ManualCommentRecordedAt = DateTime.Now;
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
