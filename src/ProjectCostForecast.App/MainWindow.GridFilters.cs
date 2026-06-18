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
    private void AttachColumnMenus(DependencyObject root)
    {
        if (root is DataGrid grid)
        {
            foreach (var column in grid.Columns)
            {
                EnsureColumnPresentation(column);
                TrackWorkspaceColumnStateWidth(column);
            }

            grid.AddHandler(DataGridColumnHeader.PreviewMouseRightButtonDownEvent, new MouseButtonEventHandler(ShowColumnMenu), true);
            if (_workspaceColumnStateTrackedGrids.Add(grid))
            {
                grid.ColumnReordered += Grid_ColumnLayoutChanged;
                grid.PreviewMouseLeftButtonUp += Grid_ColumnLayoutMouseLeftButtonUp;
            }
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            AttachColumnMenus(VisualTreeHelper.GetChild(root, i));
        }
    }

    private void AttachGridPanHandlers(DependencyObject root)
    {
        if (root is DataGrid grid)
        {
            grid.PreviewMouseDown -= Grid_PreviewMouseDown;
            grid.PreviewMouseMove -= Grid_PreviewMouseMove;
            grid.PreviewMouseUp -= Grid_PreviewMouseUp;
            grid.PreviewMouseDown += Grid_PreviewMouseDown;
            grid.PreviewMouseMove += Grid_PreviewMouseMove;
            grid.PreviewMouseUp += Grid_PreviewMouseUp;

            if (_rowHoverAttachedGrids.Add(grid))
            {
                grid.LoadingRow += Grid_LoadingRow;
                grid.MouseMove += Grid_MouseMove;
                grid.MouseLeave += Grid_MouseLeave;
            }
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            AttachGridPanHandlers(VisualTreeHelper.GetChild(root, i));
        }
    }

    private void Grid_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        var isHovered = sender is DataGrid grid
            && _hoveredRowsByGrid.TryGetValue(grid, out var hoveredRow)
            && ReferenceEquals(hoveredRow, e.Row);
        GridHoverState.SetIsRowHovered(e.Row, isHovered);
    }

    private void Grid_ColumnLayoutChanged(object? sender, DataGridColumnEventArgs e)
    {
        if (!_applyingWorkspaceColumnState && sender is DataGrid grid)
        {
            QueueCaptureGridColumnState(grid);
        }
    }

    private void Grid_ColumnLayoutMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_applyingWorkspaceColumnState
            && sender is DataGrid grid
            && e.OriginalSource is DependencyObject source
            && FindParent<DataGridColumnHeader>(source) is not null)
        {
            QueueCaptureGridColumnState(grid);
        }
    }

    private void TrackWorkspaceColumnStateWidth(DataGridColumn column)
    {
        if (!_workspaceColumnStateTrackedColumns.Add(column))
        {
            return;
        }

        ForecastColumnActualWidthDescriptor?.AddValueChanged(column, WorkspaceColumnActualWidthChanged);
    }

    private void WorkspaceColumnActualWidthChanged(object? sender, EventArgs e)
    {
        if (_applyingWorkspaceColumnState || sender is not DataGridColumn column)
        {
            return;
        }

        var grid = GetColumnStateGrid(column);
        if (grid is not null)
        {
            QueueCaptureGridColumnState(grid);
        }
    }

    private DataGrid? GetColumnStateGrid(DataGridColumn column)
    {
        return GetWorkspaceStateGrids().FirstOrDefault(grid => grid.Columns.Contains(column));
    }

    private void QueueCaptureGridColumnState(DataGrid grid)
    {
        if (_applyingWorkspaceColumnState || !_workspaceColumnStateCaptureQueuedGrids.Add(grid))
        {
            return;
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            _workspaceColumnStateCaptureQueuedGrids.Remove(grid);
            if (!_applyingWorkspaceColumnState)
            {
                CaptureGridColumnState(grid);
            }
        }));
    }

    private void Grid_MouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not DataGrid grid)
        {
            return;
        }

        var row = e.OriginalSource is DependencyObject source
            ? FindParent<DataGridRow>(source)
            : null;
        UpdateHoveredRow(grid, row);
        if (ReferenceEquals(grid, ForecastLinesGrid) && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
        {
            UpdateHoveredForecastLine(e.OriginalSource);
        }
    }

    private void Grid_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is DataGrid grid)
        {
            UpdateHoveredRow(grid, null);
        }
    }

    private void UpdateHoveredRow(DataGrid grid, DataGridRow? row)
    {
        _hoveredRowsByGrid.TryGetValue(grid, out var previousRow);
        if (ReferenceEquals(previousRow, row))
        {
            return;
        }

        if (previousRow is not null)
        {
            SetRowHoverState(previousRow, false);
        }

        if (row is null)
        {
            _hoveredRowsByGrid.Remove(grid);
            return;
        }

        _hoveredRowsByGrid[grid] = row;
        SetRowHoverState(row, true);
    }

    private static void SetRowHoverState(DataGridRow row, bool isHovered)
    {
        GridHoverState.SetIsRowHovered(row, isHovered);
    }

    private static void ApplyVisibleCellHighlightState(DataGrid grid)
    {
        grid.InvalidateVisual();
    }

    private static void ApplyVisibleCellHighlightState(DataGrid grid, ISet<string> columnKeys)
    {
        grid.InvalidateVisual();
    }

    private void AttachForecastGridScrollSync()
    {
        var scrollViewer = FindChild<ScrollViewer>(ForecastLinesGrid);
        if (scrollViewer is null || ReferenceEquals(scrollViewer, _forecastGridScrollViewer))
        {
            return;
        }

        if (_forecastGridScrollViewer is not null)
        {
            _forecastGridScrollViewer.ScrollChanged -= ForecastGridScrollViewer_ScrollChanged;
        }

        _forecastGridScrollViewer = scrollViewer;
        _forecastGridScrollViewer.ScrollChanged += ForecastGridScrollViewer_ScrollChanged;
        RebuildForecastYearBands();
    }

    private void ForecastGridScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.HorizontalChange != 0 || e.ExtentWidthChange != 0 || e.ViewportWidthChange != 0)
        {
            QueueRebuildForecastYearBands();
        }
    }

    private void OpenColumnFilterMenu(DataGridColumnHeader header, DataGrid grid)
    {
        var accessor = GetColumnValueAccessor(header.Column);
        if (accessor is null)
        {
            return;
        }

        var columnKey = GetColumnPersistenceKey(header.Column);
        if (string.IsNullOrWhiteSpace(columnKey))
        {
            return;
        }

        var values = GetColumnFilterValues(grid, header.Column, columnKey, accessor);
        if (values.Count == 0)
        {
            return;
        }

        var menu = BuildColumnFilterMenu(grid, header.Column, columnKey, values);
        header.ContextMenu = menu;
        menu.PlacementTarget = header;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private ContextMenu BuildColumnFilterMenu(DataGrid grid, DataGridColumn column, string columnKey, IReadOnlyList<ColumnFilterValue> values)
    {
        var state = GetGridColumnFilterState(grid);
        var activeValues = state.SelectedValuesByColumn.TryGetValue(columnKey, out var selectedValues)
            ? selectedValues
            : null;
        var workingSelection = activeValues is null
            ? values.Select(value => value.Key).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(activeValues, StringComparer.Ordinal);

        var menu = new ContextMenu
        {
            MinWidth = 310,
            MaxWidth = 380
        };

        var label = GetForecastColumnMenuLabel(column);
        var clearButton = new Button
        {
            Content = $"Clear Filter From \"{label}\"",
            IsEnabled = activeValues is not null,
            MinHeight = 30,
            Margin = new Thickness(8, 8, 8, 6),
            Padding = new Thickness(10, 4, 10, 4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = activeValues is not null ? BrushFactory.Frozen(0xF8, 0xFA, 0xFC) : BrushFactory.Frozen(0xF1, 0xF5, 0xF9),
            BorderBrush = BrushFactory.Frozen(0x94, 0xA3, 0xB8),
            BorderThickness = new Thickness(1),
            Foreground = activeValues is not null ? BrushFactory.Frozen(0x0F, 0x17, 0x2A) : BrushFactory.Frozen(0x94, 0xA3, 0xB8)
        };
        clearButton.Click += (_, _) =>
        {
            ClearColumnFilter(grid, columnKey);
            menu.IsOpen = false;
        };
        menu.Items.Add(new MenuItem
        {
            Header = clearButton,
            Padding = new Thickness(0),
            StaysOpenOnClick = true
        });
        menu.Items.Add(new Separator());

        var searchBox = new TextBox
        {
            Margin = new Thickness(8, 6, 8, 4),
            MinWidth = 270,
            Height = 28,
            Padding = new Thickness(6, 3, 6, 3),
            BorderBrush = BrushFactory.Frozen(0x94, 0xA3, 0xB8),
            Background = Brushes.White
        };
        menu.Items.Add(new MenuItem
        {
            Header = searchBox,
            Padding = new Thickness(0),
            StaysOpenOnClick = true
        });

        var optionsPanel = new StackPanel();
        var scrollViewer = new ScrollViewer
        {
            Content = optionsPanel,
            MaxHeight = 260,
            Margin = new Thickness(8, 0, 8, 8),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            BorderBrush = BrushFactory.Frozen(0xD9, 0xD9, 0xD9),
            BorderThickness = new Thickness(1)
        };
        menu.Items.Add(new MenuItem
        {
            Header = scrollViewer,
            Padding = new Thickness(0),
            StaysOpenOnClick = true
        });

        void RefreshOptions()
        {
            optionsPanel.Children.Clear();
            var searchText = searchBox.Text.Trim();
            var visibleValues = values
                .Where(value => string.IsNullOrWhiteSpace(searchText)
                    || value.Display.Contains(searchText, StringComparison.CurrentCultureIgnoreCase))
                .ToList();

            var selectAll = new CheckBox
            {
                Content = "(Select All)",
                Margin = new Thickness(8, 4, 8, 4),
                FontWeight = FontWeights.SemiBold,
                IsThreeState = true,
                IsChecked = GetSelectAllState(visibleValues, workingSelection)
            };
            selectAll.Click += (_, _) =>
            {
                var shouldSelect = selectAll.IsChecked == true;
                foreach (var value in visibleValues)
                {
                    if (shouldSelect)
                    {
                        workingSelection.Add(value.Key);
                    }
                    else
                    {
                        workingSelection.Remove(value.Key);
                    }
                }

                RefreshOptions();
            };
            optionsPanel.Children.Add(CreateFilterOptionRow(selectAll));

            foreach (var value in visibleValues)
            {
                var option = new CheckBox
                {
                    Content = value.Display,
                    Margin = new Thickness(22, 4, 8, 4),
                    IsChecked = workingSelection.Contains(value.Key)
                };
                option.Click += (_, _) =>
                {
                    if (option.IsChecked == true)
                    {
                        workingSelection.Add(value.Key);
                    }
                    else
                    {
                        workingSelection.Remove(value.Key);
                    }

                    RefreshOptions();
                };
                optionsPanel.Children.Add(CreateFilterOptionRow(option));
            }

            if (visibleValues.Count == 0)
            {
                optionsPanel.Children.Add(new TextBlock
                {
                    Text = "No matching values",
                    Margin = new Thickness(8, 8, 8, 8),
                    Foreground = FindResource("MutedTextBrush") as Brush ?? Brushes.Gray
                });
            }
        }

        searchBox.TextChanged += (_, _) => RefreshOptions();
        RefreshOptions();

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(8, 0, 8, 8)
        };
        var okButton = new Button
        {
            Content = "OK",
            MinWidth = 64,
            Height = 30
        };
        okButton.Click += (_, _) =>
        {
            if (workingSelection.Count == values.Count)
            {
                ClearColumnFilter(grid, columnKey);
            }
            else
            {
                var selectedLabels = values
                    .Where(value => workingSelection.Contains(value.Key))
                    .Select(value => value.Display)
                    .ToList();
                SetColumnFilter(grid, columnKey, workingSelection, BuildFilterLabel(selectedLabels));
            }

            menu.IsOpen = false;
        };
        var cancelButton = new Button
        {
            Content = "Cancel",
            MinWidth = 72,
            Height = 30,
            Background = Brushes.White,
            Foreground = BrushFactory.Frozen(0x0F, 0x17, 0x2A),
            BorderBrush = BrushFactory.Frozen(0x94, 0xA3, 0xB8),
            BorderThickness = new Thickness(1)
        };
        cancelButton.Click += (_, _) => menu.IsOpen = false;
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        menu.Items.Add(new MenuItem
        {
            Header = buttonPanel,
            Padding = new Thickness(0),
            StaysOpenOnClick = true
        });

        menu.Opened += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            searchBox.Focus();
            searchBox.SelectAll();
        }, DispatcherPriority.Input);

        return menu;
    }

    private Border CreateFilterOptionRow(CheckBox checkBox)
    {
        var row = new Border
        {
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(4, 1, 4, 1),
            Child = checkBox
        };

        row.MouseEnter += (_, _) => row.Background = BrushFactory.Frozen(0xE0, 0xF2, 0xFE);
        row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
        return row;
    }

    private static bool? GetSelectAllState(IReadOnlyCollection<ColumnFilterValue> visibleValues, ISet<string> selectedKeys)
    {
        if (visibleValues.Count == 0)
        {
            return false;
        }

        var selectedCount = visibleValues.Count(value => selectedKeys.Contains(value.Key));
        if (selectedCount == 0)
        {
            return false;
        }

        return selectedCount == visibleValues.Count ? true : null;
    }

    private List<ColumnFilterValue> GetColumnFilterValues(DataGrid grid, DataGridColumn column, string columnKey, Func<object, object?> accessor)
    {
        var view = GetGridCollectionView(grid);
        if (view is null)
        {
            return [];
        }

        CaptureBaseViewFilter(view);
        var sourceItems = view.SourceCollection ?? grid.ItemsSource ?? grid.Items;
        var valuesByKey = new Dictionary<string, ColumnFilterValue>(StringComparer.Ordinal);
        foreach (var item in EnumerateSourceItems(sourceItems))
        {
            if (!PassesBaseAndOtherColumnFilters(grid, item, columnKey))
            {
                continue;
            }

            var value = accessor(item);
            var key = GetFilterValueKey(value);
            if (!valuesByKey.ContainsKey(key))
            {
                valuesByKey[key] = new ColumnFilterValue(key, FormatFilterValue(value, column));
            }
        }

        return valuesByKey.Values
            .OrderBy(value => string.Equals(value.Display, BlanksFilterText, StringComparison.Ordinal))
            .ThenBy(value => value.Display, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private void SetColumnFilter(DataGrid grid, string columnKey, IEnumerable<string> selectedKeys, string? displayLabel = null)
    {
        var state = GetGridColumnFilterState(grid);
        state.SelectedValuesByColumn[columnKey] = selectedKeys.ToHashSet(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(displayLabel))
        {
            state.LabelsByColumn.Remove(columnKey);
        }
        else
        {
            state.LabelsByColumn[columnKey] = displayLabel.Trim();
        }
        ApplyGridColumnFilters(grid);
        RefreshGridStatePills(grid);
    }

    private static string BuildFilterLabel(IReadOnlyList<string> labels)
    {
        return labels.Count switch
        {
            0 => "No values",
            1 => labels[0],
            2 => $"{labels[0]}, {labels[1]}",
            _ => $"{labels.Count} values"
        };
    }

    private void ClearColumnFilter(DataGrid grid, string columnKey)
    {
        var state = GetGridColumnFilterState(grid);
        state.SelectedValuesByColumn.Remove(columnKey);
        state.LabelsByColumn.Remove(columnKey);
        ApplyGridColumnFilters(grid);
        RefreshGridStatePills(grid);
    }

    private void ClearGridColumnFilters_Click(object sender, RoutedEventArgs e)
    {
        foreach (var grid in _gridColumnFilters.Keys.ToList())
        {
            _gridColumnFilters[grid].SelectedValuesByColumn.Clear();
            _gridColumnFilters[grid].LabelsByColumn.Clear();
            ApplyGridColumnFilters(grid);
            RefreshGridStatePills(grid);
        }
    }

    private void ApplyGridColumnFilters(DataGrid grid)
    {
        var view = GetGridCollectionView(grid);
        if (view is null)
        {
            return;
        }

        CaptureBaseViewFilter(view);
        var state = GetGridColumnFilterState(grid);
        if (state.SelectedValuesByColumn.Count == 0)
        {
            ApplyCollectionViewFilter(view, _baseViewFilters.GetValueOrDefault(view));
            return;
        }

        ApplyCollectionViewFilter(view, item => PassesBaseAndColumnFilters(grid, item));
    }

    private static void ApplyCollectionViewFilter(ICollectionView view, Predicate<object>? filter)
    {
        using (view.DeferRefresh())
        {
            view.Filter = filter;
        }
    }

    private bool PassesBaseAndOtherColumnFilters(DataGrid grid, object item, string excludedColumnKey)
    {
        var view = GetGridCollectionView(grid);
        if (view is null)
        {
            return true;
        }

        CaptureBaseViewFilter(view);
        var baseFilter = _baseViewFilters.GetValueOrDefault(view);
        return (baseFilter?.Invoke(item) ?? true) && PassesColumnFilters(grid, item, excludedColumnKey);
    }

    private bool PassesBaseAndColumnFilters(DataGrid grid, object item)
    {
        var view = GetGridCollectionView(grid);
        if (view is null)
        {
            return true;
        }

        var baseFilter = _baseViewFilters.GetValueOrDefault(view);
        return (baseFilter?.Invoke(item) ?? true) && PassesColumnFilters(grid, item, excludedColumnKey: null);
    }

    private bool PassesColumnFilters(DataGrid grid, object item, string? excludedColumnKey)
    {
        if (!_gridColumnFilters.TryGetValue(grid, out var state))
        {
            return true;
        }

        foreach (var (columnKey, selectedValues) in state.SelectedValuesByColumn)
        {
            if (string.Equals(columnKey, excludedColumnKey, StringComparison.Ordinal))
            {
                continue;
            }

            var column = grid.Columns.FirstOrDefault(candidate => string.Equals(GetColumnPersistenceKey(candidate), columnKey, StringComparison.Ordinal));
            var accessor = column is null ? null : GetColumnValueAccessor(column);
            if (accessor is null || !selectedValues.Contains(GetFilterValueKey(accessor(item))))
            {
                return false;
            }
        }

        return true;
    }

    private void CaptureBaseViewFilter(ICollectionView view)
    {
        if (_baseViewFilters.ContainsKey(view))
        {
            return;
        }

        _baseViewFilters[view] = view.Filter;
    }

    private GridColumnFilterState GetGridColumnFilterState(DataGrid grid)
    {
        if (!_gridColumnFilters.TryGetValue(grid, out var state))
        {
            state = new GridColumnFilterState();
            _gridColumnFilters[grid] = state;
        }

        return state;
    }

    private static ICollectionView? GetGridCollectionView(DataGrid grid)
    {
        if (grid.ItemsSource is ICollectionView directView)
        {
            return directView;
        }

        return grid.ItemsSource is null ? null : CollectionViewSource.GetDefaultView(grid.ItemsSource);
    }

    private static IEnumerable<object> EnumerateSourceItems(IEnumerable source)
    {
        foreach (var item in source)
        {
            if (item is not CollectionViewGroup)
            {
                yield return item;
            }
        }
    }

    private static Func<object, object?>? GetColumnValueAccessor(DataGridColumn column)
    {
        if (column is DataGridTemplateColumn { Header: ForecastMonthColumnDefinition monthColumn })
        {
            return monthColumn.IsTotal
                ? item => item is ForecastLine line ? line[monthColumn.Key] : null
                : null;
        }

        var path = GetColumnBindingPath(column);
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return item => ResolveBindingPathValue(item, path);
    }

    private static string? GetColumnBindingPath(DataGridColumn column)
    {
        if (column is DataGridBoundColumn { Binding: Binding binding })
        {
            return binding.Path?.Path;
        }

        return string.IsNullOrWhiteSpace(column.SortMemberPath) ? null : column.SortMemberPath;
    }

    private static object? ResolveBindingPathValue(object item, string path)
    {
        object? current = item;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current is null)
            {
                return null;
            }

            if (segment.Length > 2 && segment[0] == '[' && segment[^1] == ']')
            {
                current = ResolveIndexerValue(current, segment[1..^1]);
                continue;
            }

            var property = current.GetType().GetProperty(segment, BindingFlags.Instance | BindingFlags.Public);
            current = property?.GetValue(current);
        }

        return current;
    }

    private static object? ResolveIndexerValue(object item, string key)
    {
        var indexer = item.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(property =>
            {
                var parameters = property.GetIndexParameters();
                return parameters.Length == 1 && parameters[0].ParameterType == typeof(string);
            });

        return indexer?.GetValue(item, [key]);
    }

    private static string GetFilterValueKey(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string text when string.IsNullOrWhiteSpace(text) => string.Empty,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string FormatFilterValue(object? value, DataGridColumn column)
    {
        if (value is null)
        {
            return BlanksFilterText;
        }

        if (value is string text)
        {
            return string.IsNullOrWhiteSpace(text) ? BlanksFilterText : text;
        }

        if (column is DataGridBoundColumn { Binding: Binding { StringFormat: { } stringFormat } }
            && !string.IsNullOrWhiteSpace(stringFormat))
        {
            try
            {
                var format = stringFormat.StartsWith("{}", StringComparison.Ordinal) ? stringFormat[2..] : stringFormat;
                return string.Format(CultureInfo.CurrentCulture, format, value);
            }
            catch (FormatException)
            {
                // Fall back to the raw value if a WPF-specific format cannot be used here.
            }
        }

        return value switch
        {
            DateOnly date => date.ToString("d", CultureInfo.CurrentCulture),
            DateTime date => date.ToString("d", CultureInfo.CurrentCulture),
            DateTimeOffset date => date.ToString("g", CultureInfo.CurrentCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.CurrentCulture) ?? BlanksFilterText,
            _ => value.ToString() ?? BlanksFilterText
        };
    }
}
