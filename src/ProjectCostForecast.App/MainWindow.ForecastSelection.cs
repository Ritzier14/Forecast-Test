using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.ViewModels;

namespace ProjectCostForecast.App;

public partial class MainWindow
{
    private bool IsForecastRowSelectorCell(DataGrid grid, DataGridCell cell)
    {
        return ReferenceEquals(grid, ForecastLinesGrid)
            && IsForecastRowSelectorColumn(cell.Column);
    }

    private static bool IsForecastRowSelectorColumn(DataGridColumn? column)
    {
        return column is not null
            && string.Equals(
                GridColumnRoleState.GetRole(column),
                GridColumnRoleState.ForecastRowSelector,
                StringComparison.Ordinal);
    }

    private void SelectForecastRowsFromSelectorClick(DataGrid grid, object? item)
    {
        if (item is null || item == CollectionView.NewItemPlaceholder)
        {
            return;
        }

        var modifiers = Keyboard.Modifiers;
        var anchorLine = _forecastRowSelectionAnchor as ForecastLine
            ?? grid.CurrentCell.Item as ForecastLine
            ?? grid.SelectedItem as ForecastLine
            ?? grid.SelectedCells.Select(cell => cell.Item).OfType<ForecastLine>().FirstOrDefault();
        if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift
            && anchorLine is not null
            && item is ForecastLine endLine)
        {
            SelectGridRowRange(
                grid,
                anchorLine,
                endLine,
                preserveExistingSelection: (modifiers & ModifierKeys.Control) == ModifierKeys.Control);
            return;
        }

        _forecastRowSelectionAnchor = item;
        if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            ToggleGridRowSelection(grid, item);
            return;
        }

        SelectGridRows(grid, [item], item);
    }

    private void SelectGridRowRange(
        DataGrid grid,
        object startItem,
        object endItem,
        bool preserveExistingSelection = false)
    {
        var visibleItems = ReferenceEquals(grid, ForecastLinesGrid)
            ? GetForecastRowItemsInViewOrder(grid)
            : grid.Items
                .Cast<object>()
                .Where(item => item != CollectionView.NewItemPlaceholder)
                .ToList();
        var startIndex = visibleItems.IndexOf(startItem);
        var endIndex = visibleItems.IndexOf(endItem);
        if (startIndex < 0 || endIndex < 0)
        {
            return;
        }

        var rangeItems = visibleItems
            .Skip(Math.Min(startIndex, endIndex))
            .Take(Math.Abs(endIndex - startIndex) + 1)
            .ToList();
        var existingRows = preserveExistingSelection
            ? GetFullySelectedGridRowItems(grid)
            : null;
        var selectedRows = existingRows is not null
            ? visibleItems
                .Where(item => existingRows.Contains(item) || rangeItems.Contains(item))
                .ToList()
            : rangeItems;
        SelectGridRows(grid, selectedRows, endItem);
    }

    private List<object> GetForecastRowItemsInViewOrder(DataGrid grid)
    {
        var gridItems = grid.Items
            .Cast<object>()
            .OfType<ForecastLine>()
            .Cast<object>()
            .ToList();
        if (gridItems.Count > 0)
        {
            return gridItems;
        }

        if (DataContext is MainWindowViewModel viewModel)
        {
            var viewItems = viewModel.ForecastLinesView
                .Cast<object>()
                .OfType<ForecastLine>()
                .Cast<object>()
                .ToList();
            if (viewItems.Count > 0)
            {
                return viewItems;
            }
        }

        return grid.Items
            .Cast<object>()
            .Where(item => item is ForecastLine)
            .ToList();
    }

    private void ToggleGridRowSelection(DataGrid grid, object item)
    {
        var visibleColumns = GetVisibleSpreadsheetColumns(grid);
        if (visibleColumns.Count == 0)
        {
            return;
        }

        var existingCells = grid.SelectedCells
            .Where(cell => ReferenceEquals(cell.Item, item))
            .ToList();
        var selectedColumns = existingCells
            .Select(cell => cell.Column)
            .ToHashSet();
        var isFullySelected = visibleColumns.All(selectedColumns.Contains);
        if (isFullySelected)
        {
            foreach (var cell in existingCells)
            {
                grid.SelectedCells.Remove(cell);
            }

            if (grid.CurrentCell.IsValid && ReferenceEquals(grid.CurrentCell.Item, item))
            {
                var nextCell = grid.SelectedCells.FirstOrDefault(cell => cell.IsValid);
                grid.CurrentCell = nextCell.IsValid
                    ? nextCell
                    : new DataGridCellInfo(item, visibleColumns[0]);
            }

            QueueSpreadsheetSelectionUpdate(grid, [item], refreshAllVisuals: false);
            return;
        }

        foreach (var column in visibleColumns)
        {
            var info = new DataGridCellInfo(item, column);
            if (!grid.SelectedCells.Contains(info))
            {
                grid.SelectedCells.Add(info);
            }
        }

        grid.CurrentCell = new DataGridCellInfo(item, visibleColumns[0]);
        SelectGridRowContext(grid, item);
        QueueSpreadsheetSelectionUpdate(grid, [item], refreshAllVisuals: false);
    }

    private void SelectGridRows(DataGrid grid, IReadOnlyList<object> items, object currentItem)
    {
        var visibleColumns = GetVisibleSpreadsheetColumns(grid);
        if (visibleColumns.Count == 0 || items.Count == 0)
        {
            return;
        }

        grid.CommitEdit(DataGridEditingUnit.Cell, true);
        grid.CommitEdit(DataGridEditingUnit.Row, true);
        grid.SelectedCells.Clear();
        foreach (var item in items)
        {
            foreach (var column in visibleColumns)
            {
                grid.SelectedCells.Add(new DataGridCellInfo(item, column));
            }
        }

        grid.CurrentCell = new DataGridCellInfo(currentItem, visibleColumns[0]);
        SelectGridRowContext(grid, currentItem);
        QueueSpreadsheetSelectionUpdate(grid, items, refreshAllVisuals: true);
    }

    private static List<DataGridColumn> GetVisibleSpreadsheetColumns(DataGrid grid)
    {
        return grid.Columns
            .Where(column => column.Visibility == Visibility.Visible)
            .OrderBy(column => column.DisplayIndex)
            .ToList();
    }

    private static HashSet<object> GetFullySelectedGridRowItems(DataGrid grid)
    {
        var visibleColumns = GetVisibleSpreadsheetColumns(grid);
        var visibleColumnSet = visibleColumns.ToHashSet();
        var selectedColumnsByItem = new Dictionary<object, HashSet<DataGridColumn>>(ReferenceEqualityComparer.Instance);
        foreach (var cell in grid.SelectedCells)
        {
            if (cell.Item is null
                || cell.Item == CollectionView.NewItemPlaceholder
                || cell.Column is null
                || !visibleColumnSet.Contains(cell.Column))
            {
                continue;
            }

            if (!selectedColumnsByItem.TryGetValue(cell.Item, out var selectedColumns))
            {
                selectedColumns = [];
                selectedColumnsByItem[cell.Item] = selectedColumns;
            }

            selectedColumns.Add(cell.Column);
        }

        return selectedColumnsByItem
            .Where(entry => visibleColumns.All(entry.Value.Contains))
            .Select(entry => entry.Key)
            .ToHashSet(ReferenceEqualityComparer.Instance);
    }

    private IReadOnlyList<ForecastLine> GetFullySelectedForecastLines(DataGrid grid)
    {
        var fullySelectedItems = GetFullySelectedGridRowItems(grid);
        return GetForecastRowItemsInViewOrder(grid)
            .OfType<ForecastLine>()
            .Where(fullySelectedItems.Contains)
            .ToList();
    }

    private List<ForecastLine> GetSelectedForecastLines(DataGrid grid, ForecastLine fallbackLine)
    {
        var seen = new HashSet<ForecastLine>(ReferenceEqualityComparer.Instance);
        var lines = new List<ForecastLine>();
        foreach (var line in grid.SelectedCells.Select(cell => cell.Item).OfType<ForecastLine>())
        {
            if (seen.Add(line))
            {
                lines.Add(line);
            }
        }

        if (lines.Count == 0)
        {
            lines.Add(fallbackLine);
        }

        return lines;
    }
}
