using System.Diagnostics;
using System.Text;
using System.Windows.Data;

namespace ProjectCostForecast.App.Services;

/// <summary>
/// A scoped trace listener for WPF binding failures. The listener is intended
/// for debug and smoke-test harnesses: attaching it changes only the WPF data
/// binding trace source for the lifetime of this instance and restores the
/// previous source level when disposed.
/// </summary>
public sealed class WpfBindingErrorCapture : TraceListener, IDisposable
{
    private readonly object _gate = new();
    private readonly string _surfaceName;
    private readonly List<string> _errors = [];
    private readonly StringBuilder _pendingMessage = new();
    private TraceSource? _source;
    private SourceLevels _previousLevel;
    private bool _attached;
    private bool _disposed;

    public WpfBindingErrorCapture(string surfaceName)
    {
        _surfaceName = string.IsNullOrWhiteSpace(surfaceName)
            ? throw new ArgumentException("A binding capture surface name is required.", nameof(surfaceName))
            : surfaceName.Trim();
    }

    public IReadOnlyList<string> Errors
    {
        get
        {
            lock (_gate)
            {
                return _errors.ToArray();
            }
        }
    }

    public void Attach()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_attached)
            {
                return;
            }

            _source = PresentationTraceSources.DataBindingSource;
            _previousLevel = _source.Switch.Level;
            _source.Switch.Level = SourceLevels.Error;
            _source.Listeners.Add(this);
            _attached = true;
        }
    }

    public override void Write(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        lock (_gate)
        {
            if (!_disposed)
            {
                _pendingMessage.Append(message);
            }
        }
    }

    public override void WriteLine(string? message)
    {
        string completeMessage;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _pendingMessage.Append(message);
            completeMessage = _pendingMessage.ToString();
            _pendingMessage.Clear();
        }

        Record(completeMessage);
    }

    public override void TraceEvent(
        TraceEventCache? eventCache,
        string source,
        TraceEventType eventType,
        int id,
        string? message)
    {
        if (eventType is TraceEventType.Critical or TraceEventType.Error)
        {
            Record(message);
        }
    }

    public override void TraceData(
        TraceEventCache? eventCache,
        string source,
        TraceEventType eventType,
        int id,
        object? data)
    {
        if (eventType is TraceEventType.Critical or TraceEventType.Error)
        {
            Record(data?.ToString());
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing)
        {
            base.Dispose(disposing);
            return;
        }

        TraceSource? source;
        SourceLevels previousLevel;

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            source = _source;
            previousLevel = _previousLevel;
            _source = null;
            _attached = false;
            _pendingMessage.Clear();
        }

        if (source is not null)
        {
            source.Listeners.Remove(this);
            source.Switch.Level = previousLevel;
        }

        base.Dispose(disposing);
    }

    private void Record(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        lock (_gate)
        {
            if (!_disposed)
            {
                _errors.Add($"{_surfaceName}: {message.Trim()}");
            }
        }
    }
}
