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
    private void RebuildMonthlyPivotColumns()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        ConfigureMonthlyPivotGrid(RawTransactionsMonthlyPivotGrid, viewModel.RawTransactionsMonthlyPivotPeriods);
        ConfigureMonthlyPivotGrid(LedgerMonthlyPivotGrid, viewModel.LedgerMonthlyPivotPeriods);
        ConfigureCategoryMonthlyPivotGrid(CategoryMonthlyPivotGrid, viewModel.MonthlyPivotPeriods);
        ConfigureCustomPivotGrid(CustomPivotGrid, viewModel.PivotResultColumns);
        ApplyDefaultColumnPresentation(this);
        ApplyForecastColumnHighlightState();
        QueueApplyCurrentWorkspaceViewColumnState();
        QueueApplyCurrentDetailWorkspaceViewColumnState();
    }

    private void RebuildForecastGridColumns()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (FindResource("ForecastMonthColumnHeaderTemplate") is not DataTemplate headerTemplate)
        {
            return;
        }

        using (GridPerformanceDiagnostics.Measure("forecast-grid-column-rebuild"))
        {
            ConfigureForecastGrid(ForecastLinesGrid, viewModel, headerTemplate);
            ConfigureManagementResourceGrid(
                ManagementResourceAllocationGrid,
                viewModel,
                headerTemplate,
                ManagementResourceMetric.AllocationPercentage);
            ConfigureManagementResourceGrid(
                ManagementResourceHoursGrid,
                viewModel,
                headerTemplate,
                ManagementResourceMetric.Hours);
            ConfigureManagementResourceGrid(
                ManagementResourceCostGrid,
                viewModel,
                headerTemplate,
                ManagementResourceMetric.Cost);
        }

        ApplyDefaultColumnPresentation(ForecastLinesGrid);
        ApplyDefaultColumnPresentation(ManagementResourceAllocationGrid);
        ApplyDefaultColumnPresentation(ManagementResourceHoursGrid);
        ApplyDefaultColumnPresentation(ManagementResourceCostGrid);
        RefreshForecastColumnWidthSubscriptions();
        QueueSynchronizeManagementResourceGrids();
        ApplyForecastColumnHighlightState();
        QueueRebuildForecastYearBands();
        QueueRefreshForecastGroupHeaderPresenters();
        QueueSpreadsheetSelectionUpdate(ForecastLinesGrid, refreshAllVisuals: true);
        QueueApplyCurrentWorkspaceViewColumnState();
        RefreshForecastGridStatePills();
    }

    private void RebuildForecastYearBands()
    {
        if (!TryRebuildForecastYearBands())
        {
            QueueRebuildForecastYearBands();
        }
    }

    private bool TryRebuildForecastYearBands()
    {
        if (DataContext is not MainWindowViewModel viewModel || !viewModel.ShowCtcMonthForecastColumns)
        {
            ClearForecastOverlays();
            return true;
        }

        if (!ForecastLinesGrid.IsLoaded || !ForecastLinesGrid.IsVisible)
        {
            return true;
        }

        if (ForecastGridHost.ActualWidth <= 0)
        {
            return false;
        }

        var orderedColumns = ForecastLinesGrid.Columns
            .OrderBy(column => column.DisplayIndex)
            .Where(column => column.Visibility == Visibility.Visible)
            .ToList();

        if (orderedColumns.Count == 0)
        {
            ClearForecastOverlays();
            return true;
        }

        if (orderedColumns.Any(column => column.ActualWidth <= 0))
        {
            return false;
        }

        var horizontalOffset = _forecastGridScrollViewer?.HorizontalOffset ?? 0;
        var monthColumns = new List<(ForecastMonthColumnDefinition Definition, double Left, double Width, bool IsFrozen)>();
        var yearBandColumns = new List<(ForecastMonthColumnDefinition Definition, double Left, double Width)>();
        double? freezeBoundaryX = null;
        var frozenColumnCount = ForecastLinesGrid.FrozenColumnCount;
        var frozenWidth = 0d;
        var scrollableLeft = 0d;

        foreach (var column in orderedColumns)
        {
            var columnWidth = column.ActualWidth;
            if (columnWidth <= 0)
            {
                return false;
            }

            var isFrozenColumn = column.DisplayIndex < frozenColumnCount;
            var columnLeft = isFrozenColumn
                ? frozenWidth
                : frozenWidth + scrollableLeft - horizontalOffset;

            if (column.Header is ForecastMonthColumnDefinition definition)
            {
                yearBandColumns.Add((definition, columnLeft, columnWidth));
                if (!definition.IsTotal)
                {
                    monthColumns.Add((definition, columnLeft, columnWidth, isFrozenColumn));
                }
            }

            if (isFrozenColumn)
            {
                frozenWidth += columnWidth;
                freezeBoundaryX = frozenWidth;
            }
            else
            {
                scrollableLeft += columnWidth;
            }
        }

        if (monthColumns.Count == 0)
        {
            ClearForecastOverlays();
            return true;
        }

        var overlaySignature = BuildForecastOverlayGeometrySignature(
            orderedColumns,
            horizontalOffset,
            ForecastGridHost.ActualWidth,
            ForecastGridHost.ActualHeight,
            frozenColumnCount);
        if (string.Equals(overlaySignature, _forecastOverlayGeometrySignature, StringComparison.Ordinal))
        {
            return true;
        }

        using var overlayMeasure = GridPerformanceDiagnostics.Measure(
            "forecast-overlay-rebuild",
            minimumMillisecondsToLog: 8,
            sampleRate: 100);
        _forecastOverlayGeometrySignature = overlaySignature;
        _forecastOverlaysCleared = false;

        ForecastYearBandCanvas.Children.Clear();
        ForecastFreezeBoundaryCanvas.Children.Clear();
        ForecastYearBandCanvas.Width = ForecastGridHost.ActualWidth;
        ForecastYearBandCanvas.Height = ForecastYearBandHeight;
        ForecastYearBandCanvas.Visibility = Visibility.Visible;
        ForecastFreezeBoundaryCanvas.Width = ForecastGridHost.ActualWidth;
        ForecastFreezeBoundaryCanvas.Height = ForecastGridHost.ActualHeight;
        ForecastFreezeBoundaryCanvas.Visibility = Visibility.Visible;

        var groupedHeaders = yearBandColumns
            .GroupBy(item => item.Definition.YearLabel)
            .ToList();
        var yearBandClipLeft = Math.Max(0, freezeBoundaryX ?? 0);
        ForecastYearBandCanvas.Clip = new RectangleGeometry(new Rect(
            yearBandClipLeft,
            0,
            Math.Max(0, ForecastGridHost.ActualWidth - yearBandClipLeft),
            ForecastYearBandHeight));

        foreach (var group in groupedHeaders)
        {
            var columns = group.ToList();
            var left = columns.First().Left;
            var right = columns.Last().Left + columns.Last().Width;
            var width = Math.Max(0, right - left);
            if (width <= 0)
            {
                continue;
            }

            var band = new Border
            {
                Width = width,
                Height = ForecastYearBandHeight,
                Background = BrushFactory.Frozen(0xF8, 0xFA, 0xFC),
                BorderBrush = BrushFactory.Frozen(0x94, 0xA3, 0xB8),
                BorderThickness = new Thickness(1, 1, 1, 1),
                Child = new TextBlock
                {
                    Text = $"Calendar year {group.Key}",
                    FontWeight = FontWeights.Bold,
                    Foreground = BrushFactory.Frozen(0x0F, 0x17, 0x2A),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                }
            };

            Canvas.SetLeft(band, left);
            Canvas.SetTop(band, 0);
            ForecastYearBandCanvas.Children.Add(band);
        }

        var gridGuideHeight = Math.Max(0, ForecastGridHost.ActualHeight);
        var rowGuideTop = ForecastYearBandHeight;
        foreach (var column in monthColumns)
        {
            if (column.Definition.RightDashedSeparatorVisibility != Visibility.Visible)
            {
                continue;
            }

            var right = column.Left + column.Width;
            if ((!column.IsFrozen && right <= yearBandClipLeft) || right < 0 || right > ForecastGridHost.ActualWidth)
            {
                continue;
            }

            var dashedLine = new Line
            {
                X1 = right,
                X2 = right,
                Y1 = rowGuideTop,
                Y2 = gridGuideHeight,
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 3, 2 },
                SnapsToDevicePixels = true
            };

            ForecastFreezeBoundaryCanvas.Children.Add(dashedLine);
        }

        foreach (var group in groupedHeaders)
        {
            var columns = group.ToList();
            var right = columns.Last().Left + columns.Last().Width;
            if (right <= yearBandClipLeft || right < 0 || right > ForecastGridHost.ActualWidth)
            {
                continue;
            }

            var calendarBoundary = new Rectangle
            {
                Width = 3,
                Height = gridGuideHeight,
                Fill = BrushFactory.Frozen(0x94, 0xA3, 0xB8),
                IsHitTestVisible = false
            };

            Canvas.SetLeft(calendarBoundary, right - 1.5);
            Canvas.SetTop(calendarBoundary, 0);
            ForecastFreezeBoundaryCanvas.Children.Add(calendarBoundary);
        }

        if (freezeBoundaryX is double boundaryX
            && boundaryX >= 0
            && boundaryX <= ForecastGridHost.ActualWidth)
        {
            var topFreezeLine = new Rectangle
            {
                Width = 4,
                Height = ForecastYearBandCanvas.Height,
                Fill = BrushFactory.Frozen(0x25, 0x63, 0xEB),
                IsHitTestVisible = false
            };

            Canvas.SetLeft(topFreezeLine, boundaryX - 2);
            Canvas.SetTop(topFreezeLine, 0);
            ForecastYearBandCanvas.Children.Add(topFreezeLine);

            var fullHeightFreezeLine = new Rectangle
            {
                Width = 4,
                Height = gridGuideHeight,
                Fill = BrushFactory.Frozen(0x25, 0x63, 0xEB),
                Opacity = 0.9,
                IsHitTestVisible = false
            };

            Canvas.SetLeft(fullHeightFreezeLine, boundaryX - 2);
            Canvas.SetTop(fullHeightFreezeLine, 0);
            ForecastFreezeBoundaryCanvas.Children.Add(fullHeightFreezeLine);

            var label = new Border
            {
                Background = BrushFactory.Frozen(0x25, 0x63, 0xEB),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 2, 5, 2),
                Child = new TextBlock
                {
                    Text = "Frozen",
                    Foreground = Brushes.White,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold
                }
            };

            Canvas.SetLeft(label, Math.Max(0, boundaryX + 6));
            Canvas.SetTop(label, 4);
            ForecastFreezeBoundaryCanvas.Children.Add(label);
        }

        return true;
    }

    private void QueueRebuildForecastYearBands()
    {
        if (_forecastYearBandRebuildQueued)
        {
            return;
        }

        _forecastYearBandRebuildQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            _forecastYearBandRebuildQueued = false;
            RebuildForecastYearBands();
        }));
    }

    private void RefreshForecastColumnWidthSubscriptions()
    {
        var currentColumns = ForecastLinesGrid.Columns.ToHashSet();
        foreach (var column in _trackedForecastColumns.Where(column => !currentColumns.Contains(column)).ToList())
        {
            ForecastColumnActualWidthDescriptor?.RemoveValueChanged(column, ForecastColumnWidthChanged);
            _trackedForecastColumns.Remove(column);
        }

        foreach (var column in currentColumns.Where(column => !_trackedForecastColumns.Contains(column)))
        {
            ForecastColumnActualWidthDescriptor?.AddValueChanged(column, ForecastColumnWidthChanged);
            _trackedForecastColumns.Add(column);
        }
    }

    private void ForecastColumnWidthChanged(object? sender, EventArgs e)
    {
        QueueRebuildForecastYearBands();
        QueueRefreshForecastGroupHeaderPresenters();
    }

    private void ClearForecastOverlays()
    {
        if (_forecastOverlaysCleared)
        {
            return;
        }

        ForecastYearBandCanvas.Children.Clear();
        ForecastYearBandCanvas.Visibility = Visibility.Collapsed;
        ForecastYearBandCanvas.Clip = null;
        ForecastFreezeBoundaryCanvas.Children.Clear();
        ForecastFreezeBoundaryCanvas.Visibility = Visibility.Collapsed;
        _forecastOverlayGeometrySignature = string.Empty;
        _forecastOverlaysCleared = true;
        GridPerformanceDiagnostics.Count("forecast-overlay-clear");
    }

    private static string BuildForecastOverlayGeometrySignature(
        IReadOnlyList<DataGridColumn> columns,
        double horizontalOffset,
        double hostWidth,
        double hostHeight,
        int frozenColumnCount)
    {
        return $"{horizontalOffset:F2}|{hostWidth:F2}|{hostHeight:F2}|{frozenColumnCount}|"
               + string.Join('|', columns.Select(column =>
                   $"{column.DisplayIndex}:{column.Visibility}:{column.ActualWidth:F2}:{column.Header}"));
    }

    private static void ConfigureMonthlyPivotGrid(DataGrid grid, IEnumerable<string> periods)
    {
        grid.Columns.Clear();
        grid.Columns.Add(new DataGridTextColumn { Header = "Task", Binding = new Binding(nameof(MonthlyPivotRow.TaskNumber)), Width = 115 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Resource", Binding = new Binding(nameof(MonthlyPivotRow.ResourceName)), Width = 220 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Group", Binding = new Binding(nameof(MonthlyPivotRow.ProjectCode)), Width = 130 });

        foreach (var period in periods)
        {
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = period,
                Binding = BuildAccountingBinding($"[{period}]"),
                Width = 88
            });
        }

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Total",
            Binding = BuildAccountingBinding(nameof(MonthlyPivotRow.Total)),
            Width = 105
        });
    }

    private static void ConfigureCategoryMonthlyPivotGrid(DataGrid grid, IEnumerable<string> periods)
    {
        grid.Columns.Clear();
        grid.Columns.Add(new DataGridTextColumn { Header = "Category", Binding = new Binding(nameof(MonthlyPivotRow.ProjectCode)), Width = 180 });
        foreach (var period in periods)
        {
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = period,
                Binding = BuildAccountingBinding($"[{period}]"),
                Width = 88
            });
        }

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Total",
            Binding = BuildAccountingBinding(nameof(MonthlyPivotRow.Total)),
            Width = 110
        });
    }

    private void ConfigureSelectedMonthlyForecastGrid()
    {
        SelectedMonthlyForecastsGrid.Columns.Clear();
        SelectedMonthlyForecastsGrid.Columns.Add(CreateSelectableReadOnlyTextColumn("Period", nameof(MonthlyForecast.PeriodLabel), 90));
        SelectedMonthlyForecastsGrid.Columns.Add(CreateSelectableReadOnlyTextColumn("Start", nameof(MonthlyForecast.PeriodStartDate), 100));

        var forecastColumn = new DataGridTemplateColumn
        {
            Header = "Forecast",
            Width = 120,
            SortMemberPath = nameof(MonthlyForecast.Amount),
            CellTemplate = CreateSelectableReadOnlyTextTemplate(new Binding(nameof(MonthlyForecast.Amount))
            {
                Mode = BindingMode.OneWay,
                StringFormat = "{0:F2}"
            }),
            CellEditingTemplate = CreateMonthlyForecastEditingTemplate()
        };

        SelectedMonthlyForecastsGrid.Columns.Add(forecastColumn);
        ApplyDefaultColumnPresentation(SelectedMonthlyForecastsGrid);
    }

    private static void ConfigureCustomPivotGrid(DataGrid grid, IEnumerable<PivotResultColumn> columns)
    {
        grid.Columns.Clear();
        foreach (var column in columns)
        {
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = column.Header,
                Binding = column.IsNumeric ? BuildAccountingBinding($"[{column.Key}]") : new Binding($"[{column.Key}]"),
                Width = column.IsNumeric ? 120 : 150,
                IsReadOnly = true
            });
        }
    }

    private static DataGridTemplateColumn CreateManualLineIndicatorColumn()
    {
        var textFactory = new FrameworkElementFactory(typeof(TextBlock));
        textFactory.SetValue(TextBlock.TextProperty, "!");
        textFactory.SetValue(TextBlock.ForegroundProperty, BrushFactory.Frozen(0xDC, 0x26, 0x26));
        textFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
        textFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        textFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        textFactory.SetValue(TextBlock.ToolTipProperty, "Manually added line - did not come from imported raw data");
        textFactory.SetBinding(TextBlock.VisibilityProperty, new Binding(nameof(ForecastLine.IsManuallyAdded))
        {
            Converter = new BooleanToVisibilityConverter()
        });

        return new DataGridTemplateColumn
        {
            Header = "!",
            Width = 26,
            IsReadOnly = true,
            CanUserResize = false,
            CellTemplate = new DataTemplate { VisualTree = textFactory }
        };
    }

    private static DataGridTemplateColumn CreateForecastCommentsColumn()
    {
        var content = new FrameworkElementFactory(typeof(ContentControl));
        content.SetBinding(ContentControl.ContentProperty, new Binding()
        {
            Converter = new FormattedCommentConverter()
        });
        return new DataGridTemplateColumn
        {
            Header = "All Month Comments",
            Width = 320,
            IsReadOnly = true,
            SortMemberPath = nameof(ForecastLine.AllMonthComments),
            CellTemplate = new DataTemplate { VisualTree = content }
        };
    }

    private void ConfigureForecastGrid(DataGrid grid, MainWindowViewModel viewModel, DataTemplate monthHeaderTemplate)
    {
        ConfigureForecastGridPerformance(grid);
        grid.Columns.Clear();
        var freezeCandidates = new List<(DataGridColumn Column, string FreezeKey)>();

        grid.Columns.Add(CreateManualLineIndicatorColumn());
        AddForecastGridColumn(grid, freezeCandidates, ApplyMinimumWidth(CreateReadOnlyTextColumn("Task", nameof(ForecastLine.TaskNumber), 110, numeric: false, leftPadding: 33), 90), MainWindowViewModel.ForecastFreezeTaskKey);
        AddForecastGridColumn(grid, freezeCandidates, ApplyMinimumWidth(CreateEditableTextColumn("Resource", nameof(ForecastLine.ResourceName), 220), 140), MainWindowViewModel.ForecastFreezeResourceKey);
        AddForecastGridColumn(grid, freezeCandidates, ApplyMinimumWidth(CreateReadOnlyTextColumn("Category", nameof(ForecastLine.ProjectCode), 160, numeric: false), 110), MainWindowViewModel.ForecastFreezeCategoryKey);
        AddForecastGridColumn(grid, freezeCandidates, ApplyMinimumWidth(CreateReadOnlyTextColumn("CTD", BuildAccountingBinding(nameof(ForecastLine.CostToDateSummary)), 90), 72), MainWindowViewModel.ForecastFreezeCostToDateKey);
        AddForecastGridColumn(grid, freezeCandidates, ApplyMinimumWidth(CreateReadOnlyTextColumn("Month Cost", BuildAccountingBinding(nameof(ForecastLine.CurrentMonthCost)), 95), 78), MainWindowViewModel.ForecastFreezeMonthCostKey);
        AddForecastGridColumn(grid, freezeCandidates, ApplyMinimumWidth(CreateReadOnlyTextColumn("Last Forecast", BuildAccountingBinding(nameof(ForecastLine.LastMonthForecast)), 105), 84), MainWindowViewModel.ForecastFreezeLastForecastKey);
        var monthVarianceColumn = CreateReadOnlyTextColumn("Month Var", BuildAccountingBinding(nameof(ForecastLine.VarianceLastMonthToDate)), 95);
        monthVarianceColumn.MinWidth = 78;
        ApplyVarianceIndicatorStyle(monthVarianceColumn, viewModel, nameof(ForecastLine.VarianceLastMonthToDate));
        AddForecastGridColumn(grid, freezeCandidates, monthVarianceColumn, MainWindowViewModel.ForecastFreezeMonthVarianceKey);
        AddForecastGridColumn(grid, freezeCandidates, ApplyMinimumWidth(CreateReadOnlyTextColumn("CTC", BuildAccountingBinding(nameof(ForecastLine.TotalForecastCtc)), 105), 84), MainWindowViewModel.ForecastFreezeCtcKey);
        AddForecastGridColumn(grid, freezeCandidates, ApplyMinimumWidth(CreateReadOnlyTextColumn("FCC", BuildAccountingBinding(nameof(ForecastLine.PlannedCostFcc)), 105), 84), MainWindowViewModel.ForecastFreezeFccKey);
        AddForecastGridColumn(grid, freezeCandidates, ApplyMinimumWidth(CreateReadOnlyTextColumn("Budget", BuildAccountingBinding(nameof(ForecastLine.Budget)), 105), 84), MainWindowViewModel.ForecastFreezeBudgetKey);
        var budgetVarianceColumn = new DataGridTextColumn
        {
            Header = "Budget Var",
            Binding = BuildAccountingBinding(nameof(ForecastLine.TotalBudgetVariance)),
            Width = 105,
            IsReadOnly = true,
            ElementStyle = CreateNumericTextStyle()
        };
        budgetVarianceColumn.MinWidth = 84;
        ApplyVarianceIndicatorStyle(budgetVarianceColumn, viewModel, nameof(ForecastLine.TotalBudgetVariance));
        AddForecastGridColumn(grid, freezeCandidates, budgetVarianceColumn, MainWindowViewModel.ForecastFreezeBudgetVarianceKey);
        grid.Columns.Add(ApplyMinimumWidth(CreateForecastCommentsColumn(), 180));
        ApplyForecastFixedHeaderGradient(grid.Columns);

        grid.CellEditEnding -= ForecastLinesGrid_CellEditEnding;
        grid.CellEditEnding += ForecastLinesGrid_CellEditEnding;

        if (!viewModel.ShowCtcMonthForecastColumns)
        {
            grid.FrozenColumnCount = 0;
            return;
        }

        foreach (var columnDefinition in viewModel.CtcMonthForecastColumns)
        {
            var column = new DataGridTemplateColumn
            {
                Header = columnDefinition,
                HeaderTemplate = monthHeaderTemplate,
                HeaderStyle = CreateForecastMonthHeaderStyle(),
                Width = columnDefinition.IsTotal ? 110 : 88,
                MinWidth = columnDefinition.IsTotal ? 84 : 64,
                IsReadOnly = columnDefinition.IsTotal || !columnDefinition.IsEditable,
                CellStyle = CreateForecastMonthCellStyle(columnDefinition)
            };
            column.CellTemplate = CreateForecastMonthDisplayTemplate(columnDefinition, viewModel.ShowForecastZeroAsBlank);
            if (!columnDefinition.IsTotal && columnDefinition.IsEditable)
            {
                column.CellEditingTemplate = CreateForecastMonthEditingTemplate(columnDefinition, viewModel.ShowForecastZeroAsBlank);
            }

            AddForecastGridColumn(grid, freezeCandidates, column, GetForecastFreezeColumnKey(columnDefinition));
        }

        ApplyForecastFreezeBoundary(grid, viewModel, freezeCandidates);
    }

    private static void ApplyForecastFixedHeaderGradient(IEnumerable<DataGridColumn> columns)
    {
        var gradient = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0),
            EndPoint = new Point(0.5, 1)
        };
        gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0xD9, 0xE5, 0xF2), 0));
        gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0xB7, 0xC9, 0xDC), 0.48));
        gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0xD7, 0xE2, 0xEE), 1));
        gradient.Freeze();

        foreach (var column in columns)
        {
            GridColumnPresentationState.SetHeaderBackground(column, gradient);
            GridColumnPresentationState.SetBaseHeaderBackground(column, gradient);
        }
    }

    private void ConfigureManagementResourceGrid(
        DataGrid grid,
        MainWindowViewModel viewModel,
        DataTemplate monthHeaderTemplate,
        ManagementResourceMetric metric)
    {
        ConfigureForecastGridPerformance(grid);
        grid.Columns.Clear();
        var taskColumn = CreateSelectableReadOnlyTextColumn("Task", nameof(ManagementResourceTableRow.TaskNumber), 110);
        grid.Columns.Add(taskColumn);
        var resourceColumn = CreateSelectableReadOnlyTextColumn("Resource", nameof(ManagementResourceTableRow.ResourceName), 220);
        grid.Columns.Add(resourceColumn);
        var categoryColumn = CreateSelectableReadOnlyTextColumn("Category", nameof(ManagementResourceTableRow.ProjectCode), 160);
        grid.Columns.Add(categoryColumn);
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Rate",
            Binding = new Binding(nameof(ManagementResourceTableRow.HourlyRate))
            {
                Mode = metric == ManagementResourceMetric.AllocationPercentage ? BindingMode.TwoWay : BindingMode.OneWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
                StringFormat = "{0:C2}"
            },
            Width = 88,
            IsReadOnly = metric != ManagementResourceMetric.AllocationPercentage
        });
        var ctdColumn = CreateReadOnlyTextColumn(
            "CTD",
            BuildAccountingBinding("Resource.SourceLine.CostToDateSummary"),
            90);
        grid.Columns.Add(ctdColumn);
        var monthCostColumn = CreateReadOnlyTextColumn(
            "Month Cost",
            BuildAccountingBinding("Resource.SourceLine.CurrentMonthCost"),
            95);
        grid.Columns.Add(monthCostColumn);
        var lastForecastColumn = CreateReadOnlyTextColumn(
            "Last Forecast",
            BuildAccountingBinding("Resource.SourceLine.LastMonthForecast"),
            105);
        grid.Columns.Add(lastForecastColumn);
        var monthVarianceColumn = CreateReadOnlyTextColumn(
            "Month Var",
            BuildAccountingBinding("Resource.SourceLine.VarianceLastMonthToDate"),
            95);
        ApplyVarianceIndicatorStyle(monthVarianceColumn, viewModel, "Resource.SourceLine.VarianceLastMonthToDate");
        grid.Columns.Add(monthVarianceColumn);
        var ctcColumn = CreateReadOnlyTextColumn(
            "CTC",
            BuildAccountingBinding("Resource.SourceLine.TotalForecastCtc"),
            105);
        grid.Columns.Add(ctcColumn);
        var fccColumn = CreateReadOnlyTextColumn(
            "FCC",
            BuildAccountingBinding("Resource.SourceLine.PlannedCostFcc"),
            105);
        grid.Columns.Add(fccColumn);
        var budgetColumn = CreateReadOnlyTextColumn(
            "Budget",
            BuildAccountingBinding("Resource.SourceLine.Budget"),
            105);
        grid.Columns.Add(budgetColumn);
        var budgetVarianceColumn = CreateReadOnlyTextColumn(
            "Budget Var",
            BuildAccountingBinding("Resource.SourceLine.TotalBudgetVariance"),
            105);
        ApplyVarianceIndicatorStyle(budgetVarianceColumn, viewModel, "Resource.SourceLine.TotalBudgetVariance");
        grid.Columns.Add(budgetVarianceColumn);
        var commentsColumn = CreateReadOnlyTextColumn(
            "All Month Comments",
            "Resource.SourceLine.AllMonthComments",
            320,
            numeric: false);
        grid.Columns.Add(commentsColumn);

        foreach (var columnDefinition in viewModel.CtcMonthForecastColumns)
        {
            var editable = metric == ManagementResourceMetric.AllocationPercentage
                && !columnDefinition.IsTotal
                && columnDefinition.IsEditable;
            var column = new DataGridTemplateColumn
            {
                Header = columnDefinition,
                HeaderTemplate = monthHeaderTemplate,
                HeaderStyle = CreateForecastMonthHeaderStyle(),
                Width = columnDefinition.IsTotal ? 110 : 88,
                IsReadOnly = !editable,
                SortMemberPath = $"[{columnDefinition.Key}]",
                CellStyle = CreateForecastMonthCellStyle(columnDefinition),
                CellTemplate = CreateManagementResourceMonthDisplayTemplate(columnDefinition, metric)
            };
            if (editable)
            {
                column.CellEditingTemplate = CreateManagementResourceAllocationEditingTemplate(columnDefinition);
            }

            grid.Columns.Add(column);
        }

        grid.FrozenColumnCount = Math.Min(4, grid.Columns.Count);
        ApplyManagementResourceColumnWidthModel(grid);
    }

    private static void ConfigureForecastGridPerformance(DataGrid grid)
    {
        grid.AutoGenerateColumns = false;
        grid.EnableRowVirtualization = true;
        grid.EnableColumnVirtualization = false;
        grid.CanUserResizeColumns = true;
        VirtualizingPanel.SetIsVirtualizing(grid, true);
        VirtualizingPanel.SetIsVirtualizingWhenGrouping(grid, true);
        VirtualizingPanel.SetVirtualizationMode(grid, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(grid, true);
    }

    private static DataTemplate CreateManagementResourceMonthDisplayTemplate(
        ForecastMonthColumnDefinition columnDefinition,
        ManagementResourceMetric metric)
    {
        var template = new DataTemplate();
        var grid = new FrameworkElementFactory(typeof(Grid));
        var text = new FrameworkElementFactory(typeof(TextBlock));
        var binding = new Binding($"[{columnDefinition.Key}]");
        if (metric == ManagementResourceMetric.Cost)
        {
            binding.Converter = AccountingConverter;
        }
        else
        {
            binding.StringFormat = metric == ManagementResourceMetric.AllocationPercentage
                ? "{0:0.##}%"
                : "{0:0.##}";
        }

        text.SetBinding(TextBlock.TextProperty, binding);
        text.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        text.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Right);
        text.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        text.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        text.SetValue(TextBlock.PaddingProperty, new Thickness(4, 3, 4, 3));
        grid.AppendChild(text);
        AppendSeparatorElements(grid, columnDefinition);
        template.VisualTree = grid;
        return template;
    }

    private static DataTemplate CreateManagementResourceAllocationEditingTemplate(ForecastMonthColumnDefinition columnDefinition)
    {
        var template = new DataTemplate();
        var grid = new FrameworkElementFactory(typeof(Grid));
        var editor = new FrameworkElementFactory(typeof(TextBox));
        editor.SetBinding(TextBox.TextProperty, new Binding($"[{columnDefinition.Key}]")
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            StringFormat = "{0:0.##}"
        });
        editor.SetValue(Control.BorderThicknessProperty, new Thickness(0));
        editor.SetValue(Control.BackgroundProperty, Brushes.Transparent);
        editor.SetValue(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Right);
        editor.SetValue(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center);
        editor.SetValue(Control.PaddingProperty, new Thickness(4, 3, 4, 3));
        grid.AppendChild(editor);
        AppendSeparatorElements(grid, columnDefinition);
        template.VisualTree = grid;
        return template;
    }

    private static void AddForecastGridColumn(DataGrid grid, ICollection<(DataGridColumn Column, string FreezeKey)> freezeCandidates, DataGridColumn column, string freezeKey)
    {
        grid.Columns.Add(column);
        freezeCandidates.Add((column, freezeKey));
    }

    private static T ApplyMinimumWidth<T>(T column, double minWidth) where T : DataGridColumn
    {
        column.MinWidth = minWidth;
        return column;
    }

    private static void ApplyForecastFreezeBoundary(DataGrid grid, MainWindowViewModel viewModel, IReadOnlyList<(DataGridColumn Column, string FreezeKey)> freezeCandidates)
    {
        if (freezeCandidates.Count == 0)
        {
            grid.FrozenColumnCount = 0;
            return;
        }

        var selectedKey = string.IsNullOrWhiteSpace(viewModel.ForecastFreezeColumnKey)
            ? MainWindowViewModel.DefaultForecastFreezeColumnKey
            : viewModel.ForecastFreezeColumnKey;
        var visibleCandidates = freezeCandidates
            .Where(candidate => candidate.Column.Visibility == Visibility.Visible)
            .OrderBy(candidate => candidate.Column.DisplayIndex)
            .ToList();
        if (visibleCandidates.Count == 0)
        {
            grid.FrozenColumnCount = 0;
            return;
        }

        var selectedColumn = freezeCandidates
            .FirstOrDefault(candidate => string.Equals(candidate.FreezeKey, selectedKey, StringComparison.OrdinalIgnoreCase));
        var target = visibleCandidates
            .FirstOrDefault(candidate => string.Equals(candidate.FreezeKey, selectedKey, StringComparison.OrdinalIgnoreCase));

        if (target.Column is null && selectedColumn.Column is not null)
        {
            target = visibleCandidates
                .Where(candidate => candidate.Column.DisplayIndex < selectedColumn.Column.DisplayIndex)
                .LastOrDefault();
        }

        if (target.Column is null)
        {
            target = visibleCandidates
                .FirstOrDefault(candidate => string.Equals(candidate.FreezeKey, MainWindowViewModel.DefaultForecastFreezeColumnKey, StringComparison.OrdinalIgnoreCase));
        }

        if (target.Column is null)
        {
            target = visibleCandidates[0];
        }

        grid.FrozenColumnCount = Math.Clamp(target.Column.DisplayIndex + 1, 0, grid.Columns.Count);
    }

    private static DataGridTextColumn CreateReadOnlyTextColumn(string header, string path, double width, bool numeric = true, double leftPadding = 4)
    {
        return new DataGridTextColumn
        {
            Header = header,
            Binding = new Binding(path),
            Width = width,
            IsReadOnly = true,
            ElementStyle = numeric ? CreateNumericTextStyle() : CreatePlainTextStyle(leftPadding)
        };
    }

    private static DataGridTextColumn CreateEditableTextColumn(string header, string path, double width)
    {
        return new DataGridTextColumn
        {
            Header = header,
            Binding = new Binding(path)
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
            },
            Width = width,
            IsReadOnly = false,
            ElementStyle = CreatePlainTextStyle(),
            EditingElementStyle = CreateEditingTextStyle()
        };
    }

    private static DataGridTextColumn CreateReadOnlyTextColumn(string header, Binding binding, double width)
    {
        return new DataGridTextColumn
        {
            Header = header,
            Binding = binding,
            Width = width,
            IsReadOnly = true,
            ElementStyle = CreateNumericTextStyle()
        };
    }

    private static Style CreateNumericTextStyle()
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
        style.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
        style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(4, 3, 6, 3)));
        return style;
    }

    private static Style CreatePlainTextStyle(double leftPadding = 4)
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
        style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(leftPadding, 3, 4, 3)));
        style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
        return style;
    }

    private static Style CreateEditingTextStyle()
    {
        var style = new Style(typeof(TextBox));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4, 3, 4, 3)));
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        return style;
    }

    private static void ApplyVarianceIndicatorStyle(DataGridTextColumn column, MainWindowViewModel viewModel, string propertyPath)
    {
        if (!viewModel.ShowVarianceIndicators)
        {
            return;
        }

        var style = CreateNumericTextStyle();
        style.Setters.Add(new Setter(TextBlock.ForegroundProperty, new Binding(propertyPath)
        {
            Converter = VarianceBrushConverter.Instance
        }));
        column.ElementStyle = style;
    }

    private DataGridTemplateColumn CreateEditableSelectableTextColumn(string header, string path, double width)
    {
        var column = CreateSelectableReadOnlyTextColumn(header, path, width);
        column.IsReadOnly = false;
        var editorFactory = new FrameworkElementFactory(typeof(TextBox));
        editorFactory.SetBinding(TextBox.TextProperty, new Binding(path)
        {
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        column.CellEditingTemplate = new DataTemplate { VisualTree = editorFactory };
        return column;
    }

    private void ForecastLinesGrid_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Commit
            && e.Column?.Header is string header
            && string.Equals(header, "Resource", StringComparison.Ordinal)
            && DataContext is MainWindowViewModel viewModel)
        {
            Dispatcher.BeginInvoke(() => viewModel.RecalculateCommand.Execute(null), DispatcherPriority.Background);
        }
    }

    private DataGridTemplateColumn CreateSelectableReadOnlyTextColumn(string header, string path, double width, double leftPadding = 4)
    {
        return new DataGridTemplateColumn
        {
            Header = header,
            Width = width,
            IsReadOnly = true,
            SortMemberPath = path,
            CellTemplate = CreateSelectableReadOnlyTextTemplate(path, leftPadding)
        };
    }

    private DataTemplate CreateSelectableReadOnlyTextTemplate(string path, double leftPadding = 4)
    {
        return CreateSelectableReadOnlyTextTemplate(new Binding(path) { Mode = BindingMode.OneWay }, leftPadding);
    }

    private DataTemplate CreateSelectableReadOnlyTextTemplate(BindingBase binding, double leftPadding = 4)
    {
        var template = new DataTemplate();
        var textBox = new FrameworkElementFactory(typeof(TextBox));
        textBox.SetBinding(TextBox.TextProperty, binding);
        textBox.SetValue(TextBox.TagProperty, "SelectableReadOnlyForecastText");
        textBox.SetValue(TextBox.IsReadOnlyProperty, true);
        textBox.SetValue(TextBox.IsReadOnlyCaretVisibleProperty, true);
        textBox.SetValue(TextBoxBase.SelectionBrushProperty, BrushFactory.Frozen(0xBF, 0xDB, 0xFE));
        textBox.SetValue(TextBoxBase.SelectionTextBrushProperty, BrushFactory.Frozen(0x0F, 0x17, 0x2A));
        textBox.SetValue(Control.BorderThicknessProperty, new Thickness(0));
        textBox.SetValue(Control.BackgroundProperty, Brushes.Transparent);
        textBox.SetValue(Control.PaddingProperty, new Thickness(leftPadding, 3, 4, 3));
        textBox.SetValue(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center);
        textBox.SetValue(TextBox.TextWrappingProperty, TextWrapping.NoWrap);
        template.VisualTree = textBox;
        return template;
    }

    private DataTemplate CreateMonthlyForecastEditingTemplate()
    {
        var template = new DataTemplate();
        var textBox = new FrameworkElementFactory(typeof(TextBox));
        textBox.SetBinding(TextBox.TextProperty, new Binding(nameof(MonthlyForecast.Amount))
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
            StringFormat = "{0:F2}",
            Converter = ForecastAmountTextConverter.Instance
        });
        textBox.SetValue(TextBox.TagProperty, "SelectableReadOnlyForecastText");
        textBox.SetValue(TextBoxBase.SelectionBrushProperty, BrushFactory.Frozen(0xBF, 0xDB, 0xFE));
        textBox.SetValue(TextBoxBase.SelectionTextBrushProperty, BrushFactory.Frozen(0x0F, 0x17, 0x2A));
        textBox.SetValue(Control.PaddingProperty, new Thickness(4, 3, 4, 3));
        textBox.SetValue(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center);
        template.VisualTree = textBox;
        return template;
    }

    private void SelectableReadOnlyForecastText_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        e.Handled = true;
        OpenSelectableForecastTextContextMenu(textBox);
    }

    private void SelectableForecastText_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_gridRightDragging || sender is not TextBox textBox)
        {
            return;
        }

        OpenSelectableForecastTextContextMenu(textBox);
        e.Handled = true;
    }

    private void OpenSelectableForecastTextContextMenu(TextBox textBox)
    {
        var selectedText = textBox.SelectedText.Trim();
        var cell = FindParent<DataGridCell>(textBox);
        var grid = FindParent<DataGrid>(textBox);
        var rowItem = FindParent<DataGridRow>(textBox)?.Item;
        var line = rowItem as ForecastLine;
        if (line is null
            && rowItem is MonthlyForecast monthlyForecast
            && DataContext is MainWindowViewModel viewModel)
        {
            line = viewModel.GetForecastLine(monthlyForecast);
        }
        var menu = new ContextMenu();

        var hasSelectedText = !string.IsNullOrWhiteSpace(selectedText);
        if (grid is not null && cell is not null)
        {
            var info = new DataGridCellInfo(cell.DataContext, cell.Column);
            if (!grid.SelectedCells.Contains(info))
            {
                grid.SelectedCells.Clear();
                grid.SelectedCells.Add(info);
            }

            grid.CurrentCell = info;
        }

        var cutItem = new MenuItem
        {
            Header = "Cut",
            InputGestureText = "Ctrl+X",
            IsEnabled = hasSelectedText
                ? !textBox.IsReadOnly
                : grid?.SelectedCells.Any(item => CanWriteGridCell(grid, item.Item, item.Column)) == true
        };
        cutItem.Click += (_, _) =>
        {
            if (hasSelectedText)
            {
                textBox.Cut();
            }
            else if (grid is not null)
            {
                CutSelectedGridCells(grid);
            }
        };
        menu.Items.Add(cutItem);

        var copyItem = new MenuItem
        {
            Header = "Copy",
            InputGestureText = "Ctrl+C",
            IsEnabled = hasSelectedText || grid?.SelectedCells.Count > 0
        };
        copyItem.Click += (_, _) =>
        {
            if (hasSelectedText)
            {
                Clipboard.SetText(selectedText);
            }
            else if (grid is not null)
            {
                CopySelectedGridCells(grid);
            }
        };
        menu.Items.Add(copyItem);

        var pasteItem = new MenuItem
        {
            Header = "Paste",
            InputGestureText = "Ctrl+V",
            IsEnabled = Clipboard.ContainsText()
        };
        pasteItem.Click += (_, _) =>
        {
            if (hasSelectedText && !textBox.IsReadOnly)
            {
                textBox.Paste();
            }
            else if (grid is not null)
            {
                PasteIntoGrid(grid);
            }
        };
        menu.Items.Add(pasteItem);

        var filterText = hasSelectedText || cell is null
            ? selectedText
            : FormatGridCellValue(GetGridCellValue(cell.DataContext, cell.Column)).Trim();
        var filterItem = new MenuItem
        {
            Header = string.IsNullOrWhiteSpace(filterText) ? "Filter by selection" : $"Filter by \"{filterText}\"",
            IsEnabled = !string.IsNullOrWhiteSpace(filterText) && cell?.Column is not null && grid is not null
        };
        filterItem.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(filterText) && cell?.Column is not null && grid is not null)
            {
                FilterColumnBySelectedText(grid, cell.Column, filterText, exactMatch: !hasSelectedText);
            }
        };
        menu.Items.Add(filterItem);

        menu.Items.Add(new Separator());
        AddForecastLineCommentMenuItem(menu, line);

        if (DataContext is MainWindowViewModel menuViewModel)
        {
            if (grid is not null && ReferenceEquals(grid, ForecastLinesGrid) && line is not null)
            {
                menu.Items.Add(new Separator());
                var addAbove = new MenuItem { Header = "Add line above" };
                addAbove.Click += (_, _) => BeginEditingForecastResourceCell(menuViewModel.InsertForecastLine(line, below: false));
                menu.Items.Add(addAbove);

                var addBelow = new MenuItem { Header = "Add line below" };
                addBelow.Click += (_, _) => BeginEditingForecastResourceCell(menuViewModel.InsertForecastLine(line, below: true));
                menu.Items.Add(addBelow);

                if (TryGetSelectedForecastCurve(grid, out var curveLine, out var curveColumns))
                {
                    var adjustCurve = new MenuItem { Header = "Adjust curve..." };
                    adjustCurve.Click += (_, _) => OpenForecastCurveEditor(curveLine, curveColumns);
                    menu.Items.Add(adjustCurve);
                }

                var deleteLine = new MenuItem
                {
                    Header = "Delete line",
                    IsEnabled = line.IsManuallyAdded,
                    ToolTip = line.IsManuallyAdded ? null : "Lines that came from imported raw data cannot be deleted"
                };
                deleteLine.Click += (_, _) => menuViewModel.DeleteForecastLine(line);
                menu.Items.Add(deleteLine);
            }

            if (grid is not null && (ReferenceEquals(grid, LedgerTransactionsGrid) || ReferenceEquals(grid, LedgerMonthlyPivotGrid)))
            {
                menu.Items.Add(new Separator());
                var allTasks = new MenuItem
                {
                    Header = "Show data for same resource across all tasks",
                    IsCheckable = true,
                    IsChecked = menuViewModel.ShowLedgerResourceAcrossAllTasks
                };
                allTasks.Click += (_, _) => menuViewModel.ShowLedgerResourceAcrossAllTasks = !menuViewModel.ShowLedgerResourceAcrossAllTasks;
                menu.Items.Add(allTasks);
            }

            menu.Items.Add(new Separator());
            menu.Items.Add(BuildQuickFiltersMenu(menuViewModel));
        }

        textBox.ContextMenu = menu;
        menu.PlacementTarget = textBox;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    private void FilterColumnBySelectedText(DataGrid grid, DataGridColumn column, string selectedText, bool exactMatch = false)
    {
        var accessor = GetColumnValueAccessor(column);
        var columnKey = GetColumnPersistenceKey(column);
        if (accessor is null || string.IsNullOrWhiteSpace(columnKey))
        {
            return;
        }

        var matchingKeys = GetColumnFilterValues(grid, column, columnKey, accessor)
            .Where(value => exactMatch
                ? string.Equals(value.Display, selectedText, StringComparison.CurrentCultureIgnoreCase)
                : value.Display.Contains(selectedText, StringComparison.CurrentCultureIgnoreCase))
            .Select(value => value.Key)
            .ToList();

        if (matchingKeys.Count == 0)
        {
            return;
        }

        SetColumnFilter(grid, columnKey, matchingKeys, selectedText);
    }

    private void AddForecastLineCommentMenuItem(ContextMenu menu, ForecastLine? line)
    {
        var commentItem = new MenuItem
        {
            Header = "Add comment",
            IsEnabled = line is not null
        };
        commentItem.Click += (_, _) =>
        {
            if (line is not null)
            {
                OpenForecastLineCommentEditor(line);
            }
        };
        menu.Items.Add(commentItem);
    }

    private void OpenForecastLineCommentEditor(ForecastLine line)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        SelectGridRowContext(ForecastLinesGrid, line);
        line.EnsureResourceCommentMetrics();
        var window = new ResourceCommentWindow(line)
        {
            Owner = this
        };

        if (window.ShowDialog() == true)
        {
            viewModel.SaveForecastLineCommentEditor(
                line,
                window.MetricItems.Select(item => new ResourceCommentMetricPreference
                {
                    Key = item.Key,
                    Label = item.Label,
                    IsVisible = item.IsVisible,
                    DisplayOrder = item.DisplayOrder
                }),
                window.TotalBudgetVarianceComment,
                window.MonthBudgetVarianceComment,
                window.ForecastVarianceComment);
        }
    }

    private static Style CreateForecastMonthCellStyle(ForecastMonthColumnDefinition columnDefinition)
    {
        var baseStyle = Application.Current.TryFindResource(typeof(DataGridCell)) as Style;
        var style = baseStyle is null
            ? new Style(typeof(DataGridCell))
            : new Style(typeof(DataGridCell), baseStyle);
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.BackgroundProperty, columnDefinition.ValueBackground));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, BrushFactory.Frozen(0xD9, 0xD9, 0xD9)));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        style.Setters.Add(new Setter(Control.ForegroundProperty, columnDefinition.ValueForeground));

        var selectedTrigger = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
        selectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, BrushFactory.Frozen(0xDB, 0xEA, 0xFE)));
        selectedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, BrushFactory.Frozen(0x0F, 0x17, 0x2A)));
        style.Triggers.Add(selectedTrigger);

        return style;
    }

    private static Style CreateForecastMonthHeaderStyle()
    {
        var style = new Style(typeof(DataGridColumnHeader));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.MarginProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Stretch));
        style.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
        style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Stretch));
        style.Setters.Add(new Setter(FrameworkElement.SnapsToDevicePixelsProperty, true));
        style.Setters.Add(new Setter(Control.TemplateProperty, CreateForecastMonthHeaderTemplate()));
        return style;
    }

    private static ControlTemplate CreateForecastMonthHeaderTemplate()
    {
        var template = new ControlTemplate(typeof(DataGridColumnHeader));

        var root = new FrameworkElementFactory(typeof(Grid));
        root.SetValue(UIElement.SnapsToDevicePixelsProperty, true);

        var background = new FrameworkElementFactory(typeof(Border));
        background.Name = "HeaderBackground";
        background.SetBinding(
            Border.BackgroundProperty,
            new Binding
            {
                Path = new PropertyPath("Column.(0)", GridColumnPresentationState.HeaderBackgroundProperty),
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });
        root.AppendChild(background);

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Stretch);
        presenter.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        root.AppendChild(presenter);

        var leftGripper = new FrameworkElementFactory(typeof(Thumb));
        leftGripper.Name = "PART_LeftHeaderGripper";
        leftGripper.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        leftGripper.SetValue(FrameworkElement.WidthProperty, 6d);
        leftGripper.SetValue(Control.BackgroundProperty, Brushes.Transparent);
        leftGripper.SetValue(FrameworkElement.CursorProperty, Cursors.SizeWE);
        leftGripper.SetValue(Control.TemplateProperty, CreateTransparentThumbTemplate());
        root.AppendChild(leftGripper);

        var rightGripper = new FrameworkElementFactory(typeof(Thumb));
        rightGripper.Name = "PART_RightHeaderGripper";
        rightGripper.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
        rightGripper.SetValue(FrameworkElement.WidthProperty, 12d);
        rightGripper.SetValue(Control.BackgroundProperty, Brushes.Transparent);
        rightGripper.SetValue(FrameworkElement.CursorProperty, Cursors.SizeWE);
        rightGripper.SetValue(Control.TemplateProperty, CreateTransparentThumbTemplate());
        root.AppendChild(rightGripper);

        template.VisualTree = root;
        return template;
    }

    private static ControlTemplate CreateTransparentThumbTemplate()
    {
        var template = new ControlTemplate(typeof(Thumb));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        template.VisualTree = border;
        return template;
    }

    private static string GetForecastFreezeColumnKey(ForecastMonthColumnDefinition columnDefinition) => $"MONTH:{columnDefinition.Key}";

    private static string? GetForecastFreezeColumnKey(DataGridColumn column)
    {
        return column.Header switch
        {
            ForecastMonthColumnDefinition monthColumn => GetForecastFreezeColumnKey(monthColumn),
            "Task" => MainWindowViewModel.ForecastFreezeTaskKey,
            "Resource" => MainWindowViewModel.ForecastFreezeResourceKey,
            "Category" => MainWindowViewModel.ForecastFreezeCategoryKey,
            "CTD" => MainWindowViewModel.ForecastFreezeCostToDateKey,
            "Month Cost" => MainWindowViewModel.ForecastFreezeMonthCostKey,
            "Last Forecast" => MainWindowViewModel.ForecastFreezeLastForecastKey,
            "Month Var" => MainWindowViewModel.ForecastFreezeMonthVarianceKey,
            "CTC" => MainWindowViewModel.ForecastFreezeCtcKey,
            "FCC" => MainWindowViewModel.ForecastFreezeFccKey,
            "Budget" => MainWindowViewModel.ForecastFreezeBudgetKey,
            "Budget Var" => MainWindowViewModel.ForecastFreezeBudgetVarianceKey,
            _ => null
        };
    }

    private static string GetForecastColumnMenuLabel(DataGridColumn column)
    {
        return column.Header switch
        {
            ForecastMonthColumnDefinition monthColumn when monthColumn.IsTotal => "Month Forecast Total",
            ForecastMonthColumnDefinition monthColumn => $"Month Forecast {monthColumn.PrimaryLabel}/{monthColumn.SecondaryLabel}",
            _ => column.Header?.ToString() ?? "Column"
        };
    }

    private static DataTemplate CreateForecastMonthDisplayTemplate(ForecastMonthColumnDefinition columnDefinition, bool showZeroAsBlank)
    {
        var template = new DataTemplate();
        var grid = new FrameworkElementFactory(typeof(Grid));

        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding($"[{columnDefinition.Key}]")
        {
            Converter = AccountingConverter,
            ConverterParameter = showZeroAsBlank && !columnDefinition.IsTotal ? "BlankZero" : null
        });
        text.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        text.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Right);
        text.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        text.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        text.SetValue(TextBlock.FontStyleProperty, FontStyles.Italic);
        text.SetValue(TextBlock.PaddingProperty, new Thickness(4, 3, 4, 3));
        grid.AppendChild(text);

        AppendSeparatorElements(grid, columnDefinition);
        template.VisualTree = grid;
        return template;
    }

    private DataTemplate CreateForecastMonthEditingTemplate(ForecastMonthColumnDefinition columnDefinition, bool showZeroAsBlank)
    {
        var template = new DataTemplate();
        var grid = new FrameworkElementFactory(typeof(Grid));

        var textBox = new FrameworkElementFactory(typeof(TextBox));
        textBox.SetBinding(TextBox.TextProperty, new Binding($"[{columnDefinition.Key}]")
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
            StringFormat = "{0:F0}",
            Converter = ForecastAmountTextConverter.Instance,
            ConverterParameter = showZeroAsBlank ? "BlankZero" : null
        });
        textBox.SetValue(Control.BorderThicknessProperty, new Thickness(0));
        textBox.SetValue(Control.BackgroundProperty, Brushes.Transparent);
        textBox.SetValue(TextBoxBase.SelectionBrushProperty, BrushFactory.Frozen(0xBF, 0xDB, 0xFE));
        textBox.SetValue(TextBoxBase.SelectionTextBrushProperty, BrushFactory.Frozen(0x0F, 0x17, 0x2A));
        textBox.SetValue(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Right);
        textBox.SetValue(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center);
        textBox.SetValue(Control.PaddingProperty, new Thickness(4, 3, 4, 3));
        textBox.SetValue(TextBox.FontStyleProperty, FontStyles.Italic);
        textBox.SetValue(TextBox.FontWeightProperty, FontWeights.SemiBold);
        grid.AppendChild(textBox);

        AppendSeparatorElements(grid, columnDefinition);
        template.VisualTree = grid;
        return template;
    }

    private static void AppendSeparatorElements(FrameworkElementFactory grid, ForecastMonthColumnDefinition columnDefinition)
    {
        var leftSolid = new FrameworkElementFactory(typeof(Border));
        leftSolid.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        leftSolid.SetValue(FrameworkElement.WidthProperty, 1d);
        leftSolid.SetValue(Border.BackgroundProperty, Brushes.Black);
        leftSolid.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(ForecastMonthColumnDefinition.LeftSolidSeparatorVisibility)) { Source = columnDefinition });
        grid.AppendChild(leftSolid);

        var rightSolid = new FrameworkElementFactory(typeof(Border));
        rightSolid.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
        rightSolid.SetValue(FrameworkElement.WidthProperty, 1d);
        rightSolid.SetValue(Border.BackgroundProperty, Brushes.Black);
        rightSolid.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(ForecastMonthColumnDefinition.RightSolidSeparatorVisibility)) { Source = columnDefinition });
        grid.AppendChild(rightSolid);
    }



    private static TextBlock CreateMenuTextIcon(string glyph)
    {
        return new TextBlock
        {
            Text = glyph,
            Width = 18,
            Height = 18,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFactory.Frozen(0x47, 0x55, 0x69),
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
    }

    private static Border CreateColourSwatch(string hex)
    {
        return new Border
        {
            Width = 18,
            Height = 18,
            CornerRadius = new CornerRadius(4),
            Background = BrushFactory.Frozen(hex),
            BorderBrush = BrushFactory.Frozen(0xCB, 0xD5, 0xE1),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
    }

    private static Binding BuildAccountingBinding(string path)
    {
        return new Binding(path)
        {
            Converter = AccountingConverter
        };
    }

    private void ApplyDefaultColumnPresentation(DependencyObject root)
    {
        if (root is DataGrid grid)
        {
            foreach (var column in grid.Columns)
            {
                EnsureColumnPresentation(column);
                TrackWorkspaceColumnStateWidth(column);
            }
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            ApplyDefaultColumnPresentation(VisualTreeHelper.GetChild(root, i));
        }
    }

    private static void EnsureColumnPresentation(DataGridColumn column)
    {
        if (AutoSizedColumns.Add(column) && column.CanUserResize && column.Width.IsAuto && column.ActualWidth > 0)
        {
            column.Width = new DataGridLength(column.ActualWidth);
        }

        if (string.IsNullOrWhiteSpace(GridColumnPresentationState.GetIconGlyph(column)))
        {
            GridColumnPresentationState.SetIconGlyph(column, GetDefaultColumnIcon(column));
        }

        if (GridColumnPresentationState.GetBaseColumnBackground(column) is null)
        {
            GridColumnPresentationState.SetBaseColumnBackground(column, GridColumnPresentationState.GetColumnBackground(column));
        }

        if (GridColumnPresentationState.GetBaseHeaderBackground(column) is null)
        {
            GridColumnPresentationState.SetBaseHeaderBackground(column, GridColumnPresentationState.GetHeaderBackground(column));
        }
    }

    private static string GetDefaultColumnIcon(DataGridColumn column)
    {
        var label = GetForecastColumnMenuLabel(column);
        if (label.Contains("cost", StringComparison.OrdinalIgnoreCase)
            || label.Contains("forecast", StringComparison.OrdinalIgnoreCase)
            || label.Contains("budget", StringComparison.OrdinalIgnoreCase)
            || label.Contains("amount", StringComparison.OrdinalIgnoreCase)
            || label.Contains("rate", StringComparison.OrdinalIgnoreCase)
            || string.Equals(label, "CTD", StringComparison.OrdinalIgnoreCase)
            || string.Equals(label, "CTC", StringComparison.OrdinalIgnoreCase)
            || string.Equals(label, "FCC", StringComparison.OrdinalIgnoreCase))
        {
            return "$";
        }

        if (label.Contains("date", StringComparison.OrdinalIgnoreCase)
            || label.Contains("month", StringComparison.OrdinalIgnoreCase)
            || label.Contains("period", StringComparison.OrdinalIgnoreCase)
            || string.Equals(label, "FY", StringComparison.OrdinalIgnoreCase))
        {
            return "M";
        }

        if (label.Contains("units", StringComparison.OrdinalIgnoreCase)
            || label.Contains("row", StringComparison.OrdinalIgnoreCase)
            || label.Contains("total", StringComparison.OrdinalIgnoreCase)
            || label.Contains("var", StringComparison.OrdinalIgnoreCase))
        {
            return "#";
        }

        return "T";
    }

    private void RefreshColumnPresentation(DataGridColumn column)
    {
        foreach (var grid in EnumerateDataGrids(this))
        {
            if (!grid.Columns.Contains(column))
            {
                continue;
            }

            grid.Items.Refresh();
            var headersPresenter = FindChild<DataGridColumnHeadersPresenter>(grid);
            headersPresenter?.InvalidateVisual();
            grid.InvalidateVisual();
            break;
        }
    }

    private static IEnumerable<DataGrid> EnumerateDataGrids(DependencyObject root)
    {
        if (root is DataGrid grid)
        {
            yield return grid;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            foreach (var childGrid in EnumerateDataGrids(VisualTreeHelper.GetChild(root, i)))
            {
                yield return childGrid;
            }
        }
    }

    private static bool IsColumnColourSelected(DataGridColumn column, ColumnColourOption option)
    {
        return GridColumnPresentationState.GetColumnBackground(column) is SolidColorBrush brush
            && string.Equals(brush.Color.ToString(), NormalizeHex(option.ColumnHex), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeHex(string hex)
    {
        return hex.Length == 7 ? $"#FF{hex[1..]}" : hex;
    }
}
