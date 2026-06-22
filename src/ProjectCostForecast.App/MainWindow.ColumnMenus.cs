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
using ProjectCostForecast.App.Services;
using ProjectCostForecast.App.ViewModels;

namespace ProjectCostForecast.App;

public partial class MainWindow
{
    private void ShowColumnMenu(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var header = FindParent<DataGridColumnHeader>(source);
        var grid = FindParent<DataGrid>(source);
        if (header?.Column is null || grid is null)
        {
            return;
        }

        if (IsHeaderIconRightClick(header, e))
        {
            ShowColumnIconMenu(header);
            e.Handled = true;
            return;
        }

        var menu = CreateColumnContextMenu();
        var viewModel = DataContext as MainWindowViewModel;

        AddSectionHeader(menu, "Window context");
        AddWindowContextMenuItems(menu, grid, viewModel);
        if (viewModel is not null)
        {
            menu.Items.Add(BuildQuickFiltersMenu(viewModel));
        }

        AddMenuSeparator(menu);
        AddSectionHeader(menu, "Column options");
        AddColumnOptionsMenuItems(menu, grid, header);

        AddMenuSeparator(menu);
        AddSectionHeader(menu, "Column context");
        AddColumnSpecificMenuItems(menu, grid, header, viewModel);
        ApplyMenuIcons(menu);
        AttachMenuIconRightClick(menu, header);

        header.ContextMenu = menu;
        menu.PlacementTarget = header;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private static bool IsHeaderIconRightClick(DataGridColumnHeader header, MouseButtonEventArgs e)
    {
        var current = e.OriginalSource as DependencyObject;
        while (current is not null && !ReferenceEquals(current, header))
        {
            if (current is FrameworkElement { Tag: string tag } && string.Equals(tag, HeaderIconTag, StringComparison.Ordinal))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void AttachMenuIconRightClick(ItemsControl menu, DataGridColumnHeader header)
    {
        foreach (var item in menu.Items.OfType<MenuItem>().ToList())
        {
            item.PreviewMouseRightButtonUp += (sender, e) =>
            {
                if (sender is MenuItem { Icon: FrameworkElement icon } menuItem && icon.IsMouseOver)
                {
                    e.Handled = true;
                    ExecuteAfterClosingMenu(menuItem, () => ShowColumnIconMenu(header));
                }
            };
            AttachMenuIconRightClick(item, header);
        }
    }

    private void ShowColumnIconMenu(DataGridColumnHeader header)
    {
        var menu = BuildColumnIconMenu(header.Column);
        ApplyMenuIcons(menu);
        header.ContextMenu = menu;
        menu.PlacementTarget = header;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    private ContextMenu CreateColumnContextMenu()
    {
        return new ContextMenu
        {
            MinWidth = 260,
            MaxWidth = 360
        };
    }

    private ContextMenu BuildColumnIconMenu(DataGridColumn column)
    {
        var menu = CreateColumnContextMenu();
        AddSectionHeader(menu, "Icon");

        foreach (var option in ColumnIconOptions)
        {
            var item = new MenuItem
            {
                Header = option.Label,
                Icon = CreateMenuTextIcon(option.Glyph),
                IsCheckable = true,
                IsChecked = string.Equals(GridColumnPresentationState.GetIconGlyph(column), option.Glyph, StringComparison.Ordinal),
                StaysOpenOnClick = true
            };
            item.Click += (_, _) =>
            {
                SelectColumnIcon(column, item, option.Glyph);
            };
            AttachIconSelectionRightClick(item, column, option.Glyph);
            menu.Items.Add(item);
        }

        return menu;
    }

    private MenuItem BuildColumnColourMenu(DataGridColumn column)
    {
        var colourMenu = new MenuItem { Header = "Colour column", Icon = CreateMenuTextIcon("C") };
        foreach (var option in ColumnColourOptions)
        {
            var item = new MenuItem
            {
                Header = option.Label,
                Icon = CreateColourSwatch(option.ColumnHex),
                IsCheckable = true,
                IsChecked = IsColumnColourSelected(column, option),
                StaysOpenOnClick = true
            };
            item.Click += (_, _) =>
            {
                SetSingleCheckedItem(item);
                GridColumnPresentationState.SetBaseColumnBackground(column, BrushFactory.Frozen(option.ColumnHex));
                GridColumnPresentationState.SetBaseHeaderBackground(column, BrushFactory.Frozen(option.HeaderHex));
                GridColumnPresentationState.SetColumnBorderBrush(column, GetColumnBorderBrush(option));
                GridColumnPresentationState.SetHeaderBorderBrush(column, GetHeaderBorderBrush(option));
                ApplyColumnHighlightPresentation(column, GridColumnHighlightState.GetIsHighlighted(column));
                RefreshColumnPresentation(column);
            };
            colourMenu.Items.Add(item);
        }

        return colourMenu;
    }

    private MenuItem BuildColumnHeaderColourMenu(DataGrid grid, DataGridColumn column)
    {
        var colourMenu = new MenuItem { Header = "Header colour", Icon = CreateMenuTextIcon("C") };
        var targetColumns = ReferenceEquals(grid, ForecastLinesGrid)
            ? grid.Columns.Where(candidate => candidate.Header is not ForecastMonthColumnDefinition).ToList()
            : [column];
        foreach (var option in ColumnColourOptions)
        {
            var item = new MenuItem
            {
                Header = option.Label,
                Icon = CreateColourSwatch(option.HeaderHex),
                StaysOpenOnClick = true
            };
            item.Click += (_, _) =>
            {
                var targetHeaderBrush =
                    string.Equals(option.Label, "Default", StringComparison.OrdinalIgnoreCase)
                        ? CreateGridHeaderGradient()
                        : BrushFactory.FrozenHeaderGradient(option.HeaderHex);
                var targetSpec =
                    string.Equals(option.Label, "Default", StringComparison.OrdinalIgnoreCase)
                        ? string.Empty
                        : BrushFactory.SerializeHeaderGradientSpec(option.HeaderHex, "Balanced");

                foreach (var targetColumn in targetColumns)
                {
                    GridColumnPresentationState.SetBaseHeaderBackground(targetColumn, targetHeaderBrush);
                    GridColumnPresentationState.SetHeaderColorSpec(targetColumn, targetSpec);
                    GridColumnPresentationState.SetHeaderBorderBrush(targetColumn, GetHeaderBorderBrush(option));
                    ApplyColumnHighlightPresentation(targetColumn, GridColumnHighlightState.GetIsHighlighted(targetColumn));
                    RefreshColumnPresentation(targetColumn);
                }
            };
            colourMenu.Items.Add(item);
        }

        colourMenu.Items.Add(new Separator());
        var customItem = new MenuItem { Header = "Custom..." };
        customItem.Click += (_, _) => ExecuteAfterClosingMenu(customItem, () =>
            OpenHeaderColorPicker(
                ReferenceEquals(grid, ForecastLinesGrid) ? "Forecast Fixed Header Colour" : $"{GetForecastColumnMenuLabel(column)} Header Colour",
                targetColumns.Select(GridColumnPresentationState.GetHeaderColorSpec).FirstOrDefault(spec => !string.IsNullOrWhiteSpace(spec)),
                colorSpec =>
                {
                    var brush = string.IsNullOrWhiteSpace(colorSpec)
                        ? CreateGridHeaderGradient()
                        : BrushFactory.FrozenHeaderGradient(colorSpec);
                    foreach (var targetColumn in targetColumns)
                    {
                        GridColumnPresentationState.SetBaseHeaderBackground(targetColumn, brush);
                        GridColumnPresentationState.SetHeaderColorSpec(targetColumn, colorSpec ?? string.Empty);
                        GridColumnPresentationState.SetHeaderBorderBrush(targetColumn, BrushFactory.Frozen("#D9E2EC"));
                        ApplyColumnHighlightPresentation(targetColumn, GridColumnHighlightState.GetIsHighlighted(targetColumn));
                        RefreshColumnPresentation(targetColumn);
                    }
                }));
        colourMenu.Items.Add(customItem);

        return colourMenu;
    }

    private static Brush GetColumnBorderBrush(ColumnColourOption option)
    {
        return string.Equals(option.Label, "Blue", StringComparison.OrdinalIgnoreCase)
            ? BrushFactory.Frozen("#D7E5F5")
            : BrushFactory.Frozen("#EEF3F8");
    }

    private static Brush GetHeaderBorderBrush(ColumnColourOption option)
    {
        return string.Equals(option.Label, "Blue", StringComparison.OrdinalIgnoreCase)
            ? BrushFactory.Frozen("#CEDFF3")
            : BrushFactory.Frozen("#E2EAF4");
    }

    private void AddWindowContextMenuItems(ContextMenu menu, DataGrid grid, MainWindowViewModel? viewModel)
    {
        if (viewModel is not null && ReferenceEquals(grid, ForecastLinesGrid))
        {
            var toggleMonthForecast = new MenuItem
            {
                Header = "Month Forecast",
                IsCheckable = true,
                IsChecked = viewModel.ShowCtcMonthForecastColumns,
                IsEnabled = viewModel.IsCtcMonthForecastSelectionAvailable
            };
            toggleMonthForecast.Click += (_, _) => viewModel.ToggleCtcMonthForecastColumns();
            menu.Items.Add(toggleMonthForecast);

            if (viewModel.IsCtcMonthForecastSelectionAvailable)
            {
                var yearMenu = new MenuItem { Header = "Month Forecast Year" };
                foreach (var year in viewModel.AvailableCtcMonthForecastYears)
                {
                    var yearItem = new MenuItem
                    {
                        Header = year.ToString(),
                        IsCheckable = true,
                        IsChecked = viewModel.IsCtcMonthForecastYearSelected(year),
                        StaysOpenOnClick = true
                    };
                    yearItem.Click += (_, _) => viewModel.ToggleCtcMonthForecastYear(year);
                    yearMenu.Items.Add(yearItem);
                }

                menu.Items.Add(yearMenu);

                var showYearTotals = new MenuItem
                {
                    Header = "Show year totals",
                    IsCheckable = true,
                    IsChecked = viewModel.ShowCtcMonthForecastYearTotals
                };
                showYearTotals.Click += (_, _) => viewModel.ToggleCtcMonthForecastYearTotals();
                menu.Items.Add(showYearTotals);

                var swapHeaderOrder = new MenuItem
                {
                    Header = "Show month above FY",
                    IsCheckable = true,
                    IsChecked = viewModel.ShowMonthNameAboveFiscalPeriod
                };
                swapHeaderOrder.Click += (_, _) => viewModel.ToggleMonthForecastHeaderOrder();
                menu.Items.Add(swapHeaderOrder);
            }

            var showZeroAsBlank = new MenuItem
            {
                Header = "Show zero as blank",
                IsCheckable = true,
                IsChecked = viewModel.ShowForecastZeroAsBlank
            };
            showZeroAsBlank.Click += (_, _) => viewModel.SetSelectedForecastShowZeroAsBlank(showZeroAsBlank.IsChecked);
            menu.Items.Add(showZeroAsBlank);
        }

        if (viewModel is not null
            && (ReferenceEquals(grid, LedgerTransactionsGrid) || ReferenceEquals(grid, LedgerMonthlyPivotGrid))
            && string.Equals(viewModel.ActiveDetailWorkspaceKey, "Ledger Costs", StringComparison.OrdinalIgnoreCase))
        {
            var pivotByMonth = new MenuItem
            {
                Header = "View as pivot by month",
                IsCheckable = true,
                IsChecked = string.Equals(viewModel.SelectedDetailWorkspaceView?.ContentKey, "PivotByMonth", StringComparison.OrdinalIgnoreCase)
            };
            pivotByMonth.Click += (_, _) =>
            {
                viewModel.SetSelectedDetailWorkspaceContentKey(pivotByMonth.IsChecked ? "PivotByMonth" : "Default");
                QueueApplyCurrentDetailWorkspaceViewColumnState();
            };
            menu.Items.Add(pivotByMonth);
        }

        if (viewModel is not null
            && (ReferenceEquals(grid, RawTransactionsGrid) || ReferenceEquals(grid, RawTransactionsMonthlyPivotGrid))
            && string.Equals(viewModel.ActiveWorkspaceKey, "Raw Transactions", StringComparison.OrdinalIgnoreCase))
        {
            var pivotByMonth = new MenuItem
            {
                Header = "View as pivot by month",
                IsCheckable = true,
                IsChecked = viewModel.ShowRawTransactionsPivotByMonth
            };
            pivotByMonth.Click += (_, _) =>
            {
                viewModel.SetSelectedWorkspaceContentKey(pivotByMonth.IsChecked ? "PivotByMonth" : "Default");
                QueueApplyCurrentWorkspaceViewColumnState();
            };
            menu.Items.Add(pivotByMonth);
        }

        if (viewModel is not null
            && (ReferenceEquals(grid, CategoryReportGrid) || ReferenceEquals(grid, CategoryMonthlyPivotGrid))
            && string.Equals(viewModel.ActiveWorkspaceKey, "Summary View", StringComparison.OrdinalIgnoreCase))
        {
            var pivotByMonth = new MenuItem
            {
                Header = "View by month",
                IsCheckable = true,
                IsChecked = viewModel.ShowSummaryViewByMonth
            };
            pivotByMonth.Click += (_, _) =>
            {
                viewModel.SetSelectedWorkspaceContentKey(pivotByMonth.IsChecked ? "PivotByMonth" : "Default");
                QueueApplyCurrentWorkspaceViewColumnState();
            };
            menu.Items.Add(pivotByMonth);
        }

        if (viewModel is not null && IsHighlightableGrid(grid))
        {
            var keepHighlights = new MenuItem
            {
                Header = "Keep highlights when changing tabs",
                IsCheckable = true,
                IsChecked = viewModel.KeepColumnHighlightsAcrossTabs
            };
            keepHighlights.Click += (_, _) => viewModel.KeepColumnHighlightsAcrossTabs = keepHighlights.IsChecked;
            menu.Items.Add(keepHighlights);
        }

        if (grid.Items.Groups is { Count: > 0 })
        {
            var expandAll = new MenuItem { Header = "Expand all groups" };
            expandAll.Click += (_, _) => SetGroupedExpandState(grid, true);
            menu.Items.Add(expandAll);

            var collapseAll = new MenuItem { Header = "Collapse all groups" };
            collapseAll.Click += (_, _) => SetGroupedExpandState(grid, false);
            menu.Items.Add(collapseAll);
        }
    }

    private void AddColumnOptionsMenuItems(ContextMenu menu, DataGrid grid, DataGridColumnHeader header)
    {
        var sortMenu = new MenuItem { Header = "Sort" };
        var sortAscending = new MenuItem { Header = "Sort ascending" };
        sortAscending.Click += (_, _) => ApplyColumnSort(grid, header.Column, ListSortDirection.Ascending);
        sortMenu.Items.Add(sortAscending);
        var sortDescending = new MenuItem { Header = "Sort descending" };
        sortDescending.Click += (_, _) => ApplyColumnSort(grid, header.Column, ListSortDirection.Descending);
        sortMenu.Items.Add(sortDescending);
        sortMenu.IsEnabled = !string.IsNullOrWhiteSpace(GetColumnSortPath(header.Column));
        menu.Items.Add(sortMenu);

        var filterItem = new MenuItem { Header = "Filter" };
        filterItem.IsEnabled = GetColumnValueAccessor(header.Column) is not null
            && !string.IsNullOrWhiteSpace(GetColumnPersistenceKey(header.Column));
        filterItem.Click += (_, _) =>
        {
            menu.IsOpen = false;
            Dispatcher.BeginInvoke(() => OpenColumnFilterMenu(header, grid), DispatcherPriority.Input);
        };
        menu.Items.Add(filterItem);

        menu.Items.Add(BuildGroupMenu(grid));

        var visibleColumns = grid.Columns.Count(column => column.Visibility == Visibility.Visible);
        var hideCurrentColumn = new MenuItem
        {
            Header = "Hide current column",
            IsEnabled = visibleColumns > 1
        };
        hideCurrentColumn.Click += (_, _) =>
        {
            if (grid.Columns.Count(column => column.Visibility == Visibility.Visible) <= 1)
            {
                return;
            }

            header.Column.Visibility = Visibility.Collapsed;
            CaptureGridColumnState(grid);
        };
        menu.Items.Add(hideCurrentColumn);

        var hideColumnsMenu = new MenuItem { Header = "Hide Columns" };
        foreach (var column in grid.Columns
            .Where(column => !ReferenceEquals(grid, ForecastLinesGrid) || column.Header is not ForecastMonthColumnDefinition)
            .OrderBy(column => column.DisplayIndex))
        {
            var label = GetForecastColumnMenuLabel(column);
            var item = new MenuItem
            {
                Header = label,
                IsCheckable = true,
                IsChecked = column.Visibility == Visibility.Visible,
                Tag = column
            };
            item.Click += (_, _) =>
            {
                var visibleColumnCount = grid.Columns.Count(candidate => candidate.Visibility == Visibility.Visible);
                var target = (DataGridColumn)item.Tag;
                if (target.Visibility == Visibility.Visible && visibleColumnCount <= 1)
                {
                    item.IsChecked = true;
                    return;
                }

                target.Visibility = item.IsChecked ? Visibility.Visible : Visibility.Collapsed;
                CaptureGridColumnState(grid);
            };
            hideColumnsMenu.Items.Add(item);
        }

        if (ReferenceEquals(grid, ForecastLinesGrid))
        {
            AddForecastPeriodVisibilityItems(hideColumnsMenu, grid);
        }

        menu.Items.Add(hideColumnsMenu);
    }

    private void AddForecastPeriodVisibilityItems(MenuItem menu, DataGrid grid)
    {
        var monthColumns = grid.Columns
            .Where(column => column.Header is ForecastMonthColumnDefinition)
            .OrderBy(column => column.DisplayIndex)
            .ToList();
        if (monthColumns.Count == 0)
        {
            return;
        }

        menu.Items.Add(new Separator());
        var forecastMenu = new MenuItem
        {
            Header = "Forecast View"
        };
        menu.Items.Add(forecastMenu);

        AddColumnGroupVisibilityItem(forecastMenu, grid, "Show Forecast View", monthColumns);
        var financialYearsMenu = new MenuItem { Header = "Financial Years" };
        forecastMenu.Items.Add(financialYearsMenu);

        foreach (var group in monthColumns
            .Where(column => column.Header is ForecastMonthColumnDefinition { IsTotal: false })
            .GroupBy(column => FiscalPeriod.FiscalYearFromPeriodLabel(((ForecastMonthColumnDefinition)column.Header).Key))
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            AddColumnGroupVisibilityItem(financialYearsMenu, grid, group.Key, group.ToList());
        }
    }

    private void AddColumnGroupVisibilityItem(MenuItem menu, DataGrid grid, string label, IReadOnlyCollection<DataGridColumn> columns)
    {
        var item = new MenuItem
        {
            Header = label,
            IsCheckable = true,
            IsChecked = columns.All(column => column.Visibility == Visibility.Visible)
        };
        item.Click += (_, _) =>
        {
            SetColumnGroupVisibility(grid, columns, item.IsChecked);
        };
        menu.Items.Add(item);
    }

    private void SetColumnGroupVisibility(DataGrid grid, IEnumerable<DataGridColumn> columns, bool isVisible)
    {
        foreach (var column in columns)
        {
            column.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        CaptureGridColumnState(grid);
        ApplyForecastFreezeBoundaryForCurrentColumns();
    }

    private MenuItem BuildGroupMenu(DataGrid grid)
    {
        var groupMenu = new MenuItem { Header = "Group" };
        if (DataContext is not MainWindowViewModel viewModel)
        {
            groupMenu.IsEnabled = false;
            return groupMenu;
        }

        if (ReferenceEquals(grid, ForecastLinesGrid))
        {
            AddForecastGroupItem(groupMenu, viewModel, "No grouping", MainWindowViewModel.ForecastGroupByNoneKey);
            AddForecastGroupItem(groupMenu, viewModel, "Group by task", MainWindowViewModel.ForecastGroupByTaskKey);
            AddForecastGroupItem(groupMenu, viewModel, "Group by resource", MainWindowViewModel.ForecastGroupByResourceKey);
            AddForecastGroupItem(groupMenu, viewModel, "Group by category", MainWindowViewModel.ForecastGroupByCategoryKey);
        }
        else if ((ReferenceEquals(grid, LedgerTransactionsGrid) || ReferenceEquals(grid, LedgerMonthlyPivotGrid))
            && string.Equals(viewModel.ActiveDetailWorkspaceKey, "Ledger Costs", StringComparison.OrdinalIgnoreCase))
        {
            var groupByMonth = new MenuItem
            {
                Header = "Group by month",
                IsCheckable = true,
                IsChecked = string.Equals(viewModel.SelectedDetailWorkspaceView?.ContentKey, "GroupByMonth", StringComparison.OrdinalIgnoreCase)
            };
            groupByMonth.Click += (_, _) =>
            {
                ExecuteAfterClosingMenu(groupByMonth, () =>
                {
                    viewModel.SetSelectedDetailWorkspaceContentKey(groupByMonth.IsChecked ? "GroupByMonth" : "Default");
                    QueueApplyCurrentDetailWorkspaceViewColumnState();
                });
            };
            groupMenu.Items.Add(groupByMonth);
        }
        else if ((ReferenceEquals(grid, RawTransactionsGrid) || ReferenceEquals(grid, RawTransactionsMonthlyPivotGrid))
            && string.Equals(viewModel.ActiveWorkspaceKey, "Raw Transactions", StringComparison.OrdinalIgnoreCase))
        {
            var groupByMonth = new MenuItem
            {
                Header = "Group by month",
                IsCheckable = true,
                IsChecked = viewModel.ShowRawTransactionsGroupedByMonth
            };
            groupByMonth.Click += (_, _) =>
            {
                ExecuteAfterClosingMenu(groupByMonth, () =>
                {
                    viewModel.SetSelectedWorkspaceContentKey(groupByMonth.IsChecked ? "GroupByMonth" : "Default");
                    QueueApplyCurrentWorkspaceViewColumnState();
                });
            };
            groupMenu.Items.Add(groupByMonth);
        }

        groupMenu.IsEnabled = groupMenu.Items.Count > 0;
        return groupMenu;
    }

    private static void AddForecastGroupItem(MenuItem groupMenu, MainWindowViewModel viewModel, string label, string groupByKey)
    {
        var item = new MenuItem
        {
            Header = label,
            IsCheckable = true,
            IsChecked = string.Equals(viewModel.ForecastGroupByKey, groupByKey, StringComparison.OrdinalIgnoreCase)
        };
        item.Click += (_, _) =>
        {
            SetSingleCheckedItem(item);
            ExecuteAfterClosingMenu(item, () => viewModel.ForecastGroupByKey = groupByKey);
        };
        groupMenu.Items.Add(item);
    }

    private void AddColumnSpecificMenuItems(ContextMenu menu, DataGrid grid, DataGridColumnHeader header, MainWindowViewModel? viewModel)
    {
        var iconMenu = new MenuItem { Header = "Icon", Icon = CreateMenuTextIcon(GridColumnPresentationState.GetIconGlyph(header.Column)) };
        foreach (var option in ColumnIconOptions)
        {
            var iconItem = new MenuItem
            {
                Header = option.Label,
                Icon = CreateMenuTextIcon(option.Glyph),
                IsCheckable = true,
                IsChecked = string.Equals(GridColumnPresentationState.GetIconGlyph(header.Column), option.Glyph, StringComparison.Ordinal),
                StaysOpenOnClick = true
            };
            iconItem.Click += (_, _) =>
            {
                SelectColumnIcon(header.Column, iconItem, option.Glyph);
            };
            AttachIconSelectionRightClick(iconItem, header.Column, option.Glyph);
            iconMenu.Items.Add(iconItem);
        }

        iconMenu.AddHandler(UIElement.PreviewMouseRightButtonDownEvent, new MouseButtonEventHandler((_, args) =>
        {
            if (args.OriginalSource is DependencyObject source && ReferenceEquals(FindParent<MenuItem>(source), iconMenu))
            {
                iconMenu.IsSubmenuOpen = true;
                Dispatcher.BeginInvoke(() => iconMenu.IsSubmenuOpen = true, DispatcherPriority.Input);
                args.Handled = true;
            }
        }), true);

        menu.Items.Add(iconMenu);
        menu.Items.Add(BuildColumnColourMenu(header.Column));
        if (header.Column.Header is not ForecastMonthColumnDefinition)
        {
            menu.Items.Add(BuildColumnHeaderColourMenu(grid, header.Column));
        }
        if (viewModel is not null
            && ReferenceEquals(grid, ForecastLinesGrid)
            && header.Column.Header is ForecastMonthColumnDefinition { IsTotal: false } monthColumn)
        {
            menu.Items.Add(BuildForecastHeaderColourMenu(viewModel, monthColumn));
        }

        if (viewModel is not null && IsHighlightableGrid(grid))
        {
            var highlightColumn = new MenuItem
            {
                Header = "Highlight column",
                IsCheckable = true,
                IsChecked = string.Equals(
                    GetHighlightKeyForGrid(grid, viewModel),
                    GetColumnPersistenceKey(header.Column),
                    StringComparison.OrdinalIgnoreCase)
            };
            highlightColumn.Click += (_, _) => ToggleForecastColumnHighlight(header.Column);
            menu.Items.Add(highlightColumn);

            var clearHighlight = new MenuItem
            {
                Header = "Clear highlighted column",
                IsEnabled = HasHighlightForGrid(grid, viewModel)
            };
            clearHighlight.Click += (_, _) =>
            {
                var previousKey = GetHighlightKeyForGrid(grid, viewModel);
                ClearHighlightForGrid(grid, viewModel);
                ApplyForecastColumnHighlightState(grid, previousKey);
            };
            menu.Items.Add(clearHighlight);
        }

        if (viewModel is not null && ReferenceEquals(grid, ForecastLinesGrid))
        {
            if (IsForecastMoneyColumn(header.Column))
            {
                var showCurrencySymbols = new MenuItem
                {
                    Header = "Show dollar symbols",
                    IsCheckable = true,
                    IsChecked = viewModel.ShowCurrencySymbols
                };
                showCurrencySymbols.Click += (_, _) => viewModel.ShowCurrencySymbols = showCurrencySymbols.IsChecked;
                menu.Items.Add(showCurrencySymbols);
            }

            if (header.Column.Header is ForecastMonthColumnDefinition { IsTotal: false })
            {
                var decimalPlacesMenu = new MenuItem { Header = "Million decimals" };
                for (var decimals = 0; decimals <= 4; decimals++)
                {
                    var decimalsValue = decimals;
                    var decimalItem = new MenuItem
                    {
                        Header = decimalsValue.ToString(CultureInfo.InvariantCulture),
                        IsCheckable = true,
                        IsChecked = viewModel.ForecastMonthMillionDecimals == decimalsValue
                    };
                    decimalItem.Click += (_, _) => viewModel.ForecastMonthMillionDecimals = decimalsValue;
                    decimalPlacesMenu.Items.Add(decimalItem);
                }

                menu.Items.Add(decimalPlacesMenu);
            }

            if (header.Column.Header?.ToString() is string varianceHeader
                && (string.Equals(varianceHeader, "Month Var", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(varianceHeader, "Budget Var", StringComparison.OrdinalIgnoreCase)))
            {
                var varianceIndicators = new MenuItem
                {
                    Header = "Variance indicators",
                    IsCheckable = true,
                    IsChecked = viewModel.ShowVarianceIndicators,
                    ToolTip = "Show positive variances in green and negative variances in red"
                };
                varianceIndicators.Click += (_, _) => viewModel.ShowVarianceIndicators = varianceIndicators.IsChecked;
                menu.Items.Add(varianceIndicators);
            }

            var freezeKey = GetForecastFreezeColumnKey(header.Column);
            if (!string.IsNullOrWhiteSpace(freezeKey))
            {
                var freezeColumn = new MenuItem
                {
                    Header = $"Freeze through {GetForecastColumnMenuLabel(header.Column)}",
                    IsCheckable = true,
                    IsChecked = string.Equals(viewModel.ForecastFreezeColumnKey, freezeKey, StringComparison.OrdinalIgnoreCase)
                };
                freezeColumn.Click += (_, _) => viewModel.SetForecastFreezeColumn(freezeKey);
                menu.Items.Add(freezeColumn);

                var resetFreeze = new MenuItem
                {
                    Header = "Reset freeze to forecast start",
                    IsEnabled = !string.Equals(viewModel.ForecastFreezeColumnKey, MainWindowViewModel.DefaultForecastFreezeColumnKey, StringComparison.OrdinalIgnoreCase)
                };
                resetFreeze.Click += (_, _) => viewModel.ResetForecastFreezeColumn();
                menu.Items.Add(resetFreeze);
            }
        }
    }

    private static bool IsForecastMoneyColumn(DataGridColumn column)
    {
        if (column.Header is ForecastMonthColumnDefinition)
        {
            return true;
        }

        var headerText = column.Header?.ToString() ?? string.Empty;
        return string.Equals(headerText, "CTD", StringComparison.OrdinalIgnoreCase)
               || string.Equals(headerText, "Month Cost", StringComparison.OrdinalIgnoreCase)
               || string.Equals(headerText, "Last Forecast", StringComparison.OrdinalIgnoreCase)
               || string.Equals(headerText, "Month Var", StringComparison.OrdinalIgnoreCase)
               || string.Equals(headerText, "CTC", StringComparison.OrdinalIgnoreCase)
               || string.Equals(headerText, "FCC", StringComparison.OrdinalIgnoreCase)
               || string.Equals(headerText, "Budget", StringComparison.OrdinalIgnoreCase)
               || string.Equals(headerText, "Budget Var", StringComparison.OrdinalIgnoreCase);
    }

    private MenuItem BuildForecastHeaderColourMenu(MainWindowViewModel viewModel, ForecastMonthColumnDefinition monthColumn)
    {
        var fiscalYear = FiscalPeriod.FiscalYearFromPeriodLabel(monthColumn.Key);
        var menu = new MenuItem { Header = "Header colour", Icon = CreateMenuTextIcon("C") };
        menu.Items.Add(BuildForecastHeaderColourTargetMenu(
            $"Calendar year {monthColumn.YearLabel}",
            viewModel.GetForecastCalendarYearHeaderColorHex(monthColumn.YearLabel),
            colorHex =>
            {
                viewModel.SetForecastCalendarYearHeaderColor(monthColumn.YearLabel, colorHex);
                QueueRebuildForecastYearBands();
            }));
        menu.Items.Add(BuildForecastHeaderColourTargetMenu(
            fiscalYear,
            viewModel.GetForecastFiscalYearHeaderColorHex(fiscalYear),
            colorHex =>
            {
                viewModel.SetForecastFiscalYearHeaderColor(fiscalYear, colorHex);
                QueueRebuildForecastYearBands();
            }));
        return menu;
    }

    private MenuItem BuildForecastHeaderColourTargetMenu(string label, string? selectedHex, Action<string?> apply)
    {
        var menu = new MenuItem { Header = label };
        foreach (var option in ColumnColourOptions)
        {
            var optionHex = string.Equals(option.Label, "Default", StringComparison.OrdinalIgnoreCase)
                ? null
                : option.HeaderHex;
            var item = new MenuItem
            {
                Header = option.Label,
                Icon = CreateColourSwatch(option.HeaderHex),
                IsCheckable = true,
                IsChecked = string.IsNullOrWhiteSpace(optionHex)
                    ? string.IsNullOrWhiteSpace(selectedHex)
                    : string.Equals(selectedHex, optionHex, StringComparison.OrdinalIgnoreCase)
            };
            item.Click += (_, _) =>
            {
                apply(optionHex);
                CloseContainingMenu(item);
            };
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());
        var customItem = new MenuItem { Header = "Custom..." };
        customItem.Click += (_, _) => ExecuteAfterClosingMenu(customItem, () =>
            OpenHeaderColorPicker(label, selectedHex, apply));
        menu.Items.Add(customItem);

        return menu;
    }

    private void ForecastYearBand_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string yearLabel } element
            || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var menu = CreateColumnContextMenu();
        AddSectionHeader(menu, "Calendar year");
        menu.Items.Add(BuildForecastHeaderColourTargetMenu(
            $"Calendar year {yearLabel}",
            viewModel.GetForecastCalendarYearHeaderColorHex(yearLabel),
            colorHex =>
            {
                viewModel.SetForecastCalendarYearHeaderColor(yearLabel, colorHex);
                QueueRebuildForecastYearBands();
            }));
        ApplyMenuIcons(menu);
        element.ContextMenu = menu;
        menu.PlacementTarget = element;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void AddSectionHeader(ContextMenu menu, string title)
    {
        menu.Items.Add(new MenuItem
        {
            Header = new TextBlock
            {
                Text = title.ToUpperInvariant(),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFactory.Frozen(0x94, 0xA3, 0xB8)
            },
            IsEnabled = false,
            Padding = new Thickness(12, 3, 12, 2)
        });
    }

    private void AddMenuSeparator(ContextMenu menu)
    {
        menu.Items.Add(new Separator());
    }

    private static void ApplyMenuIcons(ItemsControl menu)
    {
        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            if (item.Header is TextBlock)
            {
                continue;
            }

            item.Icon ??= CreateMenuTextIcon(GetMenuIconGlyph(item.Header?.ToString()));
            ApplyMenuIcons(item);
        }
    }

    private static void SetSingleCheckedItem(MenuItem selectedItem)
    {
        if (selectedItem.Parent is not ItemsControl parent)
        {
            return;
        }

        foreach (var sibling in parent.Items.OfType<MenuItem>().Where(item => item.IsCheckable))
        {
            sibling.IsChecked = ReferenceEquals(sibling, selectedItem);
        }
    }

    private static void ExecuteAfterClosingMenu(MenuItem item, Action action)
    {
        CloseContainingMenu(item);
        item.Dispatcher.BeginInvoke(action, DispatcherPriority.Input);
    }

    private static void CloseContainingMenu(MenuItem item)
    {
        for (ItemsControl? current = item.Parent as ItemsControl; current is not null;)
        {
            switch (current)
            {
                case MenuItem parentMenuItem:
                    parentMenuItem.IsSubmenuOpen = false;
                    current = parentMenuItem.Parent as ItemsControl;
                    break;
                case ContextMenu contextMenu:
                    contextMenu.IsOpen = false;
                    return;
                default:
                    return;
            }
        }
    }

    private void AttachIconSelectionRightClick(MenuItem item, DataGridColumn column, string glyph)
    {
        MouseButtonEventHandler handler = (_, args) =>
        {
            SelectColumnIcon(column, item, glyph);
            args.Handled = true;
        };

        item.AddHandler(UIElement.PreviewMouseRightButtonDownEvent, handler, true);
        item.AddHandler(UIElement.PreviewMouseRightButtonUpEvent, handler, true);
    }

    private void SelectColumnIcon(DataGridColumn column, MenuItem item, string glyph)
    {
        SetSingleCheckedItem(item);
        GridColumnPresentationState.SetIconGlyph(column, glyph);
        RefreshColumnPresentation(column);
        CloseContainingMenu(item);
    }

    private static string GetMenuIconGlyph(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return "•";
        }

        if (header.Contains("filter", StringComparison.OrdinalIgnoreCase))
        {
            return "⌕";
        }

        if (header.Contains("sort ascending", StringComparison.OrdinalIgnoreCase))
        {
            return "↑";
        }

        if (header.Contains("sort descending", StringComparison.OrdinalIgnoreCase))
        {
            return "↓";
        }

        if (header.Contains("sort", StringComparison.OrdinalIgnoreCase))
        {
            return "↕";
        }

        if (header.Contains("group", StringComparison.OrdinalIgnoreCase))
        {
            return "☷";
        }

        if (header.Contains("hide", StringComparison.OrdinalIgnoreCase))
        {
            return "◌";
        }

        if (header.Contains("highlight", StringComparison.OrdinalIgnoreCase))
        {
            return "▣";
        }

        if (header.Contains("freeze", StringComparison.OrdinalIgnoreCase))
        {
            return "⚑";
        }

        if (header.Contains("month", StringComparison.OrdinalIgnoreCase)
            || header.Contains("year", StringComparison.OrdinalIgnoreCase))
        {
            return "M";
        }

        if (header.Contains("expand", StringComparison.OrdinalIgnoreCase))
        {
            return "+";
        }

        if (header.Contains("collapse", StringComparison.OrdinalIgnoreCase))
        {
            return "-";
        }

        return "•";
    }
}
