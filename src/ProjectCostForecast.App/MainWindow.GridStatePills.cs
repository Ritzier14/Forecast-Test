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
    private void ApplyColumnSort(DataGrid grid, DataGridColumn column, ListSortDirection direction)
    {
        var sortPath = GetColumnSortPath(column);
        var view = GetGridCollectionView(grid);
        if (view is null || string.IsNullOrWhiteSpace(sortPath))
        {
            return;
        }

        using (view.DeferRefresh())
        {
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription(sortPath, direction));
        }

        foreach (var gridColumn in grid.Columns)
        {
            gridColumn.SortDirection = null;
        }

        column.SortDirection = direction;
        if (_gridColumnFilters.TryGetValue(grid, out var state) && state.SelectedValuesByColumn.Count > 0)
        {
            ApplyGridColumnFilters(grid);
        }

        RefreshGridStatePills(grid);
    }

    private void ClearColumnSort(DataGrid grid)
    {
        var view = GetGridCollectionView(grid);
        if (view is null)
        {
            return;
        }

        using (view.DeferRefresh())
        {
            view.SortDescriptions.Clear();
        }

        foreach (var column in grid.Columns)
        {
            column.SortDirection = null;
        }

        RefreshGridStatePills(grid);
    }

    private void RefreshGridStatePills(DataGrid grid)
    {
        if (ReferenceEquals(grid, ForecastLinesGrid))
        {
            RefreshForecastGridStatePills();
        }
    }

    private void RefreshForecastGridStatePills()
    {
        ForecastGridStatePillPanel.Children.Clear();

        AddForecastSortPill();
        AddForecastFilterPills();
        AddForecastGroupPill();
        AddQuickFilterPills();
        AddPillActionButtons();

        ForecastGridStatePillPanel.Visibility = Visibility.Visible;
    }

    private void AddPillActionButtons()
    {
        AddPillActionButton("+ Sort", "Add a sort", OpenAddSortMenu);
        AddPillActionButton("+ Filter", "Add a column filter", OpenAddFilterMenu);
        AddPillActionButton("+ Group", "Group the forecast lines", OpenAddGroupMenu);
    }

    private void AddPillActionButton(string label, string toolTip, Action<Button> open)
    {
        var button = new Button
        {
            Content = new TextBlock
            {
                Text = label,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFactory.Frozen(0x3F, 0x3B, 0x36)
            },
            Padding = new Thickness(9, 3, 9, 3),
            Margin = new Thickness(0, 0, 6, 6),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = toolTip
        };
        if (FindResource("ForecastStatePillButtonStyle") is Style style)
        {
            button.Style = style;
        }

        button.Click += (_, _) => open(button);
        ForecastGridStatePillPanel.Children.Add(button);
    }

    private void OpenAddSortMenu(Button pill)
    {
        var menu = CreateColumnContextMenu();
        foreach (var column in ForecastLinesGrid.Columns
                     .Where(column => column.Visibility == Visibility.Visible && GetColumnSortPath(column) is not null)
                     .OrderBy(column => column.DisplayIndex))
        {
            var columnItem = new MenuItem { Header = GetForecastColumnMenuLabel(column) };
            var targetColumn = column;
            var ascending = new MenuItem { Header = "Ascending" };
            ascending.Click += (_, _) => ApplyColumnSort(ForecastLinesGrid, targetColumn, ListSortDirection.Ascending);
            columnItem.Items.Add(ascending);
            var descending = new MenuItem { Header = "Descending" };
            descending.Click += (_, _) => ApplyColumnSort(ForecastLinesGrid, targetColumn, ListSortDirection.Descending);
            columnItem.Items.Add(descending);
            menu.Items.Add(columnItem);
        }

        OpenPillMenu(pill, menu);
    }

    private void OpenAddFilterMenu(Button pill)
    {
        var menu = CreateColumnContextMenu();
        foreach (var column in ForecastLinesGrid.Columns
                     .Where(column => column.Visibility == Visibility.Visible)
                     .OrderBy(column => column.DisplayIndex))
        {
            var columnKey = GetColumnPersistenceKey(column);
            if (string.IsNullOrWhiteSpace(columnKey) || string.Equals(columnKey, "!", StringComparison.Ordinal))
            {
                continue;
            }

            var targetColumn = column;
            var columnItem = new MenuItem { Header = GetForecastColumnMenuLabel(column) };
            columnItem.Click += (_, _) => OpenColumnFilterMenuFromPill(pill, targetColumn, columnKey);
            menu.Items.Add(columnItem);
        }

        OpenPillMenu(pill, menu);
    }

    private void OpenAddGroupMenu(Button pill)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            OpenForecastGroupPillMenu(pill, viewModel);
        }
    }

    private void AddForecastSortPill()
    {
        var sortedColumn = ForecastLinesGrid.Columns.FirstOrDefault(column => column.SortDirection is not null);
        if (sortedColumn?.SortDirection is not ListSortDirection direction)
        {
            return;
        }

        var directionLabel = direction == ListSortDirection.Ascending ? "Asc" : "Desc";
        var pill = CreateForecastGridStatePill(
            $"{GetForecastColumnMenuLabel(sortedColumn)} {directionLabel}",
            "Sort",
            () => ClearColumnSort(ForecastLinesGrid));
        pill.Button.Click += (_, _) => OpenForecastSortPillMenu(pill.Button, sortedColumn, direction);
        ForecastGridStatePillPanel.Children.Add(pill.Container);
    }

    private void AddForecastFilterPills()
    {
        if (!_gridColumnFilters.TryGetValue(ForecastLinesGrid, out var state))
        {
            return;
        }

        foreach (var columnKey in state.SelectedValuesByColumn.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToList())
        {
            var column = FindColumnByPersistenceKey(ForecastLinesGrid, columnKey);
            if (column is null)
            {
                continue;
            }

            var filterLabel = state.LabelsByColumn.GetValueOrDefault(columnKey);
            if (string.IsNullOrWhiteSpace(filterLabel)
                && state.SelectedValuesByColumn.TryGetValue(columnKey, out var selectedKeys))
            {
                filterLabel = selectedKeys.Count == 1 ? selectedKeys.First() : $"{selectedKeys.Count} values";
            }

            var pillTitle = filterLabel?.EndsWith(" values", StringComparison.OrdinalIgnoreCase) == true
                ? $"Filter {filterLabel}"
                : $"Filter \"{filterLabel ?? GetForecastColumnMenuLabel(column)}\"";
            var pill = CreateForecastGridStatePill(
                pillTitle,
                GetForecastColumnMenuLabel(column),
                () => ClearColumnFilter(ForecastLinesGrid, columnKey));
            pill.Button.Click += (_, _) => OpenColumnFilterMenuFromPill(pill.Button, column, columnKey);
            ForecastGridStatePillPanel.Children.Add(pill.Container);
        }
    }

    private void AddForecastGroupPill()
    {
        if (DataContext is not MainWindowViewModel viewModel
            || string.Equals(viewModel.ForecastGroupByKey, MainWindowViewModel.ForecastGroupByNoneKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var label = viewModel.ForecastGroupByKey switch
        {
            MainWindowViewModel.ForecastGroupByTaskKey => "Task",
            MainWindowViewModel.ForecastGroupByResourceKey => "Resource",
            MainWindowViewModel.ForecastGroupByCategoryKey => "Category",
            _ => "Group"
        };

        var pill = CreateForecastGridStatePill(
            label,
            "Group",
            () => viewModel.ForecastGroupByKey = MainWindowViewModel.ForecastGroupByNoneKey);
        pill.Button.Click += (_, _) => OpenForecastGroupPillMenu(pill.Button, viewModel);
        ForecastGridStatePillPanel.Children.Add(pill.Container);
    }

    private void AddQuickFilterPills()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (viewModel.ShowOnlyLinesWithActualCost)
        {
            AddQuickFilterPill(viewModel, "Actual cost only", () => viewModel.ShowOnlyLinesWithActualCost = false);
        }

        if (viewModel.ShowCostThisMonthOnly)
        {
            AddQuickFilterPill(viewModel, "Cost this month only", () => viewModel.ShowCostThisMonthOnly = false);
        }

        if (viewModel.ShowOnlyLinesWithRemainingForecast)
        {
            AddQuickFilterPill(viewModel, "Remaining forecast only", () => viewModel.ShowOnlyLinesWithRemainingForecast = false);
        }

        if (!string.Equals(viewModel.SelectedMonthlyVarianceFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            AddQuickFilterPill(viewModel, $"Monthly variance: {viewModel.SelectedMonthlyVarianceFilter}", () => viewModel.SelectedMonthlyVarianceFilter = "All");
        }

        if (!string.Equals(viewModel.SelectedBudgetVarianceFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            AddQuickFilterPill(viewModel, $"Budget variance: {viewModel.SelectedBudgetVarianceFilter}", () => viewModel.SelectedBudgetVarianceFilter = "All");
        }
    }

    private void AddQuickFilterPill(MainWindowViewModel viewModel, string title, Action remove)
    {
        var pill = CreateForecastGridStatePill(title, "Quick filter", remove);
        pill.Button.Click += (_, _) => OpenPillMenu(pill.Button, new ContextMenu { Items = { BuildQuickFiltersMenu(viewModel) } });
        ForecastGridStatePillPanel.Children.Add(pill.Container);
    }

    private (Grid Container, Button Button) CreateForecastGridStatePill(string title, string type, Action remove)
    {
        var titleBlock = new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFactory.Frozen(0x3F, 0x3B, 0x36)
        };

        var typeBlock = new TextBlock
        {
            Text = type,
            FontSize = 10,
            FontStyle = FontStyles.Italic,
            Foreground = BrushFactory.Frozen(0x7A, 0x75, 0x6E),
            Margin = new Thickness(0, 1, 0, 0)
        };

        var stack = new StackPanel();
        stack.Children.Add(titleBlock);
        stack.Children.Add(typeBlock);

        var button = new Button
        {
            Content = stack,
            Padding = new Thickness(11, 5, 25, 5)
        };
        if (FindResource("ForecastStatePillButtonStyle") is Style style)
        {
            button.Style = style;
        }

        var close = new Button
        {
            Content = "×",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -6, -5, 0),
            ToolTip = $"Remove {type.ToLowerInvariant()}"
        };
        if (FindResource("ForecastStatePillCloseButtonStyle") is Style closeStyle)
        {
            close.Style = closeStyle;
        }
        close.Click += (_, e) =>
        {
            e.Handled = true;
            remove();
        };

        var container = new Grid
        {
            Margin = new Thickness(0, 0, 8, 6)
        };
        button.Margin = new Thickness(0);
        container.Children.Add(button);
        container.Children.Add(close);
        return (container, button);
    }

    private void OpenForecastSortPillMenu(Button pill, DataGridColumn column, ListSortDirection direction)
    {
        var menu = CreateColumnContextMenu();
        var ascending = new MenuItem
        {
            Header = "Sort ascending",
            IsCheckable = true,
            IsChecked = direction == ListSortDirection.Ascending
        };
        ascending.Click += (_, _) =>
        {
            ApplyColumnSort(ForecastLinesGrid, column, ListSortDirection.Ascending);
            menu.IsOpen = false;
        };
        menu.Items.Add(ascending);

        var descending = new MenuItem
        {
            Header = "Sort descending",
            IsCheckable = true,
            IsChecked = direction == ListSortDirection.Descending
        };
        descending.Click += (_, _) =>
        {
            ApplyColumnSort(ForecastLinesGrid, column, ListSortDirection.Descending);
            menu.IsOpen = false;
        };
        menu.Items.Add(descending);
        menu.Items.Add(new Separator());

        var clear = new MenuItem { Header = "Clear sort" };
        clear.Click += (_, _) =>
        {
            ClearColumnSort(ForecastLinesGrid);
            menu.IsOpen = false;
        };
        menu.Items.Add(clear);

        OpenPillMenu(pill, menu);
    }

    private void OpenForecastGroupPillMenu(Button pill, MainWindowViewModel viewModel)
    {
        var menu = CreateColumnContextMenu();
        AddForecastGroupPillMenuItem(menu, viewModel, "No grouping", MainWindowViewModel.ForecastGroupByNoneKey);
        AddForecastGroupPillMenuItem(menu, viewModel, "Group by task", MainWindowViewModel.ForecastGroupByTaskKey);
        AddForecastGroupPillMenuItem(menu, viewModel, "Group by resource", MainWindowViewModel.ForecastGroupByResourceKey);
        AddForecastGroupPillMenuItem(menu, viewModel, "Group by category", MainWindowViewModel.ForecastGroupByCategoryKey);
        OpenPillMenu(pill, menu);
    }

    private static void AddForecastGroupPillMenuItem(ContextMenu menu, MainWindowViewModel viewModel, string label, string groupByKey)
    {
        var item = new MenuItem
        {
            Header = label,
            IsCheckable = true,
            IsChecked = string.Equals(viewModel.ForecastGroupByKey, groupByKey, StringComparison.OrdinalIgnoreCase)
        };
        item.Click += (_, _) =>
        {
            ExecuteAfterClosingMenu(item, () => viewModel.ForecastGroupByKey = groupByKey);
        };
        menu.Items.Add(item);
    }

    private void OpenColumnFilterMenuFromPill(Button pill, DataGridColumn column, string columnKey)
    {
        var accessor = GetColumnValueAccessor(column);
        if (accessor is null)
        {
            return;
        }

        var values = GetColumnFilterValues(ForecastLinesGrid, column, columnKey, accessor);
        if (values.Count == 0)
        {
            return;
        }

        var menu = BuildColumnFilterMenu(ForecastLinesGrid, column, columnKey, values);
        OpenPillMenu(pill, menu);
    }

    private static DataGridColumn? FindColumnByPersistenceKey(DataGrid grid, string columnKey)
    {
        return grid.Columns.FirstOrDefault(column =>
            string.Equals(GetColumnPersistenceKey(column), columnKey, StringComparison.OrdinalIgnoreCase));
    }

    private static void OpenPillMenu(Button pill, ContextMenu menu)
    {
        pill.ContextMenu = menu;
        menu.PlacementTarget = pill;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private static string? GetColumnSortPath(DataGridColumn column)
    {
        if (column.Header is ForecastMonthColumnDefinition monthColumn)
        {
            return $"[{monthColumn.Key}]";
        }

        return GetColumnBindingPath(column);
    }
}
