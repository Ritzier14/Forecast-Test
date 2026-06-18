using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App;

public sealed class SavedMonthSnapshotWindow : Window
{
    private readonly DataGrid _snapshotGrid;
    private readonly DataGrid _lineGrid;
    private readonly ObservableCollection<SavedMonthGridView> _lineViews = [];
    private readonly TabControl _lineViewTabs;
    private SavedMonthGridView? _selectedLineView;

    public SavedMonthSnapshotWindow(IEnumerable<SavedMonthSnapshot> snapshots)
    {
        Title = "Saved Months";
        Width = 1100;
        Height = 700;
        MinWidth = 850;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var snapshotList = new ObservableCollection<SavedMonthSnapshot>(snapshots.OrderByDescending(snapshot => snapshot.SavedAt));
        var root = new DockPanel
        {
            Margin = new Thickness(12)
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0)
        };
        var closeButton = new Button
        {
            Content = "Close",
            MinWidth = 90
        };
        closeButton.Click += (_, _) => Close();
        buttonPanel.Children.Add(closeButton);
        DockPanel.SetDock(buttonPanel, Dock.Bottom);
        root.Children.Add(buttonPanel);

        _snapshotGrid = BuildSnapshotGrid(snapshotList);
        DockPanel.SetDock(_snapshotGrid, Dock.Top);
        root.Children.Add(_snapshotGrid);

        var lowerPanel = new DockPanel();
        var lineViewStrip = new DockPanel
        {
            Margin = new Thickness(0, 0, 0, 8)
        };
        DockPanel.SetDock(lineViewStrip, Dock.Top);
        lowerPanel.Children.Add(lineViewStrip);

        var viewLabel = new TextBlock
        {
            Text = "Views",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"))
        };
        DockPanel.SetDock(viewLabel, Dock.Left);
        lineViewStrip.Children.Add(viewLabel);

        var addViewButton = new Button
        {
            Content = "+",
            Width = 30,
            Padding = new Thickness(0),
            Margin = new Thickness(8, 0, 0, 0)
        };
        addViewButton.Click += (_, _) => AddLineView();
        DockPanel.SetDock(addViewButton, Dock.Right);
        lineViewStrip.Children.Add(addViewButton);

        _lineViewTabs = new TabControl
        {
            ItemsSource = _lineViews,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0)
        };
        _lineViewTabs.ItemContainerStyle = BuildViewTabItemStyle();
        _lineViewTabs.ItemTemplate = BuildViewHeaderTemplate();
        _lineViewTabs.ContentTemplate = BuildEmptyContentTemplate();
        lineViewStrip.Children.Add(_lineViewTabs);

        var lockedNotice = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF5F5")),
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FECACA")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 5, 8, 5),
            Margin = new Thickness(0, 0, 0, 8),
            Child = new TextBlock
            {
                Text = "Previous forecast locked for editing",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B91C1C")),
                FontWeight = FontWeights.SemiBold
            }
        };
        DockPanel.SetDock(lockedNotice, Dock.Top);
        lowerPanel.Children.Add(lockedNotice);

        _lineGrid = BuildLineGrid();
        _lineViewTabs.SelectionChanged += (_, _) =>
        {
            if (_lineViewTabs.SelectedItem is SavedMonthGridView view)
            {
                _selectedLineView = view;
                if (IsLoaded)
                {
                    Dispatcher.BeginInvoke(() => ApplyViewToGrid(_lineGrid, view), DispatcherPriority.Loaded);
                }
            }
        };
        lowerPanel.Children.Add(_lineGrid);
        root.Children.Add(lowerPanel);

        _snapshotGrid.SelectionChanged += (_, _) =>
        {
            _lineGrid.ItemsSource = (_snapshotGrid.SelectedItem as SavedMonthSnapshot)?.ForecastLines;
        };

        AttachColumnMenu(_snapshotGrid, null);
        AttachColumnMenu(_lineGrid, OnLineGridLayoutChanged);
        _lineGrid.ColumnReordered += (_, _) => OnLineGridLayoutChanged();

        SeedDefaultLineView();
        _snapshotGrid.SelectedIndex = snapshotList.Count > 0 ? 0 : -1;
        Content = root;
        Loaded += (_, _) =>
        {
            if (_selectedLineView is not null)
            {
                Dispatcher.BeginInvoke(() => ApplyViewToGrid(_lineGrid, _selectedLineView), DispatcherPriority.Loaded);
            }
        };
    }

    private DataGrid BuildSnapshotGrid(ObservableCollection<SavedMonthSnapshot> snapshots)
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            ItemsSource = snapshots,
            Height = 220,
            Margin = new Thickness(0, 0, 0, 12)
        };

        grid.Columns.Add(new DataGridTextColumn { Header = "Period", Binding = new Binding(nameof(SavedMonthSnapshot.Period)), Width = 100 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Cost to Date", Binding = new Binding(nameof(SavedMonthSnapshot.CostToDate)) { StringFormat = "{0:C0}" }, Width = 130 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Cost to Complete", Binding = new Binding(nameof(SavedMonthSnapshot.CostToComplete)) { StringFormat = "{0:C0}" }, Width = 145 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Final Forecast", Binding = new Binding(nameof(SavedMonthSnapshot.FinalForecast)) { StringFormat = "{0:C0}" }, Width = 130 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Total Budget Variance", Binding = new Binding(nameof(SavedMonthSnapshot.TotalBudgetVariance)) { StringFormat = "{0:C0}" }, Width = 165 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Date Saved", Binding = new Binding(nameof(SavedMonthSnapshot.SavedAt)) { StringFormat = "{0:g}" }, Width = 155 });
        AutoSizeColumns(grid);
        return SpreadsheetReadOnlyGridBehavior.Attach(grid);
    }

    private DataGrid BuildLineGrid()
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true
        };

        grid.Columns.Add(new DataGridTextColumn { Header = "Task", Binding = new Binding(nameof(SavedMonthForecastLine.TaskNumber)), Width = 120 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Resource", Binding = new Binding(nameof(SavedMonthForecastLine.ResourceName)), Width = 220 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Category", Binding = new Binding(nameof(SavedMonthForecastLine.ProjectCode)), Width = 130 });
        grid.Columns.Add(new DataGridTextColumn { Header = "CTD", Binding = new Binding(nameof(SavedMonthForecastLine.CostToDate)) { StringFormat = "{0:C0}" }, Width = 100 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Period Forecast", Binding = new Binding(nameof(SavedMonthForecastLine.CurrentPeriodForecast)) { StringFormat = "{0:C0}" }, Width = 130 });
        grid.Columns.Add(new DataGridTextColumn { Header = "CTC", Binding = new Binding(nameof(SavedMonthForecastLine.CostToComplete)) { StringFormat = "{0:C0}" }, Width = 110 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Final Forecast", Binding = new Binding(nameof(SavedMonthForecastLine.FinalForecast)) { StringFormat = "{0:C0}" }, Width = 125 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Budget", Binding = new Binding(nameof(SavedMonthForecastLine.Budget)) { StringFormat = "{0:C0}" }, Width = 110 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Budget Var", Binding = new Binding(nameof(SavedMonthForecastLine.TotalBudgetVariance)) { StringFormat = "{0:C0}" }, Width = 120 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Variance from Previous Month", Binding = new Binding(nameof(SavedMonthForecastLine.VarianceFromPreviousMonth)) { StringFormat = "{0:C0}" }, Width = 190 });
        AutoSizeColumns(grid);
        return SpreadsheetReadOnlyGridBehavior.Attach(grid);
    }

    private static void AutoSizeColumns(DataGrid grid)
    {
        foreach (var column in grid.Columns)
        {
            column.Width = DataGridLength.Auto;
        }
    }

    private static DataTemplate BuildViewHeaderTemplate()
    {
        var textFactory = new FrameworkElementFactory(typeof(TextBlock));
        textFactory.SetBinding(TextBlock.TextProperty, new Binding(nameof(SavedMonthGridView.Name)));
        textFactory.SetValue(TextBlock.MarginProperty, new Thickness(8, 2, 8, 2));
        textFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        return new DataTemplate { VisualTree = textFactory };
    }

    private static DataTemplate BuildEmptyContentTemplate()
    {
        var gridFactory = new FrameworkElementFactory(typeof(Grid));
        gridFactory.SetValue(FrameworkElement.HeightProperty, 1d);
        return new DataTemplate { VisualTree = gridFactory };
    }

    private static Style BuildViewTabItemStyle()
    {
        var style = new Style(typeof(TabItem));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 6, 0)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"))));
        style.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"))));

        var template = new ControlTemplate(typeof(TabItem));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.Name = "TabBorder";
        borderFactory.SetBinding(Border.BackgroundProperty, new Binding(nameof(Control.Background)) { RelativeSource = RelativeSource.TemplatedParent });
        borderFactory.SetBinding(Border.BorderBrushProperty, new Binding(nameof(Control.BorderBrush)) { RelativeSource = RelativeSource.TemplatedParent });
        borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(2));

        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        borderFactory.AppendChild(contentFactory);
        template.VisualTree = borderFactory;

        var selectedTrigger = new Trigger
        {
            Property = TabItem.IsSelectedProperty,
            Value = true
        };
        selectedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, Brushes.White, "TabBorder"));
        selectedTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")), "TabBorder"));
        template.Triggers.Add(selectedTrigger);

        var hoverTrigger = new Trigger
        {
            Property = UIElement.IsMouseOverProperty,
            Value = true
        };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC")), "TabBorder"));
        template.Triggers.Add(hoverTrigger);

        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        return style;
    }

    private void SeedDefaultLineView()
    {
        var defaultView = new SavedMonthGridView
        {
            Name = "Default"
        };
        SaveGridLayoutToView(_lineGrid, defaultView);
        _lineViews.Add(defaultView);
        _lineViewTabs.SelectedItem = defaultView;
    }

    private void AddLineView()
    {
        var view = new SavedMonthGridView
        {
            Name = $"View {_lineViews.Count + 1}"
        };
        SaveGridLayoutToView(_lineGrid, view);
        _lineViews.Add(view);
        _lineViewTabs.SelectedItem = view;
    }

    private void OnLineGridLayoutChanged()
    {
        if (_selectedLineView is null)
        {
            return;
        }

        SaveGridLayoutToView(_lineGrid, _selectedLineView);
    }

    private static void SaveGridLayoutToView(DataGrid grid, SavedMonthGridView view)
    {
        view.ColumnStates.Clear();
        foreach (var column in grid.Columns.OrderBy(column => column.DisplayIndex))
        {
            view.ColumnStates.Add(new SavedMonthColumnState
            {
                Header = column.Header?.ToString() ?? string.Empty,
                DisplayIndex = column.DisplayIndex,
                IsVisible = column.Visibility == Visibility.Visible
            });
        }
    }

    private static void ApplyViewToGrid(DataGrid grid, SavedMonthGridView view)
    {
        var orderedStates = view.ColumnStates
            .OrderBy(state => state.DisplayIndex)
            .ToList();

        for (var index = 0; index < orderedStates.Count; index++)
        {
            var state = orderedStates[index];
            var column = grid.Columns.FirstOrDefault(candidate => string.Equals(candidate.Header?.ToString(), state.Header, StringComparison.OrdinalIgnoreCase));
            if (column is null)
            {
                continue;
            }

            column.Visibility = state.IsVisible ? Visibility.Visible : Visibility.Collapsed;
            column.DisplayIndex = index;
        }
    }

    private static void AttachColumnMenu(DataGrid grid, Action? onLayoutChanged)
    {
        grid.AddHandler(DataGridColumnHeader.PreviewMouseRightButtonDownEvent, new RoutedEventHandler((sender, e) =>
        {
            if (e.OriginalSource is not DependencyObject source)
            {
                return;
            }

            var header = FindParent<DataGridColumnHeader>(source);
            if (header?.Column is null)
            {
                return;
            }

            var menu = new ContextMenu();
            foreach (var column in grid.Columns.OrderBy(column => column.DisplayIndex))
            {
                var label = column.Header?.ToString() ?? "Column";
                var item = new MenuItem
                {
                    Header = label,
                    IsCheckable = true,
                    IsChecked = column.Visibility == Visibility.Visible,
                    Tag = column
                };
                item.Click += (_, _) =>
                {
                    var visibleColumns = grid.Columns.Count(candidate => candidate.Visibility == Visibility.Visible);
                    var target = (DataGridColumn)item.Tag;
                    if (target.Visibility == Visibility.Visible && visibleColumns <= 1)
                    {
                        item.IsChecked = true;
                        return;
                    }

                    target.Visibility = item.IsChecked ? Visibility.Visible : Visibility.Collapsed;
                    onLayoutChanged?.Invoke();
                };
                menu.Items.Add(item);
            }

            header.ContextMenu = menu;
            menu.IsOpen = true;
            e.Handled = true;
        }), true);
    }

    private static T? FindParent<T>(DependencyObject current) where T : DependencyObject
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

public sealed class SavedMonthGridView : ObservableModel
{
    private string _name = string.Empty;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public ObservableCollection<SavedMonthColumnState> ColumnStates { get; } = [];
}

public sealed class SavedMonthColumnState
{
    public string Header { get; init; } = string.Empty;
    public int DisplayIndex { get; init; }
    public bool IsVisible { get; init; }
}
