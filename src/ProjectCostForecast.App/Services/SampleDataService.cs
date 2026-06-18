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
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "SampleData.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The seed data file was not found. Make sure Data/SampleData.json is copied to output.", path);
        }

        using var stream = File.OpenRead(path);
        var dataset = JsonSerializer.Deserialize<ProjectDataset>(stream, _jsonOptions);
        return dataset ?? new ProjectDataset();
    }
}
