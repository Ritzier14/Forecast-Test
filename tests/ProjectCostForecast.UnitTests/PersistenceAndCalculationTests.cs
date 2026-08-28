using System.IO;
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

    private static ProjectDataset CreateDataset(string title = "Calculation fixture")
    {
        return new ProjectDataset
        {
            Header = new ProjectHeader
            {
                ProjectTitle = title,
                CurrentPeriod = "26-09"
            },
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
