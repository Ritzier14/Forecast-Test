using System.IO;
using System.Text.Json;
using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App.Services;

public sealed class UserPreferencesService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _preferencesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ProjectCostForecast",
        "user-preferences.json");

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

        var directory = Path.GetDirectoryName(_preferencesPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(_preferencesPath);
        JsonSerializer.Serialize(stream, preferences, JsonOptions);
    }
}
