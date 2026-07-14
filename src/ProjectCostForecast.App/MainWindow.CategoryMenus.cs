using System.Windows.Controls;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.ViewModels;

namespace ProjectCostForecast.App;

public partial class MainWindow
{
    private MenuItem BuildEditCategoriesMenu(DataGrid grid, ForecastLine fallbackLine, MainWindowViewModel viewModel)
    {
        var menu = new MenuItem { Header = "Edit categories" };
        var selectedLines = GetSelectedForecastLines(grid, fallbackLine);
        var selectedCategories = selectedLines
            .Select(line => line.ReportingCategory)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var selectedCategory = selectedCategories.Count == 1 ? selectedCategories[0] : null;

        foreach (var category in viewModel.ProjectCategoryNames)
        {
            var categoryName = category;
            var item = new MenuItem
            {
                Header = categoryName,
                IsCheckable = true,
                IsChecked = !string.IsNullOrWhiteSpace(selectedCategory)
                    && string.Equals(selectedCategory, categoryName, StringComparison.OrdinalIgnoreCase)
            };
            item.Click += (_, _) => ApplyCategoryToSelection(grid, fallbackLine, categoryName);
            menu.Items.Add(item);
        }

        if (menu.Items.Count == 0)
        {
            menu.Items.Add(new MenuItem
            {
                Header = "No categories yet",
                IsEnabled = false
            });
        }

        menu.Items.Add(new Separator());
        var editor = new MenuItem { Header = "Category editor" };
        editor.Click += (_, _) =>
        {
            var initialCategory = GetSelectedManualCategoryOverride(selectedLines);
            OpenTaskCategoryEditor(
                TaskCategoryEditorTab.Categories,
                initialCategory,
                category => ApplyCategoryToSelection(grid, fallbackLine, category));
        };
        menu.Items.Add(editor);
        return menu;
    }

    private static string? GetSelectedManualCategoryOverride(IReadOnlyList<ForecastLine> selectedLines)
    {
        var overrides = selectedLines
            .Where(MainWindowViewModel.HasManualReportingCategoryOverride)
            .Select(line => line.ReportingCategoryOverride)
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return overrides.Count == 1 ? overrides[0] : null;
    }

    private void ApplyCategoryToSelection(DataGrid grid, ForecastLine fallbackLine, string category)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var normalized = category.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        var lines = GetSelectedForecastLines(grid, fallbackLine);
        var changed = 0;
        viewModel.BeginSpreadsheetEditBatch();
        try
        {
            foreach (var line in lines)
            {
                if (string.Equals(line.ReportingCategory, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                line.ReportingCategory = normalized;
                changed++;
            }

            if (changed > 0)
            {
                viewModel.EnsureProjectCategory(normalized);
                viewModel.RefreshTaskCategoryMetadata(markDirty: false);
            }
        }
        finally
        {
            viewModel.EndSpreadsheetEditBatch(
                $"Applied category '{normalized}' to {changed:N0} line(s)",
                changed > 0,
                rebuildFilterLists: true);
        }

        if (changed > 0)
        {
            QueueRefreshForecastGroupHeaderPresenters();
            QueueSpreadsheetSelectionUpdate(grid, lines, refreshAllVisuals: false);
        }
    }

    private void OpenTaskCategoryEditor(
        TaskCategoryEditorTab initialTab,
        string? initialCategorySelection = null,
        Action<string>? applySelectedCategoryOnSave = null)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var window = new TaskCategoryEditorWindow(viewModel, initialTab, initialCategorySelection)
        {
            Owner = this
        };
        if (window.ShowDialog() == true)
        {
            if (applySelectedCategoryOnSave is not null
                && !string.IsNullOrWhiteSpace(window.Result?.SelectedCategoryName))
            {
                applySelectedCategoryOnSave(window.Result.SelectedCategoryName);
            }

            QueueRefreshForecastGroupHeaderPresenters();
            QueueSpreadsheetSelectionUpdate(ForecastLinesGrid, refreshAllVisuals: true);
        }
    }
}
