using System.IO;
using System.Text.Json;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class PersistenceAndCalculationTests
{
    [Fact]
    public void CalculationService_recalculates_line_and_category_totals_from_inputs()
    {
        var dataset = CreateDataset();

        new CalculationService().Recalculate(dataset);

        var line = Assert.Single(dataset.ForecastLines);
        Assert.Equal(150m, line.CostToDate);
        Assert.Equal(100m, line.CurrentMonthCost);
        Assert.Equal(75m, line.TotalForecastCtc);
        Assert.Equal(25m, line.MonthForecast);
        Assert.Equal(225m, line.PlannedCostFcc);
        Assert.Equal(255m, line.VarianceLastMonthToDate);
        Assert.Equal(25m, line.MonthForecastVariance);
        Assert.Equal(275m, line.TotalBudgetVariance);

        var summary = Assert.Single(dataset.CategorySummaries);
        Assert.Equal("Category A", summary.ProjectCode);
        Assert.Equal(75m, summary.TotalForecast);
        Assert.Equal(150m, summary.CostToDate);
        Assert.Equal(225m, summary.PlannedCost);
        Assert.Equal(500m, summary.Budget);
        Assert.Equal(275m, summary.TotalBudgetVariance);
    }

    [Fact]
    public void ProjectFileService_round_trips_nested_project_without_atomic_temp_files()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Root, "nested", "project.json");
        var service = new ProjectFileService();
        var dataset = CreateDataset("Round-trip project");

        new CalculationService().Recalculate(dataset);
        service.Save(path, dataset);

        var reopened = service.Load(path);

        Assert.Equal("Round-trip project", reopened.Header.ProjectTitle);
        Assert.Equal("PROJECT-1", Assert.Single(reopened.ForecastLines).TransactionProjectCode);
        Assert.Equal(2, reopened.Transactions.Count);
        Assert.Equal(75m, Assert.Single(reopened.ForecastLines).MonthlyForecasts.Sum(month => month.Amount));
        Assert.Empty(Directory.EnumerateFiles(directory.Root, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void ProjectFileService_replaces_existing_project_atomically()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Root, "project.json");
        var service = new ProjectFileService();

        service.Save(path, CreateDataset("First version"));
        service.Save(path, CreateDataset("Second version"));

        var reopened = service.Load(path);
        Assert.Equal("Second version", reopened.Header.ProjectTitle);
        Assert.DoesNotContain("First version", File.ReadAllText(path), StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFiles(directory.Root, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void ProjectFileService_creates_distinct_loadable_backups_for_rapid_saves()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Root, "project.json");
        var service = new ProjectFileService();
        service.Save(path, CreateDataset("Backup source"));

        var firstBackup = service.CreateBackup(path);
        var secondBackup = service.CreateBackup(path);

        Assert.NotEqual(firstBackup, secondBackup);
        Assert.True(File.Exists(firstBackup));
        Assert.True(File.Exists(secondBackup));
        Assert.Equal("Backup source", service.Load(firstBackup).Header.ProjectTitle);
        Assert.Equal("Backup source", service.Load(secondBackup).Header.ProjectTitle);
        Assert.Equal(2, Directory.EnumerateFiles(Path.Combine(directory.Root, "backups"), "*.bak.json").Count());
    }

    [Fact]
    public void Legacy_project_fixture_migrates_to_current_format_deterministically_and_idempotently()
    {
        var pipeline = new ProjectDatasetMigrationPipeline();
        using var stream = File.OpenRead(FixturePath("legacy-unversioned.json"));

        var first = pipeline.Load(stream);

        Assert.Equal(ProjectDatasetMigrationPipeline.LegacyUnversionedVersion, first.SourceVersion);
        Assert.True(first.WasMigrated);
        Assert.Equal(ProjectDatasetMigrationPipeline.CurrentVersion, first.Dataset.FormatVersion);
        Assert.Equal(["26-09", "26-10", "26-11"], first.Dataset.ForecastPeriods.Select(period => period.Label));
        Assert.Equal(new DateOnly(2026, 3, 1), first.Dataset.ForecastPeriods[0].StartDate);
        Assert.Equal(new DateOnly(2026, 4, 1), first.Dataset.ForecastPeriods[1].StartDate);
        Assert.Equal(new DateOnly(2026, 5, 1), first.Dataset.ForecastPeriods[2].StartDate);
        Assert.Equal(["26-09", "26-10", "26-11"], Assert.Single(first.Dataset.ForecastLines).MonthlyForecasts.Select(forecast => forecast.PeriodLabel));
        Assert.Equal(["26-09", "26-10", "26-11"], Assert.Single(first.Dataset.ManagementResources).MonthlyAllocations.Select(allocation => allocation.PeriodLabel));
        Assert.Equal(["26-09", "26-10", "26-11"], Assert.Single(Assert.Single(first.Dataset.SavedMonthSnapshots).ForecastLines).MonthlyForecasts.Select(forecast => forecast.PeriodLabel));

        var second = pipeline.Normalize(first.Dataset);

        Assert.False(second.DataChanged);
        Assert.False(second.WasMigrated);
        Assert.Equal(ProjectDatasetMigrationPipeline.CurrentVersion, second.Dataset.FormatVersion);
    }

    [Fact]
    public void Migrated_legacy_fixture_saves_with_the_current_project_file_version()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Root, "migrated.json");
        var service = new ProjectFileService();
        var dataset = service.Load(FixturePath("legacy-unversioned.json"));

        service.Save(path, dataset);

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var version = document.RootElement.EnumerateObject()
            .Single(property => string.Equals(property.Name, nameof(ProjectDataset.FormatVersion), StringComparison.OrdinalIgnoreCase))
            .Value
            .GetInt32();
        Assert.Equal(ProjectDatasetMigrationPipeline.CurrentVersion, version);
        Assert.Equal(ProjectDatasetMigrationPipeline.CurrentVersion, service.Load(path).FormatVersion);
    }

    [Fact]
    public void Current_project_fixture_round_trips_without_data_changes()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Root, "current-round-trip.json");
        var service = new ProjectFileService();

        var loaded = service.Load(FixturePath("current-v1.json"));

        Assert.Equal(ProjectDatasetMigrationPipeline.CurrentVersion, loaded.FormatVersion);
        Assert.Equal("Current format fixture", loaded.Header.ProjectTitle);
        Assert.Equal("26-09", loaded.Header.CurrentPeriod);
        Assert.Equal(new DateOnly(2026, 3, 1), Assert.Single(loaded.ForecastPeriods).StartDate);

        service.Save(path, loaded);
        var reopened = service.Load(path);

        Assert.Equal(loaded.Header.ProjectTitle, reopened.Header.ProjectTitle);
        Assert.Equal(loaded.Header.CurrentPeriod, reopened.Header.CurrentPeriod);
        Assert.Equal(loaded.ForecastPeriods.Select(period => period.Label), reopened.ForecastPeriods.Select(period => period.Label));
        Assert.Equal(loaded.AuditEvents.Select(audit => audit.AuditId), reopened.AuditEvents.Select(audit => audit.AuditId));
    }

    [Fact]
    public void Top_level_json_null_is_rejected_as_a_format_error()
    {
        var exception = Assert.Throws<ProjectFileFormatException>(
            () => new ProjectFileService().Load(FixturePath("null-root.json")));

        Assert.Contains("JSON object", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("null", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Malformed_project_fixture_is_rejected_with_a_clear_format_error()
    {
        var exception = Assert.Throws<ProjectFileFormatException>(
            () => new ProjectFileService().Load(FixturePath("malformed.json")));

        Assert.Contains("malformed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Future_project_fixture_is_rejected_before_deserialization()
    {
        var exception = Assert.Throws<ProjectFileFormatException>(
            () => new ProjectFileService().Load(FixturePath("future-v99.json")));

        Assert.Contains("version 99", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("supports up to version 1", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string FixturePath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures", "ProjectFiles", fileName);
    }

    private static ProjectDataset CreateDataset(string title = "Calculation fixture")
    {
        return new ProjectDataset
        {
            Header = new ProjectHeader
            {
                ProjectTitle = title,
                CurrentPeriod = "26-09"
            },
            ForecastPeriods =
            [
                new ForecastPeriod { Label = "26-09", StartDate = new DateOnly(2026, 3, 1) },
                new ForecastPeriod { Label = "26-10", StartDate = new DateOnly(2026, 4, 1) }
            ],
            ForecastLines =
            [
                new ForecastLine
                {
                    RowNumber = 1,
                    TaskNumber = "TASK-1",
                    ResourceName = "Resource A",
                    ProjectCode = "Category A",
                    TransactionProjectCode = "PROJECT-1",
                    Budget = 500m,
                    LastMonthPlannedCost = 480m,
                    LastMonthForecast = 125m,
                    MonthlyForecasts =
                    [
                        new MonthlyForecast
                        {
                            PeriodLabel = "26-09",
                            Amount = 25m
                        },
                        new MonthlyForecast
                        {
                            PeriodLabel = "26-10",
                            Amount = 50m
                        }
                    ]
                }
            ],
            Transactions =
            [
                new CostTransaction
                {
                    RowNumber = 1,
                    FyPeriod = "26-09",
                    TaskNumber = "TASK-1",
                    ProjectCode = "PROJECT-1",
                    ManualName = "Resource A",
                    Amount = 100m
                },
                new CostTransaction
                {
                    RowNumber = 2,
                    FyPeriod = "26-08",
                    TaskNumber = "TASK-1",
                    ProjectCode = "PROJECT-1",
                    ManualName = "Resource A",
                    Amount = 50m
                }
            ]
        };
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "ProjectCostForecast.UnitTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
