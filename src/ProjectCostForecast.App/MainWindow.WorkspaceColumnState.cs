using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.ViewModels;

namespace ProjectCostForecast.App;

public partial class MainWindow
{
    private static void SetGroupedExpandState(DependencyObject root, bool isExpanded)
    {
        foreach (var expander in FindChildren<Expander>(root))
        {
            expander.IsExpanded = isExpanded;
        }
    }

    private static IEnumerable<T> FindChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static T? FindChild<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            var descendant = FindChild<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private void WireViewModelSubscriptions()
    {
        if (ReferenceEquals(_subscribedViewModel, DataContext))
        {
            return;
        }

        _subscribedViewModel = DataContext as MainWindowViewModel;
        if (_subscribedViewModel is null)
        {
            return;
        }

        _subscribedViewModel.MonthlyPivotPeriods.CollectionChanged += (_, _) => RebuildMonthlyPivotColumns();
        _subscribedViewModel.RawTransactionsMonthlyPivotPeriods.CollectionChanged += (_, _) => RebuildMonthlyPivotColumns();
        _subscribedViewModel.LedgerMonthlyPivotPeriods.CollectionChanged += (_, _) => RebuildMonthlyPivotColumns();
        _subscribedViewModel.PivotResultColumns.CollectionChanged += (_, _) => RebuildMonthlyPivotColumns();
        _subscribedViewModel.CtcMonthForecastColumns.CollectionChanged += (_, _) => RebuildForecastGridColumns();
        _subscribedViewModel.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(MainWindowViewModel.ShowCtcMonthForecastColumns), StringComparison.Ordinal)
                || string.Equals(e.PropertyName, nameof(MainWindowViewModel.ShowMonthNameAboveFiscalPeriod), StringComparison.Ordinal)
                || string.Equals(e.PropertyName, nameof(MainWindowViewModel.SelectedCtcMonthForecastYear), StringComparison.Ordinal)
                || string.Equals(e.PropertyName, nameof(MainWindowViewModel.ShowVarianceIndicators), StringComparison.Ordinal))
            {
                RebuildForecastGridColumns();
            }

            if (string.Equals(e.PropertyName, nameof(MainWindowViewModel.ForecastFreezeColumnKey), StringComparison.Ordinal))
            {
                ApplyForecastFreezeBoundaryForCurrentColumns();
            }

            if (string.Equals(e.PropertyName, nameof(MainWindowViewModel.ActiveWorkspaceKey), StringComparison.Ordinal))
            {
                QueueApplyCurrentWorkspaceViewColumnState();
            }

            if (string.Equals(e.PropertyName, nameof(MainWindowViewModel.SelectedWorkspaceView), StringComparison.Ordinal))
            {
                QueueApplyCurrentWorkspaceViewColumnState();
            }

            if (string.Equals(e.PropertyName, nameof(MainWindowViewModel.ActiveDetailWorkspaceKey), StringComparison.Ordinal))
            {
                QueueApplyCurrentDetailWorkspaceViewColumnState();
            }

            if (string.Equals(e.PropertyName, nameof(MainWindowViewModel.SelectedDetailWorkspaceView), StringComparison.Ordinal))
            {
                QueueApplyCurrentDetailWorkspaceViewColumnState();
            }

            if (string.Equals(e.PropertyName, nameof(MainWindowViewModel.ForecastGroupByKey), StringComparison.Ordinal)
                || string.Equals(e.PropertyName, nameof(MainWindowViewModel.ShowOnlyLinesWithActualCost), StringComparison.Ordinal)
                || string.Equals(e.PropertyName, nameof(MainWindowViewModel.ShowCostThisMonthOnly), StringComparison.Ordinal)
                || string.Equals(e.PropertyName, nameof(MainWindowViewModel.ShowOnlyLinesWithRemainingForecast), StringComparison.Ordinal)
                || string.Equals(e.PropertyName, nameof(MainWindowViewModel.SelectedMonthlyVarianceFilter), StringComparison.Ordinal)
                || string.Equals(e.PropertyName, nameof(MainWindowViewModel.SelectedBudgetVarianceFilter), StringComparison.Ordinal))
            {
                RefreshForecastGridStatePills();
            }

            if (string.Equals(e.PropertyName, nameof(MainWindowViewModel.SelectedForecastLine), StringComparison.Ordinal)
                || string.Equals(e.PropertyName, nameof(MainWindowViewModel.SelectedResourceSummary), StringComparison.Ordinal)
                || string.Equals(e.PropertyName, nameof(MainWindowViewModel.SelectedScheduleActivity), StringComparison.Ordinal))
            {
                Dispatcher.BeginInvoke(RefreshCurrentRowVisuals, DispatcherPriority.Render);
            }

            if (string.Equals(e.PropertyName, nameof(MainWindowViewModel.LedgerChartCanvasWidth), StringComparison.Ordinal)
                || string.Equals(e.PropertyName, nameof(MainWindowViewModel.LedgerActualChartGeometry), StringComparison.Ordinal)
                || string.Equals(e.PropertyName, nameof(MainWindowViewModel.LedgerForecastChartGeometry), StringComparison.Ordinal))
            {
                QueueScrollLedgerChartToEnd();
            }
        };
    }

    private void QueueScrollLedgerChartToEnd()
    {
        if (_ledgerChartScrollQueued)
        {
            return;
        }

        _ledgerChartScrollQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            _ledgerChartScrollQueued = false;
            LedgerChartScrollViewer.ScrollToRightEnd();
        }));
    }

    private void QueueApplyCurrentWorkspaceViewColumnState()
    {
        if (_workspaceViewColumnStateQueued)
        {
            return;
        }

        _workspaceViewColumnStateQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            _workspaceViewColumnStateQueued = false;
            ApplyCurrentWorkspaceViewColumnState();
        }));
    }

    private void QueueApplyCurrentDetailWorkspaceViewColumnState()
    {
        if (_detailWorkspaceViewColumnStateQueued)
        {
            return;
        }

        _detailWorkspaceViewColumnStateQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            _detailWorkspaceViewColumnStateQueued = false;
            ApplyCurrentDetailWorkspaceViewColumnState();
        }));
    }

    private void ApplyCurrentWorkspaceViewColumnState()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var grid = GetActiveWorkspaceGrid(viewModel);
        if (grid is null || grid.Columns.Count == 0)
        {
            return;
        }

        var hiddenKeys = viewModel.GetSelectedWorkspaceHiddenColumnKeys();
        foreach (var column in grid.Columns)
        {
            var key = GetColumnPersistenceKey(column);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            column.Visibility = hiddenKeys.Contains(key, StringComparer.OrdinalIgnoreCase)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        if (grid.Columns.All(column => column.Visibility != Visibility.Visible))
        {
            grid.Columns[0].Visibility = Visibility.Visible;
            CaptureCurrentWorkspaceViewColumnState();
        }

        if (ReferenceEquals(grid, ForecastLinesGrid))
        {
            ApplyForecastFreezeBoundaryForCurrentColumns();
        }
    }

    private void CaptureCurrentWorkspaceViewColumnState()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var grid = GetActiveWorkspaceGrid(viewModel);
        if (grid is null || grid.Columns.Count == 0)
        {
            return;
        }

        viewModel.SetSelectedWorkspaceHiddenColumnKeys(grid.Columns
            .Where(column => column.Visibility != Visibility.Visible)
            .Select(GetColumnPersistenceKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))!);
    }

    private void ApplyCurrentDetailWorkspaceViewColumnState()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var grid = GetActiveDetailWorkspaceGrid(viewModel);
        if (grid is null || grid.Columns.Count == 0)
        {
            return;
        }

        var hiddenKeys = viewModel.GetSelectedDetailWorkspaceHiddenColumnKeys();
        foreach (var column in grid.Columns)
        {
            var key = GetColumnPersistenceKey(column);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            column.Visibility = hiddenKeys.Contains(key, StringComparer.OrdinalIgnoreCase)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        if (grid.Columns.All(column => column.Visibility != Visibility.Visible))
        {
            grid.Columns[0].Visibility = Visibility.Visible;
            CaptureCurrentDetailWorkspaceViewColumnState();
        }
    }

    private void CaptureCurrentDetailWorkspaceViewColumnState()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var grid = GetActiveDetailWorkspaceGrid(viewModel);
        if (grid is null || grid.Columns.Count == 0)
        {
            return;
        }

        viewModel.SetSelectedDetailWorkspaceHiddenColumnKeys(grid.Columns
            .Where(column => column.Visibility != Visibility.Visible)
            .Select(GetColumnPersistenceKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))!);
    }

    private DataGrid? GetActiveWorkspaceGrid(MainWindowViewModel viewModel)
    {
        return viewModel.ActiveWorkspaceKey switch
        {
            "CTC Forecast" => ForecastLinesGrid,
            "Raw Transactions" when viewModel.ShowRawTransactionsPivotByMonth => RawTransactionsMonthlyPivotGrid,
            "Raw Transactions" => RawTransactionsGrid,
            "Summary View" when viewModel.ShowSummaryViewByMonth => CategoryMonthlyPivotGrid,
            "Summary View" => CategoryReportGrid,
            "Pivot Builder" => CustomPivotGrid,
            "Contingency" => ContingencyGrid,
            "Audit" => AuditGrid,
            _ => null
        };
    }

    private DataGrid? GetActiveDetailWorkspaceGrid(MainWindowViewModel viewModel)
    {
        return viewModel.ActiveDetailWorkspaceKey switch
        {
            "Ledger Costs" when viewModel.ShowLedgerCostsPivotByMonth => LedgerMonthlyPivotGrid,
            "Ledger Costs" => LedgerTransactionsGrid,
            "Ledger Monthly Forecast" => SelectedMonthlyForecastsGrid,
            _ => null
        };
    }

    private static string GetColumnPersistenceKey(DataGridColumn column)
    {
        return GetForecastFreezeColumnKey(column)
            ?? (column.Header?.ToString() ?? string.Empty).Trim();
    }

    private void ApplyForecastFreezeBoundaryForCurrentColumns()
    {
        if (DataContext is not MainWindowViewModel viewModel || ForecastLinesGrid.Columns.Count == 0)
        {
            return;
        }

        var freezeCandidates = ForecastLinesGrid.Columns
            .Select(column => (Column: column, FreezeKey: GetForecastFreezeColumnKey(column)))
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.FreezeKey))
            .Select(candidate => (candidate.Column, FreezeKey: candidate.FreezeKey!))
            .ToList();

        ApplyForecastFreezeBoundary(ForecastLinesGrid, viewModel, freezeCandidates);
        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(RebuildForecastYearBands));
        QueueRebuildForecastYearBands();
    }

    private void CaptureGridColumnState(DataGrid grid)
    {
        if (ReferenceEquals(grid, ForecastLinesGrid)
            || ReferenceEquals(grid, RawTransactionsGrid)
            || ReferenceEquals(grid, RawTransactionsMonthlyPivotGrid)
            || ReferenceEquals(grid, CategoryReportGrid)
            || ReferenceEquals(grid, CategoryMonthlyPivotGrid)
            || ReferenceEquals(grid, CustomPivotGrid)
            || ReferenceEquals(grid, ContingencyGrid)
            || ReferenceEquals(grid, AuditGrid))
        {
            CaptureCurrentWorkspaceViewColumnState();
            if (ReferenceEquals(grid, ForecastLinesGrid))
            {
                ApplyForecastFreezeBoundaryForCurrentColumns();
            }

            return;
        }

        if (ReferenceEquals(grid, LedgerTransactionsGrid)
            || ReferenceEquals(grid, LedgerMonthlyPivotGrid)
            || ReferenceEquals(grid, SelectedMonthlyForecastsGrid))
        {
            CaptureCurrentDetailWorkspaceViewColumnState();
        }
    }

    private sealed class GridColumnFilterState
    {
        public Dictionary<string, HashSet<string>> SelectedValuesByColumn { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> LabelsByColumn { get; } = new(StringComparer.Ordinal);
    }

    private sealed record ColumnFilterValue(string Key, string Display);

    private sealed record ColumnIconOption(string Label, string Glyph);

    private sealed record ColumnColourOption(string Label, string ColumnHex, string HeaderHex);
}
