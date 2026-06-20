using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ProjectCostForecast.App;
using Microsoft.Win32;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;

namespace ProjectCostForecast.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void InitializeWorkspaceViews(IEnumerable<WorkspaceViewLayout>? persistedViews = null)
    {
        _workspaceViews.Clear();
        _detailWorkspaceViews.Clear();

        var defaultLayouts = GetDefaultWorkspaceViewLayouts();
        var persistedLookup = (persistedViews ?? [])
            .Select(view => new WorkspaceViewLayout
            {
                WorkspaceKey = NormaliseWorkspaceKey(view.WorkspaceKey),
                ContentKey = view.ContentKey,
                Name = view.Name,
                HiddenColumnKeys = view.HiddenColumnKeys ?? [],
                ColumnLayouts = view.ColumnLayouts ?? [],
                ShowZeroAsBlank = view.ShowZeroAsBlank,
                GroupForecastLinesByTask = view.GroupForecastLinesByTask,
                ForecastGroupByKey = NormalizeForecastGroupByKey(view.ForecastGroupByKey)
            })
            .Where(view => !string.IsNullOrWhiteSpace(view.WorkspaceKey))
            .GroupBy(view => view.WorkspaceKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var group in defaultLayouts
            .Where(view => !IsDetailWorkspaceKey(view.WorkspaceKey))
            .GroupBy(view => view.WorkspaceKey, StringComparer.OrdinalIgnoreCase))
        {
            if (persistedLookup.TryGetValue(group.Key, out var savedViews) && savedViews.Count > 0)
            {
                _workspaceViews[group.Key] = CreateCollection(savedViews.Select(CreateWorkspaceViewTab));
                continue;
            }

            _workspaceViews[group.Key] = CreateCollection(group.Select(CreateWorkspaceViewTab));
        }

        foreach (var group in defaultLayouts
            .Where(view => IsDetailWorkspaceKey(view.WorkspaceKey))
            .GroupBy(view => view.WorkspaceKey, StringComparer.OrdinalIgnoreCase))
        {
            if (persistedLookup.TryGetValue(group.Key, out var savedViews) && savedViews.Count > 0)
            {
                _detailWorkspaceViews[group.Key] = CreateCollection(savedViews.Select(CreateWorkspaceViewTab));
                continue;
            }

            _detailWorkspaceViews[group.Key] = CreateCollection(group.Select(CreateWorkspaceViewTab));
        }
    }

    private void RefreshCurrentWorkspaceViews()
    {
        if (!_workspaceViews.TryGetValue(ActiveWorkspaceKey, out var views))
        {
            views = [CreateFallbackWorkspaceView(ActiveWorkspaceKey)];
            _workspaceViews[ActiveWorkspaceKey] = views;
        }

        ReplaceCollection(CurrentWorkspaceViews, views);
        if (_selectedWorkspaceViewNames.TryGetValue(ActiveWorkspaceKey, out var selectedName))
        {
            SelectedWorkspaceView = CurrentWorkspaceViews.FirstOrDefault(view => string.Equals(view.Name, selectedName, StringComparison.OrdinalIgnoreCase))
                ?? CurrentWorkspaceViews.FirstOrDefault();
        }
        else
        {
            SelectedWorkspaceView = CurrentWorkspaceViews.FirstOrDefault();
        }
    }

    private static WorkspaceViewTab CreateFallbackWorkspaceView(string workspaceKey)
    {
        var isForecast = string.Equals(workspaceKey, "CTC Forecast", StringComparison.OrdinalIgnoreCase);
        return new WorkspaceViewTab
        {
            WorkspaceKey = workspaceKey,
            ContentKey = "Default",
            Name = "Default",
            EditName = "Default",
            DefaultName = "Default",
            ShowZeroAsBlank = true,
            GroupForecastLinesByTask = isForecast,
            ForecastGroupByKey = isForecast ? ForecastGroupByTaskKey : ForecastGroupByNoneKey
        };
    }

    private static WorkspaceColumnLayout CloneWorkspaceColumnLayout(WorkspaceColumnLayout layout)
    {
        return new WorkspaceColumnLayout
        {
            Key = layout.Key,
            Width = layout.Width,
            DisplayIndex = layout.DisplayIndex
        };
    }

    private void RefreshCurrentDetailWorkspaceViews()
    {
        if (!_detailWorkspaceViews.TryGetValue(ActiveDetailWorkspaceKey, out var views))
        {
            views =
            [
                new WorkspaceViewTab
                {
                    WorkspaceKey = ActiveDetailWorkspaceKey,
                    ContentKey = "Default",
                    Name = "Default",
                    EditName = "Default",
                    DefaultName = "Default"
                }
            ];
            _detailWorkspaceViews[ActiveDetailWorkspaceKey] = views;
        }

        ReplaceCollection(CurrentDetailWorkspaceViews, views);
        if (_selectedDetailWorkspaceViewNames.TryGetValue(ActiveDetailWorkspaceKey, out var selectedName))
        {
            SelectedDetailWorkspaceView = CurrentDetailWorkspaceViews.FirstOrDefault(view => string.Equals(view.Name, selectedName, StringComparison.OrdinalIgnoreCase))
                ?? CurrentDetailWorkspaceViews.FirstOrDefault();
        }
        else
        {
            SelectedDetailWorkspaceView = CurrentDetailWorkspaceViews.FirstOrDefault();
        }
    }

    private void AddWorkspaceView()
    {
        if (!_workspaceViews.TryGetValue(ActiveWorkspaceKey, out var views))
        {
            return;
        }

        EndAllWorkspaceViewRenames();
        var sourceView = SelectedWorkspaceView ?? views.FirstOrDefault();
        var defaultName = GetNextWorkspaceViewDefaultName(views);
        var newView = new WorkspaceViewTab
        {
            WorkspaceKey = ActiveWorkspaceKey,
            ContentKey = sourceView?.ContentKey ?? "Default",
            Name = defaultName,
            EditName = defaultName,
            DefaultName = defaultName,
            RenameRestoreName = defaultName,
            HiddenColumnKeys = sourceView?.HiddenColumnKeys?.ToList() ?? [],
            ColumnLayouts = sourceView?.ColumnLayouts?.Select(CloneWorkspaceColumnLayout).ToList() ?? [],
            ShowZeroAsBlank = sourceView?.ShowZeroAsBlank ?? true,
            GroupForecastLinesByTask = sourceView?.GroupForecastLinesByTask ?? false,
            ForecastGroupByKey = NormalizeForecastGroupByKey(sourceView?.ForecastGroupByKey),
            IsEditing = true,
            IsNewlyCreated = true
        };

        views.Add(newView);
        if (!CurrentWorkspaceViews.Contains(newView))
        {
            CurrentWorkspaceViews.Add(newView);
        }

        SelectedWorkspaceView = newView;
        IsDirty = true;
    }

    private void AddDetailWorkspaceView()
    {
        if (!_detailWorkspaceViews.TryGetValue(ActiveDetailWorkspaceKey, out var views))
        {
            return;
        }

        EndAllWorkspaceViewRenames();
        var sourceView = SelectedDetailWorkspaceView ?? views.FirstOrDefault();
        var defaultName = GetNextWorkspaceViewDefaultName(views);
        var newView = new WorkspaceViewTab
        {
            WorkspaceKey = ActiveDetailWorkspaceKey,
            ContentKey = sourceView?.ContentKey ?? "Default",
            Name = defaultName,
            EditName = defaultName,
            DefaultName = defaultName,
            RenameRestoreName = defaultName,
            HiddenColumnKeys = sourceView?.HiddenColumnKeys?.ToList() ?? [],
            ColumnLayouts = sourceView?.ColumnLayouts?.Select(CloneWorkspaceColumnLayout).ToList() ?? [],
            ShowZeroAsBlank = sourceView?.ShowZeroAsBlank ?? true,
            GroupForecastLinesByTask = sourceView?.GroupForecastLinesByTask ?? false,
            ForecastGroupByKey = NormalizeForecastGroupByKey(sourceView?.ForecastGroupByKey),
            IsEditing = true,
            IsNewlyCreated = true
        };

        views.Add(newView);
        if (!CurrentDetailWorkspaceViews.Contains(newView))
        {
            CurrentDetailWorkspaceViews.Add(newView);
        }

        SelectedDetailWorkspaceView = newView;
        IsDirty = true;
    }

    public void ReorderWorkspaceView(WorkspaceViewTab view, int targetIndex, bool isDetailView)
    {
        var activeKey = isDetailView ? ActiveDetailWorkspaceKey : ActiveWorkspaceKey;
        if (!string.Equals(view.WorkspaceKey, activeKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var viewsByKey = isDetailView ? _detailWorkspaceViews : _workspaceViews;
        if (!viewsByKey.TryGetValue(activeKey, out var storedViews))
        {
            return;
        }

        var currentViews = isDetailView ? CurrentDetailWorkspaceViews : CurrentWorkspaceViews;
        if (!MoveWorkspaceView(storedViews, view, targetIndex))
        {
            return;
        }

        MoveWorkspaceView(currentViews, view, targetIndex);
        if (isDetailView)
        {
            SelectedDetailWorkspaceView = view;
        }
        else
        {
            SelectedWorkspaceView = view;
        }

        IsDirty = true;
    }

    public void SetWorkspaceTabOrder(IEnumerable<string> orderedWorkspaceKeys, bool isDetailOrder)
    {
        var orderedKeys = orderedWorkspaceKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (isDetailOrder)
        {
            _dataset.DetailWorkspaceTabOrder = orderedKeys;
        }
        else
        {
            _dataset.WorkspaceTabOrder = orderedKeys;
        }

        IsDirty = true;
    }

    private static bool MoveWorkspaceView(ObservableCollection<WorkspaceViewTab> views, WorkspaceViewTab view, int targetIndex)
    {
        var oldIndex = views.IndexOf(view);
        if (oldIndex < 0)
        {
            return false;
        }

        targetIndex = Math.Clamp(targetIndex, 0, views.Count);
        if (oldIndex < targetIndex)
        {
            targetIndex--;
        }

        targetIndex = Math.Clamp(targetIndex, 0, views.Count - 1);
        if (oldIndex == targetIndex)
        {
            return false;
        }

        views.Move(oldIndex, targetIndex);
        return true;
    }

    public void BeginRenameSelectedWorkspaceView()
    {
        if (SelectedWorkspaceView is null)
        {
            return;
        }

        BeginRenameWorkspaceView(SelectedWorkspaceView);
    }

    public void BeginRenameWorkspaceView(WorkspaceViewTab? view)
    {
        if (view is null)
        {
            return;
        }

        EndAllWorkspaceViewRenames(except: view);
        if (IsDetailWorkspaceKey(view.WorkspaceKey))
        {
            SelectedDetailWorkspaceView = view;
        }
        else
        {
            SelectedWorkspaceView = view;
        }

        view.RenameRestoreName = view.Name;
        view.EditName = view.Name;
        view.IsEditing = true;
    }

    public void EndAllWorkspaceViewRenames()
    {
        EndAllWorkspaceViewRenames(except: null);
    }

    private void EndAllWorkspaceViewRenames(WorkspaceViewTab? except)
    {
        foreach (var view in _workspaceViews.Values.SelectMany(views => views)
                     .Concat(_detailWorkspaceViews.Values.SelectMany(views => views))
                     .Where(view => view.IsEditing && !ReferenceEquals(view, except))
                     .ToList())
        {
            EndRenameWorkspaceView(view);
        }
    }

    public bool EndRenameWorkspaceView(WorkspaceViewTab? view)
    {
        if (view is null)
        {
            return true;
        }

        var fallbackName = string.IsNullOrWhiteSpace(view.RenameRestoreName)
            ? (!string.IsNullOrWhiteSpace(view.DefaultName) ? view.DefaultName : "View")
            : view.RenameRestoreName;
        var proposedName = string.IsNullOrWhiteSpace(view.EditName)
            ? fallbackName
            : view.EditName.Trim();

        if (HasWorkspaceViewNameConflict(view, proposedName))
        {
            StatusText = $"View name '{view.EditName.Trim()}' is already in use.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(proposedName))
        {
            proposedName = !string.IsNullOrWhiteSpace(view.DefaultName) ? view.DefaultName : "View";
        }

        view.Name = proposedName;
        view.EditName = proposedName;
        view.RenameRestoreName = proposedName;
        view.IsNewlyCreated = false;

        if (IsDetailWorkspaceKey(view.WorkspaceKey))
        {
            _selectedDetailWorkspaceViewNames[view.WorkspaceKey] = view.Name;
        }
        else
        {
            _selectedWorkspaceViewNames[view.WorkspaceKey] = view.Name;
        }

        view.IsEditing = false;
        IsDirty = true;
        return true;
    }

    public void CancelRenameWorkspaceView(WorkspaceViewTab? view)
    {
        if (view is null)
        {
            return;
        }

        var fallbackName = string.IsNullOrWhiteSpace(view.RenameRestoreName)
            ? (!string.IsNullOrWhiteSpace(view.DefaultName) ? view.DefaultName : "View")
            : view.RenameRestoreName;
        view.Name = fallbackName;
        view.EditName = fallbackName;
        view.IsEditing = false;
        view.IsNewlyCreated = false;
    }

    private bool HasWorkspaceViewNameConflict(WorkspaceViewTab view, string candidateName)
    {
        if (string.IsNullOrWhiteSpace(candidateName))
        {
            return false;
        }

        var collection = IsDetailWorkspaceKey(view.WorkspaceKey)
            ? _detailWorkspaceViews.GetValueOrDefault(view.WorkspaceKey)
            : _workspaceViews.GetValueOrDefault(view.WorkspaceKey);
        if (collection is null)
        {
            return false;
        }

        return collection.Any(existing =>
            !ReferenceEquals(existing, view)
            && string.Equals(existing.Name?.Trim(), candidateName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static string GetNextWorkspaceViewDefaultName(IEnumerable<WorkspaceViewTab> views)
    {
        var nextNumber = views.Count() + 1;
        while (views.Any(view => string.Equals(view.Name?.Trim(), $"View {nextNumber}", StringComparison.OrdinalIgnoreCase)))
        {
            nextNumber++;
        }

        return $"View {nextNumber}";
    }
}
