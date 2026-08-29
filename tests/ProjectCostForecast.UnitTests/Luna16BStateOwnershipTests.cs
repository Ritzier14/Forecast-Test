using System.ComponentModel;
using System.Reflection;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using ProjectCostForecast.App.ViewModels;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna16BStateOwnershipTests
{
    [Fact]
    public void Schedule_and_snapshot_collections_are_the_dataset_owned_live_state()
    {
        var viewModel = Luna11TestSupport.CreateSeedViewModel();
        var dataset = GetDataset(viewModel);

        Assert.Same(dataset.Schedule.Activities, viewModel.ScheduleActivities);
        Assert.Same(dataset.Schedule.Calendars, viewModel.ScheduleCalendars);
        Assert.Same(dataset.SavedMonthSnapshots, viewModel.SavedMonthSnapshots);
        Assert.Same(dataset.Schedule, viewModel.ScheduleDataRef);
    }

    [Fact]
    public void Collection_subscription_tracker_replaces_items_without_duplicate_or_stale_callbacks()
    {
        var oldItem = new ScheduleActivity { Id = "OLD" };
        var newItem = new ScheduleActivity { Id = "NEW" };
        var collection = new BatchObservableCollection<ScheduleActivity>([oldItem]);
        var propertyCallbacks = 0;
        var collectionCallbacks = 0;
        using var tracker = new CollectionSubscriptionTracker<ScheduleActivity>(
            (_, _) => propertyCallbacks++,
            _ => collectionCallbacks++);

        tracker.SetCollection(collection, collection);
        tracker.SetCollection(collection, collection);
        oldItem.Name = "first";
        Assert.Equal(1, propertyCallbacks);

        collection.ReplaceWith([newItem]);
        Assert.Equal(1, collectionCallbacks);
        oldItem.Name = "stale";
        newItem.Name = "current";
        Assert.Equal(2, propertyCallbacks);

        tracker.Dispose();
        newItem.Name = "detached";
        Assert.Equal(2, propertyCallbacks);
    }

    [Fact]
    public void Persisted_schedule_snapshot_workspace_and_preference_edits_have_separate_dirty_boundaries()
    {
        var viewModel = Luna11TestSupport.CreateSeedViewModel();

        viewModel.IsDirty = false;
        viewModel.ScheduleProjectStartDate = viewModel.ScheduleProjectStartDate!.Value.AddDays(1);
        Assert.True(viewModel.IsDirty);

        viewModel.IsDirty = false;
        var addedActivity = viewModel.AddScheduleActivityAt(
            ScheduleActivityKind.Task,
            viewModel.ScheduleActivities.Count,
            DateOnly.FromDateTime(DateTime.Today));
        Assert.True(viewModel.IsDirty);

        viewModel.IsDirty = false;
        viewModel.DeleteScheduleActivities([addedActivity]);
        Assert.True(viewModel.IsDirty);

        viewModel.IsDirty = false;
        viewModel.ScheduleCalendars[0].Name += " updated";
        Assert.True(viewModel.IsDirty);

        viewModel.IsDirty = false;
        viewModel.CaptureScheduleBaseline("LUNA-16B baseline");
        Assert.True(viewModel.IsDirty);

        viewModel.IsDirty = false;
        var snapshot = new SavedMonthSnapshot { Period = "99-01", SavedAt = DateTimeOffset.UtcNow };
        viewModel.SavedMonthSnapshots.Add(snapshot);
        Assert.True(viewModel.IsDirty);

        viewModel.IsDirty = false;
        viewModel.SavedMonthSnapshots.Remove(snapshot);
        Assert.True(viewModel.IsDirty);

        viewModel.IsDirty = false;
        viewModel.SelectedWorkspaceView!.ContentKey = "PivotByMonth";
        Assert.True(viewModel.IsDirty);

        viewModel.IsDirty = false;
        viewModel.SetDetailPanelPinned(!viewModel.IsDetailPanelPinned);
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public void Loading_another_project_detaches_old_schedule_and_workspace_objects()
    {
        var viewModel = Luna11TestSupport.CreateSeedViewModel();
        var oldActivity = viewModel.ScheduleActivities[0];
        var oldWorkspace = viewModel.SelectedWorkspaceView!;
        var nextDataset = BuildReloadDataset(GetDataset(viewModel));

        Luna11TestSupport.InvokeLoadDataset(viewModel, nextDataset);
        viewModel.IsDirty = false;

        oldActivity.Name = "old project mutation";
        oldWorkspace.ContentKey = "old project mutation";
        Assert.False(viewModel.IsDirty);

        viewModel.ScheduleActivities[0].Name += " current";
        Assert.True(viewModel.IsDirty);
        viewModel.IsDirty = false;
        viewModel.SelectedWorkspaceView!.ContentKey = "PivotByMonth";
        Assert.True(viewModel.IsDirty);

        Assert.Same(nextDataset.Schedule.Activities, viewModel.ScheduleActivities);
        Assert.Same(nextDataset.Schedule.Calendars, viewModel.ScheduleCalendars);
        Assert.Same(nextDataset.SavedMonthSnapshots, viewModel.SavedMonthSnapshots);
    }

    private static ProjectDataset GetDataset(MainWindowViewModel viewModel)
    {
        return (ProjectDataset)(typeof(MainWindowViewModel)
            .GetField("_dataset", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(viewModel)
            ?? throw new InvalidOperationException("MainWindowViewModel dataset field was not found."));
    }

    private static ProjectDataset BuildReloadDataset(ProjectDataset source)
    {
        var dataset = new ProjectDatasetCloner().Clone(source);
        var calendar = new ScheduleCalendar { Id = "NEXT-CALENDAR", Name = "Next calendar" };
        dataset.Schedule.Calendars.ReplaceWith([calendar]);
        dataset.Schedule.DefaultCalendarId = calendar.Id;
        dataset.Schedule.Activities.ReplaceWith(
        [
            new ScheduleActivity
            {
                Id = "NEXT-ACTIVITY",
                Name = "Next activity",
                Kind = ScheduleActivityKind.Task,
                DurationDays = 2,
                CalendarId = calendar.Id
            }
        ]);
        dataset.Schedule.Links.Clear();
        dataset.Schedule.Baselines.Clear();
        dataset.Schedule.ActiveBaselineName = string.Empty;
        dataset.WorkspaceViews.ReplaceWith(
        [
            new WorkspaceViewLayout
            {
                WorkspaceKey = "CTC Forecast",
                ContentKey = "Default",
                Name = "Reloaded forecast"
            }
        ]);
        dataset.SavedMonthSnapshots.ReplaceWith(
        [
            new SavedMonthSnapshot
            {
                Period = "99-01",
                SavedAt = DateTimeOffset.UtcNow,
                ForecastLines = []
            }
        ]);
        return dataset;
    }
}
