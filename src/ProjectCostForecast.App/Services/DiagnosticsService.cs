using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectCostForecast.App.Services;

public enum DiagnosticSeverity
{
    Information,
    Warning,
    Error,
    Critical
}

public sealed record DiagnosticEvent(
    DateTimeOffset Timestamp,
    DiagnosticSeverity Severity,
    string Operation,
    string ExceptionType,
    string Reason);

public interface IDiagnosticsService
{
    void Record(DiagnosticEvent diagnostic);

    void RecordException(
        string operation,
        Exception exception,
        string reason,
        DiagnosticSeverity severity = DiagnosticSeverity.Error);
}

/// <summary>
/// Writes a small, local, privacy-conscious rolling diagnostic log. Diagnostics
/// are deliberately best-effort: an inability to write the log must never hide
/// the failure that caused the diagnostic to be recorded.
/// </summary>
public sealed class DiagnosticsService : IDiagnosticsService
{
    public const int DefaultMaxFileBytes = 64 * 1024;
    public const int DefaultMaxFileCount = 2;

    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static readonly Regex WindowsPathPattern = new(
        @"(?i)(?:[a-z]:[\\/]|\\\\)[^\t,;|]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex UnixPathPattern = new(
        @"(?<![\w])/(?:[^\s,;|]+/)*[^\s,;|]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex QuotedValuePattern = new(
        "(?:\"[^\"]*\"|'[^']*')",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex NamedValuePattern = new(
        @"(?i)\b(?:project|resource|person|name|row|path|file|value)\s*[:=]\s*[^,;|]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly object _gate = new();
    private readonly string _logPath;
    private readonly int _maxFileBytes;
    private readonly int _maxFileCount;
    private readonly IClock _clock;

    public DiagnosticsService(
        string? logDirectory = null,
        int maxFileBytes = DefaultMaxFileBytes,
        int maxFileCount = DefaultMaxFileCount,
        IClock? clock = null)
    {
        if (maxFileBytes < 128)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFileBytes), "A diagnostic file must allow a complete bounded entry.");
        }

        if (maxFileCount < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFileCount), "At least the active and one rotated diagnostic file are required.");
        }

        var directory = string.IsNullOrWhiteSpace(logDirectory)
            ? GetDefaultLogDirectory()
            : Path.GetFullPath(logDirectory);

        _logPath = Path.Combine(directory, "diagnostics.log");
        _maxFileBytes = maxFileBytes;
        _maxFileCount = maxFileCount;
        _clock = clock ?? SystemClock.Instance;
    }

    public string LogPath => _logPath;

    public int MaxFileBytes => _maxFileBytes;

    public int MaxFileCount => _maxFileCount;

    public void RecordException(
        string operation,
        Exception exception,
        string reason,
        DiagnosticSeverity severity = DiagnosticSeverity.Error)
    {
        try
        {
            if (exception is null)
            {
                return;
            }

            Record(new DiagnosticEvent(
                _clock.UtcNow,
                severity,
                operation,
                exception.GetType().Name,
                reason));
        }
        catch
        {
            // Diagnostics are never allowed to mask the original exception.
        }
    }

    public void Record(DiagnosticEvent diagnostic)
    {
        try
        {
            if (diagnostic is null)
            {
                return;
            }

            var line = FormatDiagnostic(diagnostic);
            lock (_gate)
            {
                AppendBounded(line);
            }
        }
        catch
        {
            // Directory permissions, locked files, and full disks are all
            // secondary to the failure being diagnosed.
            Debug.WriteLine("[Diagnostics] diagnostic write failed");
        }
    }

    private static string GetDefaultLogDirectory()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrWhiteSpace(localApplicationData)
            ? Path.Combine(Path.GetTempPath(), "ProjectCostForecast")
            : Path.Combine(localApplicationData, "ProjectCostForecast");
    }

    private void AppendBounded(string line)
    {
        var payload = CreatePayload(line);
        var existingLength = File.Exists(_logPath) ? new FileInfo(_logPath).Length : 0;

        if (existingLength + payload.Length > _maxFileBytes)
        {
            Rotate();
        }

        var directory = Path.GetDirectoryName(_logPath)
            ?? throw new InvalidOperationException("The diagnostic log directory could not be determined.");
        Directory.CreateDirectory(directory);

        using var stream = new FileStream(
            _logPath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            options: FileOptions.SequentialScan);
        stream.Write(payload, 0, payload.Length);
        stream.Flush(flushToDisk: true);
    }

    private byte[] CreatePayload(string line)
    {
        var payload = Utf8.GetBytes(line + "\n");
        if (payload.Length <= _maxFileBytes)
        {
            return payload;
        }

        // All fields are reduced to ASCII by SanitizeToken, so a character
        // truncation remains valid UTF-8 and keeps a single event bounded.
        var maximumLineLength = Math.Max(1, _maxFileBytes - 1);
        var shortened = line.Length > maximumLineLength
            ? line[..maximumLineLength]
            : line;
        return Utf8.GetBytes(shortened + "\n");
    }

    private void Rotate()
    {
        for (var index = _maxFileCount - 1; index >= 1; index--)
        {
            var source = GetRotatedPath(index - 1);
            var destination = GetRotatedPath(index);
            if (!File.Exists(source))
            {
                continue;
            }

            File.Move(source, destination, overwrite: true);
        }
    }

    private string GetRotatedPath(int index)
    {
        if (index == 0)
        {
            return _logPath;
        }

        var directory = Path.GetDirectoryName(_logPath)
            ?? throw new InvalidOperationException("The diagnostic log directory could not be determined.");
        return Path.Combine(directory, $"diagnostics.{index}.log");
    }

    private static string FormatDiagnostic(DiagnosticEvent diagnostic)
    {
        var timestamp = diagnostic.Timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        return string.Join(
            '\t',
            timestamp,
            SanitizeToken(diagnostic.Severity.ToString(), "Error", 16),
            SanitizeToken(diagnostic.Operation, "unknown-operation", 64),
            SanitizeToken(diagnostic.ExceptionType, "Exception", 96),
            SanitizeReason(diagnostic.Reason));
    }

    private static string SanitizeReason(string? reason)
    {
        var text = string.IsNullOrWhiteSpace(reason) ? "unspecified" : reason.Trim();
        text = WindowsPathPattern.Replace(text, "<path>");
        text = UnixPathPattern.Replace(text, "<path>");
        text = QuotedValuePattern.Replace(text, "<value>");
        text = NamedValuePattern.Replace(text, "<redacted>");
        return SanitizeToken(text, "unspecified", 180);
    }

    private static string SanitizeToken(string? value, string fallback, int maximumLength)
    {
        var text = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        var builder = new StringBuilder(Math.Min(text.Length, maximumLength));
        var previousWasSpace = false;

        foreach (var character in text)
        {
            if (builder.Length >= maximumLength)
            {
                break;
            }

            if (character is '\r' or '\n' or '\t' || char.IsWhiteSpace(character))
            {
                if (!previousWasSpace)
                {
                    builder.Append(' ');
                    previousWasSpace = true;
                }

                continue;
            }

            previousWasSpace = false;
            if ((character is >= 'a' and <= 'z')
                || (character is >= 'A' and <= 'Z')
                || (character is >= '0' and <= '9')
                || character is '_' or '-' or '.' or '<' or '>' or ':')
            {
                builder.Append(character);
            }
            else
            {
                builder.Append('_');
            }
        }

        var result = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(result) ? fallback : result;
    }
}

public enum RuntimeExceptionBoundary
{
    UiDispatcher,
    ApplicationDomain,
    UnobservedTask
}

public sealed record RuntimeExceptionHandlingResult(string UserMessage, bool FailFast);

/// <summary>
/// Defines the boundary policy independently of WPF so it can be tested
/// without creating an application or showing a modal dialog.
/// </summary>
public sealed class RuntimeExceptionPolicy
{
    private readonly IDiagnosticsService _diagnostics;

    public RuntimeExceptionPolicy(IDiagnosticsService diagnostics)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public RuntimeExceptionHandlingResult Handle(RuntimeExceptionBoundary boundary, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var operation = boundary switch
        {
            RuntimeExceptionBoundary.UiDispatcher => "UI dispatcher",
            RuntimeExceptionBoundary.ApplicationDomain => "application domain",
            RuntimeExceptionBoundary.UnobservedTask => "unobserved task",
            _ => "application boundary"
        };

        try
        {
            _diagnostics.RecordException(
                operation,
                exception,
                "Unexpected application failure at a top-level boundary.",
                DiagnosticSeverity.Critical);
        }
        catch
        {
            // A diagnostics implementation supplied by a host must not alter
            // the boundary's original fail-fast/continue decision.
        }

        return boundary switch
        {
            RuntimeExceptionBoundary.UnobservedTask => new RuntimeExceptionHandlingResult(
                "A background operation failed. A sanitized diagnostic was recorded locally.",
                FailFast: false),
            _ => new RuntimeExceptionHandlingResult(
                "Project Cost Forecast encountered an unexpected error and will close. A sanitized diagnostic was recorded locally.",
                FailFast: true)
        };
    }
}
