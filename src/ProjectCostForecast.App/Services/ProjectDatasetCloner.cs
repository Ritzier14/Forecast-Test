using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App.Services;

public sealed class ProjectDatasetCloner
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public ProjectDatasetCloner()
    {
        DateTimeContract.AddJsonConverters(_jsonOptions);
    }

    public ProjectDataset Clone(ProjectDataset source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var json = JsonSerializer.Serialize(source, _jsonOptions);
        var clone = JsonSerializer.Deserialize<ProjectDataset>(json, _jsonOptions)
            ?? throw new InvalidOperationException("The project dataset clone was empty.");

        clone.ForecastLines ??= [];
        foreach (var pair in (source.ForecastLines ?? []).Zip(clone.ForecastLines))
        {
            // These values are intentionally not persisted, but they are needed
            // while the staged dataset is recalculated before it is committed.
            pair.Second.SetResolvedTaskMetadata(pair.First.TaskName, pair.First.ReportingCategory);
            if (pair.First.HasCustomRowHeight)
            {
                pair.Second.SetRowDisplayHeight(pair.First.RowDisplayHeight);
            }

            pair.Second.MonthlyForecasts ??= [];
            foreach (var forecastPair in (pair.First.MonthlyForecasts ?? []).Zip(pair.Second.MonthlyForecasts))
            {
                forecastPair.Second.IsLocked = forecastPair.First.IsLocked;
                forecastPair.Second.ActualCostAmount = forecastPair.First.ActualCostAmount;
            }
        }

        return clone;
    }
}
