using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using ProjectCostForecast.App.ViewModels;

namespace ProjectCostForecast.App;

public partial class MainWindow
{
    private readonly HashSet<DataGrid> _spreadsheetAttachedGrids = [];
    private readonly HashSet<ScrollBar> _spreadsheetVerticalScrollBars = [];
    private readonly Dictionary<DataGrid, object?> _spreadsheetCurrentRows = [];
    private readonly HashSet<DataGrid> _spreadsheetSelectionUpdateQueued = [];
    private static readonly Dictionary<(Type Type, string Path), PropertyInfo[]?> SpreadsheetPropertyPathCache = [];
    private static readonly Dictionary<Type, PropertyInfo?> SpreadsheetStringIndexerCache = [];

    private void QueueSpreadsheetSelectionUpdate(
        DataGrid grid,
        IEnumerable<object?>? affectedItems = null,
        bool refreshAllVisuals = false)
    {
        if (refreshAllVisuals)
        {
            _spreadsheetSelectionVisualFullRefresh.Add(grid);
            _spreadsheetSelectionVisualPendingItems.Remove(grid);
        }
        else if (!_spreadsheetSelectionVisualFullRefresh.Contains(grid) && affectedItems is not null)
        {
            if (!_spreadsheetSelectionVisualPendingItems.TryGetValue(grid, out var pendingItems))
            {
                pendingItems = new HashSet<object>(ReferenceEqualityComparer.Instance);
                _spreadsheetSelectionVisualPendingItems[grid] = pendingItems;
            }

            foreach (var item in affectedItems)
            {
                if (item is not null && item != CollectionView.NewItemPlaceholder)
                {
                    pendingItems.Add(item);
                }
            }
        }

        if (!_spreadsheetSelectionUpdateQueued.Add(grid))
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            _spreadsheetSelectionUpdateQueued.Remove(grid);
            using (GridPerformanceDiagnostics.Measure($"grid-selection-update:{grid.Name}"))
            {
                var refreshAll = _spreadsheetSelectionVisualFullRefresh.Remove(grid);
                _spreadsheetSelectionVisualPendingItems.Remove(grid, out var pendingItems);
                UpdateSpreadsheetSelectionStatus(grid);
                if (refreshAll || pendingItems is null || pendingItems.Count == 0)
                {
                    UpdateSpreadsheetSelectionVisuals(grid);
                }
                else
                {
                    UpdateSpreadsheetSelectionVisuals(grid, pendingItems);
                }
            }
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void AttachSpreadsheetGridHandlers(DependencyObject root)
    {
        if (root is DataGrid grid)
        {
            if (ReferenceEquals(grid, ScheduleGrid))
            {
                return;
            }

            grid.SelectionMode = DataGridSelectionMode.Extended;
            grid.SelectionUnit = DataGridSelectionUnit.CellOrRowHeader;
            grid.ClipboardCopyMode = DataGridClipboardCopyMode.ExcludeHeader;

            if (_spreadsheetAttachedGrids.Add(grid))
            {
                grid.PreviewMouseLeftButtonDown += SpreadsheetGrid_PreviewMouseLeftButtonDown;
                grid.PreviewMouseLeftButtonUp += SpreadsheetGrid_PreviewMouseLeftButtonUp;
                grid.MouseMove += SpreadsheetGrid_MouseMove;
                grid.PreviewMouseRightButtonUp += SpreadsheetGrid_PreviewMouseRightButtonUp;
                grid.MouseDoubleClick += SpreadsheetGrid_MouseDoubleClick;
                grid.PreviewKeyDown += SpreadsheetGrid_PreviewKeyDown;
                grid.PreviewTextInput += SpreadsheetGrid_PreviewTextInput;
                grid.BeginningEdit += SpreadsheetGrid_BeginningEdit;
                grid.CellEditEnding += SpreadsheetGrid_CellEditEnding;
                grid.SelectedCellsChanged += SpreadsheetGrid_SelectedCellsChanged;
                grid.CurrentCellChanged += SpreadsheetGrid_CurrentCellChanged;
                grid.LoadingRow += SpreadsheetGrid_LoadingRow;
                grid.Loaded += SpreadsheetGrid_Loaded;
                grid.AddHandler(FrameworkElement.ContextMenuOpeningEvent, new ContextMenuEventHandler(SpreadsheetTextBox_ContextMenuOpening), true);
            }

            UpdateSpreadsheetSelectionStatus(grid);
            UpdateSpreadsheetSelectionVisuals(grid);
            Dispatcher.BeginInvoke(() => WireSpreadsheetVerticalScrollbar(grid), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            AttachSpreadsheetGridHandlers(VisualTreeHelper.GetChild(root, index));
        }
    }

    private void SpreadsheetGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not DataGrid grid)
        {
            return;
        }

        var sourceTextBox = e.OriginalSource as TextBox;
        if (Keyboard.Modifiers == ModifierKeys.None && e.Key is Key.Delete or Key.Back)
        {
            // Delete/Backspace should clear the active spreadsheet selection even when focus is
            // still sitting inside the inline TextBox editor after a cell edit.
            if (sourceTextBox is not null && FindParent<DataGridCell>(sourceTextBox) is null)
            {
                return;
            }

            grid.CommitEdit(DataGridEditingUnit.Cell, true);
            grid.CommitEdit(DataGridEditingUnit.Row, true);
            EnsureCurrentCellSelected(grid);
            ClearSelectedGridCells(grid, e.Key == Key.Back ? "Cleared" : "Deleted");
            e.Handled = true;
            return;
        }

        if (sourceTextBox is not null)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Enter && FindParent<DataGridCell>(sourceTextBox) is not null)
            {
                FillSelectedCellsFromValue(grid, sourceTextBox.Text, "Filled");
                e.Handled = true;
            }

            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.F2)
        {
            if (TryGetCurrentSpreadsheetCell(grid, out var item, out var column) && CanWriteGridCell(grid, item, column))
            {
                BeginSpreadsheetCellEdit(grid, item, column, null, replaceText: false);
                e.Handled = true;
            }
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.A)
        {
            grid.SelectAllCells();
            QueueSpreadsheetSelectionUpdate(grid, refreshAllVisuals: true);
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C)
        {
            CopySelectedGridCells(grid);
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.X)
        {
            CutSelectedGridCells(grid);
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.V)
        {
            PasteIntoGrid(grid);
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z)
        {
            UndoSpreadsheetEdit();
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y)
        {
            RedoSpreadsheetEdit();
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Enter)
        {
            FillSelectedCellsFromCurrent(grid);
            e.Handled = true;
        }
        else if (Keyboard.Modifiers is ModifierKeys.None or ModifierKeys.Shift
                 && e.Key is Key.Enter or Key.Tab or Key.Home or Key.End or Key.Left or Key.Right or Key.Up or Key.Down)
        {
            if (TryNavigateSpreadsheetCell(grid, e.Key, (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift))
            {
                e.Handled = true;
            }
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key is Key.Home or Key.End)
        {
            if (TryNavigateSpreadsheetCell(grid, e.Key, reverse: false, control: true))
            {
                e.Handled = true;
            }
        }
    }

    private void SpreadsheetGrid_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not DataGrid grid
            || e.OriginalSource is TextBox
            || string.IsNullOrWhiteSpace(e.Text)
            || !SupportsSpreadsheetTypeOverwrite(grid))
        {
            return;
        }

        var editCell = grid.SelectedCells.Count > 1
            ? GetSelectionOriginWritableCell(grid, grid.SelectedCells)
            : null;
        var editItem = editCell?.Item;
        var editColumn = editCell?.Column;

        if (editCell is null)
        {
            if (grid.CurrentCell is not { IsValid: true } currentCell
                || currentCell.Item is null
                || currentCell.Column is null)
            {
                return;
            }

            editItem = currentCell.Item;
            editColumn = currentCell.Column;
        }

        if (editItem is null
            || editColumn is null
            || !CanWriteGridCell(grid, editItem, editColumn))
        {
            return;
        }

        BeginSpreadsheetCellEdit(grid, editItem, editColumn, e.Text, replaceText: true);
        e.Handled = true;
    }

    private void SpreadsheetGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid || e.ChangedButton != MouseButton.Left || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        if (FindParent<TextBox>(source) is not null || FindParent<DataGridColumnHeader>(source) is not null)
        {
            return;
        }

        var cell = FindParent<DataGridCell>(source);
        if (cell is null || !CanWriteGridCell(grid, cell.DataContext, cell.Column))
        {
            return;
        }

        grid.CurrentCell = new DataGridCellInfo(cell.DataContext, cell.Column);
        cell.Focus();
        if (grid.BeginEdit())
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (FindChild<TextBox>(cell) is TextBox editor)
                {
                    editor.Focus();
                    editor.SelectAll();
                }
            });
        }

        e.Handled = true;
    }

    private void SpreadsheetGrid_BeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
    {
        if (sender is not DataGrid grid || e.Row.Item is null || e.Column is null || !CanWriteGridCell(grid, e.Row.Item, e.Column))
        {
            return;
        }

        _spreadsheetEditSnapshots[grid] = new SpreadsheetEditSnapshot(
            grid,
            e.Row.Item,
            e.Column,
            FormatGridCellValue(GetGridCellValue(e.Row.Item, e.Column)));
    }

    private void SpreadsheetGrid_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        if (sender is not DataGrid grid)
        {
            return;
        }

        if (e.EditAction != DataGridEditAction.Commit)
        {
            _spreadsheetEditSnapshots.Remove(grid);
            return;
        }

        if (!_spreadsheetEditSnapshots.TryGetValue(grid, out var snapshot)
            || !ReferenceEquals(snapshot.Item, e.Row.Item)
            || !ReferenceEquals(snapshot.Column, e.Column))
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            _spreadsheetEditSnapshots.Remove(grid);
            var after = FormatGridCellValue(GetGridCellValue(snapshot.Item, snapshot.Column));
            if (string.Equals(snapshot.BeforeText, after, StringComparison.Ordinal))
            {
                return;
            }

            RegisterSpreadsheetUndoBatch(
                "Cell edit",
                [new SpreadsheetCellEdit(snapshot.Item, snapshot.Column, snapshot.BeforeText, after)]);
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void SpreadsheetGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGrid grid)
        {
            WireSpreadsheetVerticalScrollbar(grid);
            QueueSpreadsheetSelectionUpdate(grid, refreshAllVisuals: true);
            if (IsManagementResourceGrid(grid))
            {
                QueueSynchronizeManagementResourceGrids();
            }
        }
    }

    private void BeginSpreadsheetCellEdit(DataGrid grid, object item, DataGridColumn column, string? initialText, bool replaceText)
    {
        grid.SelectedItem = item;
        grid.CurrentCell = new DataGridCellInfo(item, column);
        grid.ScrollIntoView(item, column);

        Dispatcher.BeginInvoke(() =>
        {
            grid.UpdateLayout();
            if (grid.ItemContainerGenerator.ContainerFromItem(item) is not DataGridRow row)
            {
                return;
            }

            var presenter = FindChild<DataGridCellsPresenter>(row);
            var cell = presenter?.ItemContainerGenerator.ContainerFromIndex(column.DisplayIndex) as DataGridCell;
            cell?.Focus();
            if (!grid.BeginEdit())
            {
                return;
            }

            Dispatcher.BeginInvoke(() =>
            {
                if (cell is null || FindChild<TextBox>(cell) is not TextBox editor)
                {
                    return;
                }

                editor.Focus();
                if (replaceText)
                {
                    editor.Text = initialText ?? string.Empty;
                    editor.CaretIndex = editor.Text.Length;
                }
                else
                {
                    editor.SelectAll();
                }
            }, System.Windows.Threading.DispatcherPriority.Input);
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    private bool TryGetCurrentSpreadsheetCell(DataGrid grid, out object item, out DataGridColumn column)
    {
        item = grid.CurrentCell.Item;
        column = grid.CurrentCell.Column;
        if (item is null || item == CollectionView.NewItemPlaceholder || column is null)
        {
            item = grid.SelectedCells.FirstOrDefault(cell => cell.IsValid && cell.Item is not null).Item;
            column = grid.SelectedCells.FirstOrDefault(cell => cell.IsValid && cell.Column is not null).Column;
        }

        return item is not null && item != CollectionView.NewItemPlaceholder && column is not null;
    }

    private void EnsureCurrentCellSelected(DataGrid grid)
    {
        if (grid.SelectedCells.Count > 0)
        {
            return;
        }

        if (TryGetCurrentSpreadsheetCell(grid, out var item, out var column))
        {
            grid.SelectedCells.Add(new DataGridCellInfo(item, column));
        }
    }

    private bool TryNavigateSpreadsheetCell(DataGrid grid, Key key, bool reverse, bool control = false)
    {
        if (!TryGetCurrentSpreadsheetCell(grid, out var item, out var column))
        {
            return false;
        }

        grid.CommitEdit(DataGridEditingUnit.Cell, true);
        grid.CommitEdit(DataGridEditingUnit.Row, true);

        var visibleColumns = grid.Columns
            .Where(candidate => candidate.Visibility == Visibility.Visible)
            .OrderBy(candidate => candidate.DisplayIndex)
            .ToList();
        var currentRowIndex = grid.Items.IndexOf(item);
        var currentColumnIndex = visibleColumns.IndexOf(column);
        if (currentRowIndex < 0 || currentColumnIndex < 0 || visibleColumns.Count == 0)
        {
            return false;
        }

        var targetRowIndex = currentRowIndex;
        var targetColumnIndex = currentColumnIndex;
        switch (key)
        {
            case Key.Enter:
                targetRowIndex += reverse ? -1 : 1;
                break;
            case Key.Up:
                targetRowIndex--;
                break;
            case Key.Down:
                targetRowIndex++;
                break;
            case Key.Left:
                targetColumnIndex--;
                break;
            case Key.Right:
                targetColumnIndex++;
                break;
            case Key.Tab:
                targetColumnIndex += reverse ? -1 : 1;
                if (targetColumnIndex < 0)
                {
                    targetColumnIndex = visibleColumns.Count - 1;
                    targetRowIndex--;
                }
                else if (targetColumnIndex >= visibleColumns.Count)
                {
                    targetColumnIndex = 0;
                    targetRowIndex++;
                }

                break;
            case Key.Home:
                targetColumnIndex = 0;
                if (control)
                {
                    targetRowIndex = 0;
                }

                break;
            case Key.End:
                targetColumnIndex = visibleColumns.Count - 1;
                if (control)
                {
                    targetRowIndex = grid.Items.Count - 1;
                }

                break;
        }

        targetRowIndex = Math.Clamp(targetRowIndex, 0, Math.Max(0, grid.Items.Count - 1));
        targetColumnIndex = Math.Clamp(targetColumnIndex, 0, visibleColumns.Count - 1);
        if (grid.Items[targetRowIndex] == CollectionView.NewItemPlaceholder)
        {
            return false;
        }

        SelectSingleGridCell(grid, grid.Items[targetRowIndex], visibleColumns[targetColumnIndex]);
        return true;
    }

    private void SelectSingleGridCell(DataGrid grid, object item, DataGridColumn column)
    {
        var info = new DataGridCellInfo(item, column);
        grid.SelectedCells.Clear();
        grid.SelectedCells.Add(info);
        grid.CurrentCell = info;
        grid.SelectedItem = item;
        SelectGridRowContext(grid, item);
        grid.ScrollIntoView(item, column);
        grid.Focus();
        Keyboard.Focus(grid);
        Dispatcher.BeginInvoke(() =>
        {
            if (grid.ItemContainerGenerator.ContainerFromItem(item) is not DataGridRow row)
            {
                grid.Focus();
                Keyboard.Focus(grid);
                return;
            }

            var presenter = FindChild<DataGridCellsPresenter>(row);
            var cell = presenter?.ItemContainerGenerator.ContainerFromIndex(column.DisplayIndex) as DataGridCell;
            if (cell is not null)
            {
                cell.Focus();
                Keyboard.Focus(cell);
            }
            else
            {
                grid.Focus();
                Keyboard.Focus(grid);
            }
        }, System.Windows.Threading.DispatcherPriority.Input);
        QueueSpreadsheetSelectionUpdate(grid, [item], refreshAllVisuals: false);
    }

    private void QueueRestoreSpreadsheetCellFocus(DataGrid grid, object item, DataGridColumn column)
    {
        void Restore()
        {
            if (!grid.Items.Contains(item))
            {
                return;
            }

            SelectSingleGridCell(grid, item, column);
        }

        Dispatcher.BeginInvoke((Action)Restore, System.Windows.Threading.DispatcherPriority.Input);
        Dispatcher.BeginInvoke((Action)Restore, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void SpreadsheetGrid_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (sender is DataGrid grid)
        {
            UpdateSpreadsheetRowSelectionVisuals(grid, e.Row);
        }
    }

    private void WireSpreadsheetVerticalScrollbar(DataGrid grid)
    {
        if (FindChild<ScrollViewer>(grid) is not ScrollViewer scrollViewer)
        {
            return;
        }

        foreach (var scrollBar in FindChildren<ScrollBar>(grid).Where(item => item.Orientation == Orientation.Vertical))
        {
            if (_spreadsheetVerticalScrollBars.Add(scrollBar))
            {
                scrollBar.Scroll += (_, args) => scrollViewer.ScrollToVerticalOffset(args.NewValue);
            }
        }
    }

    private void SpreadsheetGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var cell = FindParent<DataGridCell>(source);
        if (cell is null)
        {
            // Clicking the row header selects the whole row; it must still drive the detail panel.
            if (FindParent<DataGridRowHeader>(source) is { } rowHeader
                && FindParent<DataGridRow>(rowHeader) is { } row)
            {
                SelectGridRowContext(grid, row.Item);
                QueueSpreadsheetSelectionUpdate(grid, [row.Item]);
            }

            return;
        }

        if (IsSpreadsheetFillHandleHit(grid, cell, e.GetPosition(cell)))
        {
            _spreadsheetFillDrag = new SpreadsheetFillDrag(
                grid,
                cell.DataContext,
                cell.Column,
                FormatGridCellValue(GetGridCellValue(cell.DataContext, cell.Column)));
            grid.CaptureMouse();
            e.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != ModifierKeys.None)
        {
            PreserveGridRowContextThroughCellSelection(grid);
            return;
        }

        SelectGridRowContext(grid, cell.DataContext);
        QueueSpreadsheetSelectionUpdate(grid, [cell.DataContext]);
        if (IsManagementResourceGrid(grid) && CanWriteGridCell(grid, cell.DataContext, cell.Column))
        {
            Dispatcher.BeginInvoke(() => BeginSpreadsheetCellEdit(grid, cell.DataContext, cell.Column, null, replaceText: false), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void SpreadsheetGrid_MouseMove(object sender, MouseEventArgs e)
    {
        if (_spreadsheetFillDrag is not { } drag || sender is not DataGrid grid || !ReferenceEquals(grid, drag.Grid))
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            CompleteSpreadsheetFillDrag(applyFill: false);
            return;
        }

        if (grid.InputHitTest(e.GetPosition(grid)) is DependencyObject source
            && FindParent<DataGridCell>(source) is { } targetCell)
        {
            SelectSpreadsheetRange(grid, drag.SourceItem, drag.SourceColumn, targetCell.DataContext, targetCell.Column);
            e.Handled = true;
        }
    }

    private void SpreadsheetGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_spreadsheetFillDrag is not { } drag || sender is not DataGrid grid || !ReferenceEquals(grid, drag.Grid))
        {
            return;
        }

        CompleteSpreadsheetFillDrag(applyFill: true);
        e.Handled = true;
    }

    private bool IsSpreadsheetFillHandleHit(DataGrid grid, DataGridCell cell, Point point)
    {
        if (!grid.CurrentCell.IsValid
            || !ReferenceEquals(grid.CurrentCell.Item, cell.DataContext)
            || !ReferenceEquals(grid.CurrentCell.Column, cell.Column)
            || !CanWriteGridCell(grid, cell.DataContext, cell.Column))
        {
            return false;
        }

        const double handleSize = 9;
        return point.X >= Math.Max(0, cell.ActualWidth - handleSize)
            && point.Y >= Math.Max(0, cell.ActualHeight - handleSize);
    }

    private void SelectSpreadsheetRange(DataGrid grid, object startItem, DataGridColumn startColumn, object endItem, DataGridColumn endColumn)
    {
        var visibleItems = grid.Items
            .Cast<object>()
            .Where(item => item != CollectionView.NewItemPlaceholder)
            .ToList();
        var visibleColumns = grid.Columns
            .Where(column => column.Visibility == Visibility.Visible)
            .OrderBy(column => column.DisplayIndex)
            .ToList();
        var startRow = visibleItems.IndexOf(startItem);
        var endRow = visibleItems.IndexOf(endItem);
        var startColumnIndex = visibleColumns.IndexOf(startColumn);
        var endColumnIndex = visibleColumns.IndexOf(endColumn);
        if (startRow < 0 || endRow < 0 || startColumnIndex < 0 || endColumnIndex < 0)
        {
            return;
        }

        grid.SelectedCells.Clear();
        for (var rowIndex = Math.Min(startRow, endRow); rowIndex <= Math.Max(startRow, endRow); rowIndex++)
        {
            for (var columnIndex = Math.Min(startColumnIndex, endColumnIndex); columnIndex <= Math.Max(startColumnIndex, endColumnIndex); columnIndex++)
            {
                grid.SelectedCells.Add(new DataGridCellInfo(visibleItems[rowIndex], visibleColumns[columnIndex]));
            }
        }

        QueueSpreadsheetSelectionUpdate(grid, refreshAllVisuals: true);
    }

    private void CompleteSpreadsheetFillDrag(bool applyFill)
    {
        var drag = _spreadsheetFillDrag;
        _spreadsheetFillDrag = null;
        if (drag is null)
        {
            return;
        }

        if (drag.Grid.IsMouseCaptured)
        {
            drag.Grid.ReleaseMouseCapture();
        }

        if (applyFill)
        {
            FillSelectedCellsFromValue(drag.Grid, drag.SourceText, "Drag-filled");
        }
    }

    private void PreserveGridRowContextThroughCellSelection(DataGrid grid)
    {
        var selectedItem = grid.SelectedItem;
        var rowContext = GetGridRowContext(grid);
        Dispatcher.BeginInvoke(() =>
        {
            if (selectedItem is null || grid.Items.Contains(selectedItem))
            {
                grid.SelectedItem = selectedItem;
            }

            RestoreGridRowContext(grid, rowContext);
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    private object? GetGridRowContext(DataGrid grid)
    {
        if (_spreadsheetCurrentRows.TryGetValue(grid, out var currentRow))
        {
            return currentRow;
        }

        if (DataContext is not MainWindowViewModel viewModel)
        {
            return grid.SelectedItem;
        }

        if (ReferenceEquals(grid, ForecastLinesGrid) || ReferenceEquals(grid, SelectedMonthlyForecastsGrid))
        {
            return viewModel.SelectedForecastLine;
        }

        if (ReferenceEquals(grid, ScheduleGrid))
        {
            return viewModel.SelectedScheduleActivity;
        }

        return grid.SelectedItem;
    }

    private void RestoreGridRowContext(DataGrid grid, object? rowContext)
    {
        _spreadsheetCurrentRows[grid] = rowContext;
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (ReferenceEquals(grid, ForecastLinesGrid) || ReferenceEquals(grid, SelectedMonthlyForecastsGrid))
        {
            viewModel.SelectedForecastLine = rowContext as ForecastLine;
        }
        else if (ReferenceEquals(grid, ScheduleGrid))
        {
            viewModel.SelectedScheduleActivity = rowContext as ScheduleActivity;
        }
    }

    private void SelectGridRowContext(DataGrid grid, object item)
    {
        _spreadsheetCurrentRows[grid] = item;
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        switch (item)
        {
            case ForecastLine line:
                viewModel.SelectedForecastLine = line;
                break;
            case MonthlyForecast forecast:
                viewModel.SelectedForecastLine = viewModel.GetForecastLine(forecast);
                break;
            case ScheduleActivity activity when ReferenceEquals(grid, ScheduleGrid):
                viewModel.SelectedScheduleActivity = activity;
                break;
        }
    }

    private bool IsManagementResourceGrid(DataGrid grid)
    {
        return ReferenceEquals(grid, ManagementResourceAllocationGrid)
            || ReferenceEquals(grid, ManagementResourceHoursGrid)
            || ReferenceEquals(grid, ManagementResourceCostGrid);
    }

    private bool SupportsSpreadsheetTypeOverwrite(DataGrid grid)
    {
        return IsManagementResourceGrid(grid)
            || ReferenceEquals(grid, ForecastLinesGrid)
            || ReferenceEquals(grid, SelectedMonthlyForecastsGrid);
    }

    private void SpreadsheetGrid_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled || _gridRightDragging || sender is not DataGrid grid || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        if (FindParent<DataGridColumnHeader>(source) is not null)
        {
            return;
        }

        if (FindParent<TextBox>(source) is TextBox textBox)
        {
            OpenSelectableForecastTextContextMenu(textBox);
            e.Handled = true;
            return;
        }

        var cell = FindParent<DataGridCell>(source);
        if (cell is null)
        {
            if (FindParent<DataGridRowHeader>(source) is { } rowHeader
                && FindParent<DataGridRow>(rowHeader) is { Item: ForecastLine rowLine } row
                && DataContext is MainWindowViewModel viewModel)
            {
                var rowMenu = new ContextMenu();
                rowMenu.Items.Add(CreateAddManagementResourceMenuItem(rowLine));
                AddForecastLineCommentMenuItem(rowMenu, rowLine);
                rowMenu.Items.Add(new Separator());
                var addAbove = new MenuItem { Header = "Add line above" };
                addAbove.Click += (_, _) => BeginEditingForecastResourceCell(viewModel.InsertForecastLine(rowLine, below: false));
                rowMenu.Items.Add(addAbove);

                var addBelow = new MenuItem { Header = "Add line below" };
                addBelow.Click += (_, _) => BeginEditingForecastResourceCell(viewModel.InsertForecastLine(rowLine, below: true));
                rowMenu.Items.Add(addBelow);

                rowMenu.PlacementTarget = rowHeader;
                rowMenu.Placement = PlacementMode.MousePoint;
                rowMenu.IsOpen = true;
                e.Handled = true;
            }

            return;
        }

        var info = new DataGridCellInfo(cell.DataContext, cell.Column);
        if (!grid.SelectedCells.Contains(info))
        {
            grid.SelectedCells.Clear();
            grid.SelectedCells.Add(info);
        }

        grid.CurrentCell = info;
        var menu = BuildSpreadsheetCellContextMenu(grid, cell);
        cell.ContextMenu = menu;
        menu.PlacementTarget = cell;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private ContextMenu BuildSpreadsheetCellContextMenu(DataGrid grid, DataGridCell cell)
    {
        var menu = new ContextMenu();
        var cut = new MenuItem { Header = "Cut", InputGestureText = "Ctrl+X", IsEnabled = grid.SelectedCells.Any(item => CanWriteGridCell(grid, item.Item, item.Column)) };
        cut.Click += (_, _) => CutSelectedGridCells(grid);
        menu.Items.Add(cut);

        var copy = new MenuItem { Header = "Copy", InputGestureText = "Ctrl+C", IsEnabled = grid.SelectedCells.Count > 0 };
        copy.Click += (_, _) => CopySelectedGridCells(grid);
        menu.Items.Add(copy);

        var paste = new MenuItem { Header = "Paste", InputGestureText = "Ctrl+V", IsEnabled = Clipboard.ContainsText() };
        paste.Click += (_, _) => PasteIntoGrid(grid);
        menu.Items.Add(paste);

        if (ReferenceEquals(grid, ForecastLinesGrid) && TryGetSelectedForecastCurve(grid, out var curveLine, out var curveColumns))
        {
            var adjustCurve = new MenuItem { Header = "Adjust curve" };
            adjustCurve.Click += (_, _) => OpenForecastCurveEditor(curveLine, curveColumns);
            menu.Items.Add(adjustCurve);
        }

        var cellText = FormatGridCellValue(GetGridCellValue(cell.DataContext, cell.Column)).Trim();
        var filter = new MenuItem
        {
            Header = string.IsNullOrWhiteSpace(cellText) ? "Filter by selection" : $"Filter by \"{cellText}\"",
            IsEnabled = !string.IsNullOrWhiteSpace(cellText)
        };
        filter.Click += (_, _) => FilterColumnBySelectedText(grid, cell.Column, cellText, exactMatch: true);
        menu.Items.Add(filter);

        var line = GetForecastLineForGridItem(cell.DataContext);
        if (line is not null)
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(CreateAddManagementResourceMenuItem(line));
            AddForecastLineCommentMenuItem(menu, line);
            if (ReferenceEquals(grid, ForecastLinesGrid)
                && string.Equals(cell.Column.Header?.ToString(), "All Month Comments", StringComparison.OrdinalIgnoreCase))
            {
                var manual = new MenuItem { Header = line.HasManualAllMonthComment ? "Edit manual comment" : "Add manual comment" };
                manual.Click += (_, _) => OpenManualForecastCommentEditor(line);
                menu.Items.Add(manual);

                if (line.HasManualAllMonthComment)
                {
                    var mode = new MenuItem
                    {
                        Header = line.UseManualAllMonthComment ? "Use pulled-through comments" : "Use saved manual comment"
                    };
                    mode.Click += (_, _) =>
                    {
                        if (DataContext is MainWindowViewModel commentViewModel)
                        {
                            commentViewModel.SetForecastCommentMode(line, !line.UseManualAllMonthComment);
                        }
                    };
                    menu.Items.Add(mode);
                }
            }
        }

        if (cell.DataContext is ManagementResourceTableRow managementRow)
        {
            menu.Items.Add(new Separator());
            var remove = new MenuItem { Header = "Remove management resource" };
            remove.Click += (_, _) =>
            {
                if (DataContext is MainWindowViewModel managementViewModel)
                {
                    managementViewModel.RemoveManagementResource(managementRow.Resource);
                }
            };
            menu.Items.Add(remove);
        }

        if (DataContext is MainWindowViewModel viewModel)
        {
            if (ReferenceEquals(grid, ForecastLinesGrid) && cell.DataContext is ForecastLine forecastLine)
            {
                menu.Items.Add(new Separator());
                var addAbove = new MenuItem { Header = "Add line above" };
                addAbove.Click += (_, _) => BeginEditingForecastResourceCell(viewModel.InsertForecastLine(forecastLine, below: false));
                menu.Items.Add(addAbove);

                var addBelow = new MenuItem { Header = "Add line below" };
                addBelow.Click += (_, _) => BeginEditingForecastResourceCell(viewModel.InsertForecastLine(forecastLine, below: true));
                menu.Items.Add(addBelow);

                var deleteLine = new MenuItem
                {
                    Header = "Delete line",
                    IsEnabled = forecastLine.IsManuallyAdded,
                    ToolTip = forecastLine.IsManuallyAdded ? null : "Lines that came from imported raw data cannot be deleted"
                };
                deleteLine.Click += (_, _) => viewModel.DeleteForecastLine(forecastLine);
                menu.Items.Add(deleteLine);
            }

            if (ReferenceEquals(grid, LedgerTransactionsGrid) || ReferenceEquals(grid, LedgerMonthlyPivotGrid))
            {
                menu.Items.Add(new Separator());
                var allTasks = new MenuItem
                {
                    Header = "Show data for same resource across all tasks",
                    IsCheckable = true,
                    IsChecked = viewModel.ShowLedgerResourceAcrossAllTasks
                };
                allTasks.Click += (_, _) => viewModel.ShowLedgerResourceAcrossAllTasks = !viewModel.ShowLedgerResourceAcrossAllTasks;
                menu.Items.Add(allTasks);
            }

            menu.Items.Add(new Separator());
            menu.Items.Add(BuildQuickFiltersMenu(viewModel));
        }

        return menu;
    }

    private void OpenManualForecastCommentEditor(ForecastLine line)
    {
        var window = new ManualCommentWindow(line.ResourceName, line.ManualAllMonthComment) { Owner = this };
        if (window.ShowDialog() == true && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SaveManualForecastComment(line, window.Comment);
        }
    }

    private bool TryGetSelectedForecastCurve(DataGrid grid, out ForecastLine line, out List<ForecastMonthColumnDefinition> columns)
    {
        line = null!;
        columns = [];
        var selected = grid.SelectedCells
            .Where(cell => cell.Item is ForecastLine && cell.Column.Header is ForecastMonthColumnDefinition { IsTotal: false, IsEditable: true })
            .OrderBy(cell => cell.Column.DisplayIndex)
            .ToList();
        if (selected.Count < 2 || selected.Select(cell => cell.Item).Distinct().Count() != 1)
        {
            return false;
        }

        var indexes = selected.Select(cell => cell.Column.DisplayIndex).Distinct().OrderBy(index => index).ToList();
        if (indexes.Count != selected.Count || indexes[^1] - indexes[0] + 1 != indexes.Count)
        {
            return false;
        }

        line = (ForecastLine)selected[0].Item;
        columns = selected.Select(cell => (ForecastMonthColumnDefinition)cell.Column.Header).ToList();
        return true;
    }

    private void OpenForecastCurveEditor(ForecastLine line, IReadOnlyList<ForecastMonthColumnDefinition> columns)
    {
        var points = columns.Select(column => new ForecastCurvePoint(
            column.Key,
            string.Equals(column.PrimaryLabel, column.Key, StringComparison.OrdinalIgnoreCase)
                ? column.SecondaryLabel
                : column.PrimaryLabel,
            column.Key,
            line[column.Key])).ToList();
        var window = new ForecastCurveWindow(line.ResourceName, line.ProjectCode, points) { Owner = this };
        if (window.ShowDialog() != true || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.BeginSpreadsheetEditBatch();
        for (var i = 0; i < columns.Count; i++)
        {
            line[columns[i].Key] = window.Values[i];
        }

        line.NotifyMonthForecastValuesChanged();
        viewModel.EndSpreadsheetEditBatch("Forecast curve adjusted", changed: true, rebuildFilterLists: false);
    }

    private static MenuItem BuildQuickFiltersMenu(MainWindowViewModel viewModel)
    {
        var menu = new MenuItem { Header = "Quick filters" };
        menu.Items.Add(CreateQuickFilterToggle("Actual cost only", viewModel.ShowOnlyLinesWithActualCost, value => viewModel.ShowOnlyLinesWithActualCost = value));
        menu.Items.Add(CreateQuickFilterToggle("Cost this month only", viewModel.ShowCostThisMonthOnly, value => viewModel.ShowCostThisMonthOnly = value));
        menu.Items.Add(CreateQuickFilterToggle("Remaining forecast only", viewModel.ShowOnlyLinesWithRemainingForecast, value => viewModel.ShowOnlyLinesWithRemainingForecast = value));

        var monthly = new MenuItem { Header = "Monthly variance" };
        foreach (var option in viewModel.MonthlyVarianceFilters)
        {
            monthly.Items.Add(CreateQuickFilterChoice(option, viewModel.SelectedMonthlyVarianceFilter, value => viewModel.SelectedMonthlyVarianceFilter = value));
        }

        menu.Items.Add(monthly);

        var budget = new MenuItem { Header = "Budget variance" };
        foreach (var option in viewModel.BudgetVarianceFilters)
        {
            budget.Items.Add(CreateQuickFilterChoice(option, viewModel.SelectedBudgetVarianceFilter, value => viewModel.SelectedBudgetVarianceFilter = value));
        }

        menu.Items.Add(budget);
        return menu;
    }

    private static MenuItem CreateQuickFilterToggle(string header, bool isChecked, Action<bool> apply)
    {
        var item = new MenuItem
        {
            Header = header,
            IsCheckable = true,
            IsChecked = isChecked
        };
        item.Click += (_, _) => apply(item.IsChecked);
        return item;
    }

    private static MenuItem CreateQuickFilterChoice(string option, string selectedOption, Action<string> apply)
    {
        var item = new MenuItem
        {
            Header = option,
            IsCheckable = true,
            IsChecked = string.Equals(option, selectedOption, StringComparison.OrdinalIgnoreCase)
        };
        item.Click += (_, _) => apply(option);
        return item;
    }

    private void BeginEditingForecastResourceCell(ForecastLine line)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var resourceColumn = ForecastLinesGrid.Columns
                .FirstOrDefault(column => column.Header is string header && string.Equals(header, "Resource", StringComparison.Ordinal));
            if (resourceColumn is null)
            {
                return;
            }

            ForecastLinesGrid.ScrollIntoView(line);
            ForecastLinesGrid.UpdateLayout();
            var info = new DataGridCellInfo(line, resourceColumn);
            ForecastLinesGrid.SelectedCells.Clear();
            ForecastLinesGrid.SelectedCells.Add(info);
            ForecastLinesGrid.CurrentCell = info;
            ForecastLinesGrid.BeginEdit();
            Dispatcher.BeginInvoke(() =>
            {
                var cell = FindChildren<DataGridCell>(ForecastLinesGrid)
                    .FirstOrDefault(candidate => ReferenceEquals(candidate.DataContext, line) && ReferenceEquals(candidate.Column, resourceColumn));
                if (cell is not null && FindChild<TextBox>(cell) is TextBox editor)
                {
                    editor.Focus();
                    editor.SelectAll();
                }
            }, System.Windows.Threading.DispatcherPriority.Input);
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private ForecastLine? GetForecastLineForGridItem(object? item)
    {
        if (item is ForecastLine line)
        {
            return line;
        }

        return item is MonthlyForecast forecast && DataContext is MainWindowViewModel viewModel
            ? viewModel.GetForecastLine(forecast)
            : null;
    }

    private void SpreadsheetGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
    {
        if (sender is DataGrid grid)
        {
            QueueSpreadsheetSelectionUpdate(
                grid,
                e.AddedCells.Select(cell => cell.Item)
                    .Concat(e.RemovedCells.Select(cell => cell.Item))
                    .Concat([grid.CurrentCell.Item]));
        }
    }

    private void SpreadsheetGrid_CurrentCellChanged(object? sender, EventArgs e)
    {
        if (sender is not DataGrid grid)
        {
            return;
        }

        _spreadsheetPreviousCurrentCells.TryGetValue(grid, out var previousCell);
        var currentCell = grid.CurrentCell;
        _spreadsheetPreviousCurrentCells[grid] = currentCell;
        QueueSpreadsheetSelectionUpdate(grid, [previousCell.Item, currentCell.Item, GetGridRowContext(grid)]);
    }

    private void SpreadsheetTextBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source || FindParent<TextBox>(source) is not TextBox textBox)
        {
            return;
        }

        e.Handled = true;
        if (textBox.ContextMenu?.IsOpen != true)
        {
            Dispatcher.BeginInvoke(() => OpenSelectableForecastTextContextMenu(textBox), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void UpdateSpreadsheetSelectionVisuals(DataGrid grid)
    {
        UpdateSpreadsheetSelectionVisuals(grid, null);
    }

    private void UpdateSpreadsheetSelectionVisuals(DataGrid grid, ISet<object>? affectedItems)
    {
        var columnPositions = grid.Columns
            .Where(column => column.Visibility == Visibility.Visible)
            .OrderBy(column => column.DisplayIndex)
            .Select((column, index) => (column, index))
            .ToDictionary(item => item.column, item => item.index);
        var selectedColumnsByItem = new Dictionary<object, HashSet<DataGridColumn>>(ReferenceEqualityComparer.Instance);
        foreach (var selectedCell in grid.SelectedCells)
        {
            if (selectedCell.Item is null || selectedCell.Column is null || !columnPositions.ContainsKey(selectedCell.Column))
            {
                continue;
            }

            if (!selectedColumnsByItem.TryGetValue(selectedCell.Item, out var selectedColumns))
            {
                selectedColumns = [];
                selectedColumnsByItem[selectedCell.Item] = selectedColumns;
            }

            selectedColumns.Add(selectedCell.Column);
        }

        var fullySelectedRowItems = new HashSet<object>(grid.SelectedItems.Cast<object>(), ReferenceEqualityComparer.Instance);
        var currentRowContext = GetGridRowContext(grid);
        var currentCell = grid.CurrentCell;
        var fillHandleCell = GetFillHandleCell(grid, fullySelectedRowItems);

        foreach (var cell in EnumerateSelectionRefreshCells(grid, affectedItems))
        {
            var item = cell.DataContext;
            var isCurrentRow = IsCurrentGridRowItem(grid, item, currentRowContext);
            GridSelectionVisualState.SetIsCurrentRow(cell, isCurrentRow);
            var column = cell.Column;
            var hasCellContext = item is not null
                && item != CollectionView.NewItemPlaceholder
                && column is not null;
            GridSelectionVisualState.SetIsLockedCell(
                cell,
                hasCellContext && IsLockedGridCell(item!, column!));
            GridSelectionVisualState.SetIsCalculatedCell(
                cell,
                hasCellContext && IsCalculatedGridCell(item!, column!));
            GridSelectionVisualState.SetIsReadOnlyCell(
                cell,
                hasCellContext && !CanWriteGridCell(grid, item!, column!));

            var isInCellSelection = item is not null
                && column is not null
                && selectedColumnsByItem.TryGetValue(item, out var selectedColumns)
                && selectedColumns.Contains(column);
            var isCurrentCell = currentCell.IsValid
                && ReferenceEquals(currentCell.Item, item)
                && ReferenceEquals(currentCell.Column, column);

            // A whole-row selection paints the row band only; individual cell picks (or the
            // current cell within a selected row) get the orange cell fill.
            GridSelectionVisualState.SetIsCellSelected(
                cell,
                (isInCellSelection && item is not null && !fullySelectedRowItems.Contains(item)) || isCurrentCell);
            var isFillHandleCell = fillHandleCell is { } handleCell
                && ReferenceEquals(handleCell.Item, item)
                && ReferenceEquals(handleCell.Column, column);
            GridSelectionVisualState.SetIsFillHandleCell(cell, isFillHandleCell);
        }
    }

    private static DataGridCellInfo? GetFillHandleCell(DataGrid grid, ISet<object> fullySelectedRowItems)
    {
        var selectedCells = grid.SelectedCells
            .Where(cell => cell.IsValid
                && cell.Item is not null
                && cell.Column is not null
                && !fullySelectedRowItems.Contains(cell.Item))
            .Select(cell => new
            {
                cell.Item,
                cell.Column,
                RowIndex = grid.Items.IndexOf(cell.Item),
                ColumnIndex = cell.Column.DisplayIndex
            })
            .Where(cell => cell.RowIndex >= 0 && cell.ColumnIndex >= 0)
            .OrderByDescending(cell => cell.RowIndex)
            .ThenByDescending(cell => cell.ColumnIndex)
            .FirstOrDefault();

        return selectedCells is null
            ? null
            : new DataGridCellInfo(selectedCells.Item, selectedCells.Column);
    }

    private static IEnumerable<DataGridCell> EnumerateSelectionRefreshCells(DataGrid grid, ISet<object>? affectedItems)
    {
        if (affectedItems is null)
        {
            return FindChildren<DataGridCell>(grid);
        }

        var cells = new List<DataGridCell>();
        foreach (var item in affectedItems)
        {
            if (grid.ItemContainerGenerator.ContainerFromItem(item) is not DataGridRow row)
            {
                continue;
            }

            cells.AddRange(FindChildren<DataGridCell>(row));
        }

        return cells;
    }

    private void UpdateSpreadsheetRowSelectionVisuals(DataGrid grid, DataGridRow row)
    {
        var item = row.Item;
        if (item is null || item == CollectionView.NewItemPlaceholder)
        {
            return;
        }

        UpdateSpreadsheetSelectionVisuals(grid, new HashSet<object>(ReferenceEqualityComparer.Instance) { item });
    }

    private static bool IsLockedGridCell(object item, DataGridColumn column)
    {
        if (item is MonthlyForecast { IsLocked: true })
        {
            return true;
        }

        return item is ForecastLine
            && column.Header is ForecastMonthColumnDefinition { IsEditable: false, IsTotal: false };
    }

    private static bool IsCalculatedGridCell(object item, DataGridColumn column)
    {
        if (item is ForecastLine && column.Header is ForecastMonthColumnDefinition { IsTotal: true })
        {
            return true;
        }

        if (item is ManagementResourceTableRow && column.Header is ForecastMonthColumnDefinition)
        {
            return column.IsReadOnly;
        }

        return item is ForecastLine
            && column.Header?.ToString() is "CTD" or "Month Cost" or "Last Forecast" or "Month Var" or "CTC" or "FCC" or "Budget" or "Budget Var";
    }

    private bool IsCurrentGridRowItem(DataGrid grid, object? item, object? currentRowContext)
    {
        if (ReferenceEquals(item, currentRowContext))
        {
            return true;
        }

        return item is MonthlyForecast monthlyForecast
            && currentRowContext is ForecastLine selectedLine
            && DataContext is MainWindowViewModel viewModel
            && ReferenceEquals(viewModel.GetForecastLine(monthlyForecast), selectedLine);
    }

    private void RefreshCurrentRowVisuals()
    {
        foreach (var grid in _spreadsheetAttachedGrids)
        {
            UpdateSpreadsheetSelectionVisuals(grid);
        }
    }

    private void UpdateSpreadsheetSelectionStatus(DataGrid grid)
    {
        var selectedCells = grid.SelectedCells
            .Where(cell => cell.IsValid && cell.Item is not null && cell.Column is not null)
            .ToList();
        if (selectedCells.Count == 0
            && ReferenceEquals(grid, ForecastLinesGrid)
            && DataContext is MainWindowViewModel viewModel)
        {
            GridSelectionStatus.SetText(grid, $"Count: {viewModel.ForecastLineCount:N0} resources");
            return;
        }

        var numericCount = 0;
        var numericSum = 0m;
        foreach (var cell in selectedCells)
        {
            if (TryGetNumericCellValue(GetGridCellValue(cell.Item, cell.Column), out var value))
            {
                numericCount++;
                numericSum += value;
            }
        }

        var text = numericCount == 0
            ? $"Count: {selectedCells.Count}"
            : $"Average: {numericSum / numericCount:N2}    Count: {numericCount}    Sum: {numericSum:N2}";
        GridSelectionStatus.SetText(grid, text);
    }

    private static bool TryGetNumericCellValue(object? value, out decimal number)
    {
        switch (value)
        {
            case decimal decimalValue:
                number = decimalValue;
                return true;
            case int intValue:
                number = intValue;
                return true;
            case long longValue:
                number = longValue;
                return true;
            case double doubleValue:
                number = (decimal)doubleValue;
                return true;
            case float floatValue:
                number = (decimal)floatValue;
                return true;
            default:
                number = 0;
                return false;
        }
    }

    private void CopySelectedGridCells(DataGrid grid)
    {
        var cells = GetOrderedSelectedCells(grid);
        if (cells.Count == 0)
        {
            return;
        }

        var minRow = cells.Min(cell => cell.RowIndex);
        var maxRow = cells.Max(cell => cell.RowIndex);
        var minColumn = cells.Min(cell => cell.ColumnIndex);
        var maxColumn = cells.Max(cell => cell.ColumnIndex);
        var selected = cells.ToDictionary(cell => (cell.RowIndex, cell.ColumnIndex));
        var rows = new List<IReadOnlyList<string>>();
        for (var rowIndex = minRow; rowIndex <= maxRow; rowIndex++)
        {
            var row = new List<string>();
            for (var columnIndex = minColumn; columnIndex <= maxColumn; columnIndex++)
            {
                row.Add(selected.TryGetValue((rowIndex, columnIndex), out var cell)
                    ? FormatGridCellValue(GetGridCellValue(cell.Item, cell.Column))
                    : string.Empty);
            }

            rows.Add(row);
        }

        Clipboard.SetText(SpreadsheetClipboardService.Serialize(rows));
    }

    private void CutSelectedGridCells(DataGrid grid)
    {
        CopySelectedGridCells(grid);
        ClearSelectedGridCells(grid, "Cut");
    }

    private void ClearSelectedGridCells(DataGrid grid, string action)
    {
        var selectedCells = grid.SelectedCells
            .Where(cell => cell.IsValid && cell.Item is not null && cell.Column is not null)
            .ToList();
        if (selectedCells.Count == 0)
        {
            return;
        }

        var anchorCell = GetSelectionOriginClearAnchorCell(grid, selectedCells);
        var anchorItem = anchorCell?.Item;
        var anchorColumn = anchorCell?.Column;
        var cells = selectedCells
            .Where(cell => CanClearGridCell(grid, cell.Item!, cell.Column!))
            .ToList();
        if (cells.Count == 0)
        {
            if (DataContext is MainWindowViewModel currentViewModel)
            {
                currentViewModel.StatusText = $"{action} blocked because the selected cell is locked or read-only.";
            }

            return;
        }

        var viewModel = DataContext as MainWindowViewModel;
        var edits = cells
            .Select(cell => CreateSpreadsheetCellEdit(grid, cell.Item!, cell.Column!, string.Empty))
            .OfType<SpreadsheetCellEdit>()
            .ToList();
        if (edits.Count == 0)
        {
            if (viewModel is not null)
            {
                viewModel.StatusText = $"{action} made no changes.";
            }

            return;
        }

        var cleared = ApplySpreadsheetCellEdits(grid, edits, action, registerUndo: true, out var invalidCount);
        if (invalidCount > 0 && viewModel is not null)
        {
            viewModel.StatusText = $"{action} skipped {invalidCount:N0} invalid cell(s).";
        }

        if (cleared > 0
            && anchorItem is not null
            && anchorColumn is not null
            && grid.Items.Contains(anchorItem))
        {
            QueueRestoreSpreadsheetCellFocus(grid, anchorItem, anchorColumn);
        }
    }

    private static DataGridCellInfo? GetSelectionOriginCell(DataGrid grid, IEnumerable<DataGridCellInfo> selectedCells)
    {
        var origin = selectedCells
            .Where(cell => cell.Item is not null && cell.Column is not null)
            .Select(cell => new
            {
                cell.Item,
                cell.Column,
                RowIndex = grid.Items.IndexOf(cell.Item),
                ColumnIndex = cell.Column.DisplayIndex
            })
            .Where(cell => cell.RowIndex >= 0 && cell.ColumnIndex >= 0)
            .OrderBy(cell => cell.RowIndex)
            .ThenBy(cell => cell.ColumnIndex)
            .FirstOrDefault();

        return origin is null
            ? null
            : new DataGridCellInfo(origin.Item, origin.Column);
    }

    private static DataGridCellInfo? GetSelectionOriginWritableCell(DataGrid grid, IEnumerable<DataGridCellInfo> selectedCells)
    {
        var origin = selectedCells
            .Where(cell => cell.IsValid
                && cell.Item is not null
                && cell.Column is not null
                && CanWriteGridCell(grid, cell.Item, cell.Column))
            .Select(cell => new
            {
                cell.Item,
                cell.Column,
                RowIndex = grid.Items.IndexOf(cell.Item),
                ColumnIndex = cell.Column.DisplayIndex
            })
            .Where(cell => cell.RowIndex >= 0 && cell.ColumnIndex >= 0)
            .OrderBy(cell => cell.RowIndex)
            .ThenBy(cell => cell.ColumnIndex)
            .FirstOrDefault();

        return origin is null
            ? null
            : new DataGridCellInfo(origin.Item, origin.Column);
    }

    private static DataGridCellInfo? GetSelectionOriginClearAnchorCell(DataGrid grid, IReadOnlyCollection<DataGridCellInfo> selectedCells)
    {
        return GetSelectionOriginCell(grid, selectedCells.Where(cell =>
                   cell.IsValid
                   && cell.Item is not null
                   && cell.Column is not null
                   && IsForecastValueClearCell(cell.Item, cell.Column)))
               ?? GetSelectionOriginCell(grid, selectedCells.Where(cell =>
                   cell.IsValid
                   && cell.Item is not null
                   && cell.Column is not null
                   && CanClearGridCell(grid, cell.Item, cell.Column)));
    }

    private void PasteIntoGrid(DataGrid grid)
    {
        var viewModel = DataContext as MainWindowViewModel;
        string clipboardText;
        try
        {
            if (!Clipboard.ContainsText())
            {
                return;
            }

            clipboardText = Clipboard.GetText();
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.ExternalException or InvalidOperationException)
        {
            if (viewModel is not null)
            {
                viewModel.StatusText = "Paste failed because the clipboard was not available. Try again.";
            }

            return;
        }

        var values = SpreadsheetClipboardService.Parse(clipboardText);
        if (values.Count == 0)
        {
            return;
        }

        using var pasteMeasure = GridPerformanceDiagnostics.Measure($"grid-paste:{grid.Name}");
        if (!TryGetCurrentSpreadsheetCell(grid, out var startItem, out var startColumn))
        {
            if (viewModel is not null)
            {
                viewModel.StatusText = "Paste needs an active target cell.";
            }

            return;
        }

        var visibleItems = grid.Items
            .Cast<object>()
            .Where(item => item != CollectionView.NewItemPlaceholder)
            .ToList();
        var startRowIndex = startItem is null ? -1 : visibleItems.IndexOf(startItem);
        var visibleColumns = grid.Columns.Where(column => column.Visibility == Visibility.Visible).OrderBy(column => column.DisplayIndex).ToList();
        var itemCount = visibleItems.Count;
        var startColumnIndex = startColumn is null ? -1 : visibleColumns.IndexOf(startColumn);
        if (startRowIndex < 0 || startColumnIndex < 0)
        {
            return;
        }

        var pasteTargets = new List<(object Item, DataGridColumn Column, string Text)>();
        var protectedCount = 0;
        if (values.Count == 1 && values[0].Count == 1 && grid.SelectedCells.Count > 1)
        {
            foreach (var cell in GetOrderedSelectedCells(grid))
            {
                pasteTargets.Add((cell.Item, cell.Column, values[0][0]));
            }
        }
        else
        {
            for (var rowOffset = 0; rowOffset < values.Count; rowOffset++)
            {
                for (var columnOffset = 0; columnOffset < values[rowOffset].Count; columnOffset++)
                {
                    var rowIndex = startRowIndex + rowOffset;
                    var columnIndex = startColumnIndex + columnOffset;
                    if (rowIndex >= visibleItems.Count || columnIndex >= visibleColumns.Count)
                    {
                        protectedCount++;
                        continue;
                    }

                    pasteTargets.Add((visibleItems[rowIndex], visibleColumns[columnIndex], values[rowOffset][columnOffset]));
                }
            }
        }

        var edits = new List<SpreadsheetCellEdit>();
        var invalidCount = 0;
        foreach (var target in pasteTargets)
        {
            if (!CanWriteGridCell(grid, target.Item, target.Column))
            {
                protectedCount++;
                continue;
            }

            if (!CanConvertGridCellValue(target.Item, target.Column, target.Text))
            {
                invalidCount++;
                continue;
            }

            if (CreateSpreadsheetCellEdit(grid, target.Item, target.Column, target.Text) is { } edit)
            {
                edits.Add(edit);
            }
        }

        if (protectedCount > 0)
        {
            var result = MessageBox.Show(
                this,
                $"Paste includes {protectedCount:N0} locked, calculated, read-only or out-of-range cell(s). Paste into editable cells only?",
                "Paste protected cells",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
            {
                if (viewModel is not null)
                {
                    viewModel.StatusText = "Paste cancelled.";
                }

                return;
            }
        }

        if (edits.Count == 0)
        {
            if (viewModel is not null)
            {
                viewModel.StatusText = invalidCount > 0
                    ? $"Paste skipped {invalidCount:N0} invalid value(s)."
                    : "Paste made no changes.";
            }

            return;
        }

        var written = ApplySpreadsheetCellEdits(grid, edits, $"Pasted {edits.Count} cells", registerUndo: true, out var applyInvalidCount);
        invalidCount += applyInvalidCount;
        if (viewModel is not null)
        {
            viewModel.StatusText = protectedCount == 0 && invalidCount == 0
                ? $"Pasted {written:N0} cell(s)."
                : $"Pasted {written:N0} cell(s). Skipped {protectedCount:N0} protected/out-of-range and {invalidCount:N0} invalid cell(s).";
        }
    }

    private void FillSelectedCellsFromCurrent(DataGrid grid)
    {
        var sourceCell = grid.SelectedCells.Count > 1
            ? GetSelectionOriginWritableCell(grid, grid.SelectedCells)
            : null;
        var sourceItem = sourceCell?.Item;
        var sourceColumn = sourceCell?.Column;
        if (sourceCell is null && !TryGetCurrentSpreadsheetCell(grid, out sourceItem, out sourceColumn))
        {
            return;
        }

        if (sourceItem is null || sourceColumn is null)
        {
            return;
        }

        var sourceText = FormatGridCellValue(GetGridCellValue(sourceItem, sourceColumn));
        FillSelectedCellsFromValue(grid, sourceText, "Filled");
    }

    private void FillSelectedCellsFromValue(DataGrid grid, string sourceText, string action)
    {
        if (grid.SelectedCells.Count <= 1)
        {
            if (DataContext is MainWindowViewModel singleCellViewModel)
            {
                singleCellViewModel.StatusText = $"{action} needs a multi-cell selection.";
            }

            return;
        }

        var protectedCount = 0;
        var invalidCount = 0;
        var edits = new List<SpreadsheetCellEdit>();
        foreach (var cell in GetOrderedSelectedCells(grid))
        {
            if (!CanWriteGridCell(grid, cell.Item, cell.Column))
            {
                protectedCount++;
                continue;
            }

            if (!CanConvertGridCellValue(cell.Item, cell.Column, sourceText))
            {
                invalidCount++;
                continue;
            }

            if (CreateSpreadsheetCellEdit(grid, cell.Item, cell.Column, sourceText) is { } edit)
            {
                edits.Add(edit);
            }
        }

        if (edits.Count == 0)
        {
            if (DataContext is MainWindowViewModel noChangeViewModel)
            {
                noChangeViewModel.StatusText = protectedCount == 0 && invalidCount == 0
                    ? $"{action} made no changes."
                    : $"{action} skipped {protectedCount:N0} protected and {invalidCount:N0} invalid cell(s).";
            }

            return;
        }

        var applied = ApplySpreadsheetCellEdits(grid, edits, $"{action} {edits.Count} cells", registerUndo: true, out var applyInvalidCount);
        invalidCount += applyInvalidCount;
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.StatusText = protectedCount == 0 && invalidCount == 0
                ? $"{action} {applied:N0} cell(s)."
                : $"{action} {applied:N0} cell(s). Skipped {protectedCount:N0} protected and {invalidCount:N0} invalid cell(s).";
        }
    }

    private static void CommitSpreadsheetChanges(
        DataGrid grid,
        int changedCount,
        string status,
        MainWindowViewModel? viewModel,
        bool rebuildFilterLists)
    {
        if (changedCount > 0)
        {
            grid.CommitEdit(DataGridEditingUnit.Cell, true);
            grid.CommitEdit(DataGridEditingUnit.Row, true);
        }

        viewModel?.EndSpreadsheetEditBatch(status, changedCount > 0, rebuildFilterLists);
    }

    private int ApplySpreadsheetCellEdits(
        DataGrid grid,
        IReadOnlyList<SpreadsheetCellEdit> edits,
        string status,
        bool registerUndo,
        out int invalidCount)
    {
        invalidCount = 0;
        if (edits.Count == 0)
        {
            return 0;
        }

        var viewModel = DataContext as MainWindowViewModel;
        var changedForecastLines = new HashSet<ForecastLine>(ReferenceEqualityComparer.Instance);
        var applied = new List<SpreadsheetCellEdit>();
        var rebuildFilterLists = false;
        viewModel?.BeginSpreadsheetEditBatch();
        try
        {
            foreach (var edit in edits)
            {
                if (!CanWriteGridCell(grid, edit.Item, edit.Column))
                {
                    continue;
                }

                if (!CanConvertGridCellValue(edit.Item, edit.Column, edit.AfterText)
                    || !TrySetGridCellValue(grid, edit.Item, edit.Column, edit.AfterText, changedForecastLines))
                {
                    invalidCount++;
                    continue;
                }

                applied.Add(edit);
                rebuildFilterLists |= !IsMonthlyForecastValueCell(edit.Item, edit.Column);
            }
        }
        finally
        {
            if (viewModel is not null)
            {
                viewModel.RecalculateForecastLinesForSpreadsheetEdit(changedForecastLines);
            }
            else
            {
                foreach (var line in changedForecastLines)
                {
                    line.NotifyMonthForecastValuesChanged();
                }
            }

            CommitSpreadsheetChanges(grid, applied.Count, status, viewModel, rebuildFilterLists);
            QueueSpreadsheetSelectionUpdate(grid);
            if (IsManagementResourceGrid(grid))
            {
                QueueSynchronizeManagementResourceGrids();
            }
        }

        if (registerUndo && applied.Count > 0)
        {
            RegisterSpreadsheetUndoBatch(status, applied);
        }

        return applied.Count;
    }

    private void RegisterSpreadsheetUndoBatch(string action, IReadOnlyList<SpreadsheetCellEdit> edits)
    {
        if (edits.Count == 0)
        {
            return;
        }

        _spreadsheetUndoStack.Push(new SpreadsheetUndoBatch(action, edits.ToList()));
        _spreadsheetRedoStack.Clear();
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.StatusText = $"{action}. Undo available with Ctrl+Z.";
        }
    }

    private void UndoSpreadsheetEdit()
    {
        if (!_spreadsheetUndoStack.TryPop(out var batch))
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.StatusText = "Nothing to undo.";
            }

            return;
        }

        var undoEdits = batch.Edits
            .Select(edit => new SpreadsheetCellEdit(edit.Item, edit.Column, edit.AfterText, edit.BeforeText))
            .ToList();
        var grid = FindSpreadsheetGridForEdit(batch.Edits[0]);
        if (grid is null)
        {
            return;
        }

        ApplySpreadsheetCellEdits(grid, undoEdits, $"Undo {batch.Action}", registerUndo: false, out _);
        _spreadsheetRedoStack.Push(batch);
    }

    private void RedoSpreadsheetEdit()
    {
        if (!_spreadsheetRedoStack.TryPop(out var batch))
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.StatusText = "Nothing to redo.";
            }

            return;
        }

        var grid = FindSpreadsheetGridForEdit(batch.Edits[0]);
        if (grid is null)
        {
            return;
        }

        ApplySpreadsheetCellEdits(grid, batch.Edits, $"Redo {batch.Action}", registerUndo: false, out _);
        _spreadsheetUndoStack.Push(batch);
    }

    private DataGrid? FindSpreadsheetGridForEdit(SpreadsheetCellEdit edit)
    {
        return _spreadsheetAttachedGrids.FirstOrDefault(grid =>
            grid.Items.Contains(edit.Item) && grid.Columns.Contains(edit.Column));
    }

    private static bool IsMonthlyForecastValueCell(object item, DataGridColumn column)
    {
        return item is ForecastLine && column.Header is ForecastMonthColumnDefinition
            || item is MonthlyForecast
            && string.Equals(GetSpreadsheetColumnBindingPath(column), nameof(MonthlyForecast.Amount), StringComparison.Ordinal);
    }

    private List<SelectedGridCell> GetOrderedSelectedCells(DataGrid grid)
    {
        var selectedCells = grid.SelectedCells
            .Where(cell => cell.IsValid && cell.Item is not null && cell.Column is not null)
            .ToList();
        var selectedItems = selectedCells
            .Select(cell => cell.Item)
            .Distinct(ReferenceEqualityComparer.Instance)
            .ToList();
        var rowIndexes = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
        if (selectedItems.Count <= 32)
        {
            foreach (var item in selectedItems)
            {
                rowIndexes[item] = grid.Items.IndexOf(item);
            }
        }
        else
        {
            var remaining = new HashSet<object>(selectedItems, ReferenceEqualityComparer.Instance);
            for (var index = 0; index < grid.Items.Count && remaining.Count > 0; index++)
            {
                var item = grid.Items[index];
                if (item is not null && remaining.Remove(item))
                {
                    rowIndexes[item] = index;
                }
            }
        }

        return selectedCells
            .Select(cell => new SelectedGridCell(rowIndexes.GetValueOrDefault(cell.Item, -1), cell.Column.DisplayIndex, cell.Item, cell.Column))
            .Where(cell => cell.RowIndex >= 0 && cell.ColumnIndex >= 0)
            .OrderBy(cell => cell.RowIndex)
            .ThenBy(cell => cell.ColumnIndex)
            .ToList();
    }

    private static bool CanWriteGridCell(DataGrid grid, object item, DataGridColumn column)
    {
        if (grid.IsReadOnly || column.IsReadOnly || item == CollectionView.NewItemPlaceholder)
        {
            return false;
        }

        // Raw transaction rows are the imported source of truth and are always locked.
        if (item is CostTransaction)
        {
            return false;
        }

        if (item is MonthlyForecast { IsLocked: true })
        {
            return false;
        }

        if (column.Header is ForecastMonthColumnDefinition monthColumn)
        {
            return item is ForecastLine && monthColumn.IsEditable && !monthColumn.IsTotal;
        }

        return GetSpreadsheetColumnBindingPath(column) is not null;
    }

    private static bool CanClearGridCell(DataGrid grid, object item, DataGridColumn column)
    {
        if (!CanWriteGridCell(grid, item, column))
        {
            return false;
        }

        return item is not ForecastLine || IsForecastValueClearCell(item, column);
    }

    private static bool IsForecastValueClearCell(object item, DataGridColumn column)
    {
        return item is ForecastLine && column.Header is ForecastMonthColumnDefinition monthColumn && monthColumn.IsEditable && !monthColumn.IsTotal;
    }

    private static bool TrySetGridCellValue(
        DataGrid grid,
        object item,
        DataGridColumn column,
        string text,
        ISet<ForecastLine>? changedForecastLines = null)
    {
        if (!CanWriteGridCell(grid, item, column))
        {
            return false;
        }

        if (item is ForecastLine line && column.Header is ForecastMonthColumnDefinition monthColumn)
        {
            if (!SpreadsheetClipboardService.TryConvert(text, typeof(decimal), out var converted) || converted is not decimal amount)
            {
                return false;
            }

            line[monthColumn.Key] = amount;
            if (changedForecastLines is null)
            {
                line.NotifyMonthForecastValuesChanged();
            }
            else
            {
                changedForecastLines.Add(line);
            }

            return true;
        }

        var path = GetSpreadsheetColumnBindingPath(column);
        return path is not null && TrySetPropertyPathValue(item, path, text);
    }

    private static bool CanConvertGridCellValue(object item, DataGridColumn column, string text)
    {
        if (item is ForecastLine && column.Header is ForecastMonthColumnDefinition)
        {
            return SpreadsheetClipboardService.TryConvert(text, typeof(decimal), out _);
        }

        var path = GetSpreadsheetColumnBindingPath(column);
        if (path is null)
        {
            return false;
        }

        if (path.StartsWith('[') && path.EndsWith(']'))
        {
            var indexer = GetStringIndexer(item.GetType());
            return indexer?.CanWrite == true
                && SpreadsheetClipboardService.TryConvert(text, indexer.PropertyType, out _);
        }

        var properties = GetPropertyPath(item.GetType(), path);
        return properties is { Length: > 0 }
            && properties[^1].CanWrite
            && SpreadsheetClipboardService.TryConvert(text, properties[^1].PropertyType, out _);
    }

    private SpreadsheetCellEdit? CreateSpreadsheetCellEdit(DataGrid grid, object item, DataGridColumn column, string afterText)
    {
        if (!CanWriteGridCell(grid, item, column) || !CanConvertGridCellValue(item, column, afterText))
        {
            return null;
        }

        var beforeText = FormatGridCellValue(GetGridCellValue(item, column));
        return string.Equals(beforeText, afterText, StringComparison.Ordinal)
            ? null
            : new SpreadsheetCellEdit(item, column, beforeText, afterText);
    }

    private static object? GetGridCellValue(object item, DataGridColumn column)
    {
        if (item is ForecastLine line && column.Header is ForecastMonthColumnDefinition monthColumn)
        {
            return line[monthColumn.Key];
        }

        var path = GetSpreadsheetColumnBindingPath(column);
        return path is null ? null : GetPropertyPathValue(item, path);
    }

    private static string? GetSpreadsheetColumnBindingPath(DataGridColumn column)
    {
        if (column is DataGridBoundColumn { Binding: Binding binding } && !string.IsNullOrWhiteSpace(binding.Path?.Path))
        {
            return binding.Path.Path;
        }

        return string.IsNullOrWhiteSpace(column.SortMemberPath) ? null : column.SortMemberPath;
    }

    private static object? GetPropertyPathValue(object source, string path)
    {
        if (path.StartsWith('[') && path.EndsWith(']'))
        {
            var key = path[1..^1];
            var indexer = GetStringIndexer(source.GetType());
            return indexer?.GetValue(source, [key]);
        }

        object? current = source;
        var properties = GetPropertyPath(source.GetType(), path);
        if (properties is null)
        {
            return null;
        }

        foreach (var property in properties)
        {
            current = current is null ? null : property.GetValue(current);
        }

        return current;
    }

    private static bool TrySetPropertyPathValue(object source, string path, string text)
    {
        if (path.StartsWith('[') && path.EndsWith(']'))
        {
            var key = path[1..^1];
            var indexer = GetStringIndexer(source.GetType());
            if (indexer?.CanWrite != true || !SpreadsheetClipboardService.TryConvert(text, indexer.PropertyType, out var converted))
            {
                return false;
            }

            indexer.SetValue(source, converted, [key]);
            return true;
        }

        var properties = GetPropertyPath(source.GetType(), path);
        if (properties is null || properties.Length == 0)
        {
            return false;
        }

        object? target = source;
        for (var index = 0; index < properties.Length - 1; index++)
        {
            target = target is null ? null : properties[index].GetValue(target);
        }

        var property = properties[^1];
        if (property?.CanWrite != true || !SpreadsheetClipboardService.TryConvert(text, property.PropertyType, out var value))
        {
            return false;
        }

        if (target is null)
        {
            return false;
        }

        property.SetValue(target, value);
        return true;
    }

    private static PropertyInfo? GetStringIndexer(Type sourceType)
    {
        if (!SpreadsheetStringIndexerCache.TryGetValue(sourceType, out var indexer))
        {
            indexer = sourceType.GetProperty("Item", [typeof(string)]);
            SpreadsheetStringIndexerCache[sourceType] = indexer;
        }

        return indexer;
    }

    private static PropertyInfo[]? GetPropertyPath(Type sourceType, string path)
    {
        var cacheKey = (sourceType, path);
        if (SpreadsheetPropertyPathCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var properties = new List<PropertyInfo>();
        var currentType = sourceType;
        foreach (var segment in path.Split('.'))
        {
            var property = currentType.GetProperty(segment, BindingFlags.Instance | BindingFlags.Public);
            if (property is null)
            {
                SpreadsheetPropertyPathCache[cacheKey] = null;
                return null;
            }

            properties.Add(property);
            currentType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        }

        var result = properties.ToArray();
        SpreadsheetPropertyPathCache[cacheKey] = result;
        return result;
    }

    private static string FormatGridCellValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DateOnly date => date.ToString("d", CultureInfo.CurrentCulture),
            DateTime date => date.ToString("g", CultureInfo.CurrentCulture),
            decimal number => number.ToString("0.##", CultureInfo.CurrentCulture),
            double number => number.ToString("0.##", CultureInfo.CurrentCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private sealed record SpreadsheetCellEdit(object Item, DataGridColumn Column, string BeforeText, string AfterText);

    private sealed record SpreadsheetUndoBatch(string Action, List<SpreadsheetCellEdit> Edits);

    private sealed record SpreadsheetEditSnapshot(DataGrid Grid, object Item, DataGridColumn Column, string BeforeText);

    private sealed record SpreadsheetFillDrag(DataGrid Grid, object SourceItem, DataGridColumn SourceColumn, string SourceText);

    private sealed record SelectedGridCell(int RowIndex, int ColumnIndex, object Item, DataGridColumn Column);
}
