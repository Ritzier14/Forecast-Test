using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.ViewModels;

namespace ProjectCostForecast.App;

public partial class MainWindow
{
    private int _reportChartCount;

    private void AddLineReportChartButton_Click(object sender, RoutedEventArgs e) =>
        ShowReportChartBuilder(ReportChartKind.Line);

    private void AddColumnReportChartButton_Click(object sender, RoutedEventArgs e) =>
        ShowReportChartBuilder(ReportChartKind.Column);

    private void ShowReportChartBuilder(ReportChartKind chartKind, Point? dropPosition = null)
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

        var model = BuildReportChartModel(viewModel, chartKind, dialog.Grouping, dialog.FromDate, dialog.ToDate);
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

        var offset = (_reportChartCount++ % 6) * 26;
        var position = dropPosition ?? new Point(24 + offset, 24 + offset);
        var toolbarKey = chartKind == ReportChartKind.Line ? "LineChart" : "ColumnChart";
        var layout = new ReportCanvasObjectLayout
        {
            ObjectType = "Chart",
            X = Math.Min(position.X, Math.Max(0, MonthlyReportChartCanvas.Width - 540)),
            Y = Math.Min(position.Y, Math.Max(0, MonthlyReportChartCanvas.Height - 310)),
            Width = 540,
            Height = 310,
            StyleKey = GetSelectedReportToolbarStyle(toolbarKey),
            ChartKind = chartKind.ToString(),
            Grouping = dialog.Grouping.ToString(),
            FromDate = dialog.FromDate,
            ToDate = dialog.ToDate
        };
        AddReportCanvasLayout(layout);
        AddReportChartCard(layout, model);
    }

    private static ReportChartModel BuildReportChartModel(
        MainWindowViewModel viewModel,
        ReportChartKind kind,
        ReportChartGrouping grouping,
        DateOnly fromDate,
        DateOnly toDate)
    {
        var transactions = viewModel.Transactions
            .Where(item => item.DocDate is { } date && date >= fromDate && date <= toDate)
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

internal sealed record ReportChartSeries(string Name, IReadOnlyList<decimal> Values);

internal sealed record ReportChartModel(
    ReportChartKind Kind,
    string Title,
    IReadOnlyList<string> Categories,
    IReadOnlyList<ReportChartSeries> Series);

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
        CornerRadius = new CornerRadius(5);
        Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            BlurRadius = 10,
            Opacity = 0.16,
            ShadowDepth = 2
        };

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            BorderThickness = new Thickness(0, 0, 0, 1),
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
        headerLayout.Children.Add(new TextBlock
        {
            Text = model.Title,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        header.Child = headerLayout;
        header.PreviewMouseLeftButtonDown += Header_MouseLeftButtonDown;
        header.PreviewMouseMove += Header_MouseMove;
        header.PreviewMouseLeftButtonUp += Header_MouseLeftButtonUp;
        layout.Children.Add(header);

        var chart = new ReportChartVisual(model, objectLayout.StyleKey) { Margin = new Thickness(8) };
        Grid.SetRow(chart, 1);
        layout.Children.Add(chart);

        var resizeThumb = ReportCanvasResizeBehavior.CreateThumb(
            this,
            objectLayout,
            minimumWidth: 260,
            minimumHeight: 180,
            () => PositionChanged?.Invoke(this, EventArgs.Empty));
        Grid.SetRowSpan(resizeThumb, 2);
        Panel.SetZIndex(resizeThumb, 20);
        layout.Children.Add(resizeThumb);
        Child = layout;
    }

    public event EventHandler? RemoveRequested;
    public event EventHandler? PositionChanged;

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
    private readonly ReportChartModel _model;
    private readonly Color[] _palette;

    public ReportChartVisual(ReportChartModel model, string? styleKey)
    {
        _model = model;
        _palette = styleKey switch
        {
            "Green" => GreenPalette,
            "Monochrome" => MonochromePalette,
            _ => BluePalette
        };
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
        var allValues = _model.Series.SelectMany(series => series.Values).Select(decimal.ToDouble).ToList();
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
            DrawLines(dc, left, top, plotWidth, plotHeight, minimum, maximum);
        }
        else
        {
            DrawColumns(dc, left, plotWidth, zeroY, top, plotHeight, minimum, maximum);
        }

        var labelStride = Math.Max(1, (int)Math.Ceiling(_model.Categories.Count / 8d));
        for (var index = 0; index < _model.Categories.Count; index += labelStride)
        {
            var x = left + (index + 0.5) * plotWidth / _model.Categories.Count;
            DrawText(dc, _model.Categories[index], 9, new Point(x - 18, top + plotHeight + 7), pixelsPerDip, Color.FromRgb(100, 116, 139));
        }

        var legendX = left;
        for (var index = 0; index < _model.Series.Count; index++)
        {
            var name = _model.Series[index].Name;
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

    private void DrawLines(DrawingContext dc, double left, double top, double width, double height, double minimum, double maximum)
    {
        foreach (var (series, seriesIndex) in _model.Series.Select((value, index) => (value, index)))
        {
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                for (var index = 0; index < series.Values.Count; index++)
                {
                    var x = left + (index + 0.5) * width / _model.Categories.Count;
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

    private void DrawColumns(DrawingContext dc, double left, double width, double zeroY, double top, double height, double minimum, double maximum)
    {
        var categoryWidth = width / _model.Categories.Count;
        var seriesCount = Math.Max(1, _model.Series.Count);
        var barWidth = Math.Max(2, Math.Min(18, categoryWidth * 0.72 / seriesCount));
        for (var categoryIndex = 0; categoryIndex < _model.Categories.Count; categoryIndex++)
        {
            var groupWidth = barWidth * seriesCount;
            var groupLeft = left + categoryIndex * categoryWidth + (categoryWidth - groupWidth) / 2;
            for (var seriesIndex = 0; seriesIndex < _model.Series.Count; seriesIndex++)
            {
                var value = decimal.ToDouble(_model.Series[seriesIndex].Values[categoryIndex]);
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
