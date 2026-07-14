using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using ProjectCostForecast.App.Models;

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
        if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift
            && _forecastRowSelectionAnchor is not null
            && grid.Items.Contains(_forecastRowSelectionAnchor))
        {
            SelectGridRowRange(grid, _forecastRowSelectionAnchor, item);
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

    private void SelectGridRowRange(DataGrid grid, object startItem, object endItem)
    {
        var visibleItems = grid.Items
            .Cast<object>()
            .Where(item => item != CollectionView.NewItemPlaceholder)
            .ToList();
        var startIndex = visibleItems.IndexOf(startItem);
        var endIndex = visibleItems.IndexOf(endItem);
        if (startIndex < 0 || endIndex < 0)
        {
            return;
        }

        var selectedRows = visibleItems
            .Skip(Math.Min(startIndex, endIndex))
            .Take(Math.Abs(endIndex - startIndex) + 1)
            .ToList();
        SelectGridRows(grid, selectedRows, endItem);
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
        var isFullySelected = existingCells.Count >= visibleColumns.Count;
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
