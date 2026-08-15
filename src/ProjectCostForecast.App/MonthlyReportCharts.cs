using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using ProjectCostForecast.App.ViewModels;

namespace ProjectCostForecast.App;

public partial class MainWindow
{
    private int _reportChartCount;

    private void AddLineReportChartButton_Click(object sender, RoutedEventArgs e) =>
        BeginReportCanvasObjectPlacement("LineChart", sender as Button);

    private void AddColumnReportChartButton_Click(object sender, RoutedEventArgs e) =>
        BeginReportCanvasObjectPlacement("ColumnChart", sender as Button);

    private void ShowReportChartBuilder(ReportChartKind chartKind, Point? dropPosition = null, Size? requestedSize = null)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var dates = viewModel.Transactions
            .Where(item => item.DocDate.HasValue)
            .Select(item => item.DocDate!.Value)
            .ToList();
        var earliest = dates.Count > 0 ? dates.Min() : DateOnly.FromDateTime(DateTime.Today.AddMonths(-11));
        var latest = dates.Count > 0 ? dates.Max() : DateOnly.FromDateTime(DateTime.Today);

        var dialog = new ReportChartBuilderWindow(chartKind, earliest, latest) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var offset = (_reportChartCount++ % 6) * 26;
        var position = dropPosition ?? new Point(24 + offset, 24 + offset);
        var requestedWidth = requestedSize?.Width ?? 540;
        var requestedHeight = requestedSize?.Height ?? 310;
        var width = Math.Clamp(requestedWidth, 180, MonthlyReportChartCanvas.Width);
        var height = Math.Clamp(requestedHeight, 140, MonthlyReportChartCanvas.Height);
        var toolbarKey = chartKind == ReportChartKind.Line ? "LineChart" : "ColumnChart";
        var layout = new ReportCanvasObjectLayout
        {
            ObjectType = "Chart",
            X = Math.Clamp(position.X, 0, Math.Max(0, MonthlyReportChartCanvas.Width - width)),
            Y = Math.Clamp(position.Y, 0, Math.Max(0, MonthlyReportChartCanvas.Height - height)),
            Width = width,
            Height = height,
            StyleKey = GetSelectedReportToolbarStyle(toolbarKey),
            ChartKind = chartKind.ToString(),
            Grouping = dialog.Grouping.ToString(),
            FromDate = dialog.FromDate,
            ToDate = dialog.ToDate,
            XAxisTickFrequency = 8
        };
        var model = BuildReportChartModel(viewModel, layout);
        if (model.Series.Count == 0 || model.Series.All(series => series.Values.All(value => value == 0)))
        {
            MessageBox.Show(
                this,
                "There is no cost data for that selection and date range.",
                "Create chart",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        AddReportCanvasLayout(layout);
        AddReportChartCard(layout, model);
    }

    private static ReportChartModel BuildReportChartModel(
        MainWindowViewModel viewModel,
        ReportCanvasObjectLayout layout)
    {
        if (!Enum.TryParse(layout.ChartKind, true, out ReportChartKind kind)
            || !Enum.TryParse(layout.Grouping, true, out ReportChartGrouping grouping)
            || layout.FromDate is not { } fromDate
            || layout.ToDate is not { } toDate)
        {
            return new ReportChartModel(ReportChartKind.Line, "Report chart", [], []);
        }

        var valueKeys = layout.ReportChartValueKeys ?? [];
        var costCodeFilters = layout.ReportChartCostCodeFilterEnabled
            ? GetReportChartCostCodeFilters(layout)
            : [];
        return valueKeys.Count > 0
            ? BuildReportValueChartModel(
                viewModel,
                kind,
                fromDate,
                toDate,
                costCodeFilters,
                valueKeys)
            : BuildReportChartModel(
                viewModel,
                kind,
                grouping,
                fromDate,
                toDate,
                costCodeFilters);
    }

    private static ReportChartModel BuildReportChartModel(
        MainWindowViewModel viewModel,
        ReportChartKind kind,
        ReportChartGrouping grouping,
        DateOnly fromDate,
        DateOnly toDate,
        IReadOnlyList<string> costCodeFilters)
    {
        var transactions = viewModel.Transactions
            .Where(item => item.DocDate is { } date
                && date >= fromDate
                && date <= toDate
                && MatchesReportCostCodeFilter(item, costCodeFilters))
            .ToList();

        string GroupKey(CostTransaction item) => grouping switch
        {
            ReportChartGrouping.ProjectCode => item.ProjectCode,
            ReportChartGrouping.CostCode => item.CostAccount,
            _ => item.LedgerResourceName
        };

        var topGroups = transactions
            .GroupBy(GroupKey)
            .Select(group => new
            {
                Name = string.IsNullOrWhiteSpace(group.Key) ? "(Unassigned)" : group.Key.Trim(),
                Total = group.Sum(item => Math.Abs(item.Amount))
            })
            .OrderByDescending(item => item.Total)
            .Take(6)
            .Select(item => item.Name)
            .ToList();

        var startMonth = new DateOnly(fromDate.Year, fromDate.Month, 1);
        var endMonth = new DateOnly(toDate.Year, toDate.Month, 1);
        var months = new List<DateOnly>();
        for (var month = startMonth; month <= endMonth; month = month.AddMonths(1))
        {
            months.Add(month);
        }

        var categories = months.Select(month => month.ToString("MMM yy", CultureInfo.InvariantCulture)).ToList();
        var series = topGroups.Select(groupName =>
        {
            var values = months.Select(month => transactions
                .Where(item =>
                {
                    var normalizedName = string.IsNullOrWhiteSpace(GroupKey(item)) ? "(Unassigned)" : GroupKey(item).Trim();
                    return string.Equals(normalizedName, groupName, StringComparison.OrdinalIgnoreCase)
                        && item.DocDate is { } date
                        && date.Year == month.Year
                        && date.Month == month.Month;
                })
                .Sum(item => item.Amount))
                .ToList();
            return new ReportChartSeries(groupName, values);
        }).ToList();

        var groupingLabel = grouping switch
        {
            ReportChartGrouping.ProjectCode => "Project code",
            ReportChartGrouping.CostCode => "Cost code",
            _ => "Resource"
        };
        var title = $"{groupingLabel} cost · {fromDate:dd MMM yyyy} – {toDate:dd MMM yyyy}";
        return new ReportChartModel(kind, title, categories, series);
    }

    private static ReportChartModel BuildReportValueChartModel(
        MainWindowViewModel viewModel,
        ReportChartKind kind,
        DateOnly fromDate,
        DateOnly toDate,
        IReadOnlyList<string> costCodeFilters,
        IReadOnlyList<string> valueKeys)
    {
        var allTransactions = viewModel.Transactions.ToList();
        var datedTransactions = allTransactions
            .Where(item => item.DocDate is { } date
                && date >= fromDate
                && date <= toDate
                && MatchesReportCostCodeFilter(item, costCodeFilters))
            .ToList();
        var filteredForecastLines = viewModel.ForecastLines
            .Where(line => ForecastLineMatchesReportCostCodeFilter(line, costCodeFilters))
            .ToList();

        var startMonth = new DateOnly(fromDate.Year, fromDate.Month, 1);
        var endMonth = new DateOnly(toDate.Year, toDate.Month, 1);
        var months = new List<DateOnly>();
        for (var month = startMonth; month <= endMonth; month = month.AddMonths(1))
        {
            months.Add(month);
        }

        var categories = months
            .Select(month => month.ToString("MMM yy", CultureInfo.InvariantCulture))
            .ToList();
        var budgetTotal = filteredForecastLines.Sum(line => line.Budget);
        var series = valueKeys
            .Where(IsReportValueDataSetKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(key => new ReportChartSeries(
                GetReportValueDataSetTitle(key),
                months.Select(month => GetReportValueForMonth(
                    key,
                    month,
                    datedTransactions,
                    filteredForecastLines,
                    budgetTotal)).ToList(),
                IsReportReferenceDataSetKey(key)))
            .ToList();

        var costCodeFilter = costCodeFilters.Count == 0
            ? string.Empty
            : GetReportChartCostCodeFilterLabel(costCodeFilters);
        var title = string.Join(" / ", series.Select(item => item.Name));
        if (string.IsNullOrWhiteSpace(title))
        {
            title = "Report values";
        }

        title += $" Â· {fromDate:dd MMM yyyy} â€“ {toDate:dd MMM yyyy}";
        if (!string.IsNullOrWhiteSpace(costCodeFilter))
        {
            title += $" Â· Cost code: {costCodeFilter}";
        }

        return new ReportChartModel(kind, title, categories, series);
    }

    private static decimal GetReportValueForMonth(
        string key,
        DateOnly month,
        IReadOnlyList<CostTransaction> transactions,
        IReadOnlyList<ForecastLine> forecastLines,
        decimal budgetTotal)
    {
        return key switch
        {
            "TotalBudget" => budgetTotal,
            "TotalSpend" => transactions
                .Where(item => item.DocDate is { } date && date.Year == month.Year && date.Month == month.Month)
                .Sum(item => item.Amount),
            "TotalForecast" => forecastLines
                .SelectMany(line => line.MonthlyForecasts)
                .Where(item => item.PeriodStartDate is { } date
                    && date.Year == month.Year
                    && date.Month == month.Month)
                .Sum(item => item.Amount),
            _ => 0m
        };
    }

    private static IReadOnlyList<string> GetReportChartCostCodeFilters(ReportCanvasObjectLayout layout)
    {
        var filters = (layout.ReportChartCostCodeFilters ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (filters.Count == 0 && !string.IsNullOrWhiteSpace(layout.ReportChartCostCodeFilter))
        {
            filters.Add(layout.ReportChartCostCodeFilter.Trim());
        }

        return filters;
    }

    internal static string GetReportChartCostCodeFilterLabel(ReportCanvasObjectLayout layout)
        => GetReportChartCostCodeFilterLabel(GetReportChartCostCodeFilters(layout));

    internal static string GetReportChartCostCodeFilterLabel(IReadOnlyList<string> filters)
        => filters.Count switch
        {
            0 => "Cost codes (all)",
            1 => $"Cost code ({filters[0]})",
            _ => "Cost (multiple)"
        };

    internal static bool IsReportFilterDataSetKey(string key)
        => string.Equals(key, "CostCodeSummary", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "HeadingFilter", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "SubHeadingFilter", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith(ReportCostCodeValuePrefix, StringComparison.OrdinalIgnoreCase);

    internal static bool IsReportValueDataSetKey(string key) => key is "TotalBudget" or "TotalSpend" or "TotalForecast";

    private static bool IsReportReferenceDataSetKey(string key) => string.Equals(key, "TotalBudget", StringComparison.OrdinalIgnoreCase);

    internal static string GetReportValueDataSetTitle(string key) => key switch
    {
        "TotalBudget" => "Total budget",
        "TotalSpend" => "Total spend",
        "TotalForecast" => "Total forecast",
        _ => "Value"
    };

    private static bool MatchesReportCostCodeFilter(CostTransaction transaction, IReadOnlyList<string> costCodeFilters)
    {
        if (costCodeFilters.Count == 0)
        {
            return true;
        }

        var taskCode = CalculationService.Normalise(transaction.TaskNumber);
        return costCodeFilters.Any(filter => string.Equals(
            taskCode,
            CalculationService.Normalise(filter),
            StringComparison.OrdinalIgnoreCase));
    }

    private static bool ForecastLineMatchesReportCostCodeFilter(
        ForecastLine line,
        IReadOnlyList<string> costCodeFilters)
    {
        if (costCodeFilters.Count == 0)
        {
            return true;
        }

        var taskCode = CalculationService.Normalise(line.TaskNumber);
        return costCodeFilters.Any(filter => string.Equals(
            taskCode,
            CalculationService.Normalise(filter),
            StringComparison.OrdinalIgnoreCase));
    }

    private void ReportCanvasSetting_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || MonthlyReportChartCanvas is null
            || ReportCanvasSizeComboBox?.SelectedItem is not ComboBoxItem sizeItem
            || ReportCanvasOrientationComboBox?.SelectedItem is not ComboBoxItem orientationItem)
        {
            return;
        }

        var isA3 = string.Equals(sizeItem.Content?.ToString(), "A3", StringComparison.OrdinalIgnoreCase);
        var isLandscape = string.Equals(orientationItem.Content?.ToString(), "Landscape", StringComparison.OrdinalIgnoreCase);
        var width = isA3 ? 1123d : 794d;
        var height = isA3 ? 1587d : 1123d;
        if (isLandscape)
        {
            (width, height) = (height, width);
        }

        MonthlyReportChartCanvas.Width = width;
        MonthlyReportChartCanvas.Height = height;
        KeepReportChartsInsideCanvas();

        if (!_isLoadingReportCanvasView && GetCurrentMonthlyReportView() is { } view)
        {
            view.ReportCanvasPageSize = sizeItem.Content?.ToString() ?? "A4";
            view.ReportCanvasOrientation = orientationItem.Content?.ToString() ?? "Portrait";
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.IsDirty = true;
            }
        }
    }

    private void KeepReportChartsInsideCanvas()
    {
        foreach (var chart in MonthlyReportChartCanvas.Children.OfType<ReportChartCard>())
        {
            Canvas.SetLeft(chart, Math.Clamp(Canvas.GetLeft(chart), 0, Math.Max(0, MonthlyReportChartCanvas.Width - chart.Width)));
            Canvas.SetTop(chart, Math.Clamp(Canvas.GetTop(chart), 0, Math.Max(0, MonthlyReportChartCanvas.Height - chart.Height)));
            chart.SyncPositionFromCanvas();
        }

        foreach (var item in MonthlyReportChartCanvas.Children.OfType<ReportCanvasObjectCard>())
        {
            Canvas.SetLeft(item, Math.Clamp(Canvas.GetLeft(item), 0, Math.Max(0, MonthlyReportChartCanvas.Width - item.Width)));
            Canvas.SetTop(item, Math.Clamp(Canvas.GetTop(item), 0, Math.Max(0, MonthlyReportChartCanvas.Height - item.Height)));
            item.SyncPositionFromCanvas();
        }
    }

    private void ClearReportChartsButton_Click(object sender, RoutedEventArgs e)
    {
        CancelReportCanvasObjectPlacement();
        ClearReportCanvasObjectSelection();
        DetachLegacyReportViewer();
        foreach (var item in MonthlyReportChartCanvas.Children
                     .OfType<FrameworkElement>()
                     .Where(item => !ReferenceEquals(item, MonthlyReportChartCanvasHint))
                     .ToList())
        {
            MonthlyReportChartCanvas.Children.Remove(item);
        }

        if (GetCurrentMonthlyReportView() is { } view)
        {
            view.ReportCanvasInitialized = true;
            view.ReportCanvasObjects.Clear();
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.IsDirty = true;
            }
        }

        MonthlyReportChartCanvasHint.Visibility = Visibility.Visible;
    }
}

internal enum ReportChartKind
{
    Line,
    Column
}

internal enum ReportChartGrouping
{
    ProjectCode,
    CostCode,
    Resource
}

internal sealed record ReportChartSeries(string Name, IReadOnlyList<decimal> Values, bool IsReferenceLine = false);

internal sealed record ReportChartModel(
    ReportChartKind Kind,
    string Title,
    IReadOnlyList<string> Categories,
    IReadOnlyList<ReportChartSeries> Series);

internal sealed class ReportChartDataSetEventArgs(string dataSetKey) : EventArgs
{
    public string DataSetKey { get; } = dataSetKey;
}

internal sealed class ReportChartBuilderWindow : Window
{
    private readonly ComboBox _groupingComboBox;
    private readonly DatePicker _fromDatePicker;
    private readonly DatePicker _toDatePicker;

    public ReportChartBuilderWindow(ReportChartKind kind, DateOnly earliest, DateOnly latest)
    {
        Title = $"Create {(kind == ReportChartKind.Line ? "line" : "column")} chart";
        Width = 440;
        Height = 330;
        MinWidth = 420;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brushes.White;

        _groupingComboBox = new ComboBox
        {
            Height = 32,
            SelectedIndex = 0,
            ItemsSource = new[] { "Project code", "Cost code", "Resource" }
        };
        _fromDatePicker = new DatePicker { Height = 32, SelectedDate = earliest.ToDateTime(TimeOnly.MinValue) };
        _toDatePicker = new DatePicker { Height = 32, SelectedDate = latest.ToDateTime(TimeOnly.MinValue) };

        var content = new Grid { Margin = new Thickness(24) };
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new TextBlock
        {
            Text = kind == ReportChartKind.Line ? "New line chart" : "New column chart",
            FontSize = 19,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59))
        };
        content.Children.Add(heading);

        AddField(content, 1, "Group data by", _groupingComboBox);
        AddField(content, 3, "From date", _fromDatePicker);
        AddField(content, 4, "To date", _toDatePicker);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel", MinWidth = 82, Height = 32, Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var create = new Button
        {
            Content = "Create chart",
            MinWidth = 104,
            Height = 32,
            Background = new SolidColorBrush(Color.FromRgb(20, 99, 243)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(20, 99, 243))
        };
        create.Click += Create_Click;
        actions.Children.Add(cancel);
        actions.Children.Add(create);
        Grid.SetRow(actions, 6);
        content.Children.Add(actions);

        Content = content;
    }

    public ReportChartGrouping Grouping => _groupingComboBox.SelectedIndex switch
    {
        1 => ReportChartGrouping.CostCode,
        2 => ReportChartGrouping.Resource,
        _ => ReportChartGrouping.ProjectCode
    };

    public DateOnly FromDate => DateOnly.FromDateTime(_fromDatePicker.SelectedDate!.Value);
    public DateOnly ToDate => DateOnly.FromDateTime(_toDatePicker.SelectedDate!.Value);

    private static void AddField(Grid grid, int row, string label, Control control)
    {
        var panel = new StackPanel { Margin = new Thickness(0, row == 1 ? 18 : 10, 0, 0) };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            Margin = new Thickness(0, 0, 0, 5),
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105))
        });
        panel.Children.Add(control);
        Grid.SetRow(panel, row);
        grid.Children.Add(panel);
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        if (_fromDatePicker.SelectedDate is null || _toDatePicker.SelectedDate is null)
        {
            MessageBox.Show(this, "Select both a start and end date.", "Create chart", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_fromDatePicker.SelectedDate > _toDatePicker.SelectedDate)
        {
            MessageBox.Show(this, "The start date must be before the end date.", "Create chart", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }
}

internal sealed class ReportChartCard : Border
{
    private readonly ReportCanvasObjectLayout _objectLayout;
    private readonly ReportChartVisual _chartVisual;
    private readonly TextBlock _titleText;
    private readonly StackPanel _dataSetPillPanel;
    private readonly Border _dataSetStrip;
    private Point _dragOrigin;
    private double _leftOrigin;
    private double _topOrigin;
    private bool _isDragging;

    public ReportChartCard(ReportChartModel model, ReportCanvasObjectLayout objectLayout)
    {
        _objectLayout = objectLayout;
        Width = objectLayout.Width > 0 ? objectLayout.Width : 540;
        Height = objectLayout.Height > 0 ? objectLayout.Height : 310;
        Background = Brushes.White;
        BorderBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184));
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(8);
        ClipToBounds = true;
        Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            BlurRadius = 10,
            Opacity = 0.16,
            ShadowDepth = 2
        };
        AllowDrop = true;
        DragOver += ReportChartCard_DragOver;
        Drop += ReportChartCard_Drop;

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new Border
        {
            Height = 24,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.SizeAll,
            Padding = new Thickness(10, 0, 5, 0)
        };
        var headerLayout = new DockPanel();
        var removeButton = new Button
        {
            Content = "×",
            Width = 27,
            Height = 27,
            FontSize = 17,
            ToolTip = "Remove chart",
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139))
        };
        removeButton.Click += (_, _) => RemoveRequested?.Invoke(this, EventArgs.Empty);
        DockPanel.SetDock(removeButton, Dock.Right);
        headerLayout.Children.Add(removeButton);
        _titleText = new TextBlock
        {
            Text = string.Empty,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Visibility = Visibility.Collapsed
        };
        headerLayout.Children.Add(_titleText);
        header.Child = headerLayout;
        header.PreviewMouseLeftButtonDown += Header_MouseLeftButtonDown;
        header.PreviewMouseMove += Header_MouseMove;
        header.PreviewMouseLeftButtonUp += Header_MouseLeftButtonUp;
        layout.Children.Add(header);

        _dataSetPillPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        _dataSetStrip = new Border
        {
            Background = BrushFactory.Frozen("#F8FAFC"),
            BorderBrush = BrushFactory.Frozen("#E2E8F0"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(8, 3, 8, 3),
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _dataSetPillPanel
            }
        };
        Grid.SetRow(_dataSetStrip, 1);
        layout.Children.Add(_dataSetStrip);

        _chartVisual = new ReportChartVisual(model, objectLayout.StyleKey, objectLayout) { Margin = new Thickness(8) };
        Grid.SetRow(_chartVisual, 2);
        layout.Children.Add(_chartVisual);

        var resizeThumb = ReportCanvasResizeBehavior.CreateThumb(
            this,
            objectLayout,
            minimumWidth: 260,
            minimumHeight: 180,
            () => PositionChanged?.Invoke(this, EventArgs.Empty));
        Grid.SetRowSpan(resizeThumb, 3);
        Panel.SetZIndex(resizeThumb, 20);
        layout.Children.Add(resizeThumb);
        Child = layout;
        RebuildDataSetPills();
    }

    public event EventHandler? RemoveRequested;
    public event EventHandler? PositionChanged;
    public event EventHandler<ReportChartDataSetEventArgs>? DataSetDropRequested;
    public event EventHandler<ReportChartDataSetEventArgs>? DataSetRemoved;

    public ReportCanvasObjectLayout Layout => _objectLayout;

    public void UpdateChart(ReportChartModel model)
    {
        _titleText.Text = model.Title;
        _chartVisual.UpdateModel(model);
        RebuildDataSetPills();
    }

    public void RefreshChart() => _chartVisual.InvalidateVisual();

    private void RebuildDataSetPills()
    {
        _dataSetPillPanel.Children.Clear();
        var hasFilterPill = _objectLayout.ReportChartCostCodeFilterEnabled
            || _objectLayout.ReportChartHeadingFilterEnabled
            || _objectLayout.ReportChartSubHeadingFilterEnabled;
        if (!hasFilterPill)
        {
            _dataSetStrip.Visibility = Visibility.Collapsed;
            return;
        }

        _dataSetStrip.Visibility = Visibility.Visible;
        if (_objectLayout.ReportChartCostCodeFilterEnabled)
        {
            _dataSetPillPanel.Children.Add(CreateCostCodeFilterPill());
        }

        if (_objectLayout.ReportChartHeadingFilterEnabled)
        {
            _dataSetPillPanel.Children.Add(CreateSimpleFilterPill("Heading (all)", "HeadingFilter"));
        }

        if (_objectLayout.ReportChartSubHeadingFilterEnabled)
        {
            _dataSetPillPanel.Children.Add(CreateSimpleFilterPill("Sub heading (all)", "SubHeadingFilter"));
        }

    }

    private Border CreateCostCodeFilterPill()
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new TextBlock
        {
            Text = MainWindow.GetReportChartCostCodeFilterLabel(_objectLayout),
            Foreground = BrushFactory.Frozen("#1E3A8A"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        });
        content.Children.Add(CreatePillRemoveButton("CostCodeSummary"));
        return CreatePillBorder(content, "#EEF4FF", "#BFDBFE");
    }

    private Border CreateSimpleFilterPill(string label, string dataSetKey)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = BrushFactory.Frozen("#1E3A8A"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        });
        content.Children.Add(CreatePillRemoveButton(dataSetKey));
        return CreatePillBorder(content, "#EEF4FF", "#BFDBFE");
    }

    private Button CreatePillRemoveButton(string dataSetKey)
    {
        var button = new Button
        {
            Content = "×",
            Width = 20,
            Height = 20,
            Margin = new Thickness(5, 0, -3, 0),
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = BrushFactory.Frozen("#64748B"),
            ToolTip = "Remove from chart"
        };
        button.Click += (_, _) => DataSetRemoved?.Invoke(this, new ReportChartDataSetEventArgs(dataSetKey));
        return button;
    }

    private static Border CreatePillBorder(UIElement content, string background, string border)
        => new()
        {
            Background = BrushFactory.Frozen(background),
            BorderBrush = BrushFactory.Frozen(border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(8, 2, 6, 2),
            Margin = new Thickness(0, 0, 6, 0),
            Child = content
        };

    private void ReportChartCard_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(MainWindow.ReportDataSetDragFormat) is string dataSetKey
            && (MainWindow.IsReportFilterDataSetKey(dataSetKey)
                || MainWindow.IsReportValueDataSetKey(dataSetKey)))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void ReportChartCard_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(MainWindow.ReportDataSetDragFormat) is string dataSetKey
            && (MainWindow.IsReportFilterDataSetKey(dataSetKey)
                || MainWindow.IsReportValueDataSetKey(dataSetKey)))
        {
            DataSetDropRequested?.Invoke(this, new ReportChartDataSetEventArgs(dataSetKey));
            e.Handled = true;
        }
    }

    public void SetSelected(bool selected)
    {
        BorderBrush = selected
            ? BrushFactory.Frozen("#2563EB")
            : new SolidColorBrush(Color.FromRgb(148, 163, 184));
        BorderThickness = new Thickness(selected ? 2 : 1);
    }

    public void SyncPositionFromCanvas()
    {
        _objectLayout.X = Canvas.GetLeft(this);
        _objectLayout.Y = Canvas.GetTop(this);
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        for (var source = e.OriginalSource as DependencyObject; source is not null; source = VisualTreeHelper.GetParent(source))
        {
            if (source is Button)
            {
                return;
            }

            if (ReferenceEquals(source, sender))
            {
                break;
            }
        }

        _isDragging = true;
        _dragOrigin = e.GetPosition(Parent as IInputElement);
        _leftOrigin = Canvas.GetLeft(this);
        _topOrigin = Canvas.GetTop(this);
        ((UIElement)sender).CaptureMouse();
        Panel.SetZIndex(this, 100);
        e.Handled = true;
    }

    private void Header_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || Parent is not Canvas canvas || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var point = e.GetPosition(canvas);
        var left = Math.Clamp(_leftOrigin + point.X - _dragOrigin.X, 0, Math.Max(0, canvas.Width - Width));
        var top = Math.Clamp(_topOrigin + point.Y - _dragOrigin.Y, 0, Math.Max(0, canvas.Height - Height));
        Canvas.SetLeft(this, left);
        Canvas.SetTop(this, top);
        SyncPositionFromCanvas();
    }

    private void Header_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        ((UIElement)sender).ReleaseMouseCapture();
        Panel.SetZIndex(this, 1);
        PositionChanged?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }
}

internal sealed class ReportChartVisual : FrameworkElement
{
    private static readonly Color[] BluePalette =
    [
        Color.FromRgb(22, 132, 216),
        Color.FromRgb(245, 158, 11),
        Color.FromRgb(16, 185, 129),
        Color.FromRgb(139, 92, 246),
        Color.FromRgb(239, 68, 68),
        Color.FromRgb(14, 165, 233)
    ];
    private static readonly Color[] GreenPalette =
    [
        Color.FromRgb(22, 163, 74),
        Color.FromRgb(101, 163, 13),
        Color.FromRgb(13, 148, 136),
        Color.FromRgb(5, 150, 105),
        Color.FromRgb(132, 204, 22),
        Color.FromRgb(20, 184, 166)
    ];
    private static readonly Color[] MonochromePalette =
    [
        Color.FromRgb(30, 41, 59),
        Color.FromRgb(71, 85, 105),
        Color.FromRgb(100, 116, 139),
        Color.FromRgb(148, 163, 184),
        Color.FromRgb(51, 65, 85),
        Color.FromRgb(203, 213, 225)
    ];
    private ReportChartModel _model;
    private readonly ReportCanvasObjectLayout _objectLayout;
    private readonly Color[] _palette;

    public ReportChartVisual(ReportChartModel model, string? styleKey, ReportCanvasObjectLayout objectLayout)
    {
        _model = model;
        _objectLayout = objectLayout;
        _palette = styleKey switch
        {
            "Green" => GreenPalette,
            "Monochrome" => MonochromePalette,
            _ => BluePalette
        };
    }

    public void UpdateModel(ReportChartModel model)
    {
        _model = model;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth < 120 || ActualHeight < 100 || _model.Categories.Count == 0)
        {
            return;
        }

        const double left = 58;
        const double top = 28;
        const double bottom = 42;
        const double right = 16;
        var plotWidth = Math.Max(1, ActualWidth - left - right);
        var plotHeight = Math.Max(1, ActualHeight - top - bottom);
        var renderedData = BuildRenderedChartData();
        var categories = renderedData.Categories;
        var series = renderedData.Series;
        var allValues = series.SelectMany(item => item.Values).Select(decimal.ToDouble).ToList();
        var minimum = Math.Min(0, allValues.DefaultIfEmpty(0).Min());
        var maximum = Math.Max(0, allValues.DefaultIfEmpty(0).Max());
        if (Math.Abs(maximum - minimum) < 0.001)
        {
            maximum = minimum + 1;
        }

        var axisPen = new Pen(new SolidColorBrush(Color.FromRgb(148, 163, 184)), 1);
        var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(226, 232, 240)), 1);
        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        for (var index = 0; index <= 4; index++)
        {
            var y = top + plotHeight * index / 4;
            dc.DrawLine(gridPen, new Point(left, y), new Point(left + plotWidth, y));
            var value = maximum - (maximum - minimum) * index / 4;
            DrawText(dc, CompactAmount(value), 9, new Point(3, y - 7), pixelsPerDip, Color.FromRgb(100, 116, 139));
        }

        dc.DrawLine(axisPen, new Point(left, top), new Point(left, top + plotHeight));
        dc.DrawLine(axisPen, new Point(left, top + plotHeight), new Point(left + plotWidth, top + plotHeight));
        var zeroY = top + maximum / (maximum - minimum) * plotHeight;

        if (_model.Kind == ReportChartKind.Line)
        {
            DrawLines(dc, left, top, plotWidth, plotHeight, minimum, maximum, categories.Count, series);
        }
        else
        {
            DrawColumns(dc, left, plotWidth, zeroY, top, plotHeight, minimum, maximum, categories.Count, series);
        }

        for (var index = 0; index < categories.Count; index++)
        {
            var x = left + (index + 0.5) * plotWidth / categories.Count;
            DrawText(dc, categories[index], 9, new Point(x - 28, top + plotHeight + 7), pixelsPerDip, Color.FromRgb(100, 116, 139));
        }

        var legendX = left;
        for (var index = 0; index < series.Count; index++)
        {
            var name = series[index].Name;
            var itemWidth = Math.Min(125, 24 + name.Length * 6);
            if (legendX + itemWidth > ActualWidth - right)
            {
                break;
            }

            dc.DrawRectangle(new SolidColorBrush(_palette[index % _palette.Length]), null, new Rect(legendX, 5, 10, 10));
            DrawText(dc, name.Length > 16 ? name[..15] + "…" : name, 9, new Point(legendX + 14, 2), pixelsPerDip, Color.FromRgb(71, 85, 105));
            legendX += itemWidth;
        }
    }

    private (IReadOnlyList<string> Categories, IReadOnlyList<ReportChartSeries> Series) BuildRenderedChartData()
    {
        var categoryCount = _model.Categories.Count;
        var tickFrequency = Math.Clamp(
            _objectLayout.XAxisTickFrequency > 0 ? _objectLayout.XAxisTickFrequency : 8,
            2,
            24);
        var bucketCount = Math.Min(categoryCount, tickFrequency);
        var bucketRanges = Enumerable.Range(0, bucketCount)
            .Select(index =>
            {
                var start = index * categoryCount / bucketCount;
                var end = ((index + 1) * categoryCount / bucketCount) - 1;
                return (Start: start, End: end);
            })
            .ToList();
        var categories = bucketRanges
            .Select(range => range.Start == range.End
                ? _model.Categories[range.Start]
                : $"{_model.Categories[range.Start]} - {_model.Categories[range.End]}")
            .ToList();
        var series = _model.Series
            .Select(item => new ReportChartSeries(
                item.Name,
                bucketRanges
                    .Select(range => item.IsReferenceLine
                        ? item.Values[range.Start]
                        : Enumerable.Range(range.Start, range.End - range.Start + 1)
                            .Sum(categoryIndex => item.Values[categoryIndex]))
                    .ToList(),
                item.IsReferenceLine))
            .ToList();
        return (categories, series);
    }

    private void DrawLines(
        DrawingContext dc,
        double left,
        double top,
        double width,
        double height,
        double minimum,
        double maximum,
        int categoryCount,
        IReadOnlyList<ReportChartSeries> seriesList)
    {
        foreach (var (series, seriesIndex) in seriesList.Select((value, index) => (value, index)))
        {
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                for (var index = 0; index < series.Values.Count; index++)
                {
                    var x = left + (index + 0.5) * width / categoryCount;
                    var y = top + (maximum - decimal.ToDouble(series.Values[index])) / (maximum - minimum) * height;
                    if (index == 0)
                    {
                        context.BeginFigure(new Point(x, y), false, false);
                    }
                    else
                    {
                        context.LineTo(new Point(x, y), true, false);
                    }
                }
            }

            geometry.Freeze();
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(_palette[seriesIndex % _palette.Length]), 2), geometry);
        }
    }

    private void DrawColumns(
        DrawingContext dc,
        double left,
        double width,
        double zeroY,
        double top,
        double height,
        double minimum,
        double maximum,
        int categoryCount,
        IReadOnlyList<ReportChartSeries> seriesList)
    {
        var categoryWidth = width / categoryCount;
        var seriesCount = Math.Max(1, seriesList.Count);
        var barWidth = Math.Max(2, Math.Min(18, categoryWidth * 0.72 / seriesCount));
        for (var categoryIndex = 0; categoryIndex < categoryCount; categoryIndex++)
        {
            var groupWidth = barWidth * seriesCount;
            var groupLeft = left + categoryIndex * categoryWidth + (categoryWidth - groupWidth) / 2;
            for (var seriesIndex = 0; seriesIndex < seriesList.Count; seriesIndex++)
            {
                var value = decimal.ToDouble(seriesList[seriesIndex].Values[categoryIndex]);
                var valueY = top + (maximum - value) / (maximum - minimum) * height;
                var rect = new Rect(groupLeft + seriesIndex * barWidth, Math.Min(zeroY, valueY), Math.Max(1, barWidth - 1), Math.Max(1, Math.Abs(zeroY - valueY)));
                dc.DrawRectangle(new SolidColorBrush(_palette[seriesIndex % _palette.Length]), null, rect);
            }
        }
    }

    private static void DrawText(DrawingContext dc, string text, double size, Point origin, double pixelsPerDip, Color color)
    {
        dc.DrawText(
            new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                size,
                new SolidColorBrush(color),
                pixelsPerDip),
            origin);
    }

    private static string CompactAmount(double value)
    {
        var absolute = Math.Abs(value);
        return absolute >= 1_000_000 ? $"{value / 1_000_000:0.#}m"
            : absolute >= 1_000 ? $"{value / 1_000:0.#}k"
            : $"{value:0}";
    }
}
