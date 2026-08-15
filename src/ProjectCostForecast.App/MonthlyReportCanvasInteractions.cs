using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.ViewModels;

namespace ProjectCostForecast.App;

public partial class MainWindow
{
    private const string ReportToolbarDragFormat = "ProjectCostForecast.ReportToolbarObject";
    internal const string ReportDataSetDragFormat = "ProjectCostForecast.ReportDataSet";
    internal const string ReportCostCodeValuePrefix = "CostCodeValue:";
    private const int DefaultReportXAxisTickFrequency = 8;
    private const int MinReportXAxisTickFrequency = 2;
    private const int MaxReportXAxisTickFrequency = 24;
    private readonly Dictionary<string, string> _selectedReportToolbarStyles = new(StringComparer.OrdinalIgnoreCase);
    private Point _reportToolbarDragStart;
    private Button? _reportToolbarDragButton;
    private Point _reportDataSetDragStart;
    private Button? _reportDataSetDragButton;
    private bool _suppressNextReportToolbarClick;
    private Button? _activeReportToolbarButton;
    private string? _pendingReportCanvasObjectType;
    private Point _reportCanvasDrawStart;
    private bool _isDrawingReportObject;
    private Border? _reportCanvasPlacementPreview;
    private string? _reportToolbarDragPreviewObjectType;
    private Point _reportCanvasPanStart;
    private double _reportCanvasPanHorizontalOffset;
    private double _reportCanvasPanVerticalOffset;
    private bool _isReportCanvasPanning;
    private ReportCanvasObjectLayout? _selectedReportCanvasObject;
    private FrameworkElement? _selectedReportCanvasElement;
    private Point _reportTickFrequencyDragStart;
    private int _reportTickFrequencyDragStartValue;
    private bool _isReportTickFrequencyDragging;
    private bool _updatingReportFormatTablePanel;

    private void InitializeReportToolbarInteractions()
    {
        ConfigureReportTool(ReportLineToolButton, "LineChart", ("Blue", "Blue"), ("Green", "Green"), ("Monochrome", "Monochrome"));
        ConfigureReportTool(ReportColumnToolButton, "ColumnChart", ("Blue", "Blue"), ("Green", "Green"), ("Monochrome", "Monochrome"));
        ConfigureReportTool(ReportProjectTitleToolButton, "ProjectTitle", ("Modern", "Modern"), ("Classic", "Classic"), ("Accent banner", "Accent"));
        ConfigureReportTool(ReportCurrentPeriodToolButton, "CurrentPeriod", ("Simple", "Simple"), ("Labelled", "Labelled"), ("Badge", "Badge"));
        ConfigureReportTool(ReportDateToolButton, "Date", ("Long date", "Long"), ("Short date", "Short"), ("Numeric", "Numeric"));
        ConfigureReportTool(ReportTableToolButton, "Table", ("Blue header", "Blue"), ("Neutral", "Neutral"), ("Compact", "Compact"));
        ConfigureReportTool(ReportTextToolButton, "Text", ("Plain", "Plain"), ("Note", "Note"), ("Callout", "Callout"));
        ConfigureReportTool(ReportTitleTextToolButton, "TitleText", ("Modern", "Modern"), ("Classic", "Classic"), ("Banner", "Banner"));
        ConfigureReportTool(ReportHeaderToolButton, "Header", ("Blue", "Blue"), ("Green", "Green"), ("Amber", "Amber"), ("Slate", "Slate"));
        SetReportToolbarPlacementToolTips();
        InitializeReportDataSetInteractions();
    }

    private void InitializeReportDataSetInteractions()
    {
        foreach (var button in new[]
        {
            ReportDataSetCostCodeButton,
            ReportDataSetHeadingButton,
            ReportDataSetSubHeadingButton,
            ReportDataSetBudgetButton,
            ReportDataSetSpendButton,
            ReportDataSetForecastButton
        })
        {
            AttachReportDataSetDragHandlers(button);
        }

        RefreshReportCostCodeFilterPills();
    }

    private void AttachReportDataSetDragHandlers(Button button)
    {
        button.PreviewMouseLeftButtonDown += ReportDataSetButton_PreviewMouseLeftButtonDown;
        button.PreviewMouseMove += ReportDataSetButton_PreviewMouseMove;
        button.PreviewMouseLeftButtonUp += ReportDataSetButton_PreviewMouseLeftButtonUp;
    }

    private void RefreshReportCostCodeFilterPills()
    {
        if (ReportCostCodeFilterPillsPanel is null)
        {
            return;
        }

        ReportCostCodeFilterPillsPanel.Children.Clear();
        foreach (var costCode in GetReportCostCodeOptions())
        {
            var button = new Button
            {
                Tag = $"{ReportCostCodeValuePrefix}{costCode}",
                Content = costCode,
                Height = 28,
                Padding = new Thickness(9, 1, 9, 1),
                Margin = new Thickness(0, 0, 5, 5),
                Background = BrushFactory.Frozen("#FFF7ED"),
                BorderBrush = BrushFactory.Frozen("#FED7AA"),
                Foreground = BrushFactory.Frozen("#9A3412"),
                ToolTip = $"Drag {costCode} onto a chart"
            };
            AttachReportDataSetDragHandlers(button);
            ReportCostCodeFilterPillsPanel.Children.Add(button);
        }
    }

    private void SetReportToolbarPlacementToolTips()
    {
        foreach (var button in new[]
        {
            ReportLineToolButton,
            ReportColumnToolButton,
            ReportProjectTitleToolButton,
            ReportCurrentPeriodToolButton,
            ReportDateToolButton,
            ReportTableToolButton,
            ReportTextToolButton,
            ReportTitleTextToolButton,
            ReportHeaderToolButton
        })
        {
            button.ToolTip = "Click to draw on the page; drag for standard size; right-click for styles";
        }
    }

    private void ConfigureReportTool(Button button, string objectType, params (string Label, string Key)[] styles)
    {
        _selectedReportToolbarStyles[objectType] = styles[0].Key;
        button.PreviewMouseLeftButtonDown += ReportToolbarButton_PreviewMouseLeftButtonDown;
        button.PreviewMouseMove += ReportToolbarButton_PreviewMouseMove;
        button.PreviewMouseLeftButtonUp += ReportToolbarButton_PreviewMouseLeftButtonUp;

        var menu = new ContextMenu();
        foreach (var (label, key) in styles)
        {
            var item = new MenuItem
            {
                Header = label,
                Tag = new ReportToolbarStyleChoice(objectType, key),
                IsCheckable = true,
                IsChecked = string.Equals(key, styles[0].Key, StringComparison.OrdinalIgnoreCase)
            };
            item.Click += ReportToolbarStyleMenuItem_Click;
            menu.Items.Add(item);
        }
        button.ContextMenu = menu;
        button.ToolTip = $"{button.ToolTip ?? button.Content} — drag onto the page; right-click for styles";
    }

    internal string GetSelectedReportToolbarStyle(string objectType)
    {
        return _selectedReportToolbarStyles.GetValueOrDefault(objectType, "Default");
    }

    private void ReportToolbarStyleMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: ReportToolbarStyleChoice choice } selected
            || selected.Parent is not ContextMenu menu)
        {
            return;
        }

        _selectedReportToolbarStyles[choice.ObjectType] = choice.StyleKey;
        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            item.IsChecked = ReferenceEquals(item, selected);
        }
    }

    private void ReportToolbarButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _suppressNextReportToolbarClick = false;
        _reportToolbarDragButton = sender as Button;
        _reportToolbarDragStart = e.GetPosition(this);
    }

    private void ReportToolbarButton_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed
            || sender is not Button button
            || !ReferenceEquals(button, _reportToolbarDragButton)
            || button.Tag is not string objectType)
        {
            return;
        }

        var point = e.GetPosition(this);
        if (Math.Abs(point.X - _reportToolbarDragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(point.Y - _reportToolbarDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _reportToolbarDragButton = null;
        _suppressNextReportToolbarClick = true;
        var data = new DataObject(ReportToolbarDragFormat, objectType);
        DragDrop.DoDragDrop(button, data, DragDropEffects.Copy);
        e.Handled = true;
    }

    private void ReportToolbarButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _reportToolbarDragButton = null;
    }

    private void ReportDataSetButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _reportDataSetDragButton = sender as Button;
        _reportDataSetDragStart = e.GetPosition(this);
    }

    private void ReportDataSetButton_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed
            || sender is not Button button
            || !ReferenceEquals(button, _reportDataSetDragButton)
            || button.Tag is not string dataSetKey)
        {
            return;
        }

        var point = e.GetPosition(this);
        if (Math.Abs(point.X - _reportDataSetDragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(point.Y - _reportDataSetDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _reportDataSetDragButton = null;
        var data = new DataObject(ReportDataSetDragFormat, dataSetKey);
        DragDrop.DoDragDrop(button, data, DragDropEffects.Copy);
        e.Handled = true;
    }

    private void ReportDataSetButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _reportDataSetDragButton = null;
    }

    private void BeginReportCanvasObjectPlacement(string objectType, Button? sourceButton)
    {
        if (_suppressNextReportToolbarClick)
        {
            _suppressNextReportToolbarClick = false;
            return;
        }

        CancelReportCanvasObjectPlacement();
        _pendingReportCanvasObjectType = objectType;
        _activeReportToolbarButton = sourceButton;
        if (_activeReportToolbarButton is not null)
        {
            _activeReportToolbarButton.Opacity = 0.78;
        }
        MonthlyReportChartCanvas.Cursor = Cursors.Cross;
    }

    private void MonthlyReportCanvas_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        if (_pendingReportCanvasObjectType is null)
        {
            if (IsReportCanvasObjectSource(source))
            {
                SelectReportCanvasObjectFromSource(source);
            }
            else
            {
                ClearReportCanvasObjectSelection();
            }

            return;
        }

        if (_isReportCanvasPanning || IsReportCanvasObjectSource(source))
        {
            return;
        }

        var start = ClampReportCanvasPoint(e.GetPosition(MonthlyReportChartCanvas));
        _reportCanvasDrawStart = start;
        _isDrawingReportObject = true;
        UpdateReportCanvasPlacementPreview(start);
        MonthlyReportChartCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void SelectReportCanvasObjectFromSource(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ReportCanvasObjectCard objectCard)
            {
                SelectReportCanvasObject(objectCard.Layout, objectCard);
                return;
            }

            if (source is ReportChartCard chartCard)
            {
                SelectReportCanvasObject(chartCard.Layout, chartCard);
                return;
            }

            source = VisualTreeHelper.GetParent(source);
        }
    }

    private void SelectReportCanvasObject(ReportCanvasObjectLayout layout, FrameworkElement element)
    {
        if (_selectedReportCanvasElement is ReportCanvasObjectCard previousObjectCard)
        {
            previousObjectCard.SetSelected(false);
        }
        else if (_selectedReportCanvasElement is ReportChartCard previousChartCard)
        {
            previousChartCard.SetSelected(false);
        }

        _selectedReportCanvasObject = layout;
        _selectedReportCanvasElement = element;
        if (element is ReportCanvasObjectCard objectCard)
        {
            objectCard.SetSelected(true);
        }
        else if (element is ReportChartCard chartCard)
        {
            chartCard.SetSelected(true);
        }

        if (IsMonthlyReportWorkspace())
        {
            ShowReportFormatTablePanel();
        }
    }

    private void ClearReportCanvasObjectSelection()
    {
        EndReportTickFrequencyDrag();
        if (_selectedReportCanvasElement is ReportCanvasObjectCard objectCard)
        {
            objectCard.SetSelected(false);
        }
        else if (_selectedReportCanvasElement is ReportChartCard chartCard)
        {
            chartCard.SetSelected(false);
        }

        _selectedReportCanvasObject = null;
        _selectedReportCanvasElement = null;
        if (IsMonthlyReportWorkspace())
        {
            SuppressDetailWorkspacePanel();
        }
    }

    private void MonthlyReportCanvas_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDrawingReportObject || _pendingReportCanvasObjectType is not string objectType)
        {
            return;
        }

        var end = ClampReportCanvasPoint(e.GetPosition(MonthlyReportChartCanvas));
        var left = Math.Min(_reportCanvasDrawStart.X, end.X);
        var top = Math.Min(_reportCanvasDrawStart.Y, end.Y);
        var width = Math.Abs(end.X - _reportCanvasDrawStart.X);
        var height = Math.Abs(end.Y - _reportCanvasDrawStart.Y);
        if (width < 8 || height < 8)
        {
            if (!TryGetReportCanvasObjectDefaults(objectType, out width, out height, out _))
            {
                CancelReportCanvasObjectPlacement();
                return;
            }
        }

        var keepPlacementPreview = IsReportChartPlacementObject(objectType);
        if (keepPlacementPreview)
        {
            SetReportCanvasPlacementPreview(new Point(left, top), new Size(width, height));
        }

        MonthlyReportChartCanvas.ReleaseMouseCapture();
        if (keepPlacementPreview)
        {
            CancelReportCanvasObjectPlacement(keepPlacementPreview: true);
        }
        else
        {
            RemoveReportCanvasPlacementPreview();
            CancelReportCanvasObjectPlacement();
        }

        AddReportCanvasObjectAt(objectType, new Point(left, top), new Size(width, height));
        if (keepPlacementPreview)
        {
            RemoveReportCanvasPlacementPreview();
            UpdateReportCanvasHint();
        }

        e.Handled = true;
    }

    private void UpdateReportCanvasPlacementPreview(Point current)
    {
        if (_pendingReportCanvasObjectType is null)
        {
            return;
        }

        var left = Math.Min(_reportCanvasDrawStart.X, current.X);
        var top = Math.Min(_reportCanvasDrawStart.Y, current.Y);
        var width = Math.Max(2, Math.Abs(current.X - _reportCanvasDrawStart.X));
        var height = Math.Max(2, Math.Abs(current.Y - _reportCanvasDrawStart.Y));
        SetReportCanvasPlacementPreview(new Point(left, top), new Size(width, height));
    }

    private void SetReportCanvasPlacementPreview(Point position, Size size)
    {
        var preview = _reportCanvasPlacementPreview ??= new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(18, 37, 99, 235)),
            BorderBrush = BrushFactory.Frozen("#2563EB"),
            BorderThickness = new Thickness(1.5),
            IsHitTestVisible = false,
            Opacity = 0.9
        };

        if (!MonthlyReportChartCanvas.Children.Contains(preview))
        {
            MonthlyReportChartCanvas.Children.Add(preview);
        }

        Canvas.SetLeft(preview, position.X);
        Canvas.SetTop(preview, position.Y);
        preview.Width = Math.Max(2, size.Width);
        preview.Height = Math.Max(2, size.Height);
        Panel.SetZIndex(preview, 1000);
    }

    private void RemoveReportCanvasPlacementPreview()
    {
        if (_reportCanvasPlacementPreview is not null)
        {
            MonthlyReportChartCanvas.Children.Remove(_reportCanvasPlacementPreview);
        }
    }

    private void ClearReportToolbarDragPreview()
    {
        if (_reportToolbarDragPreviewObjectType is null)
        {
            return;
        }

        _reportToolbarDragPreviewObjectType = null;
        if (!_isDrawingReportObject && _pendingReportCanvasObjectType is null)
        {
            RemoveReportCanvasPlacementPreview();
            UpdateReportCanvasHint();
        }
    }

    private void CancelReportCanvasObjectPlacement(bool keepPlacementPreview = false)
    {
        EndReportTickFrequencyDrag();
        _isDrawingReportObject = false;
        _pendingReportCanvasObjectType = null;
        _reportToolbarDragPreviewObjectType = null;
        if (!keepPlacementPreview)
        {
            RemoveReportCanvasPlacementPreview();
        }
        if (MonthlyReportChartCanvas.IsMouseCaptured)
        {
            MonthlyReportChartCanvas.ReleaseMouseCapture();
        }

        if (_activeReportToolbarButton is not null)
        {
            _activeReportToolbarButton.Opacity = 1;
            _activeReportToolbarButton = null;
        }

        MonthlyReportChartCanvas.Cursor = Cursors.Arrow;
        UpdateReportCanvasHint();
    }

    private static bool IsReportChartPlacementObject(string objectType)
        => string.Equals(objectType, "LineChart", StringComparison.OrdinalIgnoreCase)
        || string.Equals(objectType, "ColumnChart", StringComparison.OrdinalIgnoreCase);

    private void AddReportCanvasObjectAt(string objectType, Point position, Size size)
    {
        if (string.Equals(objectType, "LineChart", StringComparison.OrdinalIgnoreCase))
        {
            ShowReportChartBuilder(ReportChartKind.Line, position, size);
            return;
        }

        if (string.Equals(objectType, "ColumnChart", StringComparison.OrdinalIgnoreCase))
        {
            ShowReportChartBuilder(ReportChartKind.Column, position, size);
            return;
        }

        if (!TryGetReportCanvasObjectDefaults(objectType, out _, out _, out var defaultText))
        {
            return;
        }

        AddNewReportObject(objectType, size.Width, size.Height, defaultText, position);
    }

    private void AddStandardReportCanvasObject(string objectType, Point position)
    {
        if (!TryGetReportCanvasObjectDefaults(objectType, out var width, out var height, out _))
        {
            return;
        }

        var size = new Size(width, height);
        AddReportCanvasObjectAt(objectType, ClampReportCanvasObjectPosition(position, size), size);
    }

    private Point ClampReportCanvasObjectPosition(Point position, Size size)
        => new(
            Math.Clamp(position.X, 0, Math.Max(0, MonthlyReportChartCanvas.Width - size.Width)),
            Math.Clamp(position.Y, 0, Math.Max(0, MonthlyReportChartCanvas.Height - size.Height)));

    private Point ClampReportCanvasPoint(Point point)
    {
        return new Point(
            Math.Clamp(point.X, 0, Math.Max(0, MonthlyReportChartCanvas.Width)),
            Math.Clamp(point.Y, 0, Math.Max(0, MonthlyReportChartCanvas.Height)));
    }

    private static bool IsReportCanvasObjectSource(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ReportCanvasObjectCard or ReportChartCard)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void MonthlyReportCanvas_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(ReportToolbarDragFormat) is string objectType
            && TryGetReportCanvasObjectDefaults(objectType, out var toolbarWidth, out var toolbarHeight, out _))
        {
            _reportToolbarDragPreviewObjectType = objectType;
            var toolbarSize = new Size(toolbarWidth, toolbarHeight);
            var toolbarPosition = ClampReportCanvasObjectPosition(e.GetPosition(MonthlyReportChartCanvas), toolbarSize);
            SetReportCanvasPlacementPreview(toolbarPosition, toolbarSize);
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
            return;
        }

        if (_reportToolbarDragPreviewObjectType is not null)
        {
            ClearReportToolbarDragPreview();
        }

        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void MonthlyReportCanvas_DragLeave(object sender, DragEventArgs e)
    {
        ClearReportToolbarDragPreview();
        e.Handled = true;
    }

    private void MonthlyReportCanvas_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(ReportToolbarDragFormat) is not string objectType)
        {
            return;
        }

        var position = e.GetPosition(MonthlyReportChartCanvas);
        var keepPlacementPreview = IsReportChartPlacementObject(objectType);
        if (TryGetReportCanvasObjectDefaults(objectType, out var width, out var height, out _))
        {
            var size = new Size(width, height);
            position = ClampReportCanvasObjectPosition(position, size);
            if (keepPlacementPreview)
            {
                SetReportCanvasPlacementPreview(position, size);
            }
        }

        _reportToolbarDragPreviewObjectType = null;
        if (keepPlacementPreview)
        {
            CancelReportCanvasObjectPlacement(keepPlacementPreview: true);
        }
        else
        {
            CancelReportCanvasObjectPlacement();
        }

        AddStandardReportCanvasObject(objectType, position);
        if (keepPlacementPreview)
        {
            RemoveReportCanvasPlacementPreview();
            UpdateReportCanvasHint();
        }
        e.Handled = true;
    }

    private void MonthlyReportCanvas_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (TryBeginReportTickFrequencyDrag(e.OriginalSource as DependencyObject, e))
        {
            e.Handled = true;
            return;
        }

        _isReportCanvasPanning = true;
        _reportCanvasPanStart = e.GetPosition(MonthlyReportCanvasScrollViewer);
        _reportCanvasPanHorizontalOffset = MonthlyReportCanvasScrollViewer.HorizontalOffset;
        _reportCanvasPanVerticalOffset = MonthlyReportCanvasScrollViewer.VerticalOffset;
        MonthlyReportCanvasScrollViewer.Cursor = Cursors.ScrollAll;
        MonthlyReportCanvasScrollViewer.CaptureMouse();
        e.Handled = true;
    }

    private void MonthlyReportCanvas_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_isDrawingReportObject && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateReportCanvasPlacementPreview(ClampReportCanvasPoint(e.GetPosition(MonthlyReportChartCanvas)));
            e.Handled = true;
            return;
        }

        if (_isReportTickFrequencyDragging && e.RightButton == MouseButtonState.Pressed)
        {
            UpdateReportTickFrequency(e.GetPosition(MonthlyReportChartCanvas));
            e.Handled = true;
            return;
        }

        if (!_isReportCanvasPanning || e.RightButton != MouseButtonState.Pressed)
        {
            return;
        }

        var point = e.GetPosition(MonthlyReportCanvasScrollViewer);
        MonthlyReportCanvasScrollViewer.ScrollToHorizontalOffset(
            _reportCanvasPanHorizontalOffset - (point.X - _reportCanvasPanStart.X));
        MonthlyReportCanvasScrollViewer.ScrollToVerticalOffset(
            _reportCanvasPanVerticalOffset - (point.Y - _reportCanvasPanStart.Y));
        e.Handled = true;
    }

    private void MonthlyReportCanvas_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isReportTickFrequencyDragging)
        {
            EndReportTickFrequencyDrag();
            e.Handled = true;
            return;
        }

        if (!_isReportCanvasPanning)
        {
            return;
        }

        _isReportCanvasPanning = false;
        MonthlyReportCanvasScrollViewer.ReleaseMouseCapture();
        MonthlyReportCanvasScrollViewer.Cursor = Cursors.Arrow;
        e.Handled = true;
    }

    private bool TryBeginReportTickFrequencyDrag(DependencyObject? source, MouseButtonEventArgs e)
    {
        if (_selectedReportCanvasObject is not { } selectedLayout
            || !string.Equals(selectedLayout.ObjectType, "Chart", StringComparison.OrdinalIgnoreCase)
            || !IsSourceInsideSelectedReportChart(source))
        {
            return false;
        }

        _isReportTickFrequencyDragging = true;
        _reportTickFrequencyDragStart = e.GetPosition(MonthlyReportChartCanvas);
        _reportTickFrequencyDragStartValue = GetReportXAxisTickFrequency(selectedLayout);
        MonthlyReportChartCanvas.CaptureMouse();
        MonthlyReportChartCanvas.Cursor = Cursors.SizeWE;
        return true;
    }

    private bool IsSourceInsideSelectedReportChart(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ReportChartCard chartCard)
            {
                return ReferenceEquals(chartCard.Layout, _selectedReportCanvasObject);
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void UpdateReportTickFrequency(Point current)
    {
        if (_selectedReportCanvasObject is not { } layout
            || !string.Equals(layout.ObjectType, "Chart", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var horizontalDelta = current.X - _reportTickFrequencyDragStart.X;
        var steps = (int)Math.Round(horizontalDelta / 32d);
        var nextFrequency = Math.Clamp(
            _reportTickFrequencyDragStartValue + steps,
            MinReportXAxisTickFrequency,
            MaxReportXAxisTickFrequency);
        if (nextFrequency == GetReportXAxisTickFrequency(layout))
        {
            return;
        }

        layout.XAxisTickFrequency = nextFrequency;
        if (_selectedReportCanvasElement is ReportChartCard chartCard)
        {
            chartCard.RefreshChart();
        }

        UpdateReportFormatTablePanel();
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.IsDirty = true;
        }
    }

    private void EndReportTickFrequencyDrag()
    {
        if (!_isReportTickFrequencyDragging)
        {
            return;
        }

        _isReportTickFrequencyDragging = false;
        if (MonthlyReportChartCanvas.IsMouseCaptured)
        {
            MonthlyReportChartCanvas.ReleaseMouseCapture();
        }

        MonthlyReportChartCanvas.Cursor = _pendingReportCanvasObjectType is null ? Cursors.Arrow : Cursors.Cross;
    }

    private static int GetReportXAxisTickFrequency(ReportCanvasObjectLayout layout)
        => Math.Clamp(
            layout.XAxisTickFrequency > 0 ? layout.XAxisTickFrequency : DefaultReportXAxisTickFrequency,
            MinReportXAxisTickFrequency,
            MaxReportXAxisTickFrequency);

    private void ReportFormatTableTickFrequencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingReportFormatTablePanel
            || _selectedReportCanvasObject is not { } layout
            || !string.Equals(layout.ObjectType, "Chart", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var nextFrequency = Math.Clamp(
            (int)Math.Round(e.NewValue),
            MinReportXAxisTickFrequency,
            MaxReportXAxisTickFrequency);
        if (GetReportXAxisTickFrequency(layout) != nextFrequency)
        {
            layout.XAxisTickFrequency = nextFrequency;
            if (_selectedReportCanvasElement is ReportChartCard chartCard)
            {
                chartCard.RefreshChart();
            }

            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.IsDirty = true;
            }
        }

        UpdateReportFormatTablePanel();
    }

    private void UpdateReportFormatTablePanel()
    {
        if (_selectedReportCanvasObject is not { } layout)
        {
            return;
        }

        ReportFormatTableSelectionText.Text = GetReportCanvasObjectDisplayName(layout);
        ReportFormatTableObjectText.Text = layout.ObjectType switch
        {
            "Chart" => $"{layout.ChartKind} chart",
            "Table" => "Report table",
            _ => GetReportCanvasObjectDisplayName(layout)
        };

        var isChart = string.Equals(layout.ObjectType, "Chart", StringComparison.OrdinalIgnoreCase);
        ReportFormatTableTickFrequencySection.Visibility = isChart ? Visibility.Visible : Visibility.Collapsed;
        ReportFormatTableNoChartText.Visibility = isChart ? Visibility.Collapsed : Visibility.Visible;
        _updatingReportFormatTablePanel = true;
        try
        {
            if (isChart)
            {
                var frequency = GetReportXAxisTickFrequency(layout);
                ReportFormatTableTickFrequencySlider.Value = frequency;
                ReportFormatTableTickFrequencyValue.Text = $"{frequency} date ticks";
            }
        }
        finally
        {
            _updatingReportFormatTablePanel = false;
        }
    }

    private static string GetReportCanvasObjectDisplayName(ReportCanvasObjectLayout layout)
        => layout.ObjectType switch
        {
            "Chart" => "Chart",
            "ProjectTitle" => "Project title",
            "CurrentPeriod" => "Period",
            "Date" => "Date",
            "Table" => "Table",
            "TitleText" => "Title text",
            "Text" => "Text",
            "Header" => "Header",
            _ => string.IsNullOrWhiteSpace(layout.ObjectType) ? "Report object" : layout.ObjectType
        };

    private FrameworkElement CreateStyledProjectTitle(ReportCanvasObjectLayout layout)
    {
        var text = BoundText("Header.ProjectTitle", layout.StyleKey == "Classic" ? 24 : 22,
            layout.StyleKey == "Classic" ? FontWeights.Bold : FontWeights.SemiBold);
        if (layout.StyleKey == "Classic")
        {
            text.FontFamily = new FontFamily("Georgia");
        }
        if (layout.StyleKey != "Accent")
        {
            return text;
        }
        text.Foreground = Brushes.White;
        return new Border { Background = BrushFactory.Frozen("#2563EB"), Child = text };
    }

    private FrameworkElement CreateStyledCurrentPeriod(ReportCanvasObjectLayout layout)
    {
        var value = BoundText("Header.CurrentPeriod", 16, FontWeights.SemiBold);
        if (layout.StyleKey == "Simple")
        {
            return value;
        }

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new TextBlock
        {
            Text = "Period",
            Foreground = BrushFactory.Frozen("#64748B"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });
        value.Margin = new Thickness(0);
        if (layout.StyleKey == "Badge")
        {
            panel.Children.Add(new Border
            {
                Background = BrushFactory.Frozen("#DBEAFE"),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(9, 4, 9, 4),
                Child = value
            });
        }
        else
        {
            panel.Children.Add(value);
        }
        return panel;
    }

    private FrameworkElement CreateStyledReportHeader(ReportCanvasObjectLayout layout)
    {
        var (background, foreground) = layout.StyleKey switch
        {
            "Green" => ("#166534", "#FFFFFF"),
            "Amber" => ("#B45309", "#FFFFFF"),
            "Slate" => ("#334155", "#FFFFFF"),
            _ => ("#1D4ED8", "#FFFFFF")
        };
        var text = EditableText(layout, 20, FontWeights.SemiBold);
        text.Foreground = BrushFactory.Frozen(foreground);
        text.Background = Brushes.Transparent;
        text.Padding = new Thickness(14, 8, 14, 8);
        text.VerticalContentAlignment = VerticalAlignment.Center;
        return new Border
        {
            Background = BrushFactory.Frozen(background),
            BorderBrush = BrushFactory.Frozen(background),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            ClipToBounds = true,
            Child = text
        };
    }

    private static FrameworkElement CreateStyledDate(ReportCanvasObjectLayout layout)
    {
        var format = layout.StyleKey switch
        {
            "Short" => "dd MMM yyyy",
            "Numeric" => "dd/MM/yyyy",
            _ => "dddd, dd MMMM yyyy"
        };
        return new TextBlock
        {
            Text = DateTime.Today.ToString(format),
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10)
        };
    }

    private static FrameworkElement CreateStyledTable(ReportCanvasObjectLayout layout)
    {
        var table = new DataGrid
        {
            AutoGenerateColumns = true,
            IsReadOnly = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            Margin = new Thickness(4),
            RowHeight = layout.StyleKey == "Compact" ? 24 : double.NaN
        };
        var headerStyle = new Style(typeof(DataGridColumnHeader));
        headerStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        headerStyle.Setters.Add(new Setter(Control.BackgroundProperty,
            layout.StyleKey == "Neutral" ? BrushFactory.Frozen("#E2E8F0") : BrushFactory.Frozen("#5B9BD5")));
        headerStyle.Setters.Add(new Setter(Control.ForegroundProperty,
            layout.StyleKey == "Neutral" ? BrushFactory.Frozen("#334155") : Brushes.White));
        table.ColumnHeaderStyle = headerStyle;
        table.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("MonthlyReportCategoryRows"));
        return table;
    }

    private FrameworkElement CreateStyledEditableText(ReportCanvasObjectLayout layout, bool isTitle)
    {
        var text = EditableText(layout, isTitle ? 20 : 13, isTitle ? FontWeights.SemiBold : FontWeights.Normal);
        if (layout.StyleKey == "Classic")
        {
            text.FontFamily = new FontFamily("Georgia");
        }

        if ((!isTitle && layout.StyleKey == "Plain") || (isTitle && layout.StyleKey == "Modern"))
        {
            return text;
        }

        var background = layout.StyleKey switch
        {
            "Note" => "#FEF3C7",
            "Callout" => "#DBEAFE",
            "Banner" => "#1E3A8A",
            _ => "#F8FAFC"
        };
        if (layout.StyleKey == "Banner")
        {
            text.Foreground = Brushes.White;
            text.Background = Brushes.Transparent;
        }
        return new Border
        {
            Background = BrushFactory.Frozen(background),
            BorderBrush = BrushFactory.Frozen(layout.StyleKey == "Note" ? "#F59E0B" : "#CBD5E1"),
            BorderThickness = new Thickness(layout.StyleKey == "Note" ? 0 : 1, 0, 0, 0),
            Child = text
        };
    }

    private sealed record ReportToolbarStyleChoice(string ObjectType, string StyleKey);
}
