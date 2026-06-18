using System.Diagnostics;

namespace ProjectCostForecast.App;

public static class GridPerformanceDiagnostics
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, long> Counters = [];
    private static readonly Dictionary<string, long> Measurements = [];

    public static IDisposable Measure(string name, double minimumMillisecondsToLog = 0, int sampleRate = 1) =>
        new Measurement(name, minimumMillisecondsToLog, sampleRate);

    public static void Count(string name, long delta = 1)
    {
        long value;
        lock (Gate)
        {
            Counters.TryGetValue(name, out value);
            value += delta;
            Counters[name] = value;
        }

        if (value == 1 || value % 50 == 0)
        {
            Debug.WriteLine($"[GridPerf] {name} count={value}");
        }
    }

    public static void Observe(string name, TimeSpan elapsed, string? detail = null)
    {
        var suffix = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" {detail}";
        Debug.WriteLine($"[GridPerf] {name} {elapsed.TotalMilliseconds:N1} ms{suffix}");
    }

    private static long NextMeasurementCount(string name)
    {
        lock (Gate)
        {
            Measurements.TryGetValue(name, out var value);
            value++;
            Measurements[name] = value;
            return value;
        }
    }

    private sealed class Measurement(string name, double minimumMillisecondsToLog, int sampleRate) : IDisposable
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _stopwatch.Stop();
            var count = NextMeasurementCount(name);
            var sample = sampleRate > 1 && count % sampleRate == 0;
            if (_stopwatch.Elapsed.TotalMilliseconds >= minimumMillisecondsToLog || sample)
            {
                Observe(name, _stopwatch.Elapsed);
            }
        }
    }
}
