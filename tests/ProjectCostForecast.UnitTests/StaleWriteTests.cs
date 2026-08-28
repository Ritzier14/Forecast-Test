using System.IO;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using ProjectCostForecast.App.ViewModels;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class StaleWriteTests
{
    [Fact]
    public void Two_project_sessions_reject_a_stale_write_without_overwriting_newer_file()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Root, "project.json");
        var firstService = new ProjectFileService();
        var secondService = new ProjectFileService();

        firstService.Save(path, CreateDataset("Original"));
        var firstSession = firstService.LoadWithRevision(path);
        var secondSession = secondService.LoadWithRevision(path);

        secondSession.Dataset.Header.ProjectTitle = "Newer external version";
        _ = secondService.SaveWithRevision(path, secondSession.Dataset, secondSession.Revision, "Save project");

        firstSession.Dataset.Header.ProjectTitle = "Stale version";
        var conflict = Assert.Throws<ProjectFileConflictException>(
            () => firstService.SaveWithRevision(path, firstSession.Dataset, firstSession.Revision, "Save project"));

        Assert.Equal(firstSession.Revision, conflict.ExpectedRevision);
        Assert.NotEqual(firstSession.Revision, conflict.ActualRevision);
        Assert.Contains("Reload", conflict.Message, StringComparison.Ordinal);
        Assert.Contains("Save As", conflict.Message, StringComparison.Ordinal);
        Assert.Equal("Newer external version", firstService.Load(path).Header.ProjectTitle);
    }

    [Fact]
    public void A_successful_single_session_save_refreshes_the_revision_for_the_next_save()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Root, "project.json");
        var service = new ProjectFileService();

        service.Save(path, CreateDataset("Original"));
        var session = service.LoadWithRevision(path);

        session.Dataset.Header.ProjectTitle = "Second version";
        var secondRevision = service.SaveWithRevision(path, session.Dataset, session.Revision, "Save project");
        Assert.NotNull(secondRevision);

        session.Dataset.Header.ProjectTitle = "Third version";
        _ = service.SaveWithRevision(path, session.Dataset, secondRevision, "Save project");

        Assert.Equal("Third version", service.Load(path).Header.ProjectTitle);
    }

    [Fact]
    public void View_model_conflict_decision_is_injectable_without_opening_a_dialog()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Root, "project.json");
        var firstService = new ProjectFileService();
        var secondService = new ProjectFileService();
        firstService.Save(path, CreateDataset("Original"));

        var decisionCalls = 0;
        var viewModel = new MainWindowViewModel(new MainWindowViewModelDependencies
        {
            ProjectFileService = firstService,
            UserPreferencesService = new InMemoryUserPreferencesService(),
            InitialDatasetFactory = () => CreateDataset("Session changes"),
            SaveConflictDecisionHandler = conflict =>
            {
                decisionCalls++;
                Assert.Equal(path, conflict.Path);
                Assert.Equal("Save project", conflict.Operation);
                return SaveConflictDecision.Cancel;
            }
        });

        viewModel.ProjectFilePath = path;
        viewModel.SaveProjectCommand.Execute(null);

        var externalSession = secondService.LoadWithRevision(path);
        externalSession.Dataset.Header.ProjectTitle = "External version";
        _ = secondService.SaveWithRevision(path, externalSession.Dataset, externalSession.Revision, "Save project");

        viewModel.IsDirty = true;
        viewModel.SaveProjectCommand.Execute(null);

        Assert.Equal(1, decisionCalls);
        Assert.True(viewModel.IsDirty);
        Assert.Contains("cancelled", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("External version", firstService.Load(path).Header.ProjectTitle);
    }

    private static ProjectDataset CreateDataset(string title)
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
                        new MonthlyForecast { PeriodLabel = "26-09", Amount = 25m },
                        new MonthlyForecast { PeriodLabel = "26-10", Amount = 50m }
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
                }
            ]
        };
    }

    private sealed class InMemoryUserPreferencesService : IUserPreferencesService
    {
        private AppUserPreferences _preferences = new();

        public AppUserPreferences Load() => _preferences;

        public void Save(AppUserPreferences preferences)
        {
            _preferences = preferences;
        }
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
