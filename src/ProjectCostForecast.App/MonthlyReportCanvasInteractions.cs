using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App;

public partial class MainWindow
{
    private const string ReportToolbarDragFormat = "ProjectCostForecast.ReportToolbarObject";
    private readonly Dictionary<string, string> _selectedReportToolbarStyles = new(StringComparer.OrdinalIgnoreCase);
    private Point _reportToolbarDragStart;
    private Button? _reportToolbarDragButton;
    private Point _reportCanvasPanStart;
    private double _reportCanvasPanHorizontalOffset;
    private double _reportCanvasPanVerticalOffset;
    private bool _isReportCanvasPanning;

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
        var data = new DataObject(ReportToolbarDragFormat, objectType);
        DragDrop.DoDragDrop(button, data, DragDropEffects.Copy);
        e.Handled = true;
    }

    private void ReportToolbarButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _reportToolbarDragButton = null;
    }

    private void MonthlyReportCanvas_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(ReportToolbarDragFormat) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void MonthlyReportCanvas_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(ReportToolbarDragFormat) is not string objectType)
        {
            return;
        }

        var position = e.GetPosition(MonthlyReportChartCanvas);
        switch (objectType)
        {
            case "LineChart":
                ShowReportChartBuilder(ReportChartKind.Line, position);
                break;
            case "ColumnChart":
                ShowReportChartBuilder(ReportChartKind.Column, position);
                break;
            case "ProjectTitle":
                AddNewReportObject(objectType, 360, 86, dropPosition: position);
                break;
            case "CurrentPeriod":
                AddNewReportObject(objectType, 220, 78, dropPosition: position);
                break;
            case "Date":
                AddNewReportObject(objectType, 220, 76, dropPosition: position);
                break;
            case "Table":
                AddNewReportObject(objectType, 700, 320, dropPosition: position);
                break;
            case "Text":
                AddNewReportObject(objectType, 340, 170, "Enter report text here.", position);
                break;
            case "TitleText":
                AddNewReportObject(objectType, 360, 100, "Report title", position);
                break;
        }
        e.Handled = true;
    }

    private void MonthlyReportCanvas_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
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
        if (!_isReportCanvasPanning)
        {
            return;
        }

        _isReportCanvasPanning = false;
        MonthlyReportCanvasScrollViewer.ReleaseMouseCapture();
        MonthlyReportCanvasScrollViewer.Cursor = Cursors.Arrow;
        e.Handled = true;
    }

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
            Text = "Current period",
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
