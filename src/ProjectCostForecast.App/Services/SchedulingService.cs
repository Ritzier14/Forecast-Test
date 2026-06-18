using System.Text.RegularExpressions;
using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App.Services;

public sealed class SchedulingService
{
    private const int MaxCalendarScanDays = 7320; // ~20 years; guards against empty calendars

    private static readonly Regex PredecessorTokenRegex = new(
        @"^(?<id>[A-Za-z0-9_.\-]+)\s*(?<type>FS|SS|FF|SF)?\s*(?<lag>[+-]\s*\d+)?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static List<ParsedPredecessor> ParsePredecessors(string? text, out List<string> errors)
    {
        var result = new List<ParsedPredecessor>();
        errors = [];
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }

        foreach (var rawToken in text.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = PredecessorTokenRegex.Match(rawToken);
            if (!match.Success)
            {
                errors.Add($"Cannot read predecessor '{rawToken}'.");
                continue;
            }

            ActivityLink.TryParseTypeLabel(match.Groups["type"].Value, out var type);
            var lagText = match.Groups["lag"].Value.Replace(" ", string.Empty);
            var lag = string.IsNullOrEmpty(lagText) ? 0 : int.Parse(lagText);
            result.Add(new ParsedPredecessor
            {
                PredecessorId = match.Groups["id"].Value,
                Type = type,
                LagDays = lag
            });
        }

        return result;
    }

    public static string FormatPredecessor(ParsedPredecessor link)
    {
        var typeLabel = new ActivityLink { Type = link.Type }.TypeLabel;
        var lag = link.LagDays == 0 ? string.Empty : (link.LagDays > 0 ? $"+{link.LagDays}" : link.LagDays.ToString());
        return $"{link.PredecessorId} {typeLabel}{lag}".Trim();
    }

    public void Recalculate(ScheduleData schedule)
    {
        schedule.EnsureDefaultCalendar();
        var activities = schedule.Activities;
        var byId = new Dictionary<string, ScheduleActivity>(StringComparer.OrdinalIgnoreCase);
        foreach (var activity in activities)
        {
            activity.ScheduleNote = string.Empty;
            activity.EarlyStart = null;
            activity.EarlyFinish = null;
            activity.LateStart = null;
            activity.LateFinish = null;
            activity.TotalFloatDays = null;
            activity.IsCritical = false;
            if (!string.IsNullOrWhiteSpace(activity.Id))
            {
                if (!byId.TryAdd(activity.Id, activity))
                {
                    activity.ScheduleNote = "Duplicate ID";
                }
            }
        }

        RebuildLinks(schedule, byId);

        var network = activities
            .Where(a => a.Kind is ScheduleActivityKind.Task or ScheduleActivityKind.Milestone)
            .Where(a => !a.IsUnscheduled)
            .ToList();
        var ordered = TopologicalSort(network, schedule.Links, byId, out var cyclic);
        foreach (var activity in cyclic)
        {
            activity.ScheduleNote = "Circular link";
            activity.EarlyStart = null;
            activity.EarlyFinish = null;
            activity.LateStart = null;
            activity.LateFinish = null;
            activity.TotalFloatDays = null;
            activity.IsCritical = false;
        }

        var projectStart = schedule.ProjectStart ?? DateOnly.FromDateTime(DateTime.Today);
        var incomingLinks = schedule.Links
            .GroupBy(link => link.SuccessorId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var outgoingLinks = schedule.Links
            .GroupBy(link => link.PredecessorId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        ForwardPass(schedule, ordered, byId, projectStart, incomingLinks);

        var projectFinish = ordered
            .Where(a => a.EarlyFinish.HasValue)
            .Select(a => a.EarlyFinish!.Value)
            .DefaultIfEmpty(projectStart)
            .Max();
        if (schedule.MustFinishBy.HasValue && schedule.MustFinishBy.Value > projectFinish)
        {
            projectFinish = schedule.MustFinishBy.Value;
        }

        BackwardPass(schedule, ordered, byId, projectFinish, outgoingLinks);

        foreach (var activity in ordered)
        {
            var calendar = schedule.ResolveCalendar(activity.CalendarId);
            if (activity.ConstraintType == ScheduleConstraintType.AsLateAsPossible
                && activity.LateStart.HasValue)
            {
                activity.EarlyStart = activity.LateStart;
                activity.EarlyFinish = activity.LateFinish;
            }

            activity.TotalFloatDays = activity.EarlyStart.HasValue && activity.LateStart.HasValue
                ? CountWorkingDaysSigned(calendar, activity.EarlyStart.Value, activity.LateStart.Value)
                : null;
            activity.IsCritical = activity.TotalFloatDays is <= 0;
        }

        ApplyHammocks(schedule, byId);
        ApplyHeadingRollups(schedule);
        ApplyBaseline(schedule);
    }

    private static void RebuildLinks(ScheduleData schedule, Dictionary<string, ScheduleActivity> byId)
    {
        schedule.Links.Clear();
        foreach (var activity in schedule.Activities)
        {
            if (activity.Kind is ScheduleActivityKind.Heading or ScheduleActivityKind.Hammock)
            {
                continue;
            }

            if (activity.IsUnscheduled)
            {
                continue;
            }

            var parsed = ParsePredecessors(activity.PredecessorText, out var errors);
            if (errors.Count > 0)
            {
                activity.ScheduleNote = errors[0];
            }

            foreach (var link in parsed)
            {
                if (!byId.TryGetValue(link.PredecessorId, out var predecessor))
                {
                    activity.ScheduleNote = $"Unknown predecessor '{link.PredecessorId}'";
                    continue;
                }

                if (ReferenceEquals(predecessor, activity))
                {
                    activity.ScheduleNote = "Activity cannot precede itself";
                    continue;
                }

                if (predecessor.Kind is ScheduleActivityKind.Heading or ScheduleActivityKind.Hammock)
                {
                    activity.ScheduleNote = $"'{link.PredecessorId}' is not a schedulable activity";
                    continue;
                }

                schedule.Links.Add(new ActivityLink
                {
                    PredecessorId = predecessor.Id,
                    SuccessorId = activity.Id,
                    Type = link.Type,
                    LagDays = link.LagDays
                });
            }
        }
    }

    private static List<ScheduleActivity> TopologicalSort(
        List<ScheduleActivity> network,
        List<ActivityLink> links,
        Dictionary<string, ScheduleActivity> byId,
        out List<ScheduleActivity> cyclic)
    {
        var inDegree = network.ToDictionary(a => a, _ => 0);
        var successors = network.ToDictionary(a => a, _ => new List<ScheduleActivity>());
        foreach (var link in links)
        {
            if (byId.TryGetValue(link.PredecessorId, out var predecessor)
                && byId.TryGetValue(link.SuccessorId, out var successor)
                && inDegree.ContainsKey(predecessor)
                && inDegree.ContainsKey(successor))
            {
                inDegree[successor]++;
                successors[predecessor].Add(successor);
            }
        }

        var queue = new Queue<ScheduleActivity>(network.Where(a => inDegree[a] == 0));
        var ordered = new List<ScheduleActivity>(network.Count);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            ordered.Add(current);
            foreach (var successor in successors[current])
            {
                if (--inDegree[successor] == 0)
                {
                    queue.Enqueue(successor);
                }
            }
        }

        cyclic = network.Where(a => !ordered.Contains(a)).ToList();
        return ordered;
    }

    private void ForwardPass(
        ScheduleData schedule,
        List<ScheduleActivity> ordered,
        Dictionary<string, ScheduleActivity> byId,
        DateOnly projectStart,
        IReadOnlyDictionary<string, List<ActivityLink>> incomingLinks)
    {
        foreach (var activity in ordered)
        {
            var calendar = schedule.ResolveCalendar(activity.CalendarId);
            var duration = activity.Kind == ScheduleActivityKind.Milestone ? 0 : activity.DurationDays;
            var earlyStart = RollForward(calendar, projectStart);

            foreach (var link in incomingLinks.GetValueOrDefault(activity.Id) ?? [])
            {
                if (!byId.TryGetValue(link.PredecessorId, out var predecessor)
                    || predecessor.EarlyStart is null
                    || predecessor.EarlyFinish is null)
                {
                    continue;
                }

                DateOnly candidate;
                switch (link.Type)
                {
                    case ActivityLinkType.StartToStart:
                        candidate = ShiftWorkingDays(calendar, RollForward(calendar, predecessor.EarlyStart.Value), link.LagDays);
                        break;
                    case ActivityLinkType.FinishToFinish:
                    {
                        var finishCandidate = ShiftWorkingDays(calendar, RollForward(calendar, predecessor.EarlyFinish.Value), link.LagDays);
                        candidate = StartFromFinish(calendar, finishCandidate, duration);
                        break;
                    }
                    case ActivityLinkType.StartToFinish:
                    {
                        var finishCandidate = ShiftWorkingDays(calendar, RollForward(calendar, predecessor.EarlyStart.Value), link.LagDays);
                        candidate = StartFromFinish(calendar, finishCandidate, duration);
                        break;
                    }
                    default: // Finish to start
                        candidate = ShiftWorkingDays(calendar, NextWorkingDayAfter(calendar, predecessor.EarlyFinish.Value), link.LagDays);
                        break;
                }

                if (candidate > earlyStart)
                {
                    earlyStart = candidate;
                }
            }

            earlyStart = ApplyForwardConstraint(activity, calendar, duration, earlyStart);
            earlyStart = RollForward(calendar, earlyStart);
            activity.EarlyStart = earlyStart;
            activity.EarlyFinish = FinishFromStart(calendar, earlyStart, duration);
        }
    }

    private DateOnly ApplyForwardConstraint(ScheduleActivity activity, ScheduleCalendar calendar, int duration, DateOnly earlyStart)
    {
        if (activity.ConstraintDate is not { } constraintDate)
        {
            return earlyStart;
        }

        switch (activity.ConstraintType)
        {
            case ScheduleConstraintType.StartOnOrAfter:
                return constraintDate > earlyStart ? constraintDate : earlyStart;
            case ScheduleConstraintType.StartOn:
            case ScheduleConstraintType.MandatoryStart:
                return constraintDate;
            case ScheduleConstraintType.FinishOnOrAfter:
            {
                var requiredStart = StartFromFinish(calendar, RollBack(calendar, constraintDate), duration);
                return requiredStart > earlyStart ? requiredStart : earlyStart;
            }
            case ScheduleConstraintType.FinishOn:
            case ScheduleConstraintType.MandatoryFinish:
                return StartFromFinish(calendar, RollBack(calendar, constraintDate), duration);
            default:
                return earlyStart;
        }
    }

    private void BackwardPass(
        ScheduleData schedule,
        List<ScheduleActivity> ordered,
        Dictionary<string, ScheduleActivity> byId,
        DateOnly projectFinish,
        IReadOnlyDictionary<string, List<ActivityLink>> outgoingLinks)
    {
        for (var i = ordered.Count - 1; i >= 0; i--)
        {
            var activity = ordered[i];
            var calendar = schedule.ResolveCalendar(activity.CalendarId);
            var duration = activity.Kind == ScheduleActivityKind.Milestone ? 0 : activity.DurationDays;
            var lateFinish = RollBack(calendar, projectFinish);

            foreach (var link in outgoingLinks.GetValueOrDefault(activity.Id) ?? [])
            {
                if (!byId.TryGetValue(link.SuccessorId, out var successor)
                    || successor.LateStart is null
                    || successor.LateFinish is null)
                {
                    continue;
                }

                DateOnly candidate;
                switch (link.Type)
                {
                    case ActivityLinkType.StartToStart:
                    {
                        var startCandidate = ShiftWorkingDays(calendar, RollBack(calendar, successor.LateStart.Value), -link.LagDays);
                        candidate = FinishFromStart(calendar, startCandidate, duration);
                        break;
                    }
                    case ActivityLinkType.FinishToFinish:
                        candidate = ShiftWorkingDays(calendar, RollBack(calendar, successor.LateFinish.Value), -link.LagDays);
                        break;
                    case ActivityLinkType.StartToFinish:
                    {
                        var startCandidate = ShiftWorkingDays(calendar, RollBack(calendar, successor.LateFinish.Value), -link.LagDays);
                        candidate = FinishFromStart(calendar, startCandidate, duration);
                        break;
                    }
                    default: // Finish to start
                        candidate = ShiftWorkingDays(calendar, PreviousWorkingDayBefore(calendar, successor.LateStart.Value), -link.LagDays);
                        break;
                }

                if (candidate < lateFinish)
                {
                    lateFinish = candidate;
                }
            }

            lateFinish = ApplyBackwardConstraint(activity, calendar, duration, lateFinish);
            lateFinish = RollBack(calendar, lateFinish);
            activity.LateFinish = lateFinish;
            activity.LateStart = StartFromFinish(calendar, lateFinish, duration);
        }
    }

    private DateOnly ApplyBackwardConstraint(ScheduleActivity activity, ScheduleCalendar calendar, int duration, DateOnly lateFinish)
    {
        if (activity.ConstraintDate is not { } constraintDate)
        {
            return lateFinish;
        }

        switch (activity.ConstraintType)
        {
            case ScheduleConstraintType.StartOnOrBefore:
            case ScheduleConstraintType.StartOn:
            case ScheduleConstraintType.MandatoryStart:
            {
                var cappedFinish = FinishFromStart(calendar, RollForward(calendar, constraintDate), duration);
                return cappedFinish < lateFinish ? cappedFinish : lateFinish;
            }
            case ScheduleConstraintType.FinishOnOrBefore:
            case ScheduleConstraintType.FinishOn:
            case ScheduleConstraintType.MandatoryFinish:
                return constraintDate < lateFinish ? constraintDate : lateFinish;
            default:
                return lateFinish;
        }
    }

    private void ApplyHammocks(ScheduleData schedule, Dictionary<string, ScheduleActivity> byId)
    {
        foreach (var hammock in schedule.Activities.Where(a => a.Kind == ScheduleActivityKind.Hammock))
        {
            var calendar = schedule.ResolveCalendar(hammock.CalendarId);
            var members = ParsePredecessors(hammock.HammockMemberText, out _)
                .Select(token => byId.GetValueOrDefault(token.PredecessorId))
                .Where(member => member is not null && member.EarlyStart.HasValue && member.EarlyFinish.HasValue)
                .Cast<ScheduleActivity>()
                .ToList();

            if (members.Count == 0)
            {
                hammock.EarlyStart = null;
                hammock.EarlyFinish = null;
                hammock.LateStart = null;
                hammock.LateFinish = null;
                hammock.TotalFloatDays = null;
                hammock.IsCritical = false;
                hammock.ScheduleNote = "Add member IDs to span";
                continue;
            }

            hammock.EarlyStart = members.Min(m => m.EarlyStart!.Value);
            hammock.EarlyFinish = members.Max(m => m.EarlyFinish!.Value);
            hammock.LateStart = members.Min(m => m.LateStart ?? m.EarlyStart!.Value);
            hammock.LateFinish = members.Max(m => m.LateFinish ?? m.EarlyFinish!.Value);
            hammock.DurationDays = Math.Max(0, CountWorkingDaysSigned(calendar, hammock.EarlyStart.Value, hammock.EarlyFinish.Value));
            hammock.TotalFloatDays = null;
            hammock.IsCritical = members.Any(m => m.IsCritical);
        }
    }

    private static void ApplyHeadingRollups(ScheduleData schedule)
    {
        var activities = schedule.Activities;
        for (var i = 0; i < activities.Count; i++)
        {
            if (activities[i].Kind != ScheduleActivityKind.Heading)
            {
                continue;
            }

            var heading = activities[i];
            DateOnly? start = null;
            DateOnly? finish = null;
            var anyCritical = false;
            for (var j = i + 1; j < activities.Count && activities[j].OutlineLevel > heading.OutlineLevel; j++)
            {
                var child = activities[j];
                if (child.EarlyStart.HasValue && (start is null || child.EarlyStart < start))
                {
                    start = child.EarlyStart;
                }

                if (child.EarlyFinish.HasValue && (finish is null || child.EarlyFinish > finish))
                {
                    finish = child.EarlyFinish;
                }

                anyCritical |= child.IsCritical;
            }

            heading.EarlyStart = start;
            heading.EarlyFinish = finish;
            heading.LateStart = null;
            heading.LateFinish = null;
            heading.TotalFloatDays = null;
            heading.IsCritical = anyCritical;
        }
    }

    private void ApplyBaseline(ScheduleData schedule)
    {
        var baseline = schedule.ActiveBaseline;
        foreach (var activity in schedule.Activities)
        {
            var entry = baseline?.FindEntry(activity.Id);
            activity.BaselineStart = entry?.Start;
            activity.BaselineFinish = entry?.Finish;
            activity.SlipDays = entry?.Finish is { } baselineFinish && activity.EarlyFinish is { } currentFinish
                ? CountWorkingDaysSigned(schedule.ResolveCalendar(activity.CalendarId), baselineFinish, currentFinish)
                : null;
        }
    }

    public static DateOnly RollForward(ScheduleCalendar calendar, DateOnly date)
    {
        if (!calendar.HasAnyWorkingWeekday && calendar.ExtraWorkDays.Count == 0)
        {
            return date;
        }

        for (var i = 0; i < MaxCalendarScanDays && !calendar.IsWorkingDay(date); i++)
        {
            date = date.AddDays(1);
        }

        return date;
    }

    public static DateOnly RollBack(ScheduleCalendar calendar, DateOnly date)
    {
        if (!calendar.HasAnyWorkingWeekday && calendar.ExtraWorkDays.Count == 0)
        {
            return date;
        }

        for (var i = 0; i < MaxCalendarScanDays && !calendar.IsWorkingDay(date); i++)
        {
            date = date.AddDays(-1);
        }

        return date;
    }

    public static DateOnly NextWorkingDayAfter(ScheduleCalendar calendar, DateOnly date)
    {
        return RollForward(calendar, date.AddDays(1));
    }

    public static DateOnly PreviousWorkingDayBefore(ScheduleCalendar calendar, DateOnly date)
    {
        return RollBack(calendar, date.AddDays(-1));
    }

    public static DateOnly ShiftWorkingDays(ScheduleCalendar calendar, DateOnly date, int workingDays)
    {
        if (workingDays > 0)
        {
            for (var i = 0; i < workingDays; i++)
            {
                date = NextWorkingDayAfter(calendar, date);
            }
        }
        else if (workingDays < 0)
        {
            for (var i = 0; i < -workingDays; i++)
            {
                date = PreviousWorkingDayBefore(calendar, date);
            }
        }

        return date;
    }

    public static DateOnly FinishFromStart(ScheduleCalendar calendar, DateOnly start, int durationDays)
    {
        start = RollForward(calendar, start);
        return durationDays <= 0 ? start : ShiftWorkingDays(calendar, start, durationDays - 1);
    }

    public static DateOnly StartFromFinish(ScheduleCalendar calendar, DateOnly finish, int durationDays)
    {
        finish = RollBack(calendar, finish);
        return durationDays <= 0 ? finish : ShiftWorkingDays(calendar, finish, -(durationDays - 1));
    }

    public static int CountWorkingDaysSigned(ScheduleCalendar calendar, DateOnly from, DateOnly to)
    {
        if (from == to)
        {
            return 0;
        }

        var direction = to > from ? 1 : -1;
        var count = 0;
        var cursor = from;
        for (var i = 0; i < MaxCalendarScanDays && cursor != to; i++)
        {
            cursor = cursor.AddDays(direction);
            if (calendar.IsWorkingDay(cursor))
            {
                count += direction;
            }
        }

        return count;
    }
}
