using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.ViewModels;

namespace ProjectCostForecast.App;

public partial class TaskCategoryEditorWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly Dictionary<ProjectCategory, string> _categoryOriginalNames;
    private Point? _taskDragStart;
    private ProjectTaskCode? _taskDragSource;

    public TaskCategoryEditorWindow(MainWindowViewModel viewModel, TaskCategoryEditorTab initialTab)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _categoryOriginalNames = viewModel.ProjectCategories.ToDictionary(category => category, category => category.Name);
        EditorTabs.SelectedIndex = initialTab == TaskCategoryEditorTab.Categories ? 1 : 0;
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
            && string.Equals(e.Column.Header?.ToString(), "System code", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
        }
    }

    private void TaskCodesGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _taskDragStart = e.GetPosition(TaskCodesGrid);
        _taskDragSource = FindParent<DataGridRow>(e.OriginalSource as DependencyObject)?.Item as ProjectTaskCode;
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

    private void TaskCodesGrid_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(ProjectTaskCode)) is ProjectTaskCode source
            && FindParent<DataGridRow>(e.OriginalSource as DependencyObject)?.Item is ProjectTaskCode target)
        {
            _viewModel.MoveProjectTaskCode(source, target);
        }
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
}

public enum TaskCategoryEditorTab
{
    TaskCodes,
    Categories
}
