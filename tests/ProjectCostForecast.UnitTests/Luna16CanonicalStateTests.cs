using System.Reflection;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using ProjectCostForecast.App.ViewModels;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna16CanonicalStateTests
{
    [Fact]
    public void View_model_exposes_the_dataset_owned_financial_collections()
    {
        var source = CreateDataset();
        var viewModel = Luna11TestSupport.CreateSeedViewModel(() => source);
        var activeDataset = GetActiveDataset(viewModel);

        Assert.Same(activeDataset.ForecastLines, viewModel.ForecastLines);
        Assert.Same(activeDataset.Transactions, viewModel.Transactions);

        var line = Assert.Single(viewModel.ForecastLines);
        line.MonthlyForecasts[0].Amount = 75m;

        Assert.True(viewModel.IsDirty);
        Assert.Equal(75m, line.TotalForecastCtc);
        Assert.Equal(75m, Assert.Single(viewModel.CategorySummaries).TotalForecast);
        Assert.Same(line, Assert.Single(activeDataset.ForecastLines));
    }

    [Fact]
    public void Category_summary_cache_is_rebuilt_from_forecast_inputs_on_load()
    {
        var source = CreateDataset();
        source.CategorySummaries =
        [
            new CategorySummary
            {
                ProjectCode = "Stale category",
                TotalForecast = 999m,
                CostToDate = 999m,
                PlannedCost = 999m
            }
        ];

        var viewModel = Luna11TestSupport.CreateSeedViewModel(() => source);

        var summary = Assert.Single(viewModel.CategorySummaries);
        Assert.Equal(viewModel.ForecastLines[0].ReportingCategory, summary.ProjectCode);
        Assert.Equal(25m, summary.TotalForecast);
        Assert.Equal(125m, summary.PlannedCost);
    }

    [Fact]
    public void Saved_month_switch_uses_a_separate_projection_and_restores_live_rows()
    {
        var source = CreateDataset();
        var viewModel = Luna11TestSupport.CreateSeedViewModel(() => source);
        var activeDataset = GetActiveDataset(viewModel);
        var liveCollection = activeDataset.ForecastLines;
        var liveLine = Assert.Single(liveCollection);
        var liveAmount = liveLine.MonthlyForecasts[0].Amount;
        var snapshot = NewMonthOperation.BuildSavedMonthSnapshotAt(
            "26-09",
            liveCollection,
            new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero));

        viewModel.ViewSavedMonthSnapshot(snapshot);

        Assert.True(viewModel.IsViewingSavedMonth);
        Assert.NotSame(liveCollection, viewModel.ForecastLines);
        Assert.Same(liveCollection, activeDataset.ForecastLines);
        Assert.NotSame(liveLine, Assert.Single(viewModel.ForecastLines));
        Assert.Equal(liveAmount, liveLine.MonthlyForecasts[0].Amount);

        viewModel.CloseSavedMonthView();

        Assert.False(viewModel.IsViewingSavedMonth);
        Assert.Same(liveCollection, viewModel.ForecastLines);
        Assert.Same(liveLine, Assert.Single(viewModel.ForecastLines));
        Assert.Equal(liveAmount, liveLine.MonthlyForecasts[0].Amount);
    }

    [Fact]
    public void Legacy_fixture_totals_match_a_fresh_calculation_after_view_model_load()
    {
        var path = Path.Combine(
            Luna11TestSupport.RepositoryRoot,
            "tests",
            "ProjectCostForecast.UnitTests",
            "Fixtures",
            "ProjectFiles",
            "legacy-unversioned.json");
        var source = new ProjectFileService().Load(path);
        var expected = new ProjectDatasetCloner().Clone(source);
        new CalculationService().Recalculate(expected);

        var viewModel = Luna11TestSupport.CreateSeedViewModel(() => source);

        Assert.Equal(expected.Transactions.Sum(transaction => transaction.Amount), viewModel.TotalCostToDate);
        Assert.Equal(expected.ForecastLines.Sum(line => line.PlannedCostFcc), viewModel.PlannedCostFcc);
        Assert.Equal(
            expected.CategorySummaries.Select(summary => (summary.ProjectCode, summary.PlannedCost)),
            viewModel.CategorySummaries.Select(summary => (summary.ProjectCode, summary.PlannedCost)));
    }

    [Fact]
    public void Application_save_refreshes_the_derived_category_cache_before_writing()
    {
        var source = CreateDataset();
        source.CategorySummaries =
        [
            new CategorySummary
            {
                ProjectCode = "Stale category",
                TotalForecast = 999m,
                PlannedCost = 999m
            }
        ];
        var viewModel = Luna11TestSupport.CreateSeedViewModel(() => source);
        using var directory = new Luna11TemporaryDirectory();
        var path = Path.Combine(directory.Root, "canonical-save.json");
        viewModel.ProjectFilePath = path;
        viewModel.IsDirty = true;

        viewModel.SaveProjectCommand.Execute(null);

        var saved = new ProjectFileService().Load(path);
        var summary = Assert.Single(saved.CategorySummaries);
        Assert.Equal(viewModel.ForecastLines[0].ReportingCategory, summary.ProjectCode);
        Assert.Equal(25m, summary.TotalForecast);
        Assert.Equal(125m, summary.PlannedCost);
    }

    private static ProjectDataset GetActiveDataset(MainWindowViewModel viewModel)
    {
        return (ProjectDataset)(typeof(MainWindowViewModel)
            .GetField("_dataset", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(viewModel)
            ?? throw new InvalidOperationException("The active dataset field was not found."));
    }

    private static ProjectDataset CreateDataset()
    {
        return new ProjectDataset
        {
            Header = new ProjectHeader
            {
                ProjectTitle = "Canonical state test",
                ReportTitle = "Canonical state test",
                CurrentPeriod = "26-09"
            },
            ForecastPeriods =
            [
                new ForecastPeriod
                {
                    Label = "26-09",
                    StartDate = new DateOnly(2026, 3, 1)
                }
            ],
            Transactions =
            [
                new CostTransaction
                {
                    TaskNumber = "TASK-1",
                    ManualName = "Resource A",
                    FyPeriod = "26-09",
                    Amount = 100m
                }
            ],
            ForecastLines =
            [
                new ForecastLine
                {
                    RowNumber = 1,
                    TaskNumber = "TASK-1",
                    ResourceName = "Resource A",
                    ProjectCode = "Category A",
                    ReportingCategoryOverride = "Category A",
                    Budget = 200m,
                    MonthlyForecasts =
                    [
                        new MonthlyForecast
                        {
                            PeriodLabel = "26-09",
                            PeriodStartDate = new DateOnly(2026, 3, 1),
                            Amount = 25m
                        }
                    ]
                }
            ]
        };
    }
}
