using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Threading;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;

namespace ProjectCostForecast.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private readonly SchedulingService _schedulingService = new();
    private ScheduleActivity? _selectedScheduleActivity;
    private bool _scheduleRecalcQueued;
    private bool _suppressScheduleEvents;
    private ScheduleEditMode _scheduleEditMode;
    private bool _showScheduleBaselineComparison = true;
    private readonly List<string> _scheduleLinkClipboardActivityIds = [];

    public ObservableCollection<ScheduleActivity> ScheduleActivities { get; } = [];
    public ObservableCollection<ScheduleCalendar> ScheduleCalendars { get; } = [];
    public ObservableCollection<string> ScheduleBaselineNames { get; } = [];

    public static IReadOnlyList<ScheduleActivityKind> ScheduleKindOptions { get; } =
        [ScheduleActivityKind.Task, ScheduleActivityKind.Milestone, ScheduleActivityKind.Heading, ScheduleActivityKind.Hammock];

    public static IReadOnlyList<ScheduleConstraintType> ScheduleConstraintOptions { get; } =
    [
        ScheduleConstraintType.None,
        ScheduleConstraintType.StartOn,
        ScheduleConstraintType.StartOnOrAfter,
        ScheduleConstraintType.StartOnOrBefore,
        ScheduleConstraintType.FinishOn,
        ScheduleConstraintType.FinishOnOrAfter,
        ScheduleConstraintType.FinishOnOrBefore,
        ScheduleConstraintType.MandatoryStart,
        ScheduleConstraintType.MandatoryFinish,
        ScheduleConstraintType.AsLateAsPossible
    ];

    public static IReadOnlyList<ScheduleEditMode> ScheduleEditModeOptions { get; } =
        [ScheduleEditMode.CurrentProgramme, ScheduleEditMode.SelectedBaseline];

    public static IReadOnlyList<ActivityLinkType> ScheduleLinkTypeOptions { get; } =
        [ActivityLinkType.FinishToStart, ActivityLinkType.StartToStart, ActivityLinkType.FinishToFinish, ActivityLinkType.StartToFinish];

    public event EventHandler? ScheduleRecalculated;

    public ScheduleData ScheduleDataRef => _dataset.Schedule;

    public ScheduleActivity? SelectedScheduleActivity
    {
        get => _selectedScheduleActivity;
        set
        {
            if (SetProperty(ref _selectedScheduleActivity, value))
            {
                OnPropertyChanged(nameof(SelectedSchedulePredecessorLinks));
                OnPropertyChanged(nameof(SelectedScheduleSuccessorLinks));
                OnPropertyChanged(nameof(SelectedScheduleActivityValidationText));
            }
        }
    }

    public ScheduleEditMode ScheduleEditMode
    {
        get => _scheduleEditMode;
        set
        {
            if (SetProperty(ref _scheduleEditMode, value))
            {
                OnPropertyChanged(nameof(IsEditingScheduleBaseline));
                StatusText = value == ScheduleEditMode.SelectedBaseline
                    ? $"Editing baseline '{ActiveScheduleBaselineName}'. Only baseline dates are editable."
                    : "Editing the current programme.";
            }
        }
    }

    public bool IsEditingScheduleBaseline => ScheduleEditMode == ScheduleEditMode.SelectedBaseline;

    public bool ShowScheduleBaselineComparison
    {
        get => _showScheduleBaselineComparison;
        set
        {
            if (SetProperty(ref _showScheduleBaselineComparison, value))
            {
                ScheduleRecalculated?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string ScheduleLinkClipboardText => ScheduleLinkClipboardActivities.Count == 0
        ? "Link clipboard: empty"
        : $"Link clipboard: {ScheduleLinkClipboardActivities.Count} link source(s)";

    public IReadOnlyList<ScheduleActivity> ScheduleLinkClipboardActivities => _scheduleLinkClipboardActivityIds
        .Select(id => ScheduleActivities.FirstOrDefault(activity => string.Equals(activity.Id, id, StringComparison.OrdinalIgnoreCase)))
        .Where(activity => activity is not null)
        .Cast<ScheduleActivity>()
        .ToList();

    public string ScheduleHealthText
    {
        get
        {
            var schedulable = ScheduleActivities
                .Where(activity => activity.Kind is ScheduleActivityKind.Task or ScheduleActivityKind.Milestone)
                .Where(activity => !activity.IsUnscheduled)
                .ToList();
            var critical = schedulable.Count(activity => activity.IsCritical);
            var complete = schedulable.Count(activity => activity.PercentComplete >= 100);
            var warnings = ScheduleActivities.Count(activity => !string.IsNullOrWhiteSpace(activity.ScheduleNote));
            var openEnds = schedulable.Count(activity =>
                !ScheduleDataRef.Links.Any(link => string.Equals(link.PredecessorId, activity.Id, StringComparison.OrdinalIgnoreCase)));
            return $"Activities {schedulable.Count}  Critical {critical}  Complete {complete}  Open ends {openEnds}  Warnings {warnings}";
        }
    }

    public IReadOnlyList<ActivityLink> SelectedSchedulePredecessorLinks => SelectedScheduleActivity is null
        ? []
        : ScheduleDataRef.Links
            .Where(link => string.Equals(link.SuccessorId, SelectedScheduleActivity.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();

    public IReadOnlyList<ActivityLink> SelectedScheduleSuccessorLinks => SelectedScheduleActivity is null
        ? []
        : ScheduleDataRef.Links
            .Where(link => string.Equals(link.PredecessorId, SelectedScheduleActivity.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();

    public string SelectedScheduleActivityValidationText => SelectedScheduleActivity is null
        ? string.Empty
        : SelectedScheduleActivity.ScheduleNote;

    public DateTime? ScheduleProjectStartDate
    {
        get => _dataset.Schedule.ProjectStart?.ToDateTime(TimeOnly.MinValue);
        set
        {
            var newValue = value is { } date ? DateOnly.FromDateTime(date) : (DateOnly?)null;
            if (_dataset.Schedule.ProjectStart != newValue)
            {
                _dataset.Schedule.ProjectStart = newValue;
                OnPropertyChanged();
                MarkScheduleDirtyAndRecalculate();
            }
        }
    }

    public DateTime? ScheduleMustFinishByDate
    {
        get => _dataset.Schedule.MustFinishBy?.ToDateTime(TimeOnly.MinValue);
        set
        {
            var newValue = value is { } date ? DateOnly.FromDateTime(date) : (DateOnly?)null;
            if (_dataset.Schedule.MustFinishBy != newValue)
            {
                _dataset.Schedule.MustFinishBy = newValue;
                OnPropertyChanged();
                MarkScheduleDirtyAndRecalculate();
            }
        }
    }

    public string ActiveScheduleBaselineName
    {
        get => _dataset.Schedule.ActiveBaselineName;
        set
        {
            if (!string.Equals(_dataset.Schedule.ActiveBaselineName, value, StringComparison.Ordinal))
            {
                _dataset.Schedule.ActiveBaselineName = value ?? string.Empty;
                OnPropertyChanged();
                MarkScheduleDirtyAndRecalculate();
            }
        }
    }

    private void InitializeScheduleFromDataset()
    {
        _suppressScheduleEvents = true;
        try
        {
            _dataset.Schedule ??= new ScheduleData();
            _dataset.Schedule.EnsureDefaultCalendar();
            if (_dataset.Schedule.Activities.Count == 0)
            {
                SeedSampleSchedule(_dataset.Schedule);
            }

            foreach (var activity in ScheduleActivities)
            {
                activity.PropertyChanged -= ScheduleActivity_PropertyChanged;
            }

            ReplaceCollection(ScheduleActivities, _dataset.Schedule.Activities);
            ReplaceCollection(ScheduleCalendars, _dataset.Schedule.Calendars);
            RefreshScheduleBaselineNames();
            foreach (var activity in ScheduleActivities)
            {
                activity.PropertyChanged += ScheduleActivity_PropertyChanged;
            }
        }
        finally
        {
            _suppressScheduleEvents = false;
        }

        RecalculateSchedule();
    }

    private void SyncScheduleToDataset()
    {
        _dataset.Schedule.Activities = ScheduleActivities.ToList();
        _dataset.Schedule.Calendars = ScheduleCalendars.ToList();
    }

    private void RefreshScheduleBaselineNames()
    {
        ReplaceCollection(ScheduleBaselineNames, _dataset.Schedule.Baselines.Select(b => b.Name));
        OnPropertyChanged(nameof(ActiveScheduleBaselineName));
    }

    public void RecalculateSchedule()
    {
        _suppressScheduleEvents = true;
        try
        {
            SyncScheduleToDataset();
            _schedulingService.Recalculate(_dataset.Schedule);
        }
        finally
        {
            _suppressScheduleEvents = false;
        }

        ScheduleRecalculated?.Invoke(this, EventArgs.Empty);
        OnPropertyChanged(nameof(ScheduleHealthText));
        OnPropertyChanged(nameof(SelectedSchedulePredecessorLinks));
        OnPropertyChanged(nameof(SelectedScheduleSuccessorLinks));
        OnPropertyChanged(nameof(SelectedScheduleActivityValidationText));
    }

    private void MarkScheduleDirtyAndRecalculate()
    {
        if (_suppressScheduleEvents)
        {
            return;
        }

        IsDirty = true;
        QueueScheduleRecalculation();
    }

    private void QueueScheduleRecalculation()
    {
        if (_scheduleRecalcQueued)
        {
            return;
        }

        _scheduleRecalcQueued = true;
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _scheduleRecalcQueued = false;
            RecalculateSchedule();
        });
    }

    private void ScheduleActivity_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressScheduleEvents || sender is not ScheduleActivity activity)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(ScheduleActivity.BaselineStart):
            case nameof(ScheduleActivity.BaselineFinish):
                SaveEditedBaselineValue(activity, e.PropertyName == nameof(ScheduleActivity.BaselineStart));
                break;
            case nameof(ScheduleActivity.Id):
            case nameof(ScheduleActivity.Name):
            case nameof(ScheduleActivity.Kind):
            case nameof(ScheduleActivity.OutlineLevel):
            case nameof(ScheduleActivity.DurationDays):
            case nameof(ScheduleActivity.CalendarId):
            case nameof(ScheduleActivity.ConstraintType):
            case nameof(ScheduleActivity.ConstraintDate):
            case nameof(ScheduleActivity.PredecessorText):
            case nameof(ScheduleActivity.HammockMemberText):
            case nameof(ScheduleActivity.PercentComplete):
            case nameof(ScheduleActivity.IsUnscheduled):
                MarkScheduleDirtyAndRecalculate();
                break;
        }
    }

    private void SaveEditedBaselineValue(ScheduleActivity activity, bool isStart)
    {
        var baseline = _dataset.Schedule.ActiveBaseline;
        if (baseline is null)
        {
            baseline = new ScheduleBaseline { Name = "Baseline 1" };
            _dataset.Schedule.Baselines.Add(baseline);
            _dataset.Schedule.ActiveBaselineName = baseline.Name;
            RefreshScheduleBaselineNames();
        }

        var entry = baseline.EnsureEntry(activity.Id);
        var oldValue = isStart ? entry.Start : entry.Finish;
        var newValue = isStart ? activity.BaselineStart : activity.BaselineFinish;
        if (oldValue == newValue)
        {
            return;
        }

        if (isStart)
        {
            entry.Start = newValue;
        }
        else
        {
            entry.Finish = newValue;
        }

        AddAuditEvent(
            "Schedule",
            activity.Id,
            isStart ? "BaselineStart" : "BaselineFinish",
            oldValue?.ToString("yyyy-MM-dd") ?? string.Empty,
            newValue?.ToString("yyyy-MM-dd") ?? string.Empty,
            $"Edited baseline '{baseline.Name}'");
        MarkScheduleDirtyAndRecalculate();
    }

    public ScheduleActivity AddScheduleActivity(ScheduleActivityKind kind)
    {
        var insertIndex = SelectedScheduleActivity is { } selected
            ? ScheduleActivities.IndexOf(selected) + 1
            : ScheduleActivities.Count;
        return AddScheduleActivityAt(kind, insertIndex);
    }

    public ScheduleActivity AddScheduleActivityRelative(ScheduleActivityKind kind, bool above)
    {
        var selectedIndex = SelectedScheduleActivity is { } selected ? ScheduleActivities.IndexOf(selected) : -1;
        var insertIndex = selectedIndex < 0 ? ScheduleActivities.Count : selectedIndex + (above ? 0 : 1);
        return AddScheduleActivityAt(kind, insertIndex);
    }

    public ScheduleActivity AddScheduleActivityAt(
        ScheduleActivityKind kind,
        int insertIndex,
        DateOnly? imposedStart = null,
        int? durationDays = null)
    {
        var activity = new ScheduleActivity
        {
            Id = GenerateScheduleActivityId(),
            Name = imposedStart.HasValue ? kind switch
            {
                ScheduleActivityKind.Heading => "New heading",
                ScheduleActivityKind.Milestone => "New milestone",
                ScheduleActivityKind.Hammock => "New hammock",
                _ => "New activity"
            } : string.Empty,
            Kind = kind,
            DurationDays = kind == ScheduleActivityKind.Milestone ? 0 : Math.Max(1, durationDays ?? 5),
            OutlineLevel = SelectedScheduleActivity?.OutlineLevel ?? 0,
            CalendarId = _dataset.Schedule.EnsureDefaultCalendar().Id,
            ConstraintType = imposedStart.HasValue ? ScheduleConstraintType.StartOn : ScheduleConstraintType.None,
            ConstraintDate = imposedStart,
            IsUnscheduled = !imposedStart.HasValue && kind == ScheduleActivityKind.Task
        };

        activity.PropertyChanged += ScheduleActivity_PropertyChanged;
        ScheduleActivities.Insert(Math.Clamp(insertIndex, 0, ScheduleActivities.Count), activity);
        SelectedScheduleActivity = activity;
        AddAuditEvent("Schedule", activity.Id, "Created", string.Empty, kind.ToString(), "Added schedule activity");
        MarkScheduleDirtyAndRecalculate();
        return activity;
    }

    private void EnsureScheduleActivityIsScheduled(ScheduleActivity activity)
    {
        if (activity.Kind is ScheduleActivityKind.Task or ScheduleActivityKind.Milestone)
        {
            activity.IsUnscheduled = false;
        }
    }

    public void ScheduleActivityFromBarDrag(ScheduleActivity activity, DateOnly startDate, int durationDays)
    {
        activity.ConstraintType = ScheduleConstraintType.StartOnOrAfter;
        activity.ConstraintDate = startDate;
        activity.DurationDays = Math.Max(1, durationDays);
        activity.IsUnscheduled = false;
        SelectedScheduleActivity = activity;
        StatusText = $"{activity.Id} scheduled from {startDate:d MMM yyyy}.";
    }

    public void ShiftScheduleActivity(ScheduleActivity activity, int deltaDays)
    {
        if (deltaDays == 0 || activity.EarlyStart is not { } earlyStart)
        {
            return;
        }

        var calendar = ScheduleDataRef.ResolveCalendar(activity.CalendarId);
        var droppedStart = SchedulingService.RollForward(calendar, earlyStart.AddDays(deltaDays));
        activity.ConstraintDate = droppedStart;
        activity.ConstraintType = ScheduleConstraintType.StartOnOrAfter;
        activity.IsUnscheduled = false;
    }

    public void ResizeScheduleActivity(ScheduleActivity activity, int deltaDays)
    {
        if (deltaDays == 0 || activity.EarlyStart is not { } start || activity.EarlyFinish is not { } finish)
        {
            return;
        }

        var calendar = ScheduleDataRef.ResolveCalendar(activity.CalendarId);
        var droppedFinish = finish.AddDays(deltaDays);
        activity.DurationDays = droppedFinish <= start
            ? 1
            : SchedulingService.CountWorkingDaysSigned(calendar, start, droppedFinish) + 1;
        activity.IsUnscheduled = false;
    }

    public void ConvertSelectedScheduleActivityToMilestone()
    {
        if (SelectedScheduleActivity is not { } activity)
        {
            return;
        }

        activity.Kind = ScheduleActivityKind.Milestone;
        activity.DurationDays = 0;
        StatusText = $"Converted {activity.Id} to a milestone.";
    }

    public void SetSelectedScheduleProgress(double percent)
    {
        if (SelectedScheduleActivity is { Kind: ScheduleActivityKind.Task or ScheduleActivityKind.Milestone } activity)
        {
            activity.PercentComplete = percent;
            StatusText = $"{activity.Id} progress set to {activity.PercentComplete:0}%.";
        }
    }

    public void DeleteSelectedScheduleActivity()
    {
        if (SelectedScheduleActivity is not { } activity)
        {
            return;
        }

        DeleteScheduleActivities([activity]);
    }

    public void DeleteScheduleActivities(IReadOnlyList<ScheduleActivity> activities)
    {
        var targets = activities
            .Where(activity => ScheduleActivities.Contains(activity))
            .Distinct()
            .OrderByDescending(activity => ScheduleActivities.IndexOf(activity))
            .ToList();
        if (targets.Count == 0)
        {
            return;
        }

        var nextIndex = targets.Min(activity => ScheduleActivities.IndexOf(activity));
        foreach (var activity in targets)
        {
            activity.PropertyChanged -= ScheduleActivity_PropertyChanged;
            ScheduleActivities.Remove(activity);
            AddAuditEvent("Schedule", activity.Id, "Deleted", activity.Name, string.Empty, "Deleted schedule activity");
        }

        SelectedScheduleActivity = ScheduleActivities.Count > 0
            ? ScheduleActivities[Math.Min(nextIndex, ScheduleActivities.Count - 1)]
            : null;
        StatusText = targets.Count == 1
            ? $"Deleted {targets[0].Id}."
            : $"Deleted {targets.Count} schedule activities.";
        MarkScheduleDirtyAndRecalculate();
    }

    public void IndentSelectedScheduleActivity(int delta)
    {
        if (SelectedScheduleActivity is { } activity)
        {
            activity.OutlineLevel += delta;
        }
    }

    public void IndentScheduleActivities(IReadOnlyList<ScheduleActivity> activities, int delta)
    {
        foreach (var activity in activities.Where(ScheduleActivities.Contains).Distinct())
        {
            activity.OutlineLevel += delta;
        }
    }

    public void MoveSelectedScheduleActivity(int delta)
    {
        if (SelectedScheduleActivity is not { } activity)
        {
            return;
        }

        var index = ScheduleActivities.IndexOf(activity);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= ScheduleActivities.Count)
        {
            return;
        }

        ScheduleActivities.Move(index, target);
        MarkScheduleDirtyAndRecalculate();
    }

    public void MoveScheduleActivity(ScheduleActivity activity, int targetIndex)
    {
        var sourceIndex = ScheduleActivities.IndexOf(activity);
        targetIndex = Math.Clamp(targetIndex, 0, ScheduleActivities.Count - 1);
        if (sourceIndex < 0 || sourceIndex == targetIndex)
        {
            return;
        }

        ScheduleActivities.Move(sourceIndex, targetIndex);
        SelectedScheduleActivity = activity;
        StatusText = $"Moved {activity.Id} to row {targetIndex + 1}.";
        MarkScheduleDirtyAndRecalculate();
    }

    public void LinkScheduleActivities(IReadOnlyList<ScheduleActivity> selection)
    {
        if (selection.Count < 2)
        {
            StatusText = "Select two or more activities to link them in order.";
            return;
        }

        for (var i = 1; i < selection.Count; i++)
        {
            TryCreateScheduleLink(selection[i - 1], selection[i]);
        }

        StatusText = "Linked selected activities finish-to-start.";
    }

    public bool TryCreateScheduleLink(ScheduleActivity predecessor, ScheduleActivity successor)
        => TryCreateScheduleLink(predecessor, successor, ActivityLinkType.FinishToStart, 0);

    public bool TryCreateScheduleLink(
        ScheduleActivity predecessor,
        ScheduleActivity successor,
        ActivityLinkType linkType,
        int lagDays)
    {
        if (ReferenceEquals(predecessor, successor))
        {
            return false;
        }

        if (predecessor.Kind is ScheduleActivityKind.Heading or ScheduleActivityKind.Hammock
            || successor.Kind is ScheduleActivityKind.Heading or ScheduleActivityKind.Hammock)
        {
            StatusText = "Headings and hammocks cannot be linked.";
            return false;
        }

        if (WouldCreateScheduleCycle(predecessor.Id, successor.Id))
        {
            StatusText = $"Cannot link {predecessor.Id} to {successor.Id}; that relationship would create a circular path.";
            return false;
        }

        var existing = SchedulingService.ParsePredecessors(successor.PredecessorText, out _);
        if (existing.Any(link => string.Equals(link.PredecessorId, predecessor.Id, StringComparison.OrdinalIgnoreCase)))
        {
            StatusText = $"{successor.Id} is already linked to {predecessor.Id}.";
            return false;
        }

        var formattedLink = SchedulingService.FormatPredecessor(new ParsedPredecessor
        {
            PredecessorId = predecessor.Id,
            Type = linkType,
            LagDays = lagDays
        });
        successor.PredecessorText = string.IsNullOrWhiteSpace(successor.PredecessorText)
            ? formattedLink
            : $"{successor.PredecessorText}, {formattedLink}";
        EnsureScheduleActivityIsScheduled(successor);
        StatusText = $"Linked {predecessor.Id} → {successor.Id} (FS).";
        StatusText = $"Linked {predecessor.Id} to {successor.Id} ({new ActivityLink { Type = linkType }.TypeLabel}).";
        return true;
    }

    private bool WouldCreateScheduleCycle(string predecessorId, string successorId)
    {
        var outgoing = ScheduleDataRef.Links
            .GroupBy(link => link.PredecessorId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(link => link.SuccessorId).ToList(),
                StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        pending.Push(successorId);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            if (string.Equals(current, predecessorId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (outgoing.TryGetValue(current, out var successors))
            {
                foreach (var successor in successors)
                {
                    pending.Push(successor);
                }
            }
        }

        return false;
    }

    public void CopyScheduleLinkSource(ScheduleActivity activity)
    {
        _scheduleLinkClipboardActivityIds.Add(activity.Id);

        OnPropertyChanged(nameof(ScheduleLinkClipboardText));
        OnPropertyChanged(nameof(ScheduleLinkClipboardActivities));
        StatusText = $"{activity.Id} added to the link clipboard.";
    }

    public bool PasteScheduleLinkTo(ScheduleActivity successor, ActivityLinkType linkType = ActivityLinkType.FinishToStart)
    {
        for (var index = 0; index < _scheduleLinkClipboardActivityIds.Count; index++)
        {
            var predecessorId = _scheduleLinkClipboardActivityIds[index];
            var predecessor = ScheduleActivities.FirstOrDefault(activity =>
                string.Equals(activity.Id, predecessorId, StringComparison.OrdinalIgnoreCase));
            if (predecessor is null)
            {
                _scheduleLinkClipboardActivityIds.RemoveAt(index);
                index--;
                continue;
            }

            if (TryCreateScheduleLink(predecessor, successor, linkType, 0))
            {
                _scheduleLinkClipboardActivityIds.RemoveAt(index);
                OnPropertyChanged(nameof(ScheduleLinkClipboardText));
                OnPropertyChanged(nameof(ScheduleLinkClipboardActivities));
                return true;
            }
        }

        OnPropertyChanged(nameof(ScheduleLinkClipboardText));
        OnPropertyChanged(nameof(ScheduleLinkClipboardActivities));
        return false;
    }

    public void ClearScheduleLinkClipboard()
    {
        _scheduleLinkClipboardActivityIds.Clear();
        OnPropertyChanged(nameof(ScheduleLinkClipboardText));
        OnPropertyChanged(nameof(ScheduleLinkClipboardActivities));
    }

    public void BreakScheduleLink(ScheduleActivity successor, string predecessorId)
    {
        var links = SchedulingService.ParsePredecessors(successor.PredecessorText, out _);
        var remaining = links
            .Where(link => !string.Equals(link.PredecessorId, predecessorId, StringComparison.OrdinalIgnoreCase))
            .Select(SchedulingService.FormatPredecessor)
            .ToList();
        if (remaining.Count == links.Count)
        {
            return;
        }

        successor.PredecessorText = string.Join(", ", remaining);
        StatusText = $"Removed link {predecessorId} to {successor.Id}.";
    }

    public void UpdateScheduleLink(
        ScheduleActivity successor,
        string predecessorId,
        ActivityLinkType linkType,
        int lagDays)
    {
        var links = SchedulingService.ParsePredecessors(successor.PredecessorText, out _);
        var link = links.FirstOrDefault(item =>
            string.Equals(item.PredecessorId, predecessorId, StringComparison.OrdinalIgnoreCase));
        if (link is null)
        {
            return;
        }

        link.Type = linkType;
        link.LagDays = lagDays;
        successor.PredecessorText = string.Join(", ", links.Select(SchedulingService.FormatPredecessor));
        EnsureScheduleActivityIsScheduled(successor);
        StatusText = $"Updated {predecessorId} to {successor.Id}: {new ActivityLink { Type = linkType }.TypeLabel} {lagDays:+0;-0;0}d.";
    }

    public void BreakAllScheduleLinks(ScheduleActivity activity)
    {
        activity.PredecessorText = string.Empty;
        foreach (var successor in ScheduleActivities)
        {
            BreakScheduleLink(successor, activity.Id);
        }

        StatusText = $"Removed all links connected to {activity.Id}.";
    }

    public void ImposeSelectedScheduleDate(ScheduleConstraintType constraintType, DateOnly date)
    {
        if (SelectedScheduleActivity is not { } activity)
        {
            return;
        }

        activity.ConstraintType = constraintType;
        activity.ConstraintDate = date;
        StatusText = $"Applied {constraintType} on {date:d MMM yyyy} to {activity.Id}.";
    }

    public void CaptureScheduleBaseline(string? name = null)
    {
        var baselineName = string.IsNullOrWhiteSpace(name)
            ? $"Baseline {_dataset.Schedule.Baselines.Count + 1}"
            : name.Trim();
        var baseline = _dataset.Schedule.Baselines
            .FirstOrDefault(b => string.Equals(b.Name, baselineName, StringComparison.OrdinalIgnoreCase));
        if (baseline is null)
        {
            baseline = new ScheduleBaseline { Name = baselineName };
            _dataset.Schedule.Baselines.Add(baseline);
        }

        baseline.CapturedAt = DateTime.Now;
        baseline.Entries.Clear();
        foreach (var activity in ScheduleActivities)
        {
            if (activity.EarlyStart.HasValue || activity.EarlyFinish.HasValue)
            {
                baseline.Entries.Add(new ScheduleBaselineEntry
                {
                    ActivityId = activity.Id,
                    Start = activity.EarlyStart,
                    Finish = activity.EarlyFinish
                });
            }
        }

        _dataset.Schedule.ActiveBaselineName = baseline.Name;
        RefreshScheduleBaselineNames();
        AddAuditEvent("Schedule", baseline.Name, "BaselineCaptured", string.Empty, $"{baseline.Entries.Count} activities", "Captured schedule baseline");
        StatusText = $"Captured baseline '{baseline.Name}' across {baseline.Entries.Count} activities.";
        MarkScheduleDirtyAndRecalculate();
    }

    public void DeleteActiveScheduleBaseline()
    {
        var baseline = _dataset.Schedule.ActiveBaseline;
        if (baseline is null)
        {
            return;
        }

        _dataset.Schedule.Baselines.Remove(baseline);
        _dataset.Schedule.ActiveBaselineName = _dataset.Schedule.Baselines.FirstOrDefault()?.Name ?? string.Empty;
        RefreshScheduleBaselineNames();
        AddAuditEvent("Schedule", baseline.Name, "BaselineDeleted", baseline.Name, string.Empty, "Deleted schedule baseline");
        MarkScheduleDirtyAndRecalculate();
    }

    public ScheduleCalendar AddScheduleCalendar(string name)
    {
        var colors = new[] { "#3B82F6", "#10B981", "#F59E0B", "#8B5CF6", "#EC4899", "#0EA5E9" };
        var calendar = new ScheduleCalendar
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"Calendar {ScheduleCalendars.Count + 1}" : name.Trim(),
            ColorHex = colors[ScheduleCalendars.Count % colors.Length]
        };
        ScheduleCalendars.Add(calendar);
        MarkScheduleDirtyAndRecalculate();
        return calendar;
    }

    public void RemoveScheduleCalendar(ScheduleCalendar calendar)
    {
        if (ScheduleCalendars.Count <= 1)
        {
            StatusText = "At least one calendar is required.";
            return;
        }

        ScheduleCalendars.Remove(calendar);
        var fallback = ScheduleCalendars[0];
        if (string.Equals(_dataset.Schedule.DefaultCalendarId, calendar.Id, StringComparison.OrdinalIgnoreCase))
        {
            _dataset.Schedule.DefaultCalendarId = fallback.Id;
        }

        foreach (var activity in ScheduleActivities)
        {
            if (string.Equals(activity.CalendarId, calendar.Id, StringComparison.OrdinalIgnoreCase))
            {
                activity.CalendarId = fallback.Id;
            }
        }

        MarkScheduleDirtyAndRecalculate();
    }

    public void NotifyScheduleCalendarsChanged()
    {
        MarkScheduleDirtyAndRecalculate();
    }

    private void SeedSampleSchedule(ScheduleData schedule)
    {
        var fiveDay = schedule.EnsureDefaultCalendar();
        fiveDay.Name = "Standard 5 Day";
        var sevenDay = new ScheduleCalendar { Name = "7 Day Site" , WorkingDays = [true, true, true, true, true, true, true] };
        schedule.Calendars.Add(sevenDay);

        var projectStart = DateOnly.FromDateTime(DateTime.Today.AddDays(-35));
        schedule.ProjectStart = projectStart;
        schedule.Activities =
        [
            new ScheduleActivity { Id = "A1000", Name = "Design", Kind = ScheduleActivityKind.Heading, OutlineLevel = 0 },
            new ScheduleActivity { Id = "A1010", Name = "Concept design", Kind = ScheduleActivityKind.Task, DurationDays = 10, OutlineLevel = 1, CalendarId = fiveDay.Id },
            new ScheduleActivity { Id = "A1020", Name = "Detailed design", Kind = ScheduleActivityKind.Task, DurationDays = 15, OutlineLevel = 1, CalendarId = fiveDay.Id, PredecessorText = "A1010" },
            new ScheduleActivity { Id = "A1030", Name = "Design approved", Kind = ScheduleActivityKind.Milestone, OutlineLevel = 1, CalendarId = fiveDay.Id, PredecessorText = "A1020" },
            new ScheduleActivity { Id = "A2000", Name = "Procurement", Kind = ScheduleActivityKind.Heading, OutlineLevel = 0 },
            new ScheduleActivity { Id = "A2010", Name = "Tender period", Kind = ScheduleActivityKind.Task, DurationDays = 10, OutlineLevel = 1, CalendarId = fiveDay.Id, PredecessorText = "A1030" },
            new ScheduleActivity { Id = "A2020", Name = "Evaluate and award", Kind = ScheduleActivityKind.Task, DurationDays = 5, OutlineLevel = 1, CalendarId = fiveDay.Id, PredecessorText = "A2010" },
            new ScheduleActivity { Id = "A2030", Name = "Contract awarded", Kind = ScheduleActivityKind.Milestone, OutlineLevel = 1, CalendarId = fiveDay.Id, PredecessorText = "A2020" },
            new ScheduleActivity { Id = "A3000", Name = "Construction", Kind = ScheduleActivityKind.Heading, OutlineLevel = 0 },
            new ScheduleActivity { Id = "A3010", Name = "Site establishment", Kind = ScheduleActivityKind.Task, DurationDays = 5, OutlineLevel = 1, CalendarId = fiveDay.Id, PredecessorText = "A2030" },
            new ScheduleActivity { Id = "A3020", Name = "Earthworks", Kind = ScheduleActivityKind.Task, DurationDays = 12, OutlineLevel = 1, CalendarId = sevenDay.Id, PredecessorText = "A3010" },
            new ScheduleActivity { Id = "A3030", Name = "Foundations", Kind = ScheduleActivityKind.Task, DurationDays = 15, OutlineLevel = 1, CalendarId = fiveDay.Id, PredecessorText = "A3020 SS+5" },
            new ScheduleActivity { Id = "A3040", Name = "Services rough-in", Kind = ScheduleActivityKind.Task, DurationDays = 10, OutlineLevel = 1, CalendarId = fiveDay.Id, PredecessorText = "A3030 FS+2" },
            new ScheduleActivity { Id = "A3050", Name = "Practical completion", Kind = ScheduleActivityKind.Milestone, OutlineLevel = 1, CalendarId = fiveDay.Id, PredecessorText = "A3040" },
            new ScheduleActivity { Id = "A9000", Name = "Site overheads (hammock)", Kind = ScheduleActivityKind.Hammock, OutlineLevel = 0, CalendarId = fiveDay.Id, HammockMemberText = "A3010, A3050" }
        ];

        // Capture the original plan as a baseline, then introduce a delay so slip tracking has something to show.
        _schedulingService.Recalculate(schedule);
        var baseline = new ScheduleBaseline { Name = "Original plan" };
        foreach (var activity in schedule.Activities)
        {
            if (activity.EarlyStart.HasValue || activity.EarlyFinish.HasValue)
            {
                baseline.Entries.Add(new ScheduleBaselineEntry
                {
                    ActivityId = activity.Id,
                    Start = activity.EarlyStart,
                    Finish = activity.EarlyFinish
                });
            }
        }

        schedule.Baselines.Add(baseline);
        schedule.ActiveBaselineName = baseline.Name;

        var delayed = schedule.Activities.First(a => a.Id == "A3020");
        delayed.DurationDays += 4;
        var progressed = schedule.Activities.First(a => a.Id == "A1010");
        progressed.PercentComplete = 100;
        schedule.Activities.First(a => a.Id == "A1020").PercentComplete = 60;
        schedule.Activities.Add(new ScheduleActivity { Id = "A9100", Name = string.Empty, Kind = ScheduleActivityKind.Task, DurationDays = 5, OutlineLevel = 0, CalendarId = fiveDay.Id, IsUnscheduled = true });
        schedule.Activities.Add(new ScheduleActivity { Id = "A9110", Name = string.Empty, Kind = ScheduleActivityKind.Task, DurationDays = 5, OutlineLevel = 0, CalendarId = fiveDay.Id, IsUnscheduled = true });
        schedule.Activities.Add(new ScheduleActivity { Id = "A9120", Name = string.Empty, Kind = ScheduleActivityKind.Task, DurationDays = 5, OutlineLevel = 0, CalendarId = fiveDay.Id, IsUnscheduled = true });
    }

    private string GenerateScheduleActivityId()
    {
        var next = 10;
        var existing = new HashSet<string>(ScheduleActivities.Select(a => a.Id), StringComparer.OrdinalIgnoreCase);
        while (existing.Contains($"A{next:0000}"))
        {
            next += 10;
        }

        return $"A{next:0000}";
    }
}
