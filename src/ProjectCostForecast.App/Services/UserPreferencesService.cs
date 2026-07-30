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

    public UserPreferencesService(string? preferencesPath = null)
    {
        _preferencesPath = string.IsNullOrWhiteSpace(preferencesPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ProjectCostForecast",
                "user-preferences.json")
            : Path.GetFullPath(preferencesPath);
    }

    public AppUserPreferences Load()
    {
        try
        {
            if (!File.Exists(_preferencesPath))
            {
                return new AppUserPreferences();
            }

            using var stream = File.OpenRead(_preferencesPath);
            return JsonSerializer.Deserialize<AppUserPreferences>(stream, JsonOptions) ?? new AppUserPreferences();
        }
        catch
        {
            return new AppUserPreferences();
        }
    }

    public void Save(AppUserPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        AtomicJsonFile.Write(_preferencesPath, preferences, JsonOptions);
    }
}
