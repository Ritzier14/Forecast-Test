using System.Collections.ObjectModel;
using System.Diagnostics;

namespace ProjectCostForecast.App.Services;

[Flags]
public enum RefreshProjection
{
    None = 0,
    ForecastLinesView = 1 << 0,
    RawTransactionsView = 1 << 1,
    ResourceSummariesView = 1 << 2,
    RawTransactionsPivot = 1 << 3,
    Totals = 1 << 4,
    Ledger = 1 << 5,
    ForecastGrouping = 1 << 6,
    CalculatedViews = 1 << 7,
    DataViews = ForecastLinesView | RawTransactionsView | ResourceSummariesView | RawTransactionsPivot,
    All = DataViews | Totals | Ledger | ForecastGrouping | CalculatedViews
}

public enum RefreshPhase
{
    Calculation,
    CalculatedViews,
    CollectionViews,
    RawTransactionsPivot,
    GridColumns,
    Totals,
    Ledger,
    ForecastGrouping,
    FilterLists
}

public sealed record RefreshRequest(
    RefreshProjection Projections,
    string Reason = "",
    bool Recalculate = false,
    bool RebuildFilterLists = false,
    bool MarkDirty = false)
{
    public RefreshRequest Merge(RefreshRequest next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return new RefreshRequest(
            Projections | next.Projections,
            string.IsNullOrWhiteSpace(next.Reason) ? Reason : next.Reason,
            Recalculate || next.Recalculate,
            RebuildFilterLists || next.RebuildFilterLists,
            MarkDirty || next.MarkDirty);
    }
}

public sealed record RefreshDiagnosticsSnapshot(
    int RequestedRefreshes,
    int CoalescedRefreshes,
    int ExecutedRefreshes,
    TimeSpan LastRefreshDuration,
    IReadOnlyDictionary<RefreshPhase, int> PhaseCounts,
    IReadOnlyDictionary<RefreshPhase, TimeSpan> PhaseDurations)
{
    public int GetPhaseCount(RefreshPhase phase) => PhaseCounts.TryGetValue(phase, out var count) ? count : 0;

    public TimeSpan GetPhaseDuration(RefreshPhase phase) =>
        PhaseDurations.TryGetValue(phase, out var duration) ? duration : TimeSpan.Zero;
}

public sealed class RefreshDiagnostics
{
    private readonly Dictionary<RefreshPhase, int> _phaseCounts = [];
    private readonly Dictionary<RefreshPhase, TimeSpan> _phaseDurations = [];
    private int _requestedRefreshes;
    private int _coalescedRefreshes;
    private int _executedRefreshes;
    private TimeSpan _lastRefreshDuration;

    public RefreshDiagnosticsSnapshot Snapshot()
    {
        return new RefreshDiagnosticsSnapshot(
            _requestedRefreshes,
            _coalescedRefreshes,
            _executedRefreshes,
            _lastRefreshDuration,
            new ReadOnlyDictionary<RefreshPhase, int>(new Dictionary<RefreshPhase, int>(_phaseCounts)),
            new ReadOnlyDictionary<RefreshPhase, TimeSpan>(new Dictionary<RefreshPhase, TimeSpan>(_phaseDurations)));
    }

    internal void RecordRequest(bool wasCoalesced)
    {
        _requestedRefreshes++;
        if (wasCoalesced)
        {
            _coalescedRefreshes++;
        }
    }

    internal void RecordExecution(TimeSpan elapsed)
    {
        _executedRefreshes++;
        _lastRefreshDuration = elapsed;
    }

    internal void RecordPhase(RefreshPhase phase, TimeSpan elapsed)
    {
        _phaseCounts[phase] = _phaseCounts.GetValueOrDefault(phase) + 1;
        _phaseDurations[phase] = _phaseDurations.GetValueOrDefault(phase) + elapsed;
    }
}

/// <summary>
/// Owns one dispatcher refresh request and merges overlapping projection work
/// before it reaches the view model. The scheduler is injected so the merge
/// contract can be tested without constructing a WPF application.
/// </summary>
public sealed class RefreshCoordinator : IDisposable
{
    private readonly Action<Action> _schedule;
    private readonly Action<RefreshRequest> _execute;
    private readonly RefreshDiagnostics _diagnostics = new();
    private RefreshRequest? _pending;
    private bool _scheduled;
    private bool _disposed;
    private int _batchDepth;

    public RefreshCoordinator(Action<Action> schedule, Action<RefreshRequest> execute)
    {
        _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    public RefreshDiagnostics Diagnostics => _diagnostics;

    public bool HasPendingRequest => _pending is not null;

    public void BeginBatch()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _batchDepth++;
    }

    public void EndBatch()
    {
        if (_disposed || _batchDepth == 0)
        {
            return;
        }

        _batchDepth--;
        if (_batchDepth == 0)
        {
            SchedulePendingRequest();
        }
    }

    public void Request(RefreshRequest request)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);
        if (request.Projections == RefreshProjection.None && !request.Recalculate && !request.MarkDirty)
        {
            return;
        }

        var wasCoalesced = _pending is not null || _scheduled || _batchDepth > 0;
        _diagnostics.RecordRequest(wasCoalesced);
        _pending = _pending is null ? request : _pending.Merge(request);
        if (_batchDepth == 0)
        {
            SchedulePendingRequest();
        }
    }

    public void FlushNow()
    {
        if (_disposed)
        {
            return;
        }

        _scheduled = false;
        ExecutePendingRequest();
    }

    public void Measure(RefreshPhase phase, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            action();
        }
        finally
        {
            stopwatch.Stop();
            _diagnostics.RecordPhase(phase, stopwatch.Elapsed);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _pending = null;
        _scheduled = false;
        _batchDepth = 0;
    }

    private void SchedulePendingRequest()
    {
        if (_disposed || _pending is null || _scheduled)
        {
            return;
        }

        _scheduled = true;
        _schedule(ExecutePendingRequest);
    }

    private void ExecutePendingRequest()
    {
        if (_disposed)
        {
            return;
        }

        _scheduled = false;
        var request = _pending;
        _pending = null;
        if (request is null)
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            _execute(request);
        }
        finally
        {
            stopwatch.Stop();
            _diagnostics.RecordExecution(stopwatch.Elapsed);
            SchedulePendingRequest();
        }
    }
}
