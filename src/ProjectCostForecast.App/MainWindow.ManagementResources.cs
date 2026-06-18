using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ProjectCostForecast.App;

public partial class MainWindow
{
    private readonly HashSet<ScrollViewer> _managementResourceScrollViewers = [];
    private bool _managementResourceGridSyncQueued;
    private bool _managementResourceScrollSyncActive;

    private void ManagementResourceAllocationGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Alpha 1.5: single click selects/activates only. Editing starts through
        // typing overwrite, F2 or double-click in the shared spreadsheet handler.
    }

    private void ManagementResourceAllocationGrid_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // Shared spreadsheet typing overwrite handles this grid.
    }

    private void BeginManagementResourceCellEdit(DataGridCell cell, bool selectAll)
    {
        if (FindParent<DataGrid>(cell) is not { } grid)
        {
            return;
        }

        grid.CurrentCell = new DataGridCellInfo(cell.DataContext, cell.Column);
        cell.Focus();
        Dispatcher.BeginInvoke(() =>
        {
            if (!grid.BeginEdit())
            {
                return;
            }

            var editor = FindChild<TextBox>(cell);
            editor?.Focus();
            if (selectAll)
            {
                editor?.SelectAll();
            }
        }, DispatcherPriority.Input);
    }

    private void ManagementResourceGrid_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        QueueSynchronizeManagementResourceGrids();
    }

    private void QueueSynchronizeManagementResourceGrids()
    {
        if (_managementResourceGridSyncQueued)
        {
            return;
        }

        _managementResourceGridSyncQueued = true;
        Dispatcher.BeginInvoke(() =>
        {
            _managementResourceGridSyncQueued = false;
            var grids = GetManagementResourceGrids();
            AttachManagementResourceWidthSync(grids);
            var columnCount = grids.Min(grid => grid.Columns.Count);
            _managementResourceWidthSyncActive = true;
            try
            {
                for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
                {
                    if (!_managementResourceColumnWidths.TryGetValue(columnIndex, out var width))
                    {
                        width = grids
                            .Select(grid => GetStableColumnWidth(grid.Columns[columnIndex]))
                            .Where(value => value > 0)
                            .DefaultIfEmpty(0)
                            .Max();
                        if (width > 0)
                        {
                            _managementResourceColumnWidths[columnIndex] = width;
                        }
                    }

                    if (width <= 0)
                    {
                        continue;
                    }

                    foreach (var grid in grids)
                    {
                        grid.Columns[columnIndex].Width = new DataGridLength(width);
                    }
                }
            }
            finally
            {
                _managementResourceWidthSyncActive = false;
            }

            AttachManagementResourceScrollSync(grids);
        }, DispatcherPriority.Render);
    }

    private void AttachManagementResourceWidthSync(IReadOnlyList<DataGrid> grids)
    {
        var currentColumns = grids.SelectMany(grid => grid.Columns).ToHashSet();
        foreach (var column in _trackedManagementResourceColumns.Where(column => !currentColumns.Contains(column)).ToList())
        {
            ForecastColumnActualWidthDescriptor?.RemoveValueChanged(column, ManagementResourceColumnWidthChanged);
            _trackedManagementResourceColumns.Remove(column);
        }

        foreach (var column in currentColumns.Where(column => !_trackedManagementResourceColumns.Contains(column)))
        {
            ForecastColumnActualWidthDescriptor?.AddValueChanged(column, ManagementResourceColumnWidthChanged);
            _trackedManagementResourceColumns.Add(column);
        }
    }

    private void ManagementResourceColumnWidthChanged(object? sender, EventArgs e)
    {
        if (_managementResourceWidthSyncActive || sender is not DataGridColumn column)
        {
            return;
        }

        var index = GetManagementResourceColumnIndex(column);
        if (index < 0)
        {
            return;
        }

        var width = GetStableColumnWidth(column);
        if (width <= 0)
        {
            return;
        }

        _managementResourceColumnWidths[index] = width;
        QueueSynchronizeManagementResourceGrids();
    }

    private int GetManagementResourceColumnIndex(DataGridColumn column)
    {
        foreach (var grid in GetManagementResourceGrids())
        {
            var index = grid.Columns.IndexOf(column);
            if (index >= 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static double GetStableColumnWidth(DataGridColumn column)
    {
        if (column.ActualWidth > 0)
        {
            return column.ActualWidth;
        }

        if (!double.IsNaN(column.Width.DisplayValue) && column.Width.DisplayValue > 0)
        {
            return column.Width.DisplayValue;
        }

        return 0;
    }

    private void ApplyManagementResourceColumnWidthModel(DataGrid grid)
    {
        _managementResourceWidthSyncActive = true;
        try
        {
            for (var columnIndex = 0; columnIndex < grid.Columns.Count; columnIndex++)
            {
                if (_managementResourceColumnWidths.TryGetValue(columnIndex, out var width) && width > 0)
                {
                    grid.Columns[columnIndex].Width = new DataGridLength(width);
                }
            }
        }
        finally
        {
            _managementResourceWidthSyncActive = false;
        }
    }

    private void AttachManagementResourceScrollSync(IReadOnlyList<DataGrid> grids)
    {
        foreach (var grid in grids)
        {
            var scrollViewer = FindChild<ScrollViewer>(grid);
            if (scrollViewer is null || !_managementResourceScrollViewers.Add(scrollViewer))
            {
                continue;
            }

            scrollViewer.ScrollChanged += ManagementResourceScrollViewer_ScrollChanged;
        }
    }

    private void ManagementResourceScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_managementResourceScrollSyncActive || e.HorizontalChange == 0 || sender is not ScrollViewer source)
        {
            return;
        }

        _managementResourceScrollSyncActive = true;
        try
        {
            foreach (var scrollViewer in _managementResourceScrollViewers)
            {
                if (!ReferenceEquals(scrollViewer, source))
                {
                    scrollViewer.ScrollToHorizontalOffset(source.HorizontalOffset);
                }
            }
        }
        finally
        {
            _managementResourceScrollSyncActive = false;
        }
    }

    private IReadOnlyList<DataGrid> GetManagementResourceGrids() =>
    [
        ManagementResourceAllocationGrid,
        ManagementResourceHoursGrid,
        ManagementResourceCostGrid
    ];
}
