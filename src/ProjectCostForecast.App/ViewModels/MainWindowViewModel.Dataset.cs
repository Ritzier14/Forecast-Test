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
    private void LoadDataset(ProjectDataset dataset, bool markDirty)
    {
        UnsubscribeMonthlyForecastEvents();
        _dataset = dataset;
        _dataset.CostCenterNameMappings ??= [];
        _dataset.UnmatchedImportCombinations ??= [];
        _dataset.WorkspaceViews ??= [];
        _dataset.ManagementResources ??= [];
        InitializeWorkspaceViews(_dataset.WorkspaceViews);
        RefreshCurrentWorkspaceViews();
        foreach (var line in _dataset.ForecastLines)
        {
            line.EnsureResourceCommentMetrics();
        }
        ApplyClosedForecastPeriodRule();
        _calculationService.Recalculate(_dataset);

        ReplaceCollection(ForecastLines, _dataset.ForecastLines);
        LoadManagementResources(_dataset.ManagementResources);
        ReplaceCollection(Transactions, _dataset.Transactions);
        ReplaceCollection(ContingencyEntries, _dataset.ContingencyEntries);
        ReplaceCollection(Phases, _dataset.Phases);
        ReplaceCollection(SavedMonthSnapshots, _dataset.SavedMonthSnapshots.OrderByDescending(snapshot => snapshot.SavedAt));
        ReplaceCollection(UnmatchedImportCombinations, _dataset.UnmatchedImportCombinations.OrderByDescending(item => item.RecordedAt));
        ReplaceCollection(AuditEvents, _dataset.AuditEvents.OrderByDescending(a => a.ChangedAt));
        _showCtcMonthForecastYearTotals = _dataset.ShowCtcMonthForecastYearTotals;
        OnPropertyChanged(nameof(ShowCtcMonthForecastYearTotals));

        InitializeScheduleFromDataset();
        SubscribeMonthlyForecastEvents();
        RebuildCtcMonthForecastYearOptions();
        RebuildCalculatedViews(rebuildFilterLists: true);
        ApplyForecastGrouping();
        RefreshViewsAndTotals();
        OnPropertyChanged(nameof(Header));

        SelectedForecastLine = ForecastLines.FirstOrDefault();
        IsDirty = markDirty;
        ApplyUserPreferences();
    }

    public void ToggleCtcMonthForecastColumns()
    {
        if (!IsCtcMonthForecastSelectionAvailable)
        {
            return;
        }

        ShowCtcMonthForecastColumns = !ShowCtcMonthForecastColumns;
    }

    public void SetCtcMonthForecastYear(int calendarYear)
    {
        if (AvailableCtcMonthForecastYears.Contains(calendarYear))
        {
            _selectedCtcMonthForecastYears.Clear();
            _selectedCtcMonthForecastYears.Add(calendarYear);
            SelectedCtcMonthForecastYear = calendarYear;
            OnPropertyChanged(nameof(SelectedCtcMonthForecastYears));
            RebuildCtcMonthForecastColumns();
        }
    }

    public void ToggleCtcMonthForecastYear(int calendarYear)
    {
        if (!AvailableCtcMonthForecastYears.Contains(calendarYear))
        {
            return;
        }

        if (_selectedCtcMonthForecastYears.Contains(calendarYear))
        {
            if (_selectedCtcMonthForecastYears.Count == 1)
            {
                return;
            }

            _selectedCtcMonthForecastYears.Remove(calendarYear);
        }
        else
        {
            _selectedCtcMonthForecastYears.Add(calendarYear);
        }

        _selectedCtcMonthForecastYear = _selectedCtcMonthForecastYears.Min();
        _dataset.SelectedCtcMonthForecastYears = _selectedCtcMonthForecastYears.OrderBy(year => year).ToList();
        OnPropertyChanged(nameof(SelectedCtcMonthForecastYear));
        OnPropertyChanged(nameof(SelectedCtcMonthForecastYears));
        RebuildCtcMonthForecastColumns();
        IsDirty = true;
        SaveUserPreferences();
    }

    public bool IsCtcMonthForecastYearSelected(int calendarYear)
    {
        return _selectedCtcMonthForecastYears.Contains(calendarYear);
    }

    public void ToggleMonthForecastHeaderOrder()
    {
        ShowMonthNameAboveFiscalPeriod = !ShowMonthNameAboveFiscalPeriod;
    }

    public void ToggleCtcMonthForecastYearTotals()
    {
        ShowCtcMonthForecastYearTotals = !ShowCtcMonthForecastYearTotals;
    }

    public void SetForecastFreezeColumn(string key)
    {
        ForecastFreezeColumnKey = key;
    }

    public void SetHoveredForecastLine(ForecastLine? line)
    {
        if (ReferenceEquals(_hoveredForecastLine, line))
        {
            return;
        }

        _hoveredForecastLine = line;
        OnPropertyChanged(nameof(HoveredForecastLine));
        NotifyLedgerChanged();
    }

    public void ClearHoveredForecastLine()
    {
        SetHoveredForecastLine(null);
    }

    public void ResetForecastFreezeColumn()
    {
        ForecastFreezeColumnKey = DefaultForecastFreezeColumnKey;
    }

    public void SetDetailPanelCollapsed(bool collapsed)
    {
        if (_userPreferences.DetailPanelCollapsed == collapsed)
        {
            return;
        }

        _userPreferences.DetailPanelCollapsed = collapsed;
        SaveUserPreferences();
        OnPropertyChanged(nameof(IsDetailPanelCollapsed));
    }

    public void SetStartInFullScreen(bool startInFullScreen)
    {
        if (_userPreferences.StartMaximized == startInFullScreen)
        {
            return;
        }

        _userPreferences.StartMaximized = startInFullScreen;
        SaveUserPreferences();
        OnPropertyChanged(nameof(StartInFullScreen));
    }

    public IReadOnlyList<string> GetSelectedWorkspaceHiddenColumnKeys()
    {
        return SelectedWorkspaceView?.HiddenColumnKeys?.ToList() ?? [];
    }

    public void SetSelectedWorkspaceHiddenColumnKeys(IEnumerable<string> hiddenColumnKeys)
    {
        if (SelectedWorkspaceView is null)
        {
            return;
        }

        var nextKeys = hiddenColumnKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var currentKeys = SelectedWorkspaceView.HiddenColumnKeys ?? [];
        if (currentKeys.SequenceEqual(nextKeys, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        SelectedWorkspaceView.HiddenColumnKeys = nextKeys;
        IsDirty = true;
    }

    public IReadOnlyList<WorkspaceColumnLayout> GetSelectedWorkspaceColumnLayouts()
    {
        return SelectedWorkspaceView?.ColumnLayouts?.Select(CloneWorkspaceColumnLayout).ToList() ?? [];
    }

    public void SetSelectedWorkspaceColumnLayouts(IEnumerable<WorkspaceColumnLayout> columnLayouts)
    {
        if (SelectedWorkspaceView is null)
        {
            return;
        }

        var nextLayouts = NormalizeWorkspaceColumnLayouts(columnLayouts);
        if (WorkspaceColumnLayoutsEqual(SelectedWorkspaceView.ColumnLayouts, nextLayouts))
        {
            return;
        }

        SelectedWorkspaceView.ColumnLayouts = nextLayouts;
        IsDirty = true;
    }

    public void SetSelectedWorkspaceContentKey(string contentKey)
    {
        if (SelectedWorkspaceView is null || string.IsNullOrWhiteSpace(contentKey))
        {
            return;
        }

        if (string.Equals(SelectedWorkspaceView.ContentKey, contentKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SelectedWorkspaceView.ContentKey = contentKey;
        IsDirty = true;
        OnPropertyChanged(nameof(ShowActualsPivotByMonth));
        OnPropertyChanged(nameof(ShowActualsPivotByPeriodRows));
        OnPropertyChanged(nameof(ShowForecastPivotByMonth));
        OnPropertyChanged(nameof(ShowRawTransactionsGroupedByMonth));
        OnPropertyChanged(nameof(ShowRawTransactionsPivotByMonth));
        OnPropertyChanged(nameof(ShowSummaryViewByMonth));
        ApplyRawTransactionGrouping();
    }

    public IReadOnlyList<string> GetSelectedDetailWorkspaceHiddenColumnKeys()
    {
        return SelectedDetailWorkspaceView?.HiddenColumnKeys?.ToList() ?? [];
    }

    public void SetSelectedDetailWorkspaceHiddenColumnKeys(IEnumerable<string> hiddenColumnKeys)
    {
        if (SelectedDetailWorkspaceView is null)
        {
            return;
        }

        var nextKeys = hiddenColumnKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var currentKeys = SelectedDetailWorkspaceView.HiddenColumnKeys ?? [];
        if (currentKeys.SequenceEqual(nextKeys, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        SelectedDetailWorkspaceView.HiddenColumnKeys = nextKeys;
        IsDirty = true;
    }

    public IReadOnlyList<WorkspaceColumnLayout> GetSelectedDetailWorkspaceColumnLayouts()
    {
        return SelectedDetailWorkspaceView?.ColumnLayouts?.Select(CloneWorkspaceColumnLayout).ToList() ?? [];
    }

    public void SetSelectedDetailWorkspaceColumnLayouts(IEnumerable<WorkspaceColumnLayout> columnLayouts)
    {
        if (SelectedDetailWorkspaceView is null)
        {
            return;
        }

        var nextLayouts = NormalizeWorkspaceColumnLayouts(columnLayouts);
        if (WorkspaceColumnLayoutsEqual(SelectedDetailWorkspaceView.ColumnLayouts, nextLayouts))
        {
            return;
        }

        SelectedDetailWorkspaceView.ColumnLayouts = nextLayouts;
        IsDirty = true;
    }

    private static List<WorkspaceColumnLayout> NormalizeWorkspaceColumnLayouts(IEnumerable<WorkspaceColumnLayout> columnLayouts)
    {
        return columnLayouts
            .Where(layout => !string.IsNullOrWhiteSpace(layout.Key))
            .GroupBy(layout => layout.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(layout => layout.DisplayIndex)
            .Select(layout => new WorkspaceColumnLayout
            {
                Key = layout.Key,
                Width = layout.Width,
                DisplayIndex = layout.DisplayIndex
            })
            .ToList();
    }

    private static bool WorkspaceColumnLayoutsEqual(IReadOnlyList<WorkspaceColumnLayout>? current, IReadOnlyList<WorkspaceColumnLayout> next)
    {
        current ??= [];
        if (current.Count != next.Count)
        {
            return false;
        }

        for (var i = 0; i < current.Count; i++)
        {
            if (!string.Equals(current[i].Key, next[i].Key, StringComparison.OrdinalIgnoreCase)
                || current[i].DisplayIndex != next[i].DisplayIndex
                || Math.Abs(current[i].Width - next[i].Width) > 0.5)
            {
                return false;
            }
        }

        return true;
    }

    public void SetSelectedDetailWorkspaceContentKey(string contentKey)
    {
        if (SelectedDetailWorkspaceView is null || string.IsNullOrWhiteSpace(contentKey))
        {
            return;
        }

        if (string.Equals(SelectedDetailWorkspaceView.ContentKey, contentKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SelectedDetailWorkspaceView.ContentKey = contentKey;
        IsDirty = true;
        OnPropertyChanged(nameof(ShowLedgerCostsPivotByMonth));
        ApplyLedgerTransactionGrouping();
    }
}
