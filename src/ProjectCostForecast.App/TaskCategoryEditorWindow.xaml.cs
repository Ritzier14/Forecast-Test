using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.ViewModels;

namespace ProjectCostForecast.App;

public partial class TaskCategoryEditorWindow : Window
{
    private static readonly IReadOnlyList<(string Name, string? Hex)> IconColourOptions =
    [
        ("Default", null),
        ("Slate", "#475569"),
        ("Blue", "#2563EB"),
        ("Green", "#16A34A"),
        ("Orange", "#EA580C"),
        ("Red", "#DC2626"),
        ("Purple", "#7C3AED")
    ];

    private static readonly IReadOnlyList<BuiltInIconPickerOption> BuiltInOptions =
    [
        new("ic_tab_forecast_16.png", "Forecast", "/Assets/Icons/png/ic_tab_forecast_16.png", "Tabs"),
        new("ic_tab_resources_16.png", "Resources", "/Assets/Icons/png/ic_tab_resources_16.png", "Tabs"),
        new("ic_tab_raw_transactions_16.png", "Raw transactions", "/Assets/Icons/png/ic_tab_raw_transactions_16.png", "Tabs"),
        new("ic_tab_summary_16.png", "Summary", "/Assets/Icons/png/ic_tab_summary_16.png", "Tabs"),
        new("ic_tab_monthly_report_16.png", "Monthly report", "/Assets/Icons/png/ic_tab_monthly_report_16.png", "Tabs"),
        new("ic_tab_pivot_builder_16.png", "Pivot builder", "/Assets/Icons/png/ic_tab_pivot_builder_16.png", "Tabs"),
        new("ic_tab_contingency_16.png", "Contingency", "/Assets/Icons/png/ic_tab_contingency_16.png", "Tabs"),
        new("ic_tab_audit_16.png", "Audit", "/Assets/Icons/png/ic_tab_audit_16.png", "Tabs"),
        new("ic_metric_planned_cost_28.png", "Planned cost", "/Assets/Icons/png/ic_metric_planned_cost_28.png", "Kpi"),
        new("ic_metric_cost_to_date_28.png", "Cost to date", "/Assets/Icons/png/ic_metric_cost_to_date_28.png", "Kpi"),
        new("ic_metric_forecast_at_completion_28.png", "Forecast at completion", "/Assets/Icons/png/ic_metric_forecast_at_completion_28.png", "Kpi"),
        new("ic_metric_forecast_variance_28.png", "Forecast variance", "/Assets/Icons/png/ic_metric_forecast_variance_28.png", "Kpi"),
        new("ic_metric_variance_percent_28.png", "Variance percent", "/Assets/Icons/png/ic_metric_variance_percent_28.png", "Kpi"),
        new("ic_metric_budget_remaining_28.png", "Budget remaining", "/Assets/Icons/png/ic_metric_budget_remaining_28.png", "Kpi"),
        new("ic_category_project_management_20.png", "Project management", "/Assets/Icons/png/ic_category_project_management_20.png", "Groups"),
        new("ic_category_internal_staff_20.png", "Internal staff", "/Assets/Icons/png/ic_category_internal_staff_20.png", "Groups"),
        new("ic_category_design_consultants_20.png", "Design consultants", "/Assets/Icons/png/ic_category_design_consultants_20.png", "Groups"),
        new("ic_category_contractors_20.png", "Contractors", "/Assets/Icons/png/ic_category_contractors_20.png", "Groups"),
        new("ic_category_compliance_20.png", "Compliance", "/Assets/Icons/png/ic_category_compliance_20.png", "Groups"),
        new("ic_category_closeout_20.png", "Close out", "/Assets/Icons/png/ic_category_closeout_20.png", "Groups"),
        new("ic_calendar_18.png", "Calendar", "/Assets/Icons/png/ic_calendar_18.png", "Standard"),
        new("ic_nav_reports_20.png", "Reports", "/Assets/Icons/png/ic_nav_reports_20.png", "Standard")
    ];

    private readonly MainWindowViewModel _viewModel;
    private readonly Dictionary<ProjectCategory, string> _categoryOriginalNames;
    private Point? _taskDragStart;
    private ProjectTaskCode? _taskDragSource;
    private Point? _gridRightDragStart;
    private double _gridHorizontalScrollStartOffset;
    private double _gridVerticalScrollStartOffset;
    private bool _gridRightDragging;
    private ScrollViewer? _activeGridScrollViewer;

    public TaskCategoryEditorWindow(MainWindowViewModel viewModel, TaskCategoryEditorTab initialTab)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _categoryOriginalNames = viewModel.ProjectCategories.ToDictionary(category => category, category => category.Name);
        SetActiveTab(initialTab);
    }

    private void EditorTabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        SetActiveTab(string.Equals(button.Tag?.ToString(), "Categories", StringComparison.OrdinalIgnoreCase)
            ? TaskCategoryEditorTab.Categories
            : TaskCategoryEditorTab.TaskCodes);
    }

    private void SetActiveTab(TaskCategoryEditorTab tab)
    {
        var categoriesActive = tab == TaskCategoryEditorTab.Categories;
        TaskCodesPanel.Visibility = categoriesActive ? Visibility.Collapsed : Visibility.Visible;
        CategoriesPanel.Visibility = categoriesActive ? Visibility.Visible : Visibility.Collapsed;
        TaskCodesToolbar.Visibility = categoriesActive ? Visibility.Collapsed : Visibility.Visible;
        CategoriesToolbar.Visibility = categoriesActive ? Visibility.Visible : Visibility.Collapsed;

        TaskCodesTabButton.BorderBrush = categoriesActive ? Brushes.Transparent : BrushFactory.Frozen("#2563EB");
        TaskCodesTabButton.Foreground = categoriesActive ? BrushFactory.Frozen("#475569") : BrushFactory.Frozen("#2563EB");
        TaskCodesTabButton.FontWeight = categoriesActive ? FontWeights.Normal : FontWeights.SemiBold;

        CategoriesTabButton.BorderBrush = categoriesActive ? BrushFactory.Frozen("#2563EB") : Brushes.Transparent;
        CategoriesTabButton.Foreground = categoriesActive ? BrushFactory.Frozen("#2563EB") : BrushFactory.Frozen("#475569");
        CategoriesTabButton.FontWeight = categoriesActive ? FontWeights.SemiBold : FontWeights.Normal;
    }

    private void WindowHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void AddTaskAbove_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddProjectTaskCode(TaskCodesGrid.SelectedItem as ProjectTaskCode, below: false);
    }

    private void AddTaskBelow_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddProjectTaskCode(TaskCodesGrid.SelectedItem as ProjectTaskCode, below: true);
    }

    private void DeleteTask_Click(object sender, RoutedEventArgs e)
    {
        if (TaskCodesGrid.SelectedItem is ProjectTaskCode taskCode)
        {
            _viewModel.DeleteProjectTaskCode(taskCode);
        }
    }

    private void SortTasks_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SortProjectTaskCodesByName();
    }

    private void AddCategory_Click(object sender, RoutedEventArgs e)
    {
        var category = new ProjectCategory { Name = "New category", DisplayOrder = _viewModel.ProjectCategories.Count };
        _viewModel.ProjectCategories.Add(category);
        CategoriesGrid.SelectedItem = category;
        CategoriesGrid.ScrollIntoView(category);
        BeginEditFirstTextCell(CategoriesGrid, category);
    }

    private void DeleteCategory_Click(object sender, RoutedEventArgs e)
    {
        if (CategoriesGrid.SelectedItem is ProjectCategory category)
        {
            _viewModel.DeleteProjectCategory(category);
        }
    }

    private void MergeCategory_Click(object sender, RoutedEventArgs e)
    {
        if (CategoriesGrid.SelectedItem is ProjectCategory source
            && MergeTargetCombo.SelectedItem is ProjectCategory target)
        {
            _viewModel.MergeProjectCategory(source, target);
        }
    }

    private void SortCategories_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SortProjectCategoriesByName();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        foreach (var category in _viewModel.ProjectCategories)
        {
            if (_categoryOriginalNames.TryGetValue(category, out var originalName))
            {
                _viewModel.RenameProjectCategoryReferences(originalName, category.Name);
            }
        }

        _viewModel.RefreshTaskCategoryMetadata();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TaskCodesGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        if (e.Row.Item is ProjectTaskCode { IsRawDataCode: true }
            && string.Equals(e.Column.Header?.ToString(), "Task code", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
        }
    }

    private void EditorGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        if (FindParent<Button>(source) is not null
            || FindParent<TextBox>(source) is not null
            || IsScrollBarInteractionSource(source))
        {
            return;
        }

        if (grid == TaskCodesGrid)
        {
            _taskDragStart = e.GetPosition(TaskCodesGrid);
            _taskDragSource = FindParent<DataGridRow>(source)?.Item as ProjectTaskCode;
        }

        var cell = FindParent<DataGridCell>(source);
        if (cell is null || !IsEditableTextCell(cell))
        {
            return;
        }

        if (IsDirectTextEntryColumn(cell.Column))
        {
            grid.SelectedItem = cell.DataContext;
            grid.CurrentCell = new DataGridCellInfo(cell.DataContext, cell.Column);
            e.Handled = true;
            Dispatcher.BeginInvoke(() =>
            {
                if (FindDescendant<TextBox>(cell) is TextBox textBox)
                {
                    textBox.Focus();
                    textBox.SelectAll();
                }
            }, System.Windows.Threading.DispatcherPriority.Input);
            return;
        }

        cell.Focus();
        grid.SelectedItem = cell.DataContext;
        grid.CurrentCell = new DataGridCellInfo(cell.DataContext, cell.Column);
        e.Handled = true;
        Dispatcher.BeginInvoke(() => BeginCellEdit(grid, cell, null), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void TaskCodesGrid_MouseMove(object sender, MouseEventArgs e)
    {
        if (_taskDragStart is null || _taskDragSource is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(TaskCodesGrid);
        if (Math.Abs(current.X - _taskDragStart.Value.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - _taskDragStart.Value.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        DragDrop.DoDragDrop(TaskCodesGrid, _taskDragSource, DragDropEffects.Move);
        _taskDragStart = null;
        _taskDragSource = null;
    }

    private void EditorGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        if (IsScrollBarInteractionSource(source))
        {
            _gridRightDragStart = null;
            _activeGridScrollViewer = null;
            _gridRightDragging = false;
            return;
        }

        var scrollViewer = FindDescendant<ScrollViewer>(grid);
        if (scrollViewer is null)
        {
            return;
        }

        _activeGridScrollViewer = scrollViewer;
        _gridRightDragStart = e.GetPosition(scrollViewer);
        _gridHorizontalScrollStartOffset = scrollViewer.HorizontalOffset;
        _gridVerticalScrollStartOffset = scrollViewer.VerticalOffset;
        _gridRightDragging = false;
    }

    private void EditorGrid_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_gridRightDragStart is null || _activeGridScrollViewer is null || e.RightButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(_activeGridScrollViewer);
        var deltaX = current.X - _gridRightDragStart.Value.X;
        var deltaY = current.Y - _gridRightDragStart.Value.Y;
        if (!_gridRightDragging && Math.Abs(deltaX) < 6 && Math.Abs(deltaY) < 6)
        {
            return;
        }

        _gridRightDragging = true;
        _activeGridScrollViewer.ScrollToHorizontalOffset(Math.Max(0, _gridHorizontalScrollStartOffset - deltaX));
        _activeGridScrollViewer.ScrollToVerticalOffset(Math.Max(0, _gridVerticalScrollStartOffset - deltaY));
        e.Handled = true;
    }

    private void EditorGrid_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        _gridRightDragStart = null;
        _activeGridScrollViewer = null;
        Dispatcher.BeginInvoke(() => _gridRightDragging = false);
    }

    private void TaskCodesGrid_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(ProjectTaskCode)) is ProjectTaskCode source
            && FindParent<DataGridRow>(e.OriginalSource as DependencyObject)?.Item is ProjectTaskCode target)
        {
            _viewModel.MoveProjectTaskCode(source, target);
        }
    }

    private void EditorGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid grid
            && FindParent<DataGridCell>(e.OriginalSource as DependencyObject) is { } cell
            && IsEditableTextCell(cell))
        {
            if (IsDirectTextEntryColumn(cell.Column))
            {
                if (FindDescendant<TextBox>(cell) is TextBox inlineTextBox)
                {
                    inlineTextBox.Focus();
                    inlineTextBox.SelectAll();
                    e.Handled = true;
                }

                return;
            }

            BeginCellEdit(grid, cell, null);
            e.Handled = true;
        }
    }

    private void EditorInlineTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        SelectInlineTextBoxRow(textBox);
        textBox.SelectAll();
    }

    private void EditorInlineTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.IsKeyboardFocusWithin)
        {
            return;
        }

        SelectInlineTextBoxRow(textBox);
        textBox.Focus();
        e.Handled = true;
    }

    private void EditorGrid_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source
            && FindParent<TextBox>(source) is not null)
        {
            return;
        }

        if (sender is not DataGrid grid
            || string.IsNullOrWhiteSpace(e.Text)
            || Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        var cell = GetCurrentEditableCell(grid);
        if (cell is null)
        {
            return;
        }

        if (IsDirectTextEntryColumn(cell.Column))
        {
            if (FindDescendant<TextBox>(cell) is TextBox inlineTextBox)
            {
                inlineTextBox.Focus();
                inlineTextBox.Text = e.Text;
                inlineTextBox.CaretIndex = inlineTextBox.Text.Length;
                e.Handled = true;
            }

            return;
        }

        BeginCellEdit(grid, cell, e.Text);
        e.Handled = true;
    }

    private void EditorGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not DataGrid grid)
        {
            return;
        }

        if (e.Key == Key.F2)
        {
            var cell = GetCurrentEditableCell(grid);
            if (cell is not null)
            {
                if (IsDirectTextEntryColumn(cell.Column))
                {
                    if (FindDescendant<TextBox>(cell) is TextBox inlineTextBox)
                    {
                        inlineTextBox.Focus();
                        inlineTextBox.SelectAll();
                        e.Handled = true;
                    }

                    return;
                }

                BeginCellEdit(grid, cell, null);
                e.Handled = true;
            }
        }
    }

    private void TaskIconButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ProjectTaskCode taskCode })
        {
            OpenBuiltInIconPicker(
                "Task Code Icon",
                taskCode.IconKey,
                taskCode.IconColorHex,
                "ic_category_project_management_20.png",
                null,
                "Groups",
                (iconKey, iconColorHex) =>
                {
                    taskCode.IconKey = iconKey ?? string.Empty;
                    taskCode.IconColorHex = iconColorHex ?? string.Empty;
                });
        }
    }

    private void TaskColourButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ProjectTaskCode taskCode)
        {
            OpenIconColourMenu(button, taskCode.IconColorHex, colorHex => taskCode.IconColorHex = colorHex ?? string.Empty);
        }
    }

    private void CategoryIconButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ProjectCategory category })
        {
            OpenBuiltInIconPicker(
                "Category Icon",
                category.IconKey,
                category.ColorHex,
                "ic_category_project_management_20.png",
                null,
                "Groups",
                (iconKey, iconColorHex) =>
                {
                    category.IconKey = iconKey ?? string.Empty;
                    category.ColorHex = iconColorHex ?? string.Empty;
                });
        }
    }

    private void CategoryColourButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ProjectCategory category)
        {
            OpenIconColourMenu(button, category.ColorHex, colorHex => category.ColorHex = colorHex ?? string.Empty);
        }
    }

    private void OpenBuiltInIconPicker(
        string title,
        string? selectedKey,
        string? selectedColorHex,
        string defaultKey,
        string? defaultColorHex,
        string initialCategory,
        Action<string?, string?> applyIcon)
    {
        var picker = new BuiltInIconPickerWindow(title, BuiltInOptions, selectedKey, selectedColorHex, defaultKey, defaultColorHex, initialCategory, applyIcon);
        var popup = new Popup
        {
            AllowsTransparency = true,
            Child = picker,
            Placement = PlacementMode.Center,
            PlacementTarget = this,
            StaysOpen = false
        };
        picker.CloseRequested += (_, _) => popup.IsOpen = false;
        popup.IsOpen = true;
    }

    private void OpenIconColourMenu(FrameworkElement placementTarget, string? selectedHex, Action<string?> apply)
    {
        var menu = new ContextMenu();
        foreach (var (name, hex) in IconColourOptions)
        {
            var item = new MenuItem
            {
                Header = name,
                IsCheckable = true,
                IsChecked = string.Equals(selectedHex ?? string.Empty, hex ?? string.Empty, StringComparison.OrdinalIgnoreCase),
                Icon = new Border
                {
                    Width = 12,
                    Height = 12,
                    CornerRadius = new CornerRadius(3),
                    Background = string.IsNullOrWhiteSpace(hex)
                        ? BrushFactory.Frozen("#FFFFFF")
                        : BrushFactory.Frozen(hex),
                    BorderBrush = BrushFactory.Frozen("#CBD5E1"),
                    BorderThickness = new Thickness(1)
                }
            };
            item.Click += (_, _) => apply(hex);
            menu.Items.Add(item);
        }

        menu.PlacementTarget = placementTarget;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private static DataGridCell? GetCurrentEditableCell(DataGrid grid)
    {
        if (!grid.CurrentCell.IsValid || grid.CurrentCell.Item is null || grid.CurrentCell.Column is null)
        {
            return null;
        }

        var row = grid.ItemContainerGenerator.ContainerFromItem(grid.CurrentCell.Item) as DataGridRow;
        return row is null
            ? null
            : FindChildren<DataGridCell>(row).FirstOrDefault(cell => ReferenceEquals(cell.Column, grid.CurrentCell.Column));
    }

    private static bool IsEditableTextCell(DataGridCell cell)
    {
        if (cell.IsReadOnly)
        {
            return false;
        }

        if (cell.Column is DataGridTextColumn)
        {
            return true;
        }

        return cell.Column is DataGridTemplateColumn && IsDirectTextEntryColumn(cell.Column);
    }

    private static bool IsDirectTextEntryColumn(DataGridColumn? column)
    {
        return string.Equals(column?.Header?.ToString(), "Task name", StringComparison.OrdinalIgnoreCase)
            || string.Equals(column?.Header?.ToString(), "Category", StringComparison.OrdinalIgnoreCase);
    }

    private void BeginCellEdit(DataGrid grid, DataGridCell cell, string? replacementText)
    {
        cell.Focus();
        grid.SelectedItem = cell.DataContext;
        grid.CurrentCell = new DataGridCellInfo(cell.DataContext, cell.Column);
        grid.BeginEdit();
        Dispatcher.BeginInvoke(() =>
        {
            var textBox = FindDescendant<TextBox>(cell);
            if (textBox is null)
            {
                grid.UpdateLayout();
                textBox = FindDescendant<TextBox>(cell);
            }

            if (textBox is null)
            {
                return;
            }

            textBox.Focus();
            if (replacementText is null)
            {
                textBox.SelectAll();
                return;
            }

            textBox.Text = replacementText;
            textBox.CaretIndex = textBox.Text.Length;
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    private void BeginEditFirstTextCell(DataGrid grid, object item)
    {
        Dispatcher.BeginInvoke(() =>
        {
            grid.UpdateLayout();
            grid.ScrollIntoView(item);
            var row = grid.ItemContainerGenerator.ContainerFromItem(item) as DataGridRow;
            if (row is null)
            {
                return;
            }

            var firstCell = FindChildren<DataGridCell>(row)
                .FirstOrDefault(IsEditableTextCell);
            if (firstCell is not null)
            {
                BeginCellEdit(grid, firstCell, null);
            }
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private static void SelectInlineTextBoxRow(TextBox textBox)
    {
        var cell = FindParent<DataGridCell>(textBox);
        var grid = FindParent<DataGrid>(textBox);
        if (cell is null || grid is null)
        {
            return;
        }

        grid.SelectedItem = cell.DataContext;
        grid.CurrentCell = new DataGridCellInfo(cell.DataContext, cell.Column);
    }

    private static T? FindParent<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static IEnumerable<T> FindChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var nested in FindChildren<T>(child))
            {
                yield return nested;
            }
        }
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        return FindChildren<T>(root).FirstOrDefault();
    }

    private static bool IsScrollBarInteractionSource(DependencyObject source)
    {
        return FindParent<ScrollBar>(source) is not null
            || FindParent<RepeatButton>(source) is not null
            || FindParent<Thumb>(source) is not null;
    }
}

public enum TaskCategoryEditorTab
{
    TaskCodes,
    Categories
}
