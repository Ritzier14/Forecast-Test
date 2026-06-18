using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App;

public sealed class ForecastGroupHeaderPresenter : FrameworkElement
{
    private const double HeaderHeight = 36;
    private const double CellPadding = 8;
    private static readonly AccountingNoDecimalsConverter AccountingConverter = new();
    private static readonly Brush HighlightFill = BrushFactory.Frozen("#66FDE68A");
    private static readonly (Brush Background, Brush Border)[] Palette =
    [
        (BrushFactory.Frozen("#EDF8F0"), BrushFactory.Frozen("#CFE3D4")),
        (BrushFactory.Frozen("#FFF4E7"), BrushFactory.Frozen("#EBD8BF")),
        (BrushFactory.Frozen("#EEF5FF"), BrushFactory.Frozen("#D2E0F1")),
        (BrushFactory.Frozen("#F5F0FF"), BrushFactory.Frozen("#DED2F0")),
        (BrushFactory.Frozen("#ECF9FA"), BrushFactory.Frozen("#CBE4E6")),
        (BrushFactory.Frozen("#FFF0F5"), BrushFactory.Frozen("#EACFD9"))
    ];
    private static readonly Dictionary<string, ImageSource> CategoryIcons = new(StringComparer.OrdinalIgnoreCase);

    private ScrollViewer? _scrollViewer;
    private MainWindow? _registeredOwner;
    private ForecastGroupHeaderSummary? _summary;
    private bool _summaryRefreshQueued;

    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(
        nameof(Items), typeof(IEnumerable), typeof(ForecastGroupHeaderPresenter),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnItemsChanged));

    public static readonly DependencyProperty GroupNameProperty = DependencyProperty.Register(
        nameof(GroupName), typeof(string), typeof(ForecastGroupHeaderPresenter),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OwningGridProperty = DependencyProperty.Register(
        nameof(OwningGrid), typeof(DataGrid), typeof(ForecastGroupHeaderPresenter),
        new FrameworkPropertyMetadata(null, OnOwningGridChanged));

    public static readonly DependencyProperty GroupIndexProperty = DependencyProperty.Register(
        nameof(GroupIndex), typeof(int), typeof(ForecastGroupHeaderPresenter),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable? Items
    {
        get => (IEnumerable?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public string GroupName
    {
        get => (string)GetValue(GroupNameProperty);
        set => SetValue(GroupNameProperty, value);
    }

    public DataGrid? OwningGrid
    {
        get => (DataGrid?)GetValue(OwningGridProperty);
        set => SetValue(OwningGridProperty, value);
    }

    public int GroupIndex
    {
        get => (int)GetValue(GroupIndexProperty);
        set => SetValue(GroupIndexProperty, value);
    }

    public ForecastGroupHeaderPresenter()
    {
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = GetVisibleColumns().Sum(column => GetColumnWidth(column));
        return new Size(Math.Max(width, OwningGrid?.ActualWidth ?? 0), HeaderHeight);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var grid = OwningGrid;
        var summary = _summary;
        if (grid is null || summary is null || summary.LineCount == 0)
        {
            return;
        }

        var columns = GetVisibleColumns();
        var horizontalOffset = _scrollViewer?.HorizontalOffset ?? 0;
        var frozenColumnCount = grid.FrozenColumnCount;
        var palette = Palette[Math.Abs(GroupIndex) % Palette.Length];
        var naturalLeft = 0d;
        var frozenLeft = 0d;
        var columnLayouts = new List<(DataGridColumn Column, double Left, double Width, bool Frozen)>();

        foreach (var column in columns)
        {
            var width = GetColumnWidth(column);
            var frozen = column.DisplayIndex < frozenColumnCount;
            var left = frozen ? horizontalOffset + frozenLeft : naturalLeft;
            columnLayouts.Add((column, left, width, frozen));
            naturalLeft += width;
            if (frozen)
            {
                frozenLeft += width;
            }
        }

        var viewportWidth = _scrollViewer?.ViewportWidth ?? grid.ActualWidth;
        var scrollingClip = new Rect(horizontalOffset + frozenLeft, 0, Math.Max(0, viewportWidth - frozenLeft), HeaderHeight);
        drawingContext.PushClip(new RectangleGeometry(scrollingClip));
        foreach (var layout in columnLayouts.Where(layout => !layout.Frozen))
        {
            DrawCell(drawingContext, layout.Column, layout.Left, layout.Width, palette, summary);
        }
        drawingContext.Pop();

        drawingContext.PushClip(new RectangleGeometry(new Rect(horizontalOffset, 0, frozenLeft, HeaderHeight)));
        foreach (var layout in columnLayouts.Where(layout => layout.Frozen))
        {
            DrawCell(drawingContext, layout.Column, layout.Left, layout.Width, palette, summary);
        }
        drawingContext.Pop();
    }

    private void DrawCell(
        DrawingContext drawingContext,
        DataGridColumn column,
        double left,
        double width,
        (Brush Background, Brush Border) palette,
        ForecastGroupHeaderSummary summary)
    {
        var rect = new Rect(left, 0, width, HeaderHeight);
        drawingContext.DrawRectangle(palette.Background, null, rect);
        if (GridColumnHighlightState.GetIsHighlighted(column))
        {
            drawingContext.DrawRectangle(HighlightFill, null, rect);
        }

        var pen = new Pen(palette.Border, 1);
        pen.Freeze();
        drawingContext.DrawLine(pen, new Point(left + width - 0.5, 0), new Point(left + width - 0.5, HeaderHeight));
        drawingContext.DrawLine(pen, new Point(left, HeaderHeight - 0.5), new Point(left + width, HeaderHeight - 0.5));
        drawingContext.DrawLine(pen, new Point(left, 0.5), new Point(left + width, 0.5));
        if (string.Equals(column.Header?.ToString(), "!", StringComparison.Ordinal))
        {
            DrawChevron(drawingContext, left, width);
            return;
        }

        var text = GetColumnText(column, summary);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var numeric = IsNumericColumn(column);
        var textLeft = left + CellPadding;
        if (IsTaskColumn(column))
        {
            var icon = GetCategoryIcon(summary.Category);
            if (icon is not null)
            {
                drawingContext.DrawImage(icon, new Rect(left + 6, 8, 20, 20));
                textLeft = left + 33;
            }
        }

        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            11,
            BrushFactory.Frozen("#17213F"),
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = Math.Max(0, width - (CellPadding * 2)),
            Trimming = TextTrimming.CharacterEllipsis
        };

        if (numeric)
        {
            textLeft = left + width - CellPadding - formatted.Width;
        }

        drawingContext.DrawText(formatted, new Point(Math.Max(left + 2, textLeft), (HeaderHeight - formatted.Height) / 2));
    }

    private void DrawChevron(DrawingContext drawingContext, double left, double width)
    {
        var centerX = left + (width / 2);
        var centerY = HeaderHeight / 2;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            if (FindAncestor<Expander>(this)?.IsExpanded == true)
            {
                context.BeginFigure(new Point(centerX - 4, centerY - 2), false, false);
                context.LineTo(new Point(centerX, centerY + 2), true, false);
                context.LineTo(new Point(centerX + 4, centerY - 2), true, false);
            }
            else
            {
                context.BeginFigure(new Point(centerX - 2, centerY - 4), false, false);
                context.LineTo(new Point(centerX + 2, centerY), true, false);
                context.LineTo(new Point(centerX - 2, centerY + 4), true, false);
            }
        }

        geometry.Freeze();
        var pen = new Pen(BrushFactory.Frozen("#40516E"), 1.5);
        pen.Freeze();
        drawingContext.DrawGeometry(null, pen, geometry);
    }

    private string GetColumnText(DataGridColumn column, ForecastGroupHeaderSummary summary)
    {
        if (column.Header is ForecastMonthColumnDefinition month)
        {
            return FormatAmount(summary.GetMonthTotal(month.Key));
        }

        return column.Header?.ToString() switch
        {
            "Task" => GroupName,
            "Category" => summary.Category,
            "CTD" => FormatAmount(summary.CostToDate),
            "Month Cost" => FormatAmount(summary.CurrentMonthCost),
            "Last Forecast" => FormatAmount(summary.LastMonthForecast),
            "Month Var" => FormatAmount(summary.VarianceLastMonthToDate),
            "CTC" => FormatAmount(summary.TotalForecastCtc),
            "FCC" => FormatAmount(summary.PlannedCostFcc),
            "Budget" => FormatAmount(summary.Budget),
            "Budget Var" => FormatAmount(summary.TotalBudgetVariance),
            _ => string.Empty
        };
    }

    private static bool IsTaskColumn(DataGridColumn column) => string.Equals(column.Header?.ToString(), "Task", StringComparison.Ordinal);

    private static bool IsNumericColumn(DataGridColumn column) =>
        column.Header is ForecastMonthColumnDefinition
        || column.Header?.ToString() is "CTD" or "Month Cost" or "Last Forecast" or "Month Var" or "CTC" or "FCC" or "Budget" or "Budget Var";

    private static string FormatAmount(decimal value) =>
        AccountingConverter.Convert(value, typeof(string), null, CultureInfo.CurrentCulture)?.ToString() ?? string.Empty;

    private IReadOnlyList<DataGridColumn> GetVisibleColumns() => OwningGrid?.Columns
        .Where(column => column.Visibility == Visibility.Visible)
        .OrderBy(column => column.DisplayIndex)
        .ToList() ?? [];

    private static double GetColumnWidth(DataGridColumn column) => column.ActualWidth > 0 ? column.ActualWidth : column.Width.DisplayValue;

    private static Pen CreateFrozenPen(string hex, double thickness)
    {
        var pen = new Pen(BrushFactory.Frozen(hex), thickness);
        pen.Freeze();
        return pen;
    }

    private static ImageSource? GetCategoryIcon(string category)
    {
        if (CategoryIcons.TryGetValue(category, out var cached))
        {
            return cached;
        }

        var fileName = category switch
        {
            "Internal Staff Costs" => "ic_category_internal_staff_20.png",
            "Design Consultants" => "ic_category_design_consultants_20.png",
            "Contractors" => "ic_category_contractors_20.png",
            "Compliance" => "ic_category_compliance_20.png",
            "Close Out" => "ic_category_closeout_20.png",
            _ => "ic_category_project_management_20.png"
        };

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri($"pack://application:,,,/Assets/Icons/png/{fileName}", UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        CategoryIcons[category] = bitmap;
        return bitmap;
    }

    private static void OnOwningGridChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is ForecastGroupHeaderPresenter presenter)
        {
            presenter.DetachGrid(eventArgs.OldValue as DataGrid);
            presenter.AttachGrid();
            presenter.InvalidateMeasure();
            presenter.InvalidateVisual();
        }
    }

    private static void OnItemsChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is not ForecastGroupHeaderPresenter presenter)
        {
            return;
        }

        presenter.DetachItems(eventArgs.OldValue as IEnumerable);
        presenter.AttachItems(eventArgs.NewValue as IEnumerable);
        presenter.RebuildSummary();
        presenter.InvalidateVisual();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachGrid();
        AttachItems(Items);
        RebuildSummary();
        _registeredOwner = FindAncestor<MainWindow>(this);
        _registeredOwner?.RegisterForecastGroupHeaderPresenter(this);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _registeredOwner?.UnregisterForecastGroupHeaderPresenter(this);
        _registeredOwner = null;
        DetachGrid();
        DetachItems(Items);
    }

    private void AttachItems(IEnumerable? items)
    {
        if (items is INotifyCollectionChanged collection)
        {
            collection.CollectionChanged -= ItemsCollectionChanged;
            collection.CollectionChanged += ItemsCollectionChanged;
        }

        if (items is not null)
        {
            foreach (var item in items.OfType<INotifyPropertyChanged>())
            {
                item.PropertyChanged -= ItemPropertyChanged;
                item.PropertyChanged += ItemPropertyChanged;
            }
        }
    }

    private void DetachItems(IEnumerable? items)
    {
        if (items is INotifyCollectionChanged collection)
        {
            collection.CollectionChanged -= ItemsCollectionChanged;
        }

        if (items is not null)
        {
            foreach (var item in items.OfType<INotifyPropertyChanged>())
            {
                item.PropertyChanged -= ItemPropertyChanged;
            }
        }
    }

    private void ItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<INotifyPropertyChanged>())
            {
                item.PropertyChanged -= ItemPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<INotifyPropertyChanged>())
            {
                item.PropertyChanged += ItemPropertyChanged;
            }
        }

        RebuildSummary();
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void ItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ShouldRefreshSummary(e.PropertyName))
        {
            QueueSummaryVisualRefresh();
        }
    }

    private static bool ShouldRefreshSummary(string? propertyName)
    {
        return string.IsNullOrEmpty(propertyName)
            || propertyName == "Item[]"
            || propertyName is nameof(ForecastLine.TaskNumber)
                or nameof(ForecastLine.ResourceName)
                or nameof(ForecastLine.ProjectCode)
                or nameof(ForecastLine.CostToDate)
                or nameof(ForecastLine.CostToDateSummary)
                or nameof(ForecastLine.CurrentMonthCost)
                or nameof(ForecastLine.LastMonthForecast)
                or nameof(ForecastLine.VarianceLastMonthToDate)
                or nameof(ForecastLine.TotalForecastCtc)
                or nameof(ForecastLine.PlannedCostFcc)
                or nameof(ForecastLine.Budget)
                or nameof(ForecastLine.TotalBudgetVariance);
    }

    private void RebuildSummary()
    {
        var lines = Items?.OfType<ForecastLine>().ToList() ?? [];
        _summary = ForecastGroupHeaderSummary.From(lines);
    }

    private void QueueSummaryVisualRefresh()
    {
        if (_summaryRefreshQueued)
        {
            return;
        }

        _summaryRefreshQueued = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _summaryRefreshQueued = false;
            RebuildSummary();
            InvalidateVisual();
        }), DispatcherPriority.Render);
    }

    private void OnMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var horizontalOffset = _scrollViewer?.HorizontalOffset ?? 0;
        var firstColumnWidth = GetVisibleColumns().FirstOrDefault() is { } first ? GetColumnWidth(first) : 26;
        var point = e.GetPosition(this);
        var isChevronColumn = point.X >= horizontalOffset && point.X <= horizontalOffset + firstColumnWidth;
        if (e.ClickCount >= 2 && !isChevronColumn)
        {
            if (FindAncestor<Expander>(this) is { } doubleClickExpander)
            {
                doubleClickExpander.IsExpanded = !doubleClickExpander.IsExpanded;
                InvalidateVisual();
                e.Handled = true;
            }

            return;
        }

        if (!isChevronColumn)
        {
            return;
        }

        if (FindAncestor<Expander>(this) is { } expander)
        {
            expander.IsExpanded = !expander.IsExpanded;
            InvalidateVisual();
            e.Handled = true;
        }
    }

    private void AttachGrid()
    {
        var grid = OwningGrid;
        if (grid is null || !IsLoaded)
        {
            return;
        }

        grid.ColumnReordered -= GridColumnReordered;
        grid.ColumnReordered += GridColumnReordered;
        grid.SizeChanged -= GridSizeChanged;
        grid.SizeChanged += GridSizeChanged;

        _scrollViewer ??= FindChild<ScrollViewer>(grid);
        if (_scrollViewer is not null)
        {
            _scrollViewer.ScrollChanged -= GridScrollChanged;
            _scrollViewer.ScrollChanged += GridScrollChanged;
        }
    }

    private void DetachGrid(DataGrid? grid = null)
    {
        grid ??= OwningGrid;
        if (grid is not null)
        {
            grid.ColumnReordered -= GridColumnReordered;
            grid.SizeChanged -= GridSizeChanged;
        }

        if (_scrollViewer is not null)
        {
            _scrollViewer.ScrollChanged -= GridScrollChanged;
            _scrollViewer = null;
        }
    }

    private void GridColumnReordered(object? sender, DataGridColumnEventArgs e)
    {
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void GridSizeChanged(object sender, SizeChangedEventArgs e) => InvalidateVisual();

    private void GridScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.HorizontalChange != 0 || e.ViewportWidthChange != 0 || e.ExtentWidthChange != 0)
        {
            InvalidateVisual();
        }
    }

    private static T? FindChild<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                return match;
            }

            var nested = FindChild<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static T? FindAncestor<T>(DependencyObject child) where T : DependencyObject
    {
        var current = VisualTreeHelper.GetParent(child);
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private sealed class ForecastGroupHeaderSummary
    {
        private readonly Dictionary<string, decimal> _monthTotals;

        private ForecastGroupHeaderSummary(
            int lineCount,
            string category,
            decimal costToDate,
            decimal currentMonthCost,
            decimal lastMonthForecast,
            decimal varianceLastMonthToDate,
            decimal totalForecastCtc,
            decimal plannedCostFcc,
            decimal budget,
            decimal totalBudgetVariance,
            Dictionary<string, decimal> monthTotals)
        {
            LineCount = lineCount;
            Category = category;
            CostToDate = costToDate;
            CurrentMonthCost = currentMonthCost;
            LastMonthForecast = lastMonthForecast;
            VarianceLastMonthToDate = varianceLastMonthToDate;
            TotalForecastCtc = totalForecastCtc;
            PlannedCostFcc = plannedCostFcc;
            Budget = budget;
            TotalBudgetVariance = totalBudgetVariance;
            _monthTotals = monthTotals;
        }

        public int LineCount { get; }
        public string Category { get; }
        public decimal CostToDate { get; }
        public decimal CurrentMonthCost { get; }
        public decimal LastMonthForecast { get; }
        public decimal VarianceLastMonthToDate { get; }
        public decimal TotalForecastCtc { get; }
        public decimal PlannedCostFcc { get; }
        public decimal Budget { get; }
        public decimal TotalBudgetVariance { get; }

        public decimal GetMonthTotal(string key) => _monthTotals.GetValueOrDefault(key);

        public static ForecastGroupHeaderSummary From(IReadOnlyList<ForecastLine> lines)
        {
            var monthTotals = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            decimal costToDate = 0;
            decimal currentMonthCost = 0;
            decimal lastMonthForecast = 0;
            decimal varianceLastMonthToDate = 0;
            decimal totalForecastCtc = 0;
            decimal plannedCostFcc = 0;
            decimal budget = 0;
            decimal totalBudgetVariance = 0;

            foreach (var line in lines)
            {
                costToDate += line.CostToDateSummary;
                currentMonthCost += line.CurrentMonthCost;
                lastMonthForecast += line.LastMonthForecast;
                varianceLastMonthToDate += line.VarianceLastMonthToDate;
                totalForecastCtc += line.TotalForecastCtc;
                plannedCostFcc += line.PlannedCostFcc;
                budget += line.Budget;
                totalBudgetVariance += line.TotalBudgetVariance;

                foreach (var forecast in line.MonthlyForecasts)
                {
                    if (string.IsNullOrWhiteSpace(forecast.PeriodLabel))
                    {
                        continue;
                    }

                    monthTotals[forecast.PeriodLabel] = monthTotals.GetValueOrDefault(forecast.PeriodLabel) + forecast.Amount;
                    if (forecast.PeriodStartDate is { } periodStart)
                    {
                        var totalKey = $"TOTAL:{periodStart.Year}";
                        monthTotals[totalKey] = monthTotals.GetValueOrDefault(totalKey) + forecast.Amount;
                    }
                }
            }

            return new ForecastGroupHeaderSummary(
                lines.Count,
                lines.FirstOrDefault()?.ProjectCode ?? string.Empty,
                costToDate,
                currentMonthCost,
                lastMonthForecast,
                varianceLastMonthToDate,
                totalForecastCtc,
                plannedCostFcc,
                budget,
                totalBudgetVariance,
                monthTotals);
        }
    }
}
