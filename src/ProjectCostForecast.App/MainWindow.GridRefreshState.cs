using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace ProjectCostForecast.App;

public partial class MainWindow
{
    private ForecastGridRefreshState CaptureForecastGridRefreshState(DataGrid grid)
    {
        var selectedItems = grid.SelectedItems.Cast<object>().ToArray();
        var currentCell = grid.CurrentCell;
        var currentItem = currentCell.IsValid ? currentCell.Item : null;
        var currentColumnKey = currentCell is { IsValid: true, Column: not null }
            ? GetColumnPersistenceKey(currentCell.Column)
            : null;
        var editingView = grid.Items as IEditableCollectionView;
        var editingItem = editingView?.IsEditingItem == true ? editingView.CurrentEditItem : null;
        var isEditing = editingItem is not null || (grid.IsKeyboardFocusWithin && currentItem is not null);
        var item = editingItem ?? currentItem;
        var editorText = CaptureActiveEditorText(grid, item, currentColumnKey);
        var scrollViewer = _forecastGridScrollViewer ?? FindChild<ScrollViewer>(grid);

        return new ForecastGridRefreshState(
            selectedItems,
            item,
            currentColumnKey,
            isEditing,
            editorText,
            scrollViewer?.HorizontalOffset ?? 0,
            scrollViewer?.VerticalOffset ?? 0);
    }

    private static string? CaptureActiveEditorText(DataGrid grid, object? item, string? columnKey)
    {
        if (item is null || string.IsNullOrWhiteSpace(columnKey))
        {
            return null;
        }

        var focusedCell = FindParent<DataGridCell>(Keyboard.FocusedElement as DependencyObject);
        if (focusedCell?.DataContext is not null
            && ReferenceEquals(FindParent<DataGrid>(focusedCell), grid)
            && ReferenceEquals(focusedCell.DataContext, item)
            && string.Equals(GetColumnPersistenceKey(focusedCell.Column), columnKey, StringComparison.OrdinalIgnoreCase))
        {
            return FindChild<TextBox>(focusedCell)?.Text;
        }

        return null;
    }

    private void RestoreForecastGridRefreshState(DataGrid grid, ForecastGridRefreshState state)
    {
        var availableItems = grid.Items.Cast<object>().ToHashSet();
        grid.SelectedItems.Clear();
        foreach (var item in state.SelectedItems.Where(availableItems.Contains))
        {
            grid.SelectedItems.Add(item);
        }

        var currentItem = state.CurrentItem is not null && availableItems.Contains(state.CurrentItem)
            ? state.CurrentItem
            : state.SelectedItems.FirstOrDefault(availableItems.Contains);
        var currentColumn = string.IsNullOrWhiteSpace(state.CurrentColumnKey)
            ? null
            : grid.Columns.FirstOrDefault(column =>
                string.Equals(GetColumnPersistenceKey(column), state.CurrentColumnKey, StringComparison.OrdinalIgnoreCase));
        if (currentItem is not null && currentColumn is not null)
        {
            grid.SelectedItem = currentItem;
            grid.CurrentCell = new DataGridCellInfo(currentItem, currentColumn);
        }

        _pendingForecastGridRefreshState = state with { CurrentItem = currentItem };
        QueueRestoreForecastGridRefreshState(grid);
        QueueRestoreForecastGroupExpansion();
    }

    private void QueueRestoreForecastGridRefreshState(DataGrid grid)
    {
        QueueMainWindowWork(DispatcherPriority.Render, () =>
        {
            if (_pendingForecastGridRefreshState is not { } state)
            {
                return;
            }

            _pendingForecastGridRefreshState = null;
            grid.UpdateLayout();
            var scrollViewer = _forecastGridScrollViewer ?? FindChild<ScrollViewer>(grid);
            if (scrollViewer is not null)
            {
                scrollViewer.ScrollToHorizontalOffset(Math.Clamp(state.HorizontalOffset, 0, scrollViewer.ScrollableWidth));
                scrollViewer.ScrollToVerticalOffset(Math.Clamp(state.VerticalOffset, 0, scrollViewer.ScrollableHeight));
            }

            if (!state.IsEditing || state.CurrentItem is null || string.IsNullOrWhiteSpace(state.CurrentColumnKey))
            {
                return;
            }

            var column = grid.Columns.FirstOrDefault(candidate =>
                string.Equals(GetColumnPersistenceKey(candidate), state.CurrentColumnKey, StringComparison.OrdinalIgnoreCase));
            if (column is null || !grid.Items.Cast<object>().Contains(state.CurrentItem))
            {
                return;
            }

            grid.SelectedItem = state.CurrentItem;
            grid.CurrentCell = new DataGridCellInfo(state.CurrentItem, column);
            grid.ScrollIntoView(state.CurrentItem, column);
            if (!grid.BeginEdit())
            {
                return;
            }

            QueueMainWindowWork(DispatcherPriority.Input, () =>
            {
                var row = grid.ItemContainerGenerator.ContainerFromItem(state.CurrentItem) as DataGridRow;
                var presenter = row is null ? null : FindChild<DataGridCellsPresenter>(row);
                var cell = presenter?.ItemContainerGenerator.ContainerFromIndex(column.DisplayIndex) as DataGridCell;
                var editor = cell is null ? null : FindChild<TextBox>(cell);
                if (editor is null)
                {
                    return;
                }

                if (state.EditorText is not null)
                {
                    editor.Text = state.EditorText;
                    editor.CaretIndex = editor.Text.Length;
                }

                editor.Focus();
            });
        });
    }

    private void QueueRestoreForecastGroupExpansion()
    {
        if (_forecastGroupExpansionRestoreQueued)
        {
            return;
        }

        _forecastGroupExpansionRestoreQueued = true;
        if (!QueueMainWindowWork(DispatcherPriority.Loaded, () =>
        {
            _forecastGroupExpansionRestoreQueued = false;
            SetForecastGroupExpansion(ForecastLinesGrid, _forecastGroupsExpanded);
        }))
        {
            _forecastGroupExpansionRestoreQueued = false;
        }
    }

    private sealed record ForecastGridRefreshState(
        IReadOnlyList<object> SelectedItems,
        object? CurrentItem,
        string? CurrentColumnKey,
        bool IsEditing,
        string? EditorText,
        double HorizontalOffset,
        double VerticalOffset);
}
