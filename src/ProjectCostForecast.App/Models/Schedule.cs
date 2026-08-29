using System.Text.Json.Serialization;

namespace ProjectCostForecast.App.Models;

public enum ScheduleActivityKind
{
    Task,
    Milestone,
    Heading,
    Hammock
}

public enum ScheduleConstraintType
{
    None,
    StartOn,
    StartOnOrAfter,
    StartOnOrBefore,
    FinishOn,
    FinishOnOrAfter,
    FinishOnOrBefore,
    MandatoryStart,
    MandatoryFinish,
    AsLateAsPossible
}

public enum ActivityLinkType
{
    FinishToStart,
    StartToStart,
    FinishToFinish,
    StartToFinish
}

public enum ScheduleEditMode
{
    CurrentProgramme,
    SelectedBaseline
}

public sealed class ScheduleCalendar : ObservableModel
{
    private string _id = Guid.NewGuid().ToString("N")[..8];
    private string _name = "Standard 5 Day";
    private bool[] _workingDays = [false, true, true, true, true, true, false];
    private List<DateOnly> _holidays = [];
    private List<DateOnly> _extraWorkDays = [];
    private string _colorHex = "#94A3B8";
    private bool _isVisibleOnGantt = true;

    public string Id { get => _id; set => SetProperty(ref _id, value ?? string.Empty); }
    public string Name { get => _name; set => SetProperty(ref _name, value ?? string.Empty); }
    public bool[] WorkingDays { get => _workingDays; set => SetProperty(ref _workingDays, value ?? [false, true, true, true, true, true, false]); } // Sunday..Saturday
    public List<DateOnly> Holidays { get => _holidays; set => SetProperty(ref _holidays, value ?? []); }
    public List<DateOnly> ExtraWorkDays { get => _extraWorkDays; set => SetProperty(ref _extraWorkDays, value ?? []); }
    public string ColorHex { get => _colorHex; set => SetProperty(ref _colorHex, value ?? string.Empty); }
    public bool IsVisibleOnGantt { get => _isVisibleOnGantt; set => SetProperty(ref _isVisibleOnGantt, value); }

    public bool IsWorkingDay(DateOnly date)
    {
        if (ExtraWorkDays.Contains(date))
        {
            return true;
        }

        if (Holidays.Contains(date))
        {
            return false;
        }

        var index = (int)date.DayOfWeek;
        return index < WorkingDays.Length && WorkingDays[index];
    }

    public bool HasAnyWorkingWeekday => WorkingDays.Any(day => day);
}

public sealed class ActivityLink
{
    public string PredecessorId { get; set; } = string.Empty;
    public string SuccessorId { get; set; } = string.Empty;
    public ActivityLinkType Type { get; set; } = ActivityLinkType.FinishToStart;
    public int LagDays { get; set; }

    public string TypeLabel => Type switch
    {
        ActivityLinkType.StartToStart => "SS",
        ActivityLinkType.FinishToFinish => "FF",
        ActivityLinkType.StartToFinish => "SF",
        _ => "FS"
    };

    public static bool TryParseTypeLabel(string? label, out ActivityLinkType type)
    {
        type = ActivityLinkType.FinishToStart;
        switch ((label ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "FS":
            case "":
                type = ActivityLinkType.FinishToStart;
                return true;
            case "SS":
                type = ActivityLinkType.StartToStart;
                return true;
            case "FF":
                type = ActivityLinkType.FinishToFinish;
                return true;
            case "SF":
                type = ActivityLinkType.StartToFinish;
                return true;
            default:
                return false;
        }
    }
}

public sealed class ScheduleBaselineEntry : ObservableModel
{
    private string _activityId = string.Empty;
    private DateOnly? _start;
    private DateOnly? _finish;

    public string ActivityId { get => _activityId; set => SetProperty(ref _activityId, value ?? string.Empty); }
    public DateOnly? Start { get => _start; set => SetProperty(ref _start, value); }
    public DateOnly? Finish { get => _finish; set => SetProperty(ref _finish, value); }
}

public sealed class ScheduleBaseline : ObservableModel
{
    private string _name = "Baseline";
    private DateTimeOffset _capturedAt = DateTimeOffset.UnixEpoch;
    private BatchObservableCollection<ScheduleBaselineEntry> _entries = [];

    public string Name { get => _name; set => SetProperty(ref _name, value ?? string.Empty); }
    public DateTimeOffset CapturedAt { get => _capturedAt; set => SetProperty(ref _capturedAt, value); }
    public BatchObservableCollection<ScheduleBaselineEntry> Entries
    {
        get => _entries;
        set => SetProperty(ref _entries, value ?? []);
    }

    public ScheduleBaselineEntry? FindEntry(string activityId)
    {
        return Entries.FirstOrDefault(entry =>
            string.Equals(entry.ActivityId, activityId, StringComparison.OrdinalIgnoreCase));
    }

    public ScheduleBaselineEntry EnsureEntry(string activityId)
    {
        var entry = FindEntry(activityId);
        if (entry is null)
        {
            entry = new ScheduleBaselineEntry { ActivityId = activityId };
            Entries.Add(entry);
        }

        return entry;
    }
}

public sealed class ScheduleData : ObservableModel
{
    private DateOnly? _projectStart;
    private DateOnly? _mustFinishBy;
    private string _defaultCalendarId = string.Empty;
    private string _activeBaselineName = string.Empty;
    private BatchObservableCollection<ScheduleCalendar> _calendars = [];
    private BatchObservableCollection<ScheduleActivity> _activities = [];
    private BatchObservableCollection<ActivityLink> _links = [];
    private BatchObservableCollection<ScheduleBaseline> _baselines = [];

    public DateOnly? ProjectStart { get => _projectStart; set => SetProperty(ref _projectStart, value); }
    public DateOnly? MustFinishBy { get => _mustFinishBy; set => SetProperty(ref _mustFinishBy, value); }
    public string DefaultCalendarId { get => _defaultCalendarId; set => SetProperty(ref _defaultCalendarId, value ?? string.Empty); }
    public string ActiveBaselineName { get => _activeBaselineName; set => SetProperty(ref _activeBaselineName, value ?? string.Empty); }
    public BatchObservableCollection<ScheduleCalendar> Calendars { get => _calendars; set => SetProperty(ref _calendars, value ?? []); }
    public BatchObservableCollection<ScheduleActivity> Activities { get => _activities; set => SetProperty(ref _activities, value ?? []); }
    public BatchObservableCollection<ActivityLink> Links { get => _links; set => SetProperty(ref _links, value ?? []); }
    public BatchObservableCollection<ScheduleBaseline> Baselines { get => _baselines; set => SetProperty(ref _baselines, value ?? []); }

    public ScheduleCalendar EnsureDefaultCalendar()
    {
        if (Calendars.Count == 0)
        {
            Calendars.Add(new ScheduleCalendar());
        }

        var calendar = Calendars.FirstOrDefault(c => string.Equals(c.Id, DefaultCalendarId, StringComparison.OrdinalIgnoreCase))
            ?? Calendars[0];
        DefaultCalendarId = calendar.Id;
        return calendar;
    }

    public ScheduleCalendar ResolveCalendar(string? calendarId)
    {
        var calendar = Calendars.FirstOrDefault(c => string.Equals(c.Id, calendarId, StringComparison.OrdinalIgnoreCase));
        return calendar ?? EnsureDefaultCalendar();
    }

    public ScheduleBaseline? ActiveBaseline =>
        Baselines.FirstOrDefault(b => string.Equals(b.Name, ActiveBaselineName, StringComparison.OrdinalIgnoreCase));
}

public sealed class ScheduleActivity : ObservableModel
{
    private string _id = string.Empty;
    private string _name = string.Empty;
    private ScheduleActivityKind _kind = ScheduleActivityKind.Task;
    private int _outlineLevel;
    private int _durationDays = 5;
    private string _calendarId = string.Empty;
    private ScheduleConstraintType _constraintType = ScheduleConstraintType.None;
    private DateOnly? _constraintDate;
    private string _predecessorText = string.Empty;
    private string _hammockMemberText = string.Empty;
    private double _percentComplete;
    private string _notes = string.Empty;
    private bool _isUnscheduled;

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public ScheduleActivityKind Kind
    {
        get => _kind;
        set
        {
            if (SetProperty(ref _kind, value))
            {
                OnPropertyChanged(nameof(IsHeading));
                OnPropertyChanged(nameof(IsMilestone));
                OnPropertyChanged(nameof(IsHammock));
            }
        }
    }

    public int OutlineLevel
    {
        get => _outlineLevel;
        set => SetProperty(ref _outlineLevel, Math.Max(0, value));
    }

    public int DurationDays
    {
        get => _durationDays;
        set => SetProperty(ref _durationDays, Math.Max(0, value));
    }

    public string CalendarId
    {
        get => _calendarId;
        set => SetProperty(ref _calendarId, value ?? string.Empty);
    }

    public ScheduleConstraintType ConstraintType
    {
        get => _constraintType;
        set => SetProperty(ref _constraintType, value);
    }

    public DateOnly? ConstraintDate
    {
        get => _constraintDate;
        set => SetProperty(ref _constraintDate, value);
    }

    public string PredecessorText
    {
        get => _predecessorText;
        set => SetProperty(ref _predecessorText, value ?? string.Empty);
    }

    public string HammockMemberText
    {
        get => _hammockMemberText;
        set => SetProperty(ref _hammockMemberText, value ?? string.Empty);
    }

    public double PercentComplete
    {
        get => _percentComplete;
        set => SetProperty(ref _percentComplete, Math.Clamp(value, 0, 100));
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value ?? string.Empty);
    }

    public bool IsUnscheduled
    {
        get => _isUnscheduled;
        set => SetProperty(ref _isUnscheduled, value);
    }

    [JsonIgnore] public bool IsHeading => Kind == ScheduleActivityKind.Heading;
    [JsonIgnore] public bool IsMilestone => Kind == ScheduleActivityKind.Milestone;
    [JsonIgnore] public bool IsHammock => Kind == ScheduleActivityKind.Hammock;

    private DateOnly? _earlyStart;
    private DateOnly? _earlyFinish;
    private DateOnly? _lateStart;
    private DateOnly? _lateFinish;
    private int? _totalFloatDays;
    private bool _isCritical;
    private DateOnly? _baselineStart;
    private DateOnly? _baselineFinish;
    private int? _slipDays;
    private string _scheduleNote = string.Empty;

    [JsonIgnore]
    public DateOnly? EarlyStart
    {
        get => _earlyStart;
        set => SetProperty(ref _earlyStart, value);
    }

    [JsonIgnore]
    public DateOnly? EarlyFinish
    {
        get => _earlyFinish;
        set => SetProperty(ref _earlyFinish, value);
    }

    [JsonIgnore]
    public DateOnly? LateStart
    {
        get => _lateStart;
        set => SetProperty(ref _lateStart, value);
    }

    [JsonIgnore]
    public DateOnly? LateFinish
    {
        get => _lateFinish;
        set => SetProperty(ref _lateFinish, value);
    }

    [JsonIgnore]
    public int? TotalFloatDays
    {
        get => _totalFloatDays;
        set => SetProperty(ref _totalFloatDays, value);
    }

    [JsonIgnore]
    public bool IsCritical
    {
        get => _isCritical;
        set => SetProperty(ref _isCritical, value);
    }

    [JsonIgnore]
    public DateOnly? BaselineStart
    {
        get => _baselineStart;
        set => SetProperty(ref _baselineStart, value);
    }

    [JsonIgnore]
    public DateOnly? BaselineFinish
    {
        get => _baselineFinish;
        set => SetProperty(ref _baselineFinish, value);
    }

    [JsonIgnore]
    public int? SlipDays
    {
        get => _slipDays;
        set => SetProperty(ref _slipDays, value);
    }

    [JsonIgnore]
    public string ScheduleNote
    {
        get => _scheduleNote;
        set => SetProperty(ref _scheduleNote, value ?? string.Empty);
    }
}

public sealed class ParsedPredecessor
{
    public string PredecessorId { get; set; } = string.Empty;
    public ActivityLinkType Type { get; set; } = ActivityLinkType.FinishToStart;
    public int LagDays { get; set; }
}
