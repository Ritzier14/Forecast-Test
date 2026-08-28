using System.Diagnostics;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna11SchedulingCoverageTests
{
    [Fact]
    public void Scheduling_parser_calendars_constraints_baselines_and_cpm_dates_match_the_characterization()
    {
        var parsedPredecessors = SchedulingService.ParsePredecessors("A1 SS+3, A2 FF-2; A3", out var parseErrors);
        Assert.Empty(parseErrors);
        Assert.Equal(3, parsedPredecessors.Count);
        Assert.Equal(ActivityLinkType.StartToStart, parsedPredecessors[0].Type);
        Assert.Equal(3, parsedPredecessors[0].LagDays);
        Assert.Equal(-2, parsedPredecessors[1].LagDays);
        Assert.Equal(ActivityLinkType.FinishToStart, parsedPredecessors[2].Type);

        var fiveDayCalendar = new ScheduleCalendar { Id = "CAL5", Name = "Standard 5 day" };
        fiveDayCalendar.Holidays.Add(new DateOnly(2026, 7, 8));
        Assert.Equal(
            0,
            SchedulingService.CalculateFinishToStartLagFromDrop(
                fiveDayCalendar,
                new DateOnly(2026, 7, 6),
                new DateOnly(2026, 7, 6)));
        Assert.Equal(
            2,
            SchedulingService.CalculateFinishToStartLagFromDrop(
                fiveDayCalendar,
                new DateOnly(2026, 7, 6),
                new DateOnly(2026, 7, 9)));
        Assert.Equal(
            -1,
            SchedulingService.CalculateFinishToStartLagFromDrop(
                fiveDayCalendar,
                new DateOnly(2026, 7, 6),
                new DateOnly(2026, 7, 3)));
        var sevenDayCalendar = new ScheduleCalendar
        {
            Id = "CAL7",
            Name = "7 day",
            WorkingDays = [true, true, true, true, true, true, true]
        };
        var schedule = new ScheduleData
        {
            ProjectStart = new DateOnly(2026, 7, 6),
            DefaultCalendarId = "CAL5",
            Calendars = [fiveDayCalendar, sevenDayCalendar],
            ActiveBaselineName = "BL1",
            Baselines =
            [
                new ScheduleBaseline
                {
                    Name = "BL1",
                    Entries = [new ScheduleBaselineEntry { ActivityId = "A2", Start = new DateOnly(2026, 7, 10), Finish = new DateOnly(2026, 7, 13) }]
                }
            ],
            Activities =
            [
                new ScheduleActivity { Id = "H1", Name = "Stage 1", Kind = ScheduleActivityKind.Heading, OutlineLevel = 0 },
                new ScheduleActivity { Id = "A1", Name = "Dig", Kind = ScheduleActivityKind.Task, DurationDays = 3, CalendarId = "CAL5", OutlineLevel = 1 },
                new ScheduleActivity { Id = "A2", Name = "Pour", Kind = ScheduleActivityKind.Task, DurationDays = 2, CalendarId = "CAL5", OutlineLevel = 1, PredecessorText = "A1 FS+1" },
                new ScheduleActivity { Id = "A3", Name = "Cure", Kind = ScheduleActivityKind.Task, DurationDays = 4, CalendarId = "CAL7", OutlineLevel = 1, PredecessorText = "A2" },
                new ScheduleActivity { Id = "M1", Name = "Stage complete", Kind = ScheduleActivityKind.Milestone, CalendarId = "CAL7", OutlineLevel = 1, PredecessorText = "A3" },
                new ScheduleActivity { Id = "HAM", Name = "Site overheads", Kind = ScheduleActivityKind.Hammock, HammockMemberText = "A1, A3" },
                new ScheduleActivity { Id = "A4", Name = "Parallel works", Kind = ScheduleActivityKind.Task, DurationDays = 2, CalendarId = "CAL5", PredecessorText = "A1" },
                new ScheduleActivity { Id = "A5", Name = "Constrained start", Kind = ScheduleActivityKind.Task, DurationDays = 2, CalendarId = "CAL5", ConstraintType = ScheduleConstraintType.StartOnOrAfter, ConstraintDate = new DateOnly(2026, 7, 13) }
            ]
        };
        new SchedulingService().Recalculate(schedule);
        var byId = schedule.Activities.ToDictionary(activity => activity.Id, StringComparer.OrdinalIgnoreCase);
        var dig = byId["A1"];
        var pour = byId["A2"];
        var cure = byId["A3"];
        var stageMilestone = byId["M1"];
        var hammock = byId["HAM"];
        var parallel = byId["A4"];
        var constrained = byId["A5"];
        var stageHeading = byId["H1"];
        Assert.Equal(4, schedule.Links.Count);
        Assert.Equal(new DateOnly(2026, 7, 6), dig.EarlyStart);
        Assert.Equal(new DateOnly(2026, 7, 9), dig.EarlyFinish);
        Assert.Equal(new DateOnly(2026, 7, 13), pour.EarlyStart);
        Assert.Equal(new DateOnly(2026, 7, 14), pour.EarlyFinish);
        Assert.Equal(new DateOnly(2026, 7, 18), cure.EarlyFinish);
        Assert.Equal(new DateOnly(2026, 7, 19), stageMilestone.EarlyStart);
        Assert.Equal(new DateOnly(2026, 7, 6), dig.LateStart);
        Assert.Equal(0, dig.TotalFloatDays);
        Assert.True(dig.IsCritical && pour.IsCritical && cure.IsCritical && stageMilestone.IsCritical);
        Assert.Equal(4, parallel.TotalFloatDays);
        Assert.False(parallel.IsCritical);
        Assert.Equal(new DateOnly(2026, 7, 13), constrained.EarlyStart);
        Assert.Equal(new DateOnly(2026, 7, 6), stageHeading.EarlyStart);
        Assert.Equal(new DateOnly(2026, 7, 19), stageHeading.EarlyFinish);
        Assert.Equal(new DateOnly(2026, 7, 6), hammock.EarlyStart);
        Assert.Equal(new DateOnly(2026, 7, 18), hammock.EarlyFinish);
        Assert.Equal(new DateOnly(2026, 7, 13), pour.BaselineFinish);
        Assert.Equal(1, pour.SlipDays);

        var deadlineCalendar = new ScheduleCalendar { Id = "DEADLINE-CAL", Name = "Deadline calendar" };
        var deadlineSchedule = new ScheduleData
        {
            ProjectStart = new DateOnly(2026, 7, 6),
            MustFinishBy = new DateOnly(2026, 7, 8),
            DefaultCalendarId = deadlineCalendar.Id,
            Calendars = [deadlineCalendar],
            Activities = [new ScheduleActivity { Id = "D1", Name = "Deadline task", DurationDays = 5, CalendarId = deadlineCalendar.Id }]
        };
        new SchedulingService().Recalculate(deadlineSchedule);
        var deadlineActivity = Assert.Single(deadlineSchedule.Activities);
        Assert.Equal(new DateOnly(2026, 7, 10), deadlineActivity.EarlyFinish);
        Assert.Equal(new DateOnly(2026, 7, 8), deadlineActivity.LateFinish);
        Assert.Equal(-2, deadlineActivity.TotalFloatDays);
        Assert.True(deadlineActivity.IsCritical);
    }

    [Fact]
    public void Schedule_view_model_editing_supports_links_clipboard_reordering_and_baseline_dates()
    {
        var viewModel = Luna11TestSupport.CreateSeedViewModel();
        var originalScheduleCount = viewModel.ScheduleActivities.Count;
        viewModel.SelectedScheduleActivity = viewModel.ScheduleActivities[1];
        var insertedAbove = viewModel.AddScheduleActivityRelative(ScheduleActivityKind.Task, above: true);
        Assert.Equal(1, viewModel.ScheduleActivities.IndexOf(insertedAbove));
        var insertedBelow = viewModel.AddScheduleActivityRelative(ScheduleActivityKind.Task, above: false);
        Assert.Equal(2, viewModel.ScheduleActivities.IndexOf(insertedBelow));
        Assert.Equal(originalScheduleCount + 2, viewModel.ScheduleActivities.Count);
        viewModel.SelectedScheduleActivity = insertedAbove;
        viewModel.ConvertSelectedScheduleActivityToMilestone();
        Assert.Equal(ScheduleActivityKind.Milestone, insertedAbove.Kind);
        Assert.Equal(0, insertedAbove.DurationDays);
        viewModel.SetSelectedScheduleProgress(75);
        Assert.Equal(75d, insertedAbove.PercentComplete);
        insertedAbove.Kind = ScheduleActivityKind.Task;
        insertedAbove.DurationDays = 2;
        insertedAbove.PredecessorText = string.Empty;
        insertedBelow.PredecessorText = string.Empty;
        viewModel.RecalculateSchedule();
        Assert.True(viewModel.TryCreateScheduleLink(insertedAbove, insertedBelow, ActivityLinkType.StartToStart, 2));
        var addedRelationship = SchedulingService.ParsePredecessors(insertedBelow.PredecessorText, out _).Single();
        Assert.Equal(ActivityLinkType.StartToStart, addedRelationship.Type);
        Assert.Equal(2, addedRelationship.LagDays);
        viewModel.UpdateScheduleLink(insertedBelow, insertedAbove.Id, ActivityLinkType.FinishToFinish, -1);
        var movedRelationship = SchedulingService.ParsePredecessors(insertedBelow.PredecessorText, out _).Single();
        Assert.Equal(ActivityLinkType.FinishToFinish, movedRelationship.Type);
        Assert.Equal(-1, movedRelationship.LagDays);
        viewModel.CopyScheduleLinkSource(insertedAbove);
        viewModel.CopyScheduleLinkSource(insertedAbove);
        Assert.Equal(2, viewModel.ScheduleLinkClipboardActivities.Count);
        var linkClipboardTarget = viewModel.AddScheduleActivityAt(ScheduleActivityKind.Task, viewModel.ScheduleActivities.Count);
        Assert.True(viewModel.PasteScheduleLinkTo(linkClipboardTarget));
        Assert.Single(viewModel.ScheduleLinkClipboardActivities);
        var secondLinkClipboardTarget = viewModel.AddScheduleActivityAt(ScheduleActivityKind.Task, viewModel.ScheduleActivities.Count);
        Assert.True(viewModel.PasteScheduleLinkTo(secondLinkClipboardTarget));
        Assert.Empty(viewModel.ScheduleLinkClipboardActivities);
        var successorClipboardSource = viewModel.AddScheduleActivityAt(ScheduleActivityKind.Task, viewModel.ScheduleActivities.Count);
        var successorClipboardTarget = viewModel.AddScheduleActivityAt(ScheduleActivityKind.Task, viewModel.ScheduleActivities.Count);
        viewModel.CopyScheduleLinkSource(successorClipboardTarget);
        Assert.True(viewModel.PasteScheduleSuccessorFromClipboard(successorClipboardSource));
        Assert.Equal(
            successorClipboardSource.Id,
            SchedulingService.ParsePredecessors(successorClipboardTarget.PredecessorText, out _).Single().PredecessorId);
        Assert.Empty(viewModel.ScheduleLinkClipboardActivities);
        viewModel.RecalculateSchedule();
        Assert.False(viewModel.TryCreateScheduleLink(insertedAbove, secondLinkClipboardTarget));
        Assert.False(viewModel.TryCreateScheduleLink(linkClipboardTarget, insertedAbove));
        viewModel.BreakScheduleLink(insertedBelow, insertedAbove.Id);
        Assert.Empty(SchedulingService.ParsePredecessors(insertedBelow.PredecessorText, out _));
        var oldRow = viewModel.ScheduleActivities.IndexOf(insertedAbove);
        viewModel.MoveScheduleActivity(insertedAbove, Math.Min(oldRow + 3, viewModel.ScheduleActivities.Count - 1));
        Assert.NotEqual(oldRow, viewModel.ScheduleActivities.IndexOf(insertedAbove));
        viewModel.CaptureScheduleBaseline("Editable baseline");
        viewModel.ScheduleEditMode = ScheduleEditMode.SelectedBaseline;
        var editedBaselineDate = insertedAbove.EarlyStart?.AddDays(2) ?? new DateOnly(2026, 7, 6);
        insertedAbove.BaselineStart = editedBaselineDate;
        Assert.Equal(editedBaselineDate, viewModel.ScheduleDataRef.ActiveBaseline?.FindEntry(insertedAbove.Id)?.Start);
        Assert.True(viewModel.ScheduleDataRef.Baselines.Count >= 2);
    }

    [Fact]
    public void Large_schedule_recalculation_rebuilds_all_links_within_the_legacy_budget()
    {
        var calendar = new ScheduleCalendar
        {
            Id = "PERF",
            Name = "Performance",
            WorkingDays = [true, true, true, true, true, true, true]
        };
        var schedule = new ScheduleData
        {
            ProjectStart = new DateOnly(2026, 1, 1),
            DefaultCalendarId = calendar.Id,
            Calendars = [calendar]
        };
        const int activityCount = 2500;
        for (var index = 0; index < activityCount; index++)
        {
            schedule.Activities.Add(new ScheduleActivity
            {
                Id = $"P{index:0000}",
                Name = $"Performance activity {index}",
                Kind = ScheduleActivityKind.Task,
                DurationDays = 1,
                CalendarId = calendar.Id,
                PredecessorText = index == 0 ? string.Empty : $"P{index - 1:0000}"
            });
        }

        var stopwatch = Stopwatch.StartNew();
        new SchedulingService().Recalculate(schedule);
        stopwatch.Stop();
        Assert.InRange(schedule.Links.Count, activityCount - 1, activityCount - 1);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Elapsed {stopwatch.ElapsedMilliseconds} ms");
    }
}
