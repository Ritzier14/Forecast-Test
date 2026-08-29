using System.Windows;
using System.Windows.Input;
using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private readonly Dictionary<MonthlyForecast, (SavedMonthForecastLine SnapshotLine, ForecastLine DisplayLine)> _savedMonthForecastLookup = [];
    private readonly BatchObservableCollection<ForecastLine> _savedMonthDisplayLines = [];
    private ForecastLine? _workingSelectedForecastLineBeforeSavedMonthView;
    private SavedMonthSnapshot? _viewedSavedMonthSnapshot;
    private bool _isSavedMonthViewLocked;
    private SavedMonthForecastFilterState? _workingForecastFiltersBeforeSavedMonthView;

    public ICommand ToggleSavedMonthViewLockCommand { get; private set; } = null!;
    public ICommand CloseSavedMonthViewCommand { get; private set; } = null!;

    public bool IsViewingSavedMonth => _viewedSavedMonthSnapshot is not null;

    public bool IsSavedMonthViewLocked
    {
        get => _isSavedMonthViewLocked;
        private set
        {
            if (SetProperty(ref _isSavedMonthViewLocked, value))
            {
                OnPropertyChanged(nameof(SavedMonthViewMessage));
                OnPropertyChanged(nameof(SavedMonthLockButtonText));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string ViewedSavedMonthPeriod => _viewedSavedMonthSnapshot?.Period ?? string.Empty;

    public string SavedMonthViewMessage => IsSavedMonthViewLocked
        ? $"Viewing locked saved month {ViewedSavedMonthPeriod}. Editing is disabled."
        : $"Viewing unlocked saved month {ViewedSavedMonthPeriod}. Changes will update this saved snapshot.";

    public string SavedMonthLockButtonText => IsSavedMonthViewLocked ? "Unlock" : "Lock month";

    public void ViewSavedMonthSnapshot(SavedMonthSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (IsViewingSavedMonth)
        {
            CloseSavedMonthView();
        }

        UnsubscribeMonthlyForecastEvents();
        _workingSelectedForecastLineBeforeSavedMonthView = SelectedForecastLine;
        _workingForecastFiltersBeforeSavedMonthView = CaptureSavedMonthForecastFilterState();
        ApplySavedMonthForecastFilters();
        _viewedSavedMonthSnapshot = snapshot;
        _savedMonthForecastLookup.Clear();

        var displayLines = snapshot.ForecastLines
            .Select(CreateSavedMonthDisplayLine)
            .ToList();
        _savedMonthDisplayLines.ReplaceWith(displayLines);
        SetForecastLinesProjection(_savedMonthDisplayLines);
        ApplyForecastGrouping();
        foreach (var line in displayLines)
        {
            foreach (var forecast in line.MonthlyForecasts)
            {
                var snapshotLine = snapshot.ForecastLines.First(savedLine => savedLine.RowNumber == line.RowNumber);
                _savedMonthForecastLookup[forecast] = (snapshotLine, line);
                forecast.AmountChanged += SavedMonthForecastAmountChanged;
            }
        }

        IsSavedMonthViewLocked = true;
        ApplySavedMonthLockState();
        SelectedForecastLine = ForecastLines.FirstOrDefault();
        OnPropertyChanged(nameof(IsViewingSavedMonth));
        OnPropertyChanged(nameof(ViewedSavedMonthPeriod));
        OnPropertyChanged(nameof(SavedMonthViewMessage));
        RebuildCtcMonthForecastColumns();
        RefreshViewsAndTotals();
        StatusText = $"Viewing locked saved month {snapshot.Period}.";
    }

    public void ToggleSavedMonthViewLock()
    {
        SetSavedMonthViewLocked(!IsSavedMonthViewLocked, confirmUnlock: true);
    }

    public bool SetSavedMonthViewLocked(bool locked, bool confirmUnlock = true)
    {
        if (!IsViewingSavedMonth)
        {
            return false;
        }

        if (locked == IsSavedMonthViewLocked)
        {
            return true;
        }

        if (!locked
            && confirmUnlock
            && MessageBox.Show(
                $"Unlock saved month {ViewedSavedMonthPeriod} for editing? Changes will update the saved snapshot.",
                "Unlock saved month",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return false;
        }

        IsSavedMonthViewLocked = locked;
        ApplySavedMonthLockState();
        RebuildCtcMonthForecastColumns();
        StatusText = IsSavedMonthViewLocked
            ? $"Saved month {ViewedSavedMonthPeriod} is locked."
            : $"Saved month {ViewedSavedMonthPeriod} is unlocked for editing.";
        return true;
    }

    public void CloseSavedMonthView()
    {
        if (!IsViewingSavedMonth)
        {
            return;
        }

        UnsubscribeSavedMonthForecastEvents();
        SetForecastLinesProjection(_dataset.ForecastLines);
        ApplyForecastGrouping();
        _savedMonthDisplayLines.Clear();
        SubscribeMonthlyForecastEvents();
        SelectedForecastLine = _workingSelectedForecastLineBeforeSavedMonthView is { } previousSelection
            && ForecastLines.Contains(previousSelection)
                ? previousSelection
                : ForecastLines.FirstOrDefault();

        _workingSelectedForecastLineBeforeSavedMonthView = null;
        _viewedSavedMonthSnapshot = null;
        RestoreSavedMonthForecastFilters();
        IsSavedMonthViewLocked = false;
        OnPropertyChanged(nameof(IsViewingSavedMonth));
        OnPropertyChanged(nameof(ViewedSavedMonthPeriod));
        OnPropertyChanged(nameof(SavedMonthViewMessage));
        ApplyClosedForecastPeriodRule();
        ApplyForecastPeriodLockStates();
        RebuildCtcMonthForecastColumns();
        RebuildCalculatedViews(rebuildFilterLists: true);
        RefreshViewsAndTotals();
        StatusText = $"Returned to current working period {Header.CurrentPeriod}.";
    }

    private ForecastLine CreateSavedMonthDisplayLine(SavedMonthForecastLine savedLine)
    {
        var displayLine = new ForecastLine
        {
            RowNumber = savedLine.RowNumber,
            TaskNumber = savedLine.TaskNumber,
            ResourceName = savedLine.ResourceName,
            ProjectCode = savedLine.ProjectCode,
            CostToDate = savedLine.CostToDate,
            CostToDateSummary = savedLine.CostToDate,
            MonthForecast = savedLine.CurrentPeriodForecast,
            TotalForecastCtc = savedLine.CostToComplete,
            PlannedCostFcc = savedLine.FinalForecast,
            LastMonthPlannedCost = savedLine.FinalForecast + savedLine.VarianceFromPreviousMonth,
            VarianceLastMonthToDate = savedLine.VarianceFromPreviousMonth,
            Budget = savedLine.Budget,
            TotalBudgetVariance = savedLine.TotalBudgetVariance,
            MonthlyForecasts = savedLine.MonthlyForecasts.Select(amount => new MonthlyForecast
            {
                PeriodLabel = amount.PeriodLabel,
                PeriodStartDate = amount.PeriodStartDate,
                Amount = amount.Amount,
                IsLocked = true
            }).ToList()
        };
        displayLine.SetResolvedTaskMetadata(savedLine.ProjectCode, savedLine.ProjectCode);
        return displayLine;
    }

    private void ApplySavedMonthLockState()
    {
        foreach (var forecast in ForecastLines.SelectMany(line => line.MonthlyForecasts))
        {
            forecast.IsLocked = IsSavedMonthViewLocked;
        }
    }

    private void SavedMonthForecastAmountChanged(object? sender, ValueChangedEventArgs<decimal> e)
    {
        if (sender is not MonthlyForecast forecast
            || !_savedMonthForecastLookup.TryGetValue(forecast, out var context)
            || _viewedSavedMonthSnapshot is null)
        {
            return;
        }

        var savedAmount = context.SnapshotLine.MonthlyForecasts.FirstOrDefault(item =>
            string.Equals(item.PeriodLabel, forecast.PeriodLabel, StringComparison.OrdinalIgnoreCase));
        if (savedAmount is not null)
        {
            savedAmount.Amount = forecast.Amount;
        }

        RecalculateSavedMonthDisplayLine(context.SnapshotLine, context.DisplayLine, _viewedSavedMonthSnapshot.Period);
        RecalculateSavedMonthSnapshotTotals(_viewedSavedMonthSnapshot);
        AddAuditEvent(
            "SavedMonth",
            _viewedSavedMonthSnapshot.Period,
            $"{context.DisplayLine.TaskNumber}/{context.DisplayLine.ResourceName}/{forecast.PeriodLabel}",
            e.OldValue.ToString("0.##"),
            e.NewValue.ToString("0.##"),
            "Edited unlocked saved month forecast");
        IsDirty = true;
        NotifyTotalsChanged();
        StatusText = $"Updated saved month {ViewedSavedMonthPeriod}.";
    }

    private static void RecalculateSavedMonthDisplayLine(SavedMonthForecastLine savedLine, ForecastLine displayLine, string viewedPeriod)
    {
        displayLine.TotalForecastCtc = displayLine.MonthlyForecasts.Sum(month => month.Amount);
        displayLine.MonthForecast = displayLine.MonthlyForecasts
            .Where(month => string.Equals(month.PeriodLabel, viewedPeriod, StringComparison.OrdinalIgnoreCase))
            .Sum(month => month.Amount);
        displayLine.PlannedCostFcc = displayLine.CostToDateSummary + displayLine.TotalForecastCtc;
        displayLine.VarianceLastMonthToDate = displayLine.LastMonthPlannedCost - displayLine.PlannedCostFcc;
        displayLine.TotalBudgetVariance = displayLine.Budget - displayLine.PlannedCostFcc;
        displayLine.NotifyMonthForecastValuesChanged();

        savedLine.CurrentPeriodForecast = displayLine.MonthForecast;
        savedLine.CostToComplete = displayLine.TotalForecastCtc;
        savedLine.FinalForecast = displayLine.PlannedCostFcc;
        savedLine.TotalBudgetVariance = displayLine.TotalBudgetVariance;
        savedLine.VarianceFromPreviousMonth = displayLine.VarianceLastMonthToDate;
    }

    private static void RecalculateSavedMonthSnapshotTotals(SavedMonthSnapshot snapshot)
    {
        snapshot.CostToDate = snapshot.ForecastLines.Sum(line => line.CostToDate);
        snapshot.CostToComplete = snapshot.ForecastLines.Sum(line => line.CostToComplete);
        snapshot.FinalForecast = snapshot.ForecastLines.Sum(line => line.FinalForecast);
        snapshot.TotalBudgetVariance = snapshot.ForecastLines.Sum(line => line.TotalBudgetVariance);
    }

    private void UnsubscribeSavedMonthForecastEvents()
    {
        foreach (var forecast in _savedMonthForecastLookup.Keys)
        {
            forecast.AmountChanged -= SavedMonthForecastAmountChanged;
        }

        _savedMonthForecastLookup.Clear();
    }

    private void ResetSavedMonthViewStateForDatasetLoad()
    {
        UnsubscribeSavedMonthForecastEvents();
        _savedMonthDisplayLines.Clear();
        _workingSelectedForecastLineBeforeSavedMonthView = null;
        _workingForecastFiltersBeforeSavedMonthView = null;
        _viewedSavedMonthSnapshot = null;
        _isSavedMonthViewLocked = false;
        OnPropertyChanged(nameof(IsViewingSavedMonth));
        OnPropertyChanged(nameof(IsSavedMonthViewLocked));
        OnPropertyChanged(nameof(ViewedSavedMonthPeriod));
        OnPropertyChanged(nameof(SavedMonthViewMessage));
        OnPropertyChanged(nameof(SavedMonthLockButtonText));
    }

    private SavedMonthForecastFilterState CaptureSavedMonthForecastFilterState() => new(
        SearchText,
        SelectedProjectCode,
        ShowOnlyLinesWithActualCost,
        ShowCostThisMonthOnly,
        ShowOnlyLinesWithRemainingForecast,
        SelectedMonthlyVarianceFilter,
        SelectedBudgetVarianceFilter);

    private void ApplySavedMonthForecastFilters()
    {
        SetSavedMonthForecastFilters(new SavedMonthForecastFilterState(
            string.Empty,
            "All",
            false,
            false,
            false,
            "All",
            "All"));
    }

    private void RestoreSavedMonthForecastFilters()
    {
        if (_workingForecastFiltersBeforeSavedMonthView is not { } filters)
        {
            return;
        }

        _workingForecastFiltersBeforeSavedMonthView = null;
        SetSavedMonthForecastFilters(filters);
    }

    private void SetSavedMonthForecastFilters(SavedMonthForecastFilterState filters)
    {
        _searchRefreshTimer.Stop();
        var previousFilterSuppression = _suppressFilterRefresh;
        var previousPreferenceSuppression = _suppressPreferenceSave;
        _suppressFilterRefresh = true;
        _suppressPreferenceSave = true;
        try
        {
            SearchText = filters.SearchText;
            SelectedProjectCode = filters.SelectedProjectCode;
            ShowOnlyLinesWithActualCost = filters.ShowOnlyLinesWithActualCost;
            ShowCostThisMonthOnly = filters.ShowCostThisMonthOnly;
            ShowOnlyLinesWithRemainingForecast = filters.ShowOnlyLinesWithRemainingForecast;
            SelectedMonthlyVarianceFilter = filters.SelectedMonthlyVarianceFilter;
            SelectedBudgetVarianceFilter = filters.SelectedBudgetVarianceFilter;
        }
        finally
        {
            _suppressFilterRefresh = previousFilterSuppression;
            _suppressPreferenceSave = previousPreferenceSuppression;
        }

        _searchRefreshTimer.Stop();
        RefreshForecastLinesView();
    }

    private sealed record SavedMonthForecastFilterState(
        string SearchText,
        string SelectedProjectCode,
        bool ShowOnlyLinesWithActualCost,
        bool ShowCostThisMonthOnly,
        bool ShowOnlyLinesWithRemainingForecast,
        string SelectedMonthlyVarianceFilter,
        string SelectedBudgetVarianceFilter);
}
