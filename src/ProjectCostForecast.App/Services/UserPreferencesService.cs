using System.Globalization;
using System.IO;
using System.Text.Json;
using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App.Services;

public sealed class UserPreferencesService : IUserPreferencesService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _preferencesPath;
    private readonly IDiagnosticsService _diagnostics;
    private readonly Func<DateTime> _utcNow;

    public UserPreferencesService(
        string? preferencesPath = null,
        IDiagnosticsService? diagnostics = null,
        Func<DateTime>? utcNow = null)
    {
        _preferencesPath = string.IsNullOrWhiteSpace(preferencesPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ProjectCostForecast",
                "user-preferences.json")
            : Path.GetFullPath(preferencesPath);
        _diagnostics = diagnostics ?? new DiagnosticsService();
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public string? LastLoadNotice { get; private set; }

    public string? LastQuarantinedPath { get; private set; }

    public AppUserPreferences Load()
    {
        LastLoadNotice = null;
        LastQuarantinedPath = null;

        if (!File.Exists(_preferencesPath))
        {
            return new AppUserPreferences();
        }

        try
        {
            using var stream = File.OpenRead(_preferencesPath);
            return JsonSerializer.Deserialize<AppUserPreferences>(stream, JsonOptions)
                ?? throw new JsonException("The preferences document was empty.");
        }
        catch (Exception exception)
        {
            LastQuarantinedPath = TryQuarantineMalformedFile();
            LastLoadNotice = LastQuarantinedPath is null
                ? "Preferences could not be loaded; defaults are in use."
                : "Invalid preferences were quarantined; defaults are in use.";

            try
            {
                _diagnostics.RecordException(
                    "preferences.load",
                    exception,
                    LastQuarantinedPath is null
                        ? "Preferences load failed; defaults loaded."
                        : "Invalid preferences quarantined; defaults loaded.",
                    DiagnosticSeverity.Warning);
            }
            catch
            {
                // Diagnostics are best-effort and must not prevent startup.
            }

            return new AppUserPreferences();
        }
    }

    public void Save(AppUserPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        AtomicJsonFile.Write(_preferencesPath, preferences, JsonOptions);
    }

    private string? TryQuarantineMalformedFile()
    {
        if (!File.Exists(_preferencesPath))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(_preferencesPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        var baseName = Path.GetFileNameWithoutExtension(_preferencesPath);
        var extension = Path.GetExtension(_preferencesPath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".json";
        }

        var timestamp = _utcNow()
            .ToUniversalTime()
            .ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);

        for (var suffix = 0; suffix < 1000; suffix++)
        {
            var suffixText = suffix == 0 ? string.Empty : $"-{suffix}";
            var quarantinePath = Path.Combine(
                directory,
                $"{baseName}.corrupt-{timestamp}{suffixText}{extension}");

            try
            {
                File.Move(_preferencesPath, quarantinePath);
                return quarantinePath;
            }
            catch (IOException) when (File.Exists(quarantinePath))
            {
                // A previous recovery claimed this timestamp. Retry with a
                // deterministic suffix without touching the bad source file.
            }
            catch (Exception)
            {
                try
                {
                    // Copy is a fallback for filesystems that do not permit a
                    // move. The malformed source is intentionally retained.
                    File.Copy(_preferencesPath, quarantinePath, overwrite: false);
                    return quarantinePath;
                }
                catch
                {
                    return null;
                }
            }
        }

        return null;
    }
}
