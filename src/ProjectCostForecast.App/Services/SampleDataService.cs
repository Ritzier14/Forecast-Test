using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App.Services;

public sealed class SampleDataService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public ProjectDataset Load()
    {
        var initialCostLoadPath = Path.Combine(AppContext.BaseDirectory, "Data", "InitialCostLoad.xlsx");
        if (File.Exists(initialCostLoadPath))
        {
            return new InitialCostLoadService().Load(initialCostLoadPath);
        }

        // Retain the JSON fallback so existing project builds remain usable if
        // the packaged initial-load workbook is deliberately removed.
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "SampleData.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Neither the initial cost-load workbook nor the JSON seed data file was found in Data.", path);
        }

        using var stream = File.OpenRead(path);
        var dataset = JsonSerializer.Deserialize<ProjectDataset>(stream, _jsonOptions);
        return dataset ?? new ProjectDataset();
    }
}
