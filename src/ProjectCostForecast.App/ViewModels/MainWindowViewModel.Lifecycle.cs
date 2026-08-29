using System.Windows.Threading;

namespace ProjectCostForecast.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private bool _disposed;
    private DispatcherOperation? _refreshDispatcherOperation;
    private DispatcherOperation? _scheduleRecalculationOperation;

    private void PreferenceSaveTimer_Tick(object? sender, EventArgs e)
    {
        _preferenceSaveTimer.Stop();
        if (!_disposed)
        {
            PersistUserPreferences();
        }
    }

    private void SearchRefreshTimer_Tick(object? sender, EventArgs e)
    {
        _searchRefreshTimer.Stop();
        if (!_disposed)
        {
            RefreshSearchViews();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _preferenceSaveTimer.Stop();
        _preferenceSaveTimer.Tick -= PreferenceSaveTimer_Tick;
        _searchRefreshTimer.Stop();
        _searchRefreshTimer.Tick -= SearchRefreshTimer_Tick;

        _refreshDispatcherOperation?.Abort();
        _refreshDispatcherOperation = null;
        _scheduleRecalculationOperation?.Abort();
        _scheduleRecalculationOperation = null;
        _scheduleRecalcQueued = false;
        _refreshCoordinator.Dispose();

        UnsubscribeMonthlyForecastEvents();
        UnsubscribeSavedMonthForecastEvents();
        if (_subscribedSavedMonthSnapshots is not null)
        {
            _subscribedSavedMonthSnapshots.CollectionChanged -= SavedMonthSnapshots_CollectionChanged;
            _subscribedSavedMonthSnapshots = null;
        }

        if (_subscribedScheduleData is not null)
        {
            _subscribedScheduleData.PropertyChanged -= ScheduleData_PropertyChanged;
            _subscribedScheduleData = null;
        }

        _scheduleActivitySubscriptions.Detach();
        _scheduleCalendarSubscriptions.Detach();
        _scheduleBaselineSubscriptions.Detach();

        ContingencyEntries.CollectionChanged -= ContingencyEntriesChanged;
        foreach (var entry in _trackedContingencyEntries)
        {
            entry.PropertyChanged -= ContingencyEntryPropertyChanged;
        }

        UnsubscribeBudgetLineChanges();
        foreach (var resource in ManagementResources)
        {
            resource.PropertyChanged -= ManagementResource_PropertyChanged;
        }

        DetachWorkspaceViewSubscriptions();
        foreach (var field in PivotFilterFields.Concat(PivotRowFields).Concat(PivotColumnFields).Concat(PivotValueFields))
        {
            field.PropertyChanged -= PivotAreaField_PropertyChanged;
        }
    }
}
