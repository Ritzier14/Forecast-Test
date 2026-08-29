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
    private readonly string? _initialCategorySelection;
    private Point? _taskDragStart;
    private ProjectTaskCode? _taskDragSource;
    private bool _refreshingInlineEdit;

    public TaskCategoryEditorWindow(MainWindowViewModel viewModel, TaskCategoryEditorTab initialTab, string? initialCategorySelection = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _initialCategorySelection = initialCategorySelection;
        DataContext = viewModel;
        _categoryOriginalNames = viewModel.ProjectCategories.ToDictionary(category => category, category => category.Name);
        SetActiveTab(initialTab);
        Loaded += (_, _) =>
        {
            InitialiseEditorColumnPresentation();
            SelectInitialCategory();
        };
    }

    public TaskCategoryEditorResult? Result { get; private set; }

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

    private void WindowChrome_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        if (FindParent<ButtonBase>(source) is not null
            || FindParent<TextBox>(source) is not null
            || FindParent<Selector>(source) is not null
            || FindParent<DataGrid>(source) is not null
            || IsScrollBarInteractionSource(source))
        {
            return;
        }

        DragMove();
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
        Result = new TaskCategoryEditorResult((CategoriesGrid.SelectedItem as ProjectCategory)?.Name);

        foreach (var taskCode in _viewModel.ProjectTaskCodes)
        {
            _viewModel.SetForecastGroupHeaderColor(taskCode.SystemCode, taskCode.HeaderColorHex);
        }

        foreach (var category in _viewModel.ProjectCategories)
        {
            if (_categoryOriginalNames.TryGetValue(category, out var originalName))
            {
                if (!string.Equals(originalName, category.Name, StringComparison.OrdinalIgnoreCase))
                {
                    _viewModel.SetForecastGroupHeaderColor(originalName, null);
                }

                _viewModel.SetForecastGroupHeaderColor(category.Name, category.ColorHex);
                _viewModel.RenameProjectCategoryReferences(originalName, category.Name);
            }
            else
            {
                _viewModel.SetForecastGroupHeaderColor(category.Name, category.ColorHex);
            }
        }

        _viewModel.RefreshTaskCategoryMetadata();
        DialogResult = true;
    }

    private void SelectInitialCategory()
    {
        if (string.IsNullOrWhiteSpace(_initialCategorySelection))
        {
            CategoriesGrid.SelectedItem = null;
            return;
        }

        var category = _viewModel.ProjectCategories.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, _initialCategorySelection, StringComparison.OrdinalIgnoreCase));
        if (category is null)
        {
            return;
        }

        CategoriesGrid.SelectedItem = category;
        CategoriesGrid.CurrentItem = category;
        CategoriesGrid.ScrollIntoView(category);
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

        if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != ModifierKeys.None)
        {
            // ProjectDataGrid owns modifier range/toggle selection. Do not
            // replace that range by entering the clicked editor cell.
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

    private void EditorColumnHeader_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridColumnHeader { Column: { } column } header
            || FindParent<DataGrid>(header) is not { } grid)
        {
            return;
        }

        RightClickGridPanBehavior.Cancel(grid);

        var menu = new ContextMenu();
        var iconMenu = new MenuItem { Header = "Icon" };
        foreach (var option in new[] { "T", "C", "$", "M", "I", "✓", "•" })
        {
            var item = new MenuItem
            {
                Header = option,
                IsCheckable = true,
                IsChecked = string.Equals(GridColumnPresentationState.GetIconGlyph(column), option, StringComparison.Ordinal)
            };
            item.Click += (_, _) => GridColumnPresentationState.SetIconGlyph(column, option);
            iconMenu.Items.Add(item);
        }

        menu.Items.Add(iconMenu);
        menu.Items.Add(BuildEditorHeaderColourMenu(grid, column));
        menu.PlacementTarget = header;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
        e.Handled = true;
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

    private void EditorInlineTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_refreshingInlineEdit || sender is not TextBox textBox)
        {
            return;
        }

        var grid = FindParent<DataGrid>(textBox);
        if (grid is not null && (ReferenceEquals(grid, TaskCodesGrid) || ReferenceEquals(grid, CategoriesGrid)))
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (_refreshingInlineEdit)
                {
                    return;
                }

                _refreshingInlineEdit = true;
                try
                {
                    _viewModel.RefreshTaskCategoryMetadata();
                }
                finally
                {
                    _refreshingInlineEdit = false;
                }
            }, System.Windows.Threading.DispatcherPriority.Background);
        }
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
            OpenIconColourMenu(button, taskCode.HeaderColorHex, colorHex => ApplyTaskHeaderColor(taskCode, colorHex));
        }
    }

    private void TaskColourButton_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button button && button.Tag is ProjectTaskCode taskCode)
        {
            OpenCustomHeaderColourPicker(button, taskCode.HeaderColorHex, colorHex => ApplyTaskHeaderColor(taskCode, colorHex));
            e.Handled = true;
        }
    }

    private void CategoryIconButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ProjectCategory category })
        {
            OpenBuiltInIconPicker(
                "Category Icon",
                category.IconKey,
                null,
                "ic_category_project_management_20.png",
                null,
                "Groups",
                (iconKey, _) =>
                {
                    category.IconKey = iconKey ?? string.Empty;
                });
        }
    }

    private void CategoryColourButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ProjectCategory category)
        {
            OpenIconColourMenu(button, category.ColorHex, colorHex => ApplyCategoryHeaderColor(category, colorHex));
        }
    }

    private void CategoryColourButton_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button button && button.Tag is ProjectCategory category)
        {
            OpenCustomHeaderColourPicker(button, category.ColorHex, colorHex => ApplyCategoryHeaderColor(category, colorHex));
            e.Handled = true;
        }
    }

    private void ApplyTaskHeaderColor(ProjectTaskCode taskCode, string? colorHex)
    {
        taskCode.HeaderColorHex = colorHex ?? string.Empty;
        _viewModel.SetForecastGroupHeaderColor(taskCode.SystemCode, colorHex);
        _viewModel.RefreshTaskCategoryMetadata();
    }

    private void ApplyCategoryHeaderColor(ProjectCategory category, string? colorHex)
    {
        category.ColorHex = colorHex ?? string.Empty;
        _viewModel.SetForecastGroupHeaderColor(category.Name, colorHex);
        _viewModel.RefreshTaskCategoryMetadata();
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

    private void OpenCustomHeaderColourPicker(FrameworkElement placementTarget, string? selectedHex, Action<string?> apply)
    {
        var selectedSpec = string.IsNullOrWhiteSpace(selectedHex)
            ? null
            : BrushFactory.SerializeHeaderGradientSpec(selectedHex, "Balanced");
        var picker = new HeaderColorPickerPopup("Header colour", selectedSpec, spec =>
        {
            var parsed = BrushFactory.ParseHeaderGradientSpec(spec);
            apply(string.IsNullOrWhiteSpace(spec) ? null : parsed.BaseHex);
        });

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

    private MenuItem BuildEditorHeaderColourMenu(DataGrid grid, DataGridColumn column)
    {
        var menu = new MenuItem { Header = "Header colour" };
        var targetColumns = grid.Columns.ToList();
        foreach (var (name, hex) in IconColourOptions)
        {
            var item = new MenuItem
            {
                Header = name,
                Icon = new Border
                {
                    Width = 14,
                    Height = 14,
                    CornerRadius = new CornerRadius(4),
                    Background = string.IsNullOrWhiteSpace(hex)
                        ? BrushFactory.FrozenDefaultHeaderGradient()
                        : BrushFactory.FrozenHeaderGradient(hex),
                    BorderBrush = BrushFactory.Frozen("#CBD5E1"),
                    BorderThickness = new Thickness(1)
                }
            };
            item.Click += (_, _) =>
            {
                foreach (var targetColumn in targetColumns)
                {
                    ApplyEditorHeaderColour(targetColumn, hex);
                }
            };
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());
        var custom = new MenuItem { Header = "Custom..." };
        custom.Click += (_, _) => OpenHeaderColourPickerForEditorColumn(column, targetColumns);
        menu.Items.Add(custom);
        return menu;
    }

    private void OpenHeaderColourPickerForEditorColumn(DataGridColumn column, IReadOnlyList<DataGridColumn> targetColumns)
    {
        var selectedSpec = GridColumnPresentationState.GetHeaderColorSpec(column);
        var picker = new HeaderColorPickerPopup("Editor header colour", selectedSpec, spec =>
        {
            foreach (var targetColumn in targetColumns)
            {
                ApplyEditorHeaderColour(targetColumn, spec);
            }
        });

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

    private static void ApplyEditorHeaderColour(DataGridColumn column, string? colorSpec)
    {
        var brush = string.IsNullOrWhiteSpace(colorSpec)
            ? BrushFactory.FrozenDefaultHeaderGradient()
            : BrushFactory.FrozenHeaderGradient(colorSpec);
        GridColumnPresentationState.SetHeaderBackground(column, brush);
        GridColumnPresentationState.SetBaseHeaderBackground(column, brush);
        GridColumnPresentationState.SetHeaderColorSpec(column, colorSpec ?? string.Empty);
        GridColumnPresentationState.SetHeaderBorderBrush(column, BrushFactory.Frozen("#DDE7F1"));
    }

    private void InitialiseEditorColumnPresentation()
    {
        foreach (var column in TaskCodesGrid.Columns.Concat(CategoriesGrid.Columns))
        {
            if (string.IsNullOrWhiteSpace(GridColumnPresentationState.GetIconGlyph(column)))
            {
                GridColumnPresentationState.SetIconGlyph(column, "T");
            }

            if (ReferenceEquals(GridColumnPresentationState.GetHeaderBorderBrush(column), Brushes.Transparent))
            {
                GridColumnPresentationState.SetHeaderBorderBrush(column, BrushFactory.Frozen("#DDE7F1"));
            }
        }
    }

    private void RoundedGridHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is Border border && e.NewSize.Width > 0 && e.NewSize.Height > 0)
        {
            border.Clip = null;
            if (border.Child is UIElement child)
            {
                child.Clip = new RectangleGeometry(new Rect(0, 0, e.NewSize.Width, e.NewSize.Height), 17, 17);
            }
        }
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

public sealed record TaskCategoryEditorResult(string? SelectedCategoryName);
