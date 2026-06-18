using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App;

public sealed class ResourceCommentWindow : Window
{
    private readonly Point _invalidPoint = new(double.NaN, double.NaN);
    private readonly ListBox _metricList;
    private readonly TextBox _totalBudgetVarianceCommentTextBox;
    private readonly TextBox _monthBudgetVarianceCommentTextBox;
    private readonly TextBox _forecastVarianceCommentTextBox;
    private Point _dragStartPoint;
    private ResourceCommentMetricItem? _draggedMetricItem;

    public ResourceCommentWindow(ForecastLine line)
    {
        Line = line;
        MetricItems = new ObservableCollection<ResourceCommentMetricItem>(
            line.ResourceCommentMetrics
                .OrderBy(metric => metric.DisplayOrder)
                .Select(metric => new ResourceCommentMetricItem
                {
                    Key = metric.Key,
                    Label = metric.Label,
                    ValueText = GetMetricValueText(line, metric.Key),
                    IsVisible = metric.IsVisible,
                    DisplayOrder = metric.DisplayOrder
                }));

        Title = $"Comments - {line.ResourceName}";
        Width = 860;
        Height = 760;
        MinWidth = 740;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(244, 246, 248));

        _dragStartPoint = _invalidPoint;

        var root = new Grid
        {
            Margin = new Thickness(18)
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var headerPanel = new StackPanel();
        headerPanel.Children.Add(new TextBlock
        {
            Text = line.ResourceName,
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42))
        });
        headerPanel.Children.Add(new TextBlock
        {
            Text = $"{line.TaskNumber} / {line.ProjectCode}",
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139))
        });
        root.Children.Add(headerPanel);

        var guidance = new TextBlock
        {
            Text = "Turn pill boxes on or off, then drag them to rearrange the order shown for this resource.",
            Margin = new Thickness(0, 10, 0, 14),
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105))
        };
        Grid.SetRow(guidance, 1);
        root.Children.Add(guidance);

        var contentGrid = new Grid();
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(contentGrid, 2);
        root.Children.Add(contentGrid);

        var pillPanelBorder = CreatePanelBorder();
        Grid.SetColumn(pillPanelBorder, 0);
        contentGrid.Children.Add(pillPanelBorder);

        var pillPanel = new DockPanel();
        pillPanel.Children.Add(new TextBlock
        {
            Text = "Metric pill boxes",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12),
            Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42))
        });
        DockPanel.SetDock(pillPanel.Children[^1], Dock.Top);

        _metricList = new ListBox
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            ItemsSource = MetricItems
        };
        _metricList.PreviewMouseLeftButtonDown += MetricList_PreviewMouseLeftButtonDown;
        _metricList.PreviewMouseMove += MetricList_PreviewMouseMove;
        _metricList.PreviewMouseLeftButtonUp += MetricList_PreviewMouseLeftButtonUp;
        _metricList.ItemTemplate = BuildMetricItemTemplate();
        pillPanel.Children.Add(_metricList);
        pillPanelBorder.Child = pillPanel;

        var commentPanelBorder = CreatePanelBorder();
        Grid.SetColumn(commentPanelBorder, 2);
        contentGrid.Children.Add(commentPanelBorder);

        var commentScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        commentPanelBorder.Child = commentScroll;

        var commentPanel = new StackPanel();
        commentScroll.Content = commentPanel;
        commentPanel.Children.Add(new TextBlock
        {
            Text = "Variance comments",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12),
            Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42))
        });

        commentPanel.Children.Add(CreateCommentEditorSection(
            "Total Budget Variance",
            line.TotalBudgetVariance,
            out _totalBudgetVarianceCommentTextBox,
            line.CommentsOnTotalBudgetVariance));
        commentPanel.Children.Add(CreateCommentEditorSection(
            "Month Budget Variance",
            GetMonthBudgetVariance(line),
            out _monthBudgetVarianceCommentTextBox,
            line.CommentsOnMonthBudgetVariance));
        commentPanel.Children.Add(CreateCommentEditorSection(
            "Forecast Variance",
            line.VarianceLastMonthToDate,
            out _forecastVarianceCommentTextBox,
            line.CommentsOnMonthForecastVariance));

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Background = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            MinWidth = 100
        };
        cancelButton.Click += (_, _) => DialogResult = false;
        buttonPanel.Children.Add(cancelButton);

        var saveButton = new Button
        {
            Content = "Save",
            MinWidth = 100
        };
        saveButton.Click += (_, _) => DialogResult = true;
        buttonPanel.Children.Add(saveButton);

        Grid.SetRow(buttonPanel, 3);
        root.Children.Add(buttonPanel);

        Content = root;
    }

    public ForecastLine Line { get; }
    public ObservableCollection<ResourceCommentMetricItem> MetricItems { get; }
    public string TotalBudgetVarianceComment => _totalBudgetVarianceCommentTextBox.Text;
    public string MonthBudgetVarianceComment => _monthBudgetVarianceCommentTextBox.Text;
    public string ForecastVarianceComment => _forecastVarianceCommentTextBox.Text;

    private static Border CreatePanelBorder()
    {
        return new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(217, 217, 217)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14)
        };
    }

    private static DataTemplate BuildMetricItemTemplate()
    {
        var template = new DataTemplate(typeof(ResourceCommentMetricItem));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(239, 246, 255)));
        border.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(147, 197, 253)));
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(18));
        border.SetValue(Border.PaddingProperty, new Thickness(12, 10, 12, 10));
        border.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 10));

        var grid = new FrameworkElementFactory(typeof(Grid));
        grid.SetValue(FrameworkElement.CursorProperty, Cursors.SizeAll);
        grid.AppendChild(BuildMetricItemColumns());
        border.AppendChild(grid);
        template.VisualTree = border;
        return template;
    }

    private static FrameworkElementFactory BuildMetricItemColumns()
    {
        var grid = new FrameworkElementFactory(typeof(Grid));

        var check = new FrameworkElementFactory(typeof(CheckBox));
        check.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        check.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(ResourceCommentMetricItem.IsVisible)) { Mode = BindingMode.TwoWay });
        grid.AppendChild(check);

        var label = new FrameworkElementFactory(typeof(TextBlock));
        label.SetValue(FrameworkElement.MarginProperty, new Thickness(28, 0, 120, 0));
        label.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        label.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        label.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(15, 23, 42)));
        label.SetBinding(TextBlock.TextProperty, new Binding(nameof(ResourceCommentMetricItem.Label)));
        grid.AppendChild(label);

        var value = new FrameworkElementFactory(typeof(TextBlock));
        value.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
        value.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        value.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(30, 64, 175)));
        value.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        value.SetBinding(TextBlock.TextProperty, new Binding(nameof(ResourceCommentMetricItem.ValueText)));
        grid.AppendChild(value);

        return grid;
    }

    private static FrameworkElement CreateCommentEditorSection(string title, decimal amount, out TextBox textBox, string initialComment)
    {
        var panel = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 14)
        };

        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42))
        });

        panel.Children.Add(new Border
        {
            Margin = new Thickness(0, 6, 0, 8),
            Padding = new Thickness(10, 8, 10, 8),
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(217, 217, 217)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = new TextBlock
            {
                Text = amount.ToString("C0"),
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(30, 64, 175))
            }
        });

        textBox = new TextBox
        {
            Text = initialComment,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MinHeight = 86,
            Padding = new Thickness(10, 8, 10, 8)
        };
        panel.Children.Add(textBox);
        return panel;
    }

    private static string GetMetricValueText(ForecastLine line, string key)
    {
        return GetMetricValue(line, key).ToString("C0");
    }

    private static decimal GetMetricValue(ForecastLine line, string key)
    {
        return key switch
        {
            "TotalBudget" => line.Budget,
            "FinalForecast" => line.PlannedCostFcc,
            "CostToComplete" => line.TotalForecastCtc,
            "CostToDate" => line.CostToDateSummary,
            "BudgetVarianceToDate" => line.TotalBudgetVariance,
            "BudgetVarianceThisMonth" => GetMonthBudgetVariance(line),
            "ForecastLastPeriod" => line.LastMonthForecast,
            "ActualCostToDate" => line.CostToDate,
            "CostVsLastForecast" => line.MonthForecastVariance,
            _ => 0
        };
    }

    private static decimal GetMonthBudgetVariance(ForecastLine line)
    {
        return line.MonthForecast - line.CurrentMonthCost;
    }

    private void MetricList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(_metricList);
        _draggedMetricItem = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext as ResourceCommentMetricItem;
    }

    private void MetricList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || double.IsNaN(_dragStartPoint.X))
        {
            return;
        }

        var current = e.GetPosition(_metricList);
        if (Math.Abs(current.X - _dragStartPoint.X) < 6 && Math.Abs(current.Y - _dragStartPoint.Y) < 6)
        {
            return;
        }

        if (_draggedMetricItem is null)
        {
            return;
        }

        var targetItem = FindAncestor<ListBoxItem>(_metricList.InputHitTest(current) as DependencyObject)?.DataContext as ResourceCommentMetricItem;
        var sourceIndex = MetricItems.IndexOf(_draggedMetricItem);
        if (sourceIndex < 0)
        {
            return;
        }

        var targetIndex = targetItem is null ? MetricItems.Count - 1 : MetricItems.IndexOf(targetItem);
        if (targetIndex < 0 || sourceIndex == targetIndex)
        {
            return;
        }

        MetricItems.Move(sourceIndex, targetIndex);
        UpdateMetricDisplayOrder();
        _metricList.CaptureMouse();
        _dragStartPoint = current;
        e.Handled = true;
    }

    private void MetricList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_metricList.IsMouseCaptured)
        {
            _metricList.ReleaseMouseCapture();
        }

        _dragStartPoint = _invalidPoint;
        _draggedMetricItem = null;
    }

    protected override void OnClosed(EventArgs e)
    {
        UpdateMetricDisplayOrder();
        base.OnClosed(e);
    }

    private void UpdateMetricDisplayOrder()
    {
        for (var i = 0; i < MetricItems.Count; i++)
        {
            MetricItems[i].DisplayOrder = i;
        }
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
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
}

public sealed class ResourceCommentMetricItem : ObservableModel
{
    private bool _isVisible;

    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string ValueText { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }
}
