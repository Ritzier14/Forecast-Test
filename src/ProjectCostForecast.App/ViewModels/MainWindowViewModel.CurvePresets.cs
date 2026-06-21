using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App.ViewModels;

public partial class MainWindowViewModel
{
    public UserForecastCurvePreset SaveForecastCurvePreset(
        string name,
        string note,
        string resourceName,
        decimal referenceTotal,
        IReadOnlyList<decimal> weights,
        UserForecastCurvePreset? overwrite = null)
    {
        var now = DateTime.UtcNow;
        var cleanedName = string.IsNullOrWhiteSpace(name) ? "Custom curve" : name.Trim();
        var preset = overwrite ?? new UserForecastCurvePreset
        {
            Id = Guid.NewGuid().ToString("N"),
            CreatedUtc = now
        };

        preset.Name = cleanedName;
        preset.Note = note.Trim();
        preset.ProjectName = Header.ProjectTitle;
        preset.ResourceName = resourceName.Trim();
        preset.ReferenceTotal = referenceTotal;
        preset.MonthCount = weights.Count;
        preset.Weights = weights.ToList();
        preset.UpdatedUtc = now;

        if (overwrite is null)
        {
            UserForecastCurvePresets.Add(preset);
        }

        SaveUserPreferences();
        OnPropertyChanged(nameof(UserForecastCurvePresets));
        return preset;
    }

    public void DeleteForecastCurvePreset(UserForecastCurvePreset preset)
    {
        var existing = UserForecastCurvePresets.FirstOrDefault(item =>
            string.Equals(item.Id, preset.Id, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            return;
        }

        UserForecastCurvePresets.Remove(existing);
        SaveUserPreferences();
        OnPropertyChanged(nameof(UserForecastCurvePresets));
    }

    public void SaveForecastCurvePresetChanges()
    {
        foreach (var preset in UserForecastCurvePresets)
        {
            preset.Name = string.IsNullOrWhiteSpace(preset.Name) ? "Custom curve" : preset.Name.Trim();
            preset.Note = preset.Note.Trim();
            preset.UpdatedUtc = DateTime.UtcNow;
        }

        SaveUserPreferences();
        OnPropertyChanged(nameof(UserForecastCurvePresets));
    }

    public static UserForecastCurvePreset CloneForecastCurvePreset(UserForecastCurvePreset preset)
    {
        return new UserForecastCurvePreset
        {
            Id = preset.Id,
            Name = preset.Name,
            Note = preset.Note,
            ProjectName = preset.ProjectName,
            ResourceName = preset.ResourceName,
            ReferenceTotal = preset.ReferenceTotal,
            MonthCount = preset.MonthCount,
            CreatedUtc = preset.CreatedUtc,
            UpdatedUtc = preset.UpdatedUtc,
            Weights = preset.Weights.ToList()
        };
    }
}
