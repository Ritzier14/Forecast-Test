using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using ProjectCostForecast.App.ViewModels;

namespace ProjectCostForecast.App;

public partial class MainWindow
{
    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_mainWindowClosed)
        {
            return;
        }

        _mainWindowLoaded = true;
        _mainWindowLifetimeVersion++;
        WireViewModelSubscriptions();
        WireGanttSubscriptions();
        AttachWindowVisualSubscriptions();

        if (!_mainWindowVisualsInitialized)
        {
            StartForecastGridFirstDrawMeasure();
        }

        if (!_mainWindowVisualsInitialized || _mainWindowNeedsVisualRefresh)
        {
            RefreshMainWindowVisuals();
            _mainWindowVisualsInitialized = true;
            _mainWindowNeedsVisualRefresh = false;
        }
        else
        {
            ReattachWindowVisuals();
        }
    }

    private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_mainWindowClosed)
        {
            return;
        }

        WireViewModelSubscriptions();
        WireGanttSubscriptions();
        if (_mainWindowLoaded)
        {
            _mainWindowNeedsVisualRefresh = true;
            RefreshMainWindowVisuals();
            _mainWindowVisualsInitialized = true;
            _mainWindowNeedsVisualRefresh = false;
        }
        else
        {
            _mainWindowNeedsVisualRefresh = true;
        }
    }

    private void MainWindow_Unloaded(object sender, RoutedEventArgs e)
    {
        _mainWindowLoaded = false;
        _mainWindowLifetimeVersion++;
        StopDetailWorkspaceHoverTimer();
        DetachWindowVisualSubscriptions();
        UnwireGanttVisualSubscriptions();
        UnwireGanttSubscriptions();
        UnwireViewModelSubscriptions();
        CancelPendingWindowWork();
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.Dispose();
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        if (_mainWindowClosed)
        {
            return;
        }

        _mainWindowClosed = true;
        _mainWindowLoaded = false;
        _mainWindowLifetimeVersion++;
        StopDetailWorkspaceHoverTimer();
        DetachWindowVisualSubscriptions();
        UnwireGanttVisualSubscriptions();
        UnwireGanttSubscriptions();
        UnwireViewModelSubscriptions();
        CancelPendingWindowWork();
    }

    private void RefreshMainWindowVisuals()
    {
        ApplyWindowPreferences();
        ApplySavedWorkspaceTabOrders();
        RebuildMonthlyPivotColumns();
        RebuildBudgetGridColumns();
        ConfigureSelectedMonthlyForecastGrid();
        ConfigureTaskCodeReviewGrid();
        RebuildForecastGridColumns();
        AttachColumnMenus(this);
        ApplyDefaultColumnPresentation(this);
        AttachGridPanHandlers(this);
        AttachSpreadsheetGridHandlers(this);
        AttachForecastGridScrollSync();
        RebuildForecastYearBands();
        QueueApplyCurrentWorkspaceViewColumnState();
        QueueApplyCurrentDetailWorkspaceViewColumnState();
        QueueScrollLedgerChartToEnd();
        RefreshForecastGridStatePills();
        RefreshWorkspaceTabIcons();
        UpdateForecastGroupToggleVisual();
        QueueReportForecastGridFirstDraw();
        InitializeMonthlyReportCanvas();
    }

    private void ReattachWindowVisuals()
    {
        if (!_forecastGridFirstDrawReported && _forecastGridFirstDrawTimer is null)
        {
            StartForecastGridFirstDrawMeasure();
        }

        AttachColumnMenus(this);
        ApplyDefaultColumnPresentation(this);
        AttachGridPanHandlers(this);
        AttachSpreadsheetGridHandlers(this);
        AttachForecastGridScrollSync();
        QueueRefreshForecastGroupHeaderPresenters();
        QueueApplyCurrentWorkspaceViewColumnState();
        QueueApplyCurrentDetailWorkspaceViewColumnState();
        QueueSynchronizeManagementResourceGrids();
        QueueReportForecastGridFirstDraw();
    }

    private void AttachWindowVisualSubscriptions()
    {
        ForecastGridHost.SizeChanged -= ForecastGridHost_SizeChanged;
        ForecastGridHost.SizeChanged += ForecastGridHost_SizeChanged;
    }

    private void DetachWindowVisualSubscriptions()
    {
        ForecastGridHost.SizeChanged -= ForecastGridHost_SizeChanged;

        foreach (var grid in GetAllMainWindowDataGrids())
        {
            grid.PreviewMouseDown -= Grid_PreviewMouseDown;
            grid.PreviewMouseMove -= Grid_PreviewMouseMove;
            grid.PreviewMouseUp -= Grid_PreviewMouseUp;
            grid.LoadingRow -= Grid_LoadingRow;
            grid.MouseMove -= Grid_MouseMove;
            grid.MouseLeave -= Grid_MouseLeave;

            grid.RemoveHandler(
                DataGridColumnHeader.PreviewMouseRightButtonDownEvent,
                new MouseButtonEventHandler(ShowColumnMenu));
            grid.ColumnReordered -= Grid_ColumnLayoutChanged;
            grid.PreviewMouseLeftButtonUp -= Grid_ColumnLayoutMouseLeftButtonUp;

            grid.PreviewMouseLeftButtonDown -= SpreadsheetGrid_PreviewMouseLeftButtonDown;
            grid.PreviewMouseLeftButtonUp -= SpreadsheetGrid_PreviewMouseLeftButtonUp;
            grid.MouseMove -= SpreadsheetGrid_MouseMove;
            grid.PreviewMouseRightButtonUp -= SpreadsheetGrid_PreviewMouseRightButtonUp;
            grid.MouseDoubleClick -= SpreadsheetGrid_MouseDoubleClick;
            grid.PreviewKeyDown -= SpreadsheetGrid_PreviewKeyDown;
            grid.PreviewTextInput -= SpreadsheetGrid_PreviewTextInput;
            grid.BeginningEdit -= SpreadsheetGrid_BeginningEdit;
            grid.CellEditEnding -= SpreadsheetGrid_CellEditEnding;
            grid.SelectedCellsChanged -= SpreadsheetGrid_SelectedCellsChanged;
            grid.CurrentCellChanged -= SpreadsheetGrid_CurrentCellChanged;
            grid.LoadingRow -= SpreadsheetGrid_LoadingRow;
            grid.Loaded -= SpreadsheetGrid_Loaded;
            grid.RemoveHandler(
                FrameworkElement.ContextMenuOpeningEvent,
                new ContextMenuEventHandler(SpreadsheetTextBox_ContextMenuOpening));

            if (grid is ProjectDataGrid projectGrid)
            {
                projectGrid.ModifierRowSelectionCompleted -= SpreadsheetGrid_ModifierRowSelectionCompleted;
            }

            if (ReferenceEquals(grid, ForecastLinesGrid))
            {
                grid.RemoveHandler(
                    UIElement.PreviewMouseLeftButtonDownEvent,
                    new MouseButtonEventHandler(ForecastLinesGrid_PreviewMouseLeftButtonDown));
                grid.RemoveHandler(
                    UIElement.PreviewMouseMoveEvent,
                    new MouseEventHandler(ForecastLinesGrid_PreviewMouseMove));
                grid.RemoveHandler(
                    UIElement.PreviewMouseLeftButtonUpEvent,
                    new MouseButtonEventHandler(ForecastLinesGrid_PreviewMouseLeftButtonUp));
                grid.LostMouseCapture -= ForecastLinesGrid_ColumnReorderLostMouseCapture;
                grid.PreviewMouseLeftButtonDown -= ForecastLinesGrid_ColumnReorderMouseDown;
                grid.PreviewMouseMove -= ForecastLinesGrid_ColumnReorderMouseMove;
                grid.PreviewMouseLeftButtonUp -= ForecastLinesGrid_ColumnReorderMouseUp;
                grid.CellEditEnding -= ForecastLinesGrid_CellEditEnding;
            }
        }

        foreach (var column in _trackedForecastColumns)
        {
            ForecastColumnActualWidthDescriptor?.RemoveValueChanged(column, ForecastColumnWidthChanged);
        }

        foreach (var column in _trackedManagementResourceColumns)
        {
            ForecastColumnActualWidthDescriptor?.RemoveValueChanged(column, ManagementResourceColumnWidthChanged);
        }

        foreach (var scrollViewer in _managementResourceScrollViewers)
        {
            scrollViewer.ScrollChanged -= ManagementResourceScrollViewer_ScrollChanged;
        }

        if (_forecastGridScrollViewer is not null)
        {
            _forecastGridScrollViewer.ScrollChanged -= ForecastGridScrollViewer_ScrollChanged;
            _forecastGridScrollViewer = null;
        }

        _workspaceColumnStateTrackedGrids.Clear();
        _workspaceColumnStateTrackedColumns.Clear();
        _workspaceColumnStateCaptureQueuedGrids.Clear();
        _rowHoverAttachedGrids.Clear();
        _hoveredRowsByGrid.Clear();
        _spreadsheetAttachedGrids.Clear();
        _spreadsheetSelectionUpdateQueued.Clear();
        _spreadsheetSelectionVisualPendingItems.Clear();
        _spreadsheetSelectionVisualFullRefresh.Clear();
        _spreadsheetPreviousCurrentCells.Clear();
        _spreadsheetEditSnapshots.Clear();
        _trackedForecastColumns.Clear();
        _trackedManagementResourceColumns.Clear();
        _managementResourceScrollViewers.Clear();
        _forecastColumnReorderHandlersAttached = false;
        _forecastGroupHeaderPresenters.Clear();
    }

    private IEnumerable<DataGrid> GetAllMainWindowDataGrids()
    {
        var grids = new HashSet<DataGrid>(ReferenceEqualityComparer.Instance);
        foreach (var grid in FindChildren<DataGrid>(this))
        {
            grids.Add(grid);
        }

        foreach (var grid in _workspaceColumnStateTrackedGrids
            .Concat(_rowHoverAttachedGrids)
            .Concat(_spreadsheetAttachedGrids))
        {
            grids.Add(grid);
        }

        return grids;
    }

    private void CancelPendingWindowWork()
    {
        _forecastYearBandRebuildQueued = false;
        _forecastGroupHeaderRefreshQueued = false;
        _monthlyPivotColumnsRebuildQueued = false;
        _forecastGridColumnsRebuildQueued = false;
        _budgetGridColumnsRebuildQueued = false;
        _forecastGroupExpansionRestoreQueued = false;
        _workspaceViewColumnStateQueued = false;
        _detailWorkspaceViewColumnStateQueued = false;
        _ledgerChartScrollQueued = false;
        _ganttRedrawQueued = false;
        _managementResourceGridSyncQueued = false;
        _workspaceColumnStateCaptureQueuedGrids.Clear();
        _selectionVisualRefreshQueued.Clear();
        _pendingWorkspaceEditorFocusView = null;
        _forecastYearBandDeferredRetryCount = 0;
        _pendingForecastGridRefreshState = null;
        _forecastGridFirstDrawTimer?.Stop();
        _forecastGridFirstDrawTimer = null;
    }

    private bool IsMainWindowWorkActive => _mainWindowLoaded && !_mainWindowClosed;

    private bool QueueMainWindowWork(DispatcherPriority priority, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var lifetimeVersion = _mainWindowLifetimeVersion;
        if (!IsMainWindowWorkActive || Dispatcher.HasShutdownStarted)
        {
            return false;
        }

        try
        {
            Dispatcher.BeginInvoke(priority, new Action(() =>
            {
                if (!IsMainWindowWorkActive || lifetimeVersion != _mainWindowLifetimeVersion)
                {
                    return;
                }

                action();
            }));
            return true;
        }
        catch (InvalidOperationException) when (Dispatcher.HasShutdownStarted)
        {
            // The dispatcher can begin shutting down between the guard and BeginInvoke.
            return false;
        }
    }
}
