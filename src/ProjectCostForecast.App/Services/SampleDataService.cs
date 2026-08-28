using System.IO;
using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App.Services;

public sealed class SampleDataService
{
    private readonly ProjectDatasetMigrationPipeline _migrationPipeline = new();
    private readonly IClock _clock;

    public SampleDataService(IClock? clock = null)
    {
        _clock = clock ?? SystemClock.Instance;
    }

    public ProjectDataset Load()
    {
        var initialCostLoadPath = Path.Combine(AppContext.BaseDirectory, "Data", "data_anonymised.xlsx");
        if (File.Exists(initialCostLoadPath))
        {
            return new InitialCostLoadService(clock: _clock).Load(initialCostLoadPath);
        }

        // Retain the JSON fallback so existing project builds remain usable if
        // the packaged initial-load workbook is deliberately removed.
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "SampleData.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Neither data_anonymised.xlsx nor the JSON seed data file was found in Data.", path);
        }

        using var stream = File.OpenRead(path);
        return _migrationPipeline.Load(stream).Dataset;
    }
}
