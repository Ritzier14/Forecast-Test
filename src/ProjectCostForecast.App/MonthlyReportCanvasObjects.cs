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
    private WorkspaceViewTab? _loadedMonthlyReportView;
    private ScrollViewer? _legacyMonthlyReportViewer;
    private bool _monthlyReportCanvasInitialized;
    private bool _isLoadingReportCanvasView;

    private void InitializeMonthlyReportCanvas()
    {
        if (_monthlyReportCanvasInitialized)
        {
            return;
        }

        _monthlyReportCanvasInitialized = true;
        InitializeReportToolbarInteractions();
        _legacyMonthlyReportViewer = LegacyMonthlyReportViewer;
        if (VisualTreeHelper.GetParent(LegacyMonthlyReportViewer) is Panel parent)
        {
            parent.Children.Remove(LegacyMonthlyReportViewer);
        }

        SwitchMonthlyReportCanvasView();
    }

    private WorkspaceViewTab? GetCurrentMonthlyReportView()
    {
        return DataContext is MainWindowViewModel
        {
            ActiveWorkspaceKey: "Monthly Report",
            SelectedWorkspaceView: { } view
        } ? view : null;
    }

    private void SwitchMonthlyReportCanvasView()
    {
        if (!_monthlyReportCanvasInitialized || GetCurrentMonthlyReportView() is not { } view)
        {
            return;
        }

        if (ReferenceEquals(_loadedMonthlyReportView, view))
        {
            return;
        }

        DetachLegacyReportViewer();
        foreach (var child in MonthlyReportChartCanvas.Children
                     .OfType<FrameworkElement>()
                     .Where(child => !ReferenceEquals(child, MonthlyReportChartCanvasHint))
                     .ToList())
        {
            MonthlyReportChartCanvas.Children.Remove(child);
        }

        if (!view.ReportCanvasInitialized)
        {
            view.ReportCanvasInitialized = true;
            view.ReportCanvasPageSize = "A4";
            view.ReportCanvasOrientation = "Portrait";
            view.ReportCanvasObjects =
            [
                new ReportCanvasObjectLayout
                {
                    ObjectType = "LegacyReport",
                    X = 24,
                    Y = 24,
                    Width = 746,
                    Height = 1048
                }
            ];
        }
        else if (DataContext is MainWindowViewModel reportViewModel
                 && !ReferenceEquals(reportViewModel.CurrentWorkspaceViews.FirstOrDefault(), view)
                 && IsUnchangedLegacyReportCopy(view.ReportCanvasObjects))
        {
            // Early report views copied the complete legacy report into every new view.
            // Migrate only that exact, untouched duplicate so existing View 2 pages become
            // independent blank canvases without removing genuinely edited layouts.
            view.ReportCanvasObjects.Clear();
            if (DataContext is MainWindowViewModel migratedViewModel)
            {
                migratedViewModel.IsDirty = true;
            }
        }

        _loadedMonthlyReportView = view;
        _isLoadingReportCanvasView = true;
        ReportCanvasSizeComboBox.SelectedIndex = string.Equals(view.ReportCanvasPageSize, "A3", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        ReportCanvasOrientationComboBox.SelectedIndex = string.Equals(view.ReportCanvasOrientation, "Landscape", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        ApplyReportCanvasDimensions(view.ReportCanvasPageSize, view.ReportCanvasOrientation);
        _isLoadingReportCanvasView = false;

        foreach (var layout in view.ReportCanvasObjects.ToList())
        {
            AddReportCanvasObject(layout);
        }

        UpdateReportCanvasHint();
    }

    private static bool IsUnchangedLegacyReportCopy(IReadOnlyCollection<ReportCanvasObjectLayout> layouts)
    {
        if (layouts.Count != 1)
        {
            return false;
        }

        var layout = layouts.First();
        return string.Equals(layout.ObjectType, "LegacyReport", StringComparison.OrdinalIgnoreCase)
               && Math.Abs(layout.X - 24) < 0.01
               && Math.Abs(layout.Y - 24) < 0.01
               && Math.Abs(layout.Width - 746) < 0.01
               && Math.Abs(layout.Height - 1048) < 0.01;
    }

    private void ApplyReportCanvasDimensions(string pageSize, string orientation)
    {
        var isA3 = string.Equals(pageSize, "A3", StringComparison.OrdinalIgnoreCase);
        var width = isA3 ? 1123d : 794d;
        var height = isA3 ? 1587d : 1123d;
        if (string.Equals(orientation, "Landscape", StringComparison.OrdinalIgnoreCase))
        {
            (width, height) = (height, width);
        }

        MonthlyReportChartCanvas.Width = width;
        MonthlyReportChartCanvas.Height = height;
    }

    private void AddReportCanvasObject(ReportCanvasObjectLayout layout)
    {
        if (string.Equals(layout.ObjectType, "Chart", StringComparison.OrdinalIgnoreCase))
        {
            if (DataContext is not MainWindowViewModel viewModel
                || !Enum.TryParse(layout.ChartKind, true, out ReportChartKind kind)
                || !Enum.TryParse(layout.Grouping, true, out ReportChartGrouping grouping)
                || layout.FromDate is not { } fromDate
                || layout.ToDate is not { } toDate)
            {
                return;
            }

            AddReportChartCard(layout, BuildReportChartModel(viewModel, kind, grouping, fromDate, toDate));
            return;
        }

        var card = CreateReportObjectCard(layout);
        if (card is null)
        {
            return;
        }

        AddCardToCanvas(card, layout);
    }

    private ReportCanvasObjectCard? CreateReportObjectCard(ReportCanvasObjectLayout layout)
    {
        FrameworkElement content;
        string heading;
        switch (layout.ObjectType)
        {
            case "LegacyReport":
                if (_legacyMonthlyReportViewer is null)
                {
                    return null;
                }
                heading = "Monthly report";
                content = _legacyMonthlyReportViewer;
                break;
            case "ProjectTitle":
                heading = "Project title";
                content = CreateStyledProjectTitle(layout);
                break;
            case "CurrentPeriod":
                heading = "Current period";
                content = CreateStyledCurrentPeriod(layout);
                break;
            case "Date":
                heading = "Date";
                content = CreateStyledDate(layout);
                break;
            case "Table":
                heading = "Report table";
                content = CreateStyledTable(layout);
                break;
            case "TitleText":
                heading = "Title text";
                content = CreateStyledEditableText(layout, isTitle: true);
                break;
            case "Text":
                heading = "Text box";
                content = CreateStyledEditableText(layout, isTitle: false);
                break;
            default:
                return null;
        }

        var card = new ReportCanvasObjectCard(heading, content, layout);
        card.RemoveRequested += ReportCanvasCard_RemoveRequested;
        card.PositionChanged += ReportCanvasCard_PositionChanged;
        return card;
    }

    private static TextBlock BoundText(string path, double fontSize, FontWeight fontWeight)
    {
        var text = new TextBlock
        {
            FontSize = fontSize,
            FontWeight = fontWeight,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(10)
        };
        text.SetBinding(TextBlock.TextProperty, new Binding(path));
        return text;
    }

    private TextBox EditableText(ReportCanvasObjectLayout layout, double fontSize, FontWeight fontWeight)
    {
        var text = new TextBox
        {
            Text = layout.Text,
            FontSize = fontSize,
            FontWeight = fontWeight,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8)
        };
        text.TextChanged += (_, _) =>
        {
            layout.Text = text.Text;
            if (!_isLoadingReportCanvasView && DataContext is MainWindowViewModel viewModel)
            {
                viewModel.IsDirty = true;
            }
        };
        return text;
    }

    private void AddReportChartCard(ReportCanvasObjectLayout layout, ReportChartModel model)
    {
        var chart = new ReportChartCard(model, layout);
        chart.RemoveRequested += (_, _) => RemoveReportCanvasItem(chart, layout);
        chart.PositionChanged += ReportCanvasCard_PositionChanged;
        AddCardToCanvas(chart, layout);
    }

    private void AddCardToCanvas(FrameworkElement card, ReportCanvasObjectLayout layout)
    {
        Canvas.SetLeft(card, Math.Clamp(layout.X, 0, Math.Max(0, MonthlyReportChartCanvas.Width - card.Width)));
        Canvas.SetTop(card, Math.Clamp(layout.Y, 0, Math.Max(0, MonthlyReportChartCanvas.Height - card.Height)));
        MonthlyReportChartCanvas.Children.Add(card);
        UpdateReportCanvasHint();
    }

    private void AddReportCanvasLayout(ReportCanvasObjectLayout layout)
    {
        if (GetCurrentMonthlyReportView() is not { } view)
        {
            return;
        }

        view.ReportCanvasInitialized = true;
        view.ReportCanvasObjects.Add(layout);
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.IsDirty = true;
        }
    }

    private void ReportCanvasCard_RemoveRequested(object? sender, EventArgs e)
    {
        if (sender is ReportCanvasObjectCard card)
        {
            if (string.Equals(card.Layout.ObjectType, "LegacyReport", StringComparison.OrdinalIgnoreCase))
            {
                card.TakeContent();
            }
            RemoveReportCanvasItem(card, card.Layout);
        }
    }

    private void RemoveReportCanvasItem(FrameworkElement element, ReportCanvasObjectLayout layout)
    {
        MonthlyReportChartCanvas.Children.Remove(element);
        if (GetCurrentMonthlyReportView() is { } view)
        {
            view.ReportCanvasObjects.Remove(layout);
        }
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.IsDirty = true;
        }
        UpdateReportCanvasHint();
    }

    private void ReportCanvasCard_PositionChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.IsDirty = true;
        }
    }

    private void DetachLegacyReportViewer()
    {
        var legacyCard = MonthlyReportChartCanvas.Children
            .OfType<ReportCanvasObjectCard>()
            .FirstOrDefault(card => string.Equals(card.Layout.ObjectType, "LegacyReport", StringComparison.OrdinalIgnoreCase));
        legacyCard?.TakeContent();
    }

    private void UpdateReportCanvasHint()
    {
        MonthlyReportChartCanvasHint.Visibility = MonthlyReportChartCanvas.Children
            .OfType<FrameworkElement>()
            .Any(child => !ReferenceEquals(child, MonthlyReportChartCanvasHint))
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void AddProjectTitleReportObjectButton_Click(object sender, RoutedEventArgs e) =>
        AddNewReportObject("ProjectTitle", 360, 86);

    private void AddCurrentPeriodReportObjectButton_Click(object sender, RoutedEventArgs e) =>
        AddNewReportObject("CurrentPeriod", 220, 78);

    private void AddDateReportObjectButton_Click(object sender, RoutedEventArgs e) =>
        AddNewReportObject("Date", 220, 76);

    private void AddTableReportObjectButton_Click(object sender, RoutedEventArgs e) =>
        AddNewReportObject("Table", 700, 320);

    private void AddTextReportObjectButton_Click(object sender, RoutedEventArgs e) =>
        AddNewReportObject("Text", 340, 170, "Enter report text here.");

    private void AddTitleTextReportObjectButton_Click(object sender, RoutedEventArgs e) =>
        AddNewReportObject("TitleText", 360, 100, "Report title");

    private void AddNewReportObject(
        string objectType,
        double width,
        double height,
        string text = "",
        Point? dropPosition = null)
    {
        var offset = (_reportChartCount++ % 8) * 22;
        var position = dropPosition ?? new Point(24 + offset, 24 + offset);
        var layout = new ReportCanvasObjectLayout
        {
            ObjectType = objectType,
            X = position.X,
            Y = position.Y,
            Width = width,
            Height = height,
            Text = text,
            StyleKey = GetSelectedReportToolbarStyle(objectType)
        };
        AddReportCanvasLayout(layout);
        AddReportCanvasObject(layout);
    }
}

internal sealed class ReportCanvasObjectCard : Border
{
    private readonly ContentControl _contentHost;
    private Point _dragOrigin;
    private double _leftOrigin;
    private double _topOrigin;
    private bool _isDragging;

    public ReportCanvasObjectCard(string heading, FrameworkElement content, ReportCanvasObjectLayout layout)
    {
        Layout = layout;
        Width = layout.Width;
        Height = layout.Height;
        Background = Brushes.White;
        BorderBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184));
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(5);

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Cursor = Cursors.SizeAll,
            Padding = new Thickness(10, 0, 4, 0)
        };
        var headerPanel = new DockPanel();
        var remove = new Button
        {
            Content = "×",
            Width = 27,
            Height = 27,
            FontSize = 17,
            ToolTip = "Remove object",
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139))
        };
        remove.Click += (_, _) => RemoveRequested?.Invoke(this, EventArgs.Empty);
        DockPanel.SetDock(remove, Dock.Right);
        headerPanel.Children.Add(remove);
        headerPanel.Children.Add(new TextBlock
        {
            Text = heading,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
            VerticalAlignment = VerticalAlignment.Center
        });
        header.Child = headerPanel;
        header.PreviewMouseLeftButtonDown += Header_MouseLeftButtonDown;
        header.PreviewMouseMove += Header_MouseMove;
        header.PreviewMouseLeftButtonUp += Header_MouseLeftButtonUp;
        grid.Children.Add(header);

        _contentHost = new ContentControl { Content = content };
        Grid.SetRow(_contentHost, 1);
        grid.Children.Add(_contentHost);

        var resizeThumb = ReportCanvasResizeBehavior.CreateThumb(
            this,
            layout,
            minimumWidth: string.Equals(layout.ObjectType, "LegacyReport", StringComparison.OrdinalIgnoreCase) ? 360 : 120,
            minimumHeight: string.Equals(layout.ObjectType, "LegacyReport", StringComparison.OrdinalIgnoreCase) ? 260 : 70,
            () => PositionChanged?.Invoke(this, EventArgs.Empty));
        Grid.SetRowSpan(resizeThumb, 2);
        Panel.SetZIndex(resizeThumb, 20);
        grid.Children.Add(resizeThumb);
        Child = grid;
    }

    public ReportCanvasObjectLayout Layout { get; }
    public event EventHandler? RemoveRequested;
    public event EventHandler? PositionChanged;

    public FrameworkElement? TakeContent()
    {
        var content = _contentHost.Content as FrameworkElement;
        _contentHost.Content = null;
        return content;
    }

    public void SyncPositionFromCanvas()
    {
        Layout.X = Canvas.GetLeft(this);
        Layout.Y = Canvas.GetTop(this);
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindButtonAncestor(e.OriginalSource as DependencyObject, sender as DependencyObject))
        {
            return;
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
        Canvas.SetLeft(this, Math.Clamp(_leftOrigin + point.X - _dragOrigin.X, 0, Math.Max(0, canvas.Width - Width)));
        Canvas.SetTop(this, Math.Clamp(_topOrigin + point.Y - _dragOrigin.Y, 0, Math.Max(0, canvas.Height - Height)));
        SyncPositionFromCanvas();
    }

    private void Header_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        ((UIElement)sender).ReleaseMouseCapture();
        Panel.SetZIndex(this, 1);
        PositionChanged?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private static bool FindButtonAncestor(DependencyObject? source, DependencyObject? stop)
    {
        while (source is not null)
        {
            if (source is Button)
            {
                return true;
            }
            if (ReferenceEquals(source, stop))
            {
                return false;
            }
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }
}

internal static class ReportCanvasResizeBehavior
{
    public static Thumb CreateThumb(
        FrameworkElement target,
        ReportCanvasObjectLayout layout,
        double minimumWidth,
        double minimumHeight,
        Action changed)
    {
        var thumb = new Thumb
        {
            Width = 14,
            Height = 14,
            Margin = new Thickness(0, 0, 3, 3),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Cursor = Cursors.SizeNWSE,
            Background = BrushFactory.Frozen("#2563EB"),
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1),
            ToolTip = "Drag to resize. Hold Shift to preserve proportions."
        };

        var aspectRatio = target.Width / Math.Max(1, target.Height);
        thumb.DragStarted += (_, _) =>
        {
            aspectRatio = target.ActualWidth / Math.Max(1, target.ActualHeight);
        };
        thumb.DragDelta += (_, e) =>
        {
            var proposedWidth = Math.Max(minimumWidth, target.Width + e.HorizontalChange);
            var proposedHeight = Math.Max(minimumHeight, target.Height + e.VerticalChange);

            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                if (Math.Abs(e.HorizontalChange) >= Math.Abs(e.VerticalChange))
                {
                    proposedHeight = Math.Max(minimumHeight, proposedWidth / Math.Max(0.01, aspectRatio));
                    proposedWidth = proposedHeight * aspectRatio;
                }
                else
                {
                    proposedWidth = Math.Max(minimumWidth, proposedHeight * aspectRatio);
                    proposedHeight = proposedWidth / Math.Max(0.01, aspectRatio);
                }
            }

            if (target.Parent is Canvas canvas)
            {
                var left = Math.Max(0, Canvas.GetLeft(target));
                var top = Math.Max(0, Canvas.GetTop(target));
                proposedWidth = Math.Min(proposedWidth, Math.Max(minimumWidth, canvas.Width - left));
                proposedHeight = Math.Min(proposedHeight, Math.Max(minimumHeight, canvas.Height - top));

                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                {
                    var scale = Math.Min(
                        proposedWidth / Math.Max(1, target.Width),
                        proposedHeight / Math.Max(1, target.Height));
                    proposedWidth = Math.Max(minimumWidth, target.Width * scale);
                    proposedHeight = Math.Max(minimumHeight, target.Height * scale);
                }
            }

            target.Width = proposedWidth;
            target.Height = proposedHeight;
            layout.Width = proposedWidth;
            layout.Height = proposedHeight;
            changed();
            e.Handled = true;
        };

        return thumb;
    }
}
