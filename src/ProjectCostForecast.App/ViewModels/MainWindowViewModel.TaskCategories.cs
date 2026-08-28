using System.Collections.ObjectModel;
using System.ComponentModel;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;

namespace ProjectCostForecast.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private static readonly string[] DefaultGroupHeaderColorHexes =
    [
        "#EDF8F0",
        "#FFF4E7",
        "#EEF5FF",
        "#F5F0FF",
        "#ECF9FA",
        "#FFF0F5"
    ];

    private void InitializeTaskCategoryMetadata()
    {
        _dataset.ProjectTaskCodes ??= [];
        _dataset.ProjectCategories ??= [];

        foreach (var line in _dataset.ForecastLines)
        {
            if (string.IsNullOrWhiteSpace(line.ReportingCategoryOverride)
                && !string.IsNullOrWhiteSpace(line.ProjectCode))
            {
                line.ReportingCategoryOverride = line.ProjectCode;
            }
        }

        var rawTaskCodes = _dataset.Transactions
            .Select(transaction => CalculationService.Normalise(transaction.TaskNumber))
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var taskCodes = _dataset.ProjectTaskCodes
            .Where(task => !string.IsNullOrWhiteSpace(task.SystemCode))
            .GroupBy(task => task.SystemCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(task => task.DisplayOrder)
            .ThenBy(task => task.SystemCode, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var liveForecastLines = _dataset.ForecastLines.Concat(ForecastLines).Distinct().ToList();
        var forecastTaskCodes = liveForecastLines
            .Select(line => CalculationService.Normalise(line.TaskNumber))
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var code in forecastTaskCodes.Concat(rawTaskCodes))
        {
            if (string.IsNullOrWhiteSpace(code)
                || taskCodes.Any(task => string.Equals(task.SystemCode, code, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var isImportedCode = IsImportedTaskCode(code, rawTaskCodes, forecastTaskCodes);

            taskCodes.Add(new ProjectTaskCode
            {
                SystemCode = code,
                IsRawDataCode = isImportedCode,
                IsManualCode = !isImportedCode,
                DisplayOrder = taskCodes.Count
            });
        }

        foreach (var task in taskCodes)
        {
            task.IsRawDataCode = IsImportedTaskCode(task.SystemCode, rawTaskCodes, forecastTaskCodes);
            task.IsManualCode = !task.IsRawDataCode;
            task.HeaderColorHex = _dataset.ForecastGroupHeaderColorHexes.GetValueOrDefault(task.SystemCode) ?? task.HeaderColorHex;
        }

        _dataset.ProjectTaskCodes = NormalizeTaskCodes(taskCodes);
        ApplyDefaultTaskHeaderColors(_dataset.ProjectTaskCodes);

        var categories = _dataset.ProjectCategories
            .Where(category => !string.IsNullOrWhiteSpace(category.Name))
            .GroupBy(category => category.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var categoryName in liveForecastLines
            .Select(line => CalculationService.Normalise(line.ReportingCategoryOverride))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!categories.Any(category => string.Equals(category.Name, categoryName, StringComparison.OrdinalIgnoreCase)))
            {
                categories.Add(new ProjectCategory { Name = categoryName, DisplayOrder = categories.Count });
            }
        }

        foreach (var category in categories)
        {
            category.ColorHex = _dataset.ForecastGroupHeaderColorHexes.GetValueOrDefault(category.Name) ?? category.ColorHex;
        }

        _dataset.ProjectCategories = NormalizeCategories(categories);
        ApplyDefaultCategoryColors(_dataset.ProjectCategories);
        ReplaceCollection(ProjectTaskCodes, _dataset.ProjectTaskCodes);
        ReplaceCollection(ProjectCategories, _dataset.ProjectCategories);
        RefreshForecastLineTaskCategoryResolution();
        RebuildTaskCodeReviewRows();
    }

    public void RefreshTaskCategoryMetadata(bool markDirty = true)
    {
        _dataset.ProjectTaskCodes = NormalizeTaskCodes(ProjectTaskCodes);
        _dataset.ProjectCategories = NormalizeCategories(ProjectCategories);
        ApplyDefaultTaskHeaderColors(_dataset.ProjectTaskCodes);
        ApplyDefaultCategoryColors(_dataset.ProjectCategories);
        ReplaceCollection(ProjectTaskCodes, _dataset.ProjectTaskCodes);
        ReplaceCollection(ProjectCategories, _dataset.ProjectCategories);
        RefreshForecastLineTaskCategoryResolution();
        OnPropertyChanged(nameof(ProjectTaskCodes));
        OnPropertyChanged(nameof(ProjectCategoryNames));
        _calculationService.Recalculate(_dataset);
        ReplaceCollection(CategorySummaries, _dataset.CategorySummaries);
        RebuildCalculatedViews(rebuildFilterLists: true);
        RebuildTaskCodeReviewRows();
        ApplyForecastGrouping();
        if (markDirty)
        {
            IsDirty = true;
        }
    }

    public void SetForecastLineReportingCategory(ForecastLine line, string category)
    {
        var normalized = CalculationService.Normalise(category);
        line.ReportingCategoryOverride = normalized;
        EnsureProjectCategory(normalized);
        RefreshTaskCategoryMetadata();
    }

    public void EnsureProjectCategory(string category)
    {
        var normalized = CalculationService.Normalise(category);
        if (string.IsNullOrWhiteSpace(normalized)
            || ProjectCategories.Any(existing => string.Equals(existing.Name, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        ProjectCategories.Add(new ProjectCategory
        {
            Name = normalized,
            DisplayOrder = ProjectCategories.Count
        });
        OnPropertyChanged(nameof(ProjectCategoryNames));
    }

    public void DeleteProjectCategory(ProjectCategory category)
    {
        var name = category.Name;
        ProjectCategories.Remove(category);
        foreach (var line in ForecastLines.Where(line => string.Equals(line.ReportingCategoryOverride, name, StringComparison.OrdinalIgnoreCase)))
        {
            line.ReportingCategoryOverride = string.Empty;
        }

        RefreshTaskCategoryMetadata();
    }

    public void MergeProjectCategory(ProjectCategory source, ProjectCategory target)
    {
        if (ReferenceEquals(source, target))
        {
            return;
        }

        foreach (var line in ForecastLines.Where(line => string.Equals(line.ReportingCategoryOverride, source.Name, StringComparison.OrdinalIgnoreCase)))
        {
            line.ReportingCategoryOverride = target.Name;
        }

        ProjectCategories.Remove(source);
        RefreshTaskCategoryMetadata();
    }

    public void RenameProjectCategoryReferences(string oldName, string newName)
    {
        var previous = CalculationService.Normalise(oldName);
        var next = CalculationService.Normalise(newName);
        if (string.IsNullOrWhiteSpace(previous)
            || string.IsNullOrWhiteSpace(next)
            || string.Equals(previous, next, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var line in ForecastLines.Where(line => string.Equals(line.ReportingCategoryOverride, previous, StringComparison.OrdinalIgnoreCase)))
        {
            line.ReportingCategoryOverride = next;
        }
    }

    public void AddProjectTaskCode(ProjectTaskCode? anchor, bool below)
    {
        var index = anchor is null ? ProjectTaskCodes.Count : ProjectTaskCodes.IndexOf(anchor) + (below ? 1 : 0);
        index = Math.Clamp(index, 0, ProjectTaskCodes.Count);
        ProjectTaskCodes.Insert(index, new ProjectTaskCode
        {
            SystemCode = CreateManualTaskCode(),
            TaskName = "Unnamed task",
            IsManualCode = true,
            DisplayOrder = index
        });
        RefreshTaskCategoryMetadata();
    }

    public void DeleteProjectTaskCode(ProjectTaskCode taskCode)
    {
        if (!taskCode.CanDelete)
        {
            return;
        }

        ProjectTaskCodes.Remove(taskCode);
        RefreshTaskCategoryMetadata();
    }

    public void SortProjectTaskCodesByName()
    {
        ReplaceCollection(ProjectTaskCodes, ProjectTaskCodes.OrderBy(task => GetResolvedTaskName(task), StringComparer.OrdinalIgnoreCase));
        RefreshTaskCategoryMetadata();
    }

    public void SortProjectCategoriesByName()
    {
        ReplaceCollection(ProjectCategories, ProjectCategories.OrderBy(category => category.Name, StringComparer.OrdinalIgnoreCase));
        RefreshTaskCategoryMetadata();
    }

    public void MoveProjectTaskCode(ProjectTaskCode source, ProjectTaskCode target)
    {
        var oldIndex = ProjectTaskCodes.IndexOf(source);
        var newIndex = ProjectTaskCodes.IndexOf(target);
        if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex)
        {
            return;
        }

        ProjectTaskCodes.Move(oldIndex, newIndex);
        RefreshTaskCategoryMetadata();
    }

    private void RefreshForecastLineTaskCategoryResolution()
    {
        var taskLookup = ProjectTaskCodes
            .Where(task => !string.IsNullOrWhiteSpace(task.SystemCode))
            .GroupBy(task => task.SystemCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var line in ForecastLines.Concat(_dataset.ForecastLines).Distinct())
        {
            var task = taskLookup.GetValueOrDefault(CalculationService.Normalise(line.TaskNumber));
            var taskName = task is null ? "Unnamed task" : GetResolvedTaskName(task);
            var reportingCategory = HasManualReportingCategoryOverride(line)
                ? line.ReportingCategoryOverride
                : taskName;
            line.SetResolvedTaskMetadata(taskName, reportingCategory);
        }
    }

    public static bool HasManualReportingCategoryOverride(ForecastLine line)
    {
        var overrideValue = CalculationService.Normalise(line.ReportingCategoryOverride);
        if (string.IsNullOrWhiteSpace(overrideValue))
        {
            return false;
        }

        var legacyProjectCode = CalculationService.Normalise(line.ProjectCode);
        return string.IsNullOrWhiteSpace(legacyProjectCode)
            || !string.Equals(overrideValue, legacyProjectCode, StringComparison.OrdinalIgnoreCase);
    }

    private static List<ProjectTaskCode> NormalizeTaskCodes(IEnumerable<ProjectTaskCode> source)
    {
        var seenNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        return source
            .Where(task => !string.IsNullOrWhiteSpace(task.SystemCode))
            .GroupBy(task => task.SystemCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select((task, index) =>
            {
                task.DisplayOrder = index;
                task.TaskName = ApplyDuplicateSuffix(task.TaskName, seenNames);
                return task;
            })
            .ToList();
    }

    private static List<ProjectCategory> NormalizeCategories(IEnumerable<ProjectCategory> source)
    {
        return source
            .Where(category => !string.IsNullOrWhiteSpace(category.Name))
            .GroupBy(category => category.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select((category, index) =>
            {
                category.DisplayOrder = index;
                return category;
            })
            .ToList();
    }

    private static string ApplyDuplicateSuffix(string taskName, IDictionary<string, int> seenNames)
    {
        var baseName = string.IsNullOrWhiteSpace(taskName) ? "Unnamed task" : taskName.Trim();
        seenNames.TryGetValue(baseName, out var count);
        seenNames[baseName] = count + 1;
        return count == 0 ? baseName : $"{baseName} ({count})";
    }

    private static string GetResolvedTaskName(ProjectTaskCode taskCode)
    {
        return string.IsNullOrWhiteSpace(taskCode.TaskName) ? "Unnamed task" : taskCode.TaskName.Trim();
    }

    private static void ApplyDefaultTaskHeaderColors(IReadOnlyList<ProjectTaskCode> taskCodes)
    {
        for (var index = 0; index < taskCodes.Count; index++)
        {
            taskCodes[index].DefaultHeaderColorHex = DefaultGroupHeaderColorHexes[index % DefaultGroupHeaderColorHexes.Length];
        }
    }

    private static void ApplyDefaultCategoryColors(IReadOnlyList<ProjectCategory> categories)
    {
        for (var index = 0; index < categories.Count; index++)
        {
            categories[index].DefaultColorHex = DefaultGroupHeaderColorHexes[index % DefaultGroupHeaderColorHexes.Length];
        }
    }

    private string CreateManualTaskCode()
    {
        var counter = 1;
        string code;
        do
        {
            code = $"MANUAL-{counter:000}";
            counter++;
        }
        while (ProjectTaskCodes.Any(task => string.Equals(task.SystemCode, code, StringComparison.OrdinalIgnoreCase)));

        return code;
    }

    private static bool IsImportedTaskCode(string? code, IReadOnlySet<string> rawTaskCodes, IReadOnlySet<string> forecastTaskCodes)
    {
        var normalized = CalculationService.Normalise(code);
        return !string.IsNullOrWhiteSpace(normalized)
            && !IsManualTaskCode(normalized)
            && (rawTaskCodes.Contains(normalized) || forecastTaskCodes.Contains(normalized));
    }

    private static bool IsManualTaskCode(string? code)
    {
        return !string.IsNullOrWhiteSpace(code)
            && (code.StartsWith("MANUAL-", StringComparison.OrdinalIgnoreCase)
                || code.StartsWith("MAN-", StringComparison.OrdinalIgnoreCase));
    }
}
