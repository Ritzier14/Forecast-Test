using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.ViewModels;

namespace ProjectCostForecast.App;

public partial class MainWindow
{
    private void AddForecastRowButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (viewModel.IsViewingSavedMonth)
        {
            return;
        }

        BeginEditingForecastResourceCell(viewModel.InsertForecastLine(null, below: true));
    }

    private void ForecastLinesGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        if (IsScrollBarInteractionSource(source))
        {
            _forecastLeftDragStart = null;
            _forecastDragLine = null;
            return;
        }

        if (sender is ProjectDataGrid { IsHandlingRowResizeInput: true })
        {
            _forecastLeftDragStart = null;
            _forecastDragLine = null;
            return;
        }

        if (e.Handled
            && sender is ProjectDataGrid { ModifierClickSelectsWholeRows: true }
            && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != ModifierKeys.None)
        {
            _forecastLeftDragStart = null;
            _forecastDragLine = null;
            return;
        }

        var rowSelectionModifiers = Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift);
        if (rowSelectionModifiers == ModifierKeys.None
            && FindParent<DataGridRow>(source)?.Item is ForecastLine clickedLine)
        {
            // Record the anchor before DataGrid's own cell-selection handling
            // can replace the current cell with a different input target.
            _forecastRowSelectionAnchor = clickedLine;
        }

        _forecastLeftDragStart = e.GetPosition(ForecastLinesGrid);
        var cell = FindParent<DataGridCell>(source);
        _forecastDragLine = cell is not null && IsForecastIdentifierCell(cell)
            ? FindParent<DataGridRow>(source)?.Item as ForecastLine
            : null;
    }

    private static bool IsSelectableReadOnlyForecastTextSource(DependencyObject source)
    {
        return FindParent<TextBox>(source) is { Tag: "SelectableReadOnlyForecastText" };
    }

    private void ForecastLinesGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is ProjectDataGrid { IsHandlingRowResizeInput: true })
        {
            _forecastLeftDragStart = null;
            _forecastDragLine = null;
            return;
        }

        if (ForecastLinesGrid.IsMouseCaptured)
        {
            ForecastLinesGrid.ReleaseMouseCapture();
        }

        _forecastLeftDragStart = null;
        _forecastDragLine = null;
    }

    private void ForecastLinesGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // The shared spreadsheet right-click handler builds the complete row/cell menu.
        // Keeping this method empty avoids the old task/resource/category-only menu.
    }

    private MenuItem CreateAddManagementResourceMenuItem(ForecastLine line)
    {
        var alreadyAdded = DataContext is MainWindowViewModel viewModel && viewModel.IsManagementResource(line);
        var item = new MenuItem
        {
            Header = alreadyAdded ? "Add as management task (already added)" : "Add as management task",
            IsEnabled = !alreadyAdded
        };
        item.Click += (_, _) =>
        {
            if (DataContext is not MainWindowViewModel currentViewModel)
            {
                return;
            }

            currentViewModel.AddManagementResource(line);
            WorkspaceTabControl.SelectedItem = ManagementResourcesTab;
        };
        return item;
    }

    private void ForecastLinesGrid_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is ProjectDataGrid { IsRowResizeInProgress: true })
        {
            e.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
        {
            UpdateHoveredForecastLine(e.OriginalSource);
        }
        else
        {
            ClearHoveredForecastLine();
        }

        if (_forecastLeftDragStart is null || _forecastDragLine is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(ForecastLinesGrid);
        if (Math.Abs(current.X - _forecastLeftDragStart.Value.X) < DragThreshold
            && Math.Abs(current.Y - _forecastLeftDragStart.Value.Y) < DragThreshold)
        {
            return;
        }

        ForecastLinesGrid.CaptureMouse();
        var targetLine = GetForecastLineFromDragSource(ForecastLinesGrid.InputHitTest(current));
        if (targetLine is not null
            && !ReferenceEquals(_forecastDragLine, targetLine)
            && string.Equals(_forecastDragLine.ProjectCode, targetLine.ProjectCode, StringComparison.OrdinalIgnoreCase)
            && DataContext is MainWindowViewModel viewModel
            && viewModel.TryMoveForecastLineWithinProjectCode(_forecastDragLine, targetLine))
        {
            _forecastLeftDragStart = current;
        }
    }

    private void ForecastLinesGrid_MouseLeave(object sender, MouseEventArgs e)
    {
        ClearHoveredForecastLine();
    }

    private void ForecastLinesGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        ToggleForecastColumnHighlight(e.Column);
    }

    private void ColumnHighlightGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        ToggleForecastColumnHighlight(e.Column);
    }

    private void ForecastLinesGrid_DragOver(object sender, DragEventArgs e)
    {
        var sourceLine = e.Data.GetData(typeof(ForecastLine)) as ForecastLine;
        var targetLine = GetForecastLineFromDragSource(e.OriginalSource);
        e.Effects = sourceLine is not null
            && targetLine is not null
            && !ReferenceEquals(sourceLine, targetLine)
            && string.Equals(sourceLine.ProjectCode, targetLine.ProjectCode, StringComparison.OrdinalIgnoreCase)
                ? DragDropEffects.Move
                : DragDropEffects.None;
        e.Handled = true;
    }

    private void ForecastLinesGrid_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            var sourceLine = e.Data.GetData(typeof(ForecastLine)) as ForecastLine;
            var targetLine = GetForecastLineFromDragSource(e.OriginalSource);
            if (sourceLine is null || targetLine is null)
            {
                return;
            }

            viewModel.TryMoveForecastLineWithinProjectCode(sourceLine, targetLine);
        }
        finally
        {
            _forecastLeftDragStart = null;
            _forecastDragLine = null;
        }
    }

    private static ForecastLine? GetForecastLineFromDragSource(object originalSource)
    {
        return originalSource is DependencyObject source
            ? FindParent<DataGridRow>(source)?.Item as ForecastLine
            : null;
    }

    private ForecastLine? GetForecastLineFromEventSource(DependencyObject source)
    {
        if (FindParent<DataGridColumnHeader>(source) is not null
            || IsScrollBarInteractionSource(source))
        {
            return null;
        }

        if (FindParent<DataGridRow>(source)?.Item is ForecastLine rowLine)
        {
            return rowLine;
        }

        if (FindParent<DataGridCell>(source)?.DataContext is ForecastLine cellLine)
        {
            return cellLine;
        }

        // A DataGrid can promote a handled double-click from a cell template to
        // the grid itself. The first click has already selected the row, so the
        // selected item is a safe final fallback for that routed-event shape.
        return ForecastLinesGrid.SelectedItem as ForecastLine;
    }

    private static bool IsSupportedImportFile(string path)
    {
        var extension = System.IO.Path.GetExtension(path);
        return string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".xlsm", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEditableForecastMonthCell(DataGridCell cell)
    {
        return cell.Column is DataGridTemplateColumn templateColumn
            && templateColumn.Header is ForecastMonthColumnDefinition monthColumn
            && !monthColumn.IsTotal
            && monthColumn.IsEditable;
    }

    private void SelectedMonthlyForecastsGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        if (e.Row.Item is MonthlyForecast forecast && forecast.IsLocked)
        {
            e.Cancel = true;
        }

        if (e.Row.Item is MonthlyForecastAcrossRow acrossRow
            && (!string.Equals(acrossRow.Metric, "Forecast", StringComparison.OrdinalIgnoreCase)
                || DataContext is not MainWindowViewModel viewModel
                || viewModel.IsViewingSavedMonth))
        {
            e.Cancel = true;
        }
    }

    private void SelectedMonthlyForecastsGrid_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (RightClickGridPanBehavior.IsDraggingFrom(e.OriginalSource as DependencyObject)
            || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var grid = sender as DataGrid;
        var isHeader = FindParent<DataGridColumnHeader>(source) is not null;
        var isForecastRow = FindParent<DataGridRow>(source)?.Item is MonthlyForecast
            || FindParent<DataGridRow>(source)?.Item is MonthlyForecastAcrossRow;
        if (DataContext is MainWindowViewModel viewModel
            && grid is not null
            && (isHeader || isForecastRow))
        {
            var menu = new ContextMenu();
            var flip = new MenuItem
            {
                Header = "Flip Table",
                ToolTip = viewModel.IsMonthlyForecastMonthsAcross
                    ? "Show months down the table"
                    : "Show months across the table"
            };
            flip.Click += (_, _) =>
            {
                viewModel.ToggleMonthlyForecastOrientation();
                menu.IsOpen = false;
            };
            menu.Items.Add(flip);
            ApplyMenuIcons(menu);
            menu.PlacementTarget = source as UIElement ?? grid;
            menu.Placement = PlacementMode.MousePoint;
            menu.IsOpen = true;
            e.Handled = true;
            return;
        }

        if (FindParent<TextBox>(source) is not { Tag: "SelectableReadOnlyForecastText" } textBox)
        {
            return;
        }

        OpenSelectableForecastTextContextMenu(textBox);
        e.Handled = true;
    }

    private static bool IsForecastIdentifierCell(DataGridCell cell)
    {
        if (IsForecastRowSelectorColumn(cell.Column))
        {
            return true;
        }

        return cell.Column?.Header?.ToString() is string header
            && (string.Equals(header, "Task", StringComparison.OrdinalIgnoreCase)
                || string.Equals(header, "Resource", StringComparison.OrdinalIgnoreCase)
                || string.Equals(header, "Category", StringComparison.OrdinalIgnoreCase));
    }

    private void UpdateHoveredForecastLine(object? originalSource)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Alt) != ModifierKeys.Alt)
        {
            viewModel.ClearHoveredForecastLine();
            return;
        }

        if (originalSource is not DependencyObject source)
        {
            return;
        }

        var hoveredLine = FindParent<DataGridRow>(source)?.Item as ForecastLine;
        viewModel.SetHoveredForecastLine(hoveredLine);
    }

    private void UpdateHoveredForecastLineFromPointer()
    {
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != ModifierKeys.Alt)
        {
            ClearHoveredForecastLine();
            return;
        }

        if (!ForecastLinesGrid.IsMouseOver)
        {
            return;
        }

        var position = Mouse.GetPosition(ForecastLinesGrid);
        if (position.X < 0
            || position.Y < 0
            || position.X > ForecastLinesGrid.ActualWidth
            || position.Y > ForecastLinesGrid.ActualHeight)
        {
            ClearHoveredForecastLine();
            return;
        }

        UpdateHoveredForecastLine(ForecastLinesGrid.InputHitTest(position));
    }

    private void ClearHoveredForecastLine()
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ClearHoveredForecastLine();
        }
    }

    private void ToggleForecastColumnHighlight(DataGridColumn column)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var columnKey = GetColumnPersistenceKey(column);
        if (string.IsNullOrWhiteSpace(columnKey))
        {
            return;
        }

        var grid = GetOwningGrid(column);
        var previousKey = GetHighlightKeyForColumn(column, viewModel);
        var currentKey = GetHighlightKeyForColumn(column, viewModel);
        SetHighlightKeyForColumn(
            column,
            viewModel,
            string.Equals(currentKey, columnKey, StringComparison.OrdinalIgnoreCase)
                ? null
                : columnKey);

        ApplyForecastColumnHighlightState(grid, previousKey);
    }

    private void ApplyForecastColumnHighlightState(DataGrid? changedGrid = null, string? previousHighlightedColumnKey = null)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var grids = changedGrid is null
            ? GetHighlightableGrids().ToList()
            : GetRelatedHighlightableGrids(changedGrid, viewModel).ToList();

        foreach (var grid in grids)
        {
            var highlightedColumnKey = GetHighlightKeyForGrid(grid, viewModel);
            var keysToRefresh = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(previousHighlightedColumnKey))
            {
                keysToRefresh.Add(previousHighlightedColumnKey);
            }

            if (!string.IsNullOrWhiteSpace(highlightedColumnKey))
            {
                keysToRefresh.Add(highlightedColumnKey);
            }

            foreach (var column in grid.Columns)
            {
                if (changedGrid is null)
                {
                    column.SortDirection = null;
                }

                var columnKey = GetColumnPersistenceKey(column);
                var isHighlighted =
                    !string.IsNullOrWhiteSpace(highlightedColumnKey)
                    && string.Equals(columnKey, highlightedColumnKey, StringComparison.OrdinalIgnoreCase);
                GridColumnHighlightState.SetIsHighlighted(column, isHighlighted);
                if (changedGrid is null || keysToRefresh.Count == 0 || keysToRefresh.Contains(columnKey))
                {
                    ApplyColumnHighlightPresentation(column, isHighlighted);
                }
            }

            if (changedGrid is null || keysToRefresh.Count == 0)
            {
                ApplyVisibleCellHighlightState(grid);
                continue;
            }

            ApplyVisibleCellHighlightState(grid, keysToRefresh);
        }

        QueueRefreshForecastGroupHeaderPresenters();
    }

    private static void ApplyColumnHighlightPresentation(DataGridColumn column, bool isHighlighted)
    {
        EnsureColumnPresentation(column);
        GridColumnPresentationState.SetColumnBackground(
            column,
            isHighlighted
                ? HighlightedColumnCellBrush
                : GridColumnPresentationState.GetBaseColumnBackground(column) ?? Brushes.White);
        GridColumnPresentationState.SetHeaderBackground(
            column,
            isHighlighted
                ? HighlightedColumnHeaderBrush
                : GridColumnPresentationState.GetBaseHeaderBackground(column) ?? BrushFactory.Frozen(0xEA, 0xF0, 0xF8));
    }

    private void RefreshForecastGroupHeaderPresenters()
    {
        foreach (var presenter in _forecastGroupHeaderPresenters.ToList())
        {
            if (!presenter.IsLoaded)
            {
                _forecastGroupHeaderPresenters.Remove(presenter);
                continue;
            }

            presenter.InvalidateVisual();
        }
    }

    private void ForecastLinesGrid_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var line = GetForecastLineFromEventSource(source);
        if (line is not null)
        {
            OpenForecastTaskPlanningWindow(line);
            e.Handled = true;
        }
    }

    internal void RegisterForecastGroupHeaderPresenter(ForecastGroupHeaderPresenter presenter)
    {
        _forecastGroupHeaderPresenters.Add(presenter);
    }

    internal void UnregisterForecastGroupHeaderPresenter(ForecastGroupHeaderPresenter presenter)
    {
        _forecastGroupHeaderPresenters.Remove(presenter);
    }

    private void QueueRefreshForecastGroupHeaderPresenters()
    {
        if (_forecastGroupHeaderRefreshQueued)
        {
            return;
        }

        _forecastGroupHeaderRefreshQueued = true;
        if (!QueueMainWindowWork(DispatcherPriority.Render, () =>
        {
            _forecastGroupHeaderRefreshQueued = false;
            RefreshForecastGroupHeaderPresenters();
        }))
        {
            _forecastGroupHeaderRefreshQueued = false;
        }
    }

    private IEnumerable<DataGrid> GetHighlightableGrids()
    {
        yield return ForecastLinesGrid;
        yield return RawTransactionsGrid;
        yield return RawTransactionsMonthlyPivotGrid;
        yield return CategoryReportGrid;
        yield return CategoryMonthlyPivotGrid;
        yield return LedgerTransactionsGrid;
        yield return LedgerMonthlyPivotGrid;
        yield return SelectedMonthlyForecastsGrid;
    }

    private IEnumerable<DataGrid> GetRelatedHighlightableGrids(DataGrid changedGrid, MainWindowViewModel viewModel)
    {
        if (!TryGetHighlightScopeKey(changedGrid, viewModel, out var scopeKey, out var isDetailScope))
        {
            yield break;
        }

        foreach (var grid in GetHighlightableGrids())
        {
            if (TryGetHighlightScopeKey(grid, viewModel, out var candidateScopeKey, out var candidateIsDetailScope)
                && candidateIsDetailScope == isDetailScope
                && string.Equals(candidateScopeKey, scopeKey, StringComparison.OrdinalIgnoreCase))
            {
                yield return grid;
            }
        }
    }

    private void HandleHighlightScopeChange(string previousScopeKey, string currentScopeKey, bool isDetailScope)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (!viewModel.KeepColumnHighlightsAcrossTabs)
        {
            GetHighlightStateStore(isDetailScope).Clear();
        }

        ApplyForecastColumnHighlightState();
    }

    private bool IsHighlightableGrid(DataGrid grid)
    {
        return GetHighlightableGrids().Any(candidate => ReferenceEquals(candidate, grid));
    }

    private bool HasHighlightForGrid(DataGrid grid, MainWindowViewModel viewModel)
    {
        return !string.IsNullOrWhiteSpace(GetHighlightKeyForGrid(grid, viewModel));
    }

    private void ClearHighlightForGrid(DataGrid grid, MainWindowViewModel viewModel)
    {
        if (!TryGetHighlightScopeKey(grid, viewModel, out var scopeKey, out var isDetailScope))
        {
            return;
        }

        GetHighlightStateStore(isDetailScope).Remove(scopeKey);
    }

    private string? GetHighlightKeyForColumn(DataGridColumn column, MainWindowViewModel viewModel)
    {
        var grid = GetOwningGrid(column);
        return grid is null ? null : GetHighlightKeyForGrid(grid, viewModel);
    }

    private void SetHighlightKeyForColumn(DataGridColumn column, MainWindowViewModel viewModel, string? columnKey)
    {
        var grid = GetOwningGrid(column);
        if (grid is null || !TryGetHighlightScopeKey(grid, viewModel, out var scopeKey, out var isDetailScope))
        {
            return;
        }

        var store = GetHighlightStateStore(isDetailScope);
        if (string.IsNullOrWhiteSpace(columnKey))
        {
            store.Remove(scopeKey);
            return;
        }

        store[scopeKey] = columnKey;
    }

    private string? GetHighlightKeyForGrid(DataGrid grid, MainWindowViewModel viewModel)
    {
        if (!TryGetHighlightScopeKey(grid, viewModel, out var scopeKey, out var isDetailScope))
        {
            return null;
        }

        return GetHighlightStateStore(isDetailScope).GetValueOrDefault(scopeKey);
    }

    private Dictionary<string, string?> GetHighlightStateStore(bool isDetailScope)
    {
        return isDetailScope ? _detailWorkspaceHighlightedColumnKeys : _workspaceHighlightedColumnKeys;
    }

    private bool TryGetHighlightScopeKey(DataGrid grid, MainWindowViewModel viewModel, out string scopeKey, out bool isDetailScope)
    {
        scopeKey = string.Empty;
        isDetailScope = false;

        if (ReferenceEquals(grid, ForecastLinesGrid))
        {
            scopeKey = "CTC Forecast";
            return true;
        }

        if (ReferenceEquals(grid, RawTransactionsGrid) || ReferenceEquals(grid, RawTransactionsMonthlyPivotGrid))
        {
            scopeKey = "Raw Transactions";
            return true;
        }

        if (ReferenceEquals(grid, CategoryReportGrid) || ReferenceEquals(grid, CategoryMonthlyPivotGrid))
        {
            scopeKey = "Summary View";
            return true;
        }

        if (ReferenceEquals(grid, LedgerTransactionsGrid) || ReferenceEquals(grid, LedgerMonthlyPivotGrid))
        {
            scopeKey = "Ledger Costs";
            isDetailScope = true;
            return true;
        }

        if (ReferenceEquals(grid, SelectedMonthlyForecastsGrid))
        {
            scopeKey = "Ledger Monthly Forecast";
            isDetailScope = true;
            return true;
        }

        return false;
    }

    private DataGrid? GetOwningGrid(DataGridColumn column)
    {
        return GetHighlightableGrids().FirstOrDefault(grid => grid.Columns.Contains(column));
    }

    private void BeginEditingForecastCell(DataGridCell cell)
    {
        if (cell.DataContext is null)
        {
            return;
        }

        cell.Focus();
        ForecastLinesGrid.SelectedItem = cell.DataContext;
        ForecastLinesGrid.CurrentCell = new DataGridCellInfo(cell.DataContext, cell.Column);
        ForecastLinesGrid.BeginEdit();
        QueueMainWindowWork(DispatcherPriority.Normal, () =>
        {
            if (FindChild<TextBox>(cell) is TextBox textBox)
            {
                textBox.Focus();
                textBox.SelectAll();
            }
        });
    }

    private static T? FindParent<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = current switch
            {
                Visual or Visual3D => VisualTreeHelper.GetParent(current),
                FrameworkContentElement contentElement => contentElement.Parent,
                ContentElement contentElement => ContentOperations.GetParent(contentElement),
                _ => null
            };
        }

        return null;
    }
}
