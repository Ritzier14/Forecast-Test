using System.IO;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using ProjectCostForecast.App.ViewModels;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class StaleWriteTests
{
    private static readonly TimeSpan InterleavingTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Interleaved_revision_writers_allow_the_old_checks_to_both_pass_but_commit_only_one()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Root, "project.json");
        var seedService = new ProjectFileService();
        seedService.Save(path, CreateDataset("Original"));

        var firstInterleaving = new BlockingWriteInterleaving();
        var secondInterleaving = new BlockingWriteInterleaving();
        var firstService = new ProjectFileService(writeInterleaving: firstInterleaving);
        var secondService = new ProjectFileService(writeInterleaving: secondInterleaving);
        var firstSession = firstService.LoadWithRevision(path);
        var secondSession = secondService.LoadWithRevision(path);

        Assert.Equal(firstSession.Revision, secondSession.Revision);
        firstSession.Dataset.Header.ProjectTitle = "First writer";
        secondSession.Dataset.Header.ProjectTitle = "Second writer";

        Task<ProjectFileRevision>? firstSave = null;
        Task<ProjectFileRevision>? secondSave = null;
        try
        {
            firstSave = Task.Run(() => firstService.SaveWithRevision(
                path,
                firstSession.Dataset,
                firstSession.Revision,
                "First save"));

            await AwaitWithDiagnosticTimeoutAsync(
                firstInterleaving.AfterExpectedRevisionCheckReached.Task,
                "the first writer to reach its post-revision-check barrier");
            Assert.False(firstInterleaving.ReleaseSignal.Task.IsCompleted);

            // The second service enters while the first writer is paused, but its
            // revision check cannot run until the shared writer boundary opens.
            secondSave = Task.Run(() => secondService.SaveWithRevision(
                path,
                secondSession.Dataset,
                secondSession.Revision,
                "Second save"));
            await AwaitWithDiagnosticTimeoutAsync(
                secondInterleaving.BeforeWriterLockReached.Task,
                "the second writer to reach its pre-lock barrier");

            // This is the deterministic interleaving that the old check-then-write
            // shape exposed: the first writer has passed its check and the second
            // writer would also pass a check against the still-original bytes.
            var revisionDuringPause = secondService.GetRevision(path);
            Assert.True(firstSession.Revision!.Matches(revisionDuringPause));
            Assert.True(secondSession.Revision!.Matches(revisionDuringPause));

            firstInterleaving.Release();
            var winningRevision = await AwaitWithDiagnosticTimeoutAsync(
                firstSave,
                "the first writer to commit after its release signal");

            // The second writer now owns the boundary, observes the first commit,
            // and reports the actual winning revision instead of overwriting it.
            secondInterleaving.Release();
            var conflict = await Assert.ThrowsAsync<ProjectFileConflictException>(async () =>
            {
                _ = await AwaitWithDiagnosticTimeoutAsync(
                    secondSave,
                    "the second writer to reject the stale revision after its release signal");
            });

            Assert.Equal(winningRevision, conflict.ActualRevision);
            Assert.Equal("First writer", new ProjectFileService().Load(path).Header.ProjectTitle);
        }
        finally
        {
            // Assertions and regressions must not strand either writer at its
            // injected barrier. Release both first, then observe both tasks
            // within one bounded cleanup window so CI cannot wait forever.
            firstInterleaving.Release();
            secondInterleaving.Release();
            await ObserveSpawnedTasksForCleanupAsync(firstSave, secondSave);
        }
    }

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

    private static async Task AwaitWithDiagnosticTimeoutAsync(Task task, string diagnostic)
    {
        try
        {
            await task.WaitAsync(InterleavingTimeout);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException(
                $"Timed out after {InterleavingTimeout.TotalSeconds:0} seconds waiting for {diagnostic}.",
                ex);
        }
    }

    private static async Task<T> AwaitWithDiagnosticTimeoutAsync<T>(Task<T> task, string diagnostic)
    {
        try
        {
            return await task.WaitAsync(InterleavingTimeout);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException(
                $"Timed out after {InterleavingTimeout.TotalSeconds:0} seconds waiting for {diagnostic}.",
                ex);
        }
    }

    private static async Task ObserveSpawnedTasksForCleanupAsync(params Task?[] tasks)
    {
        var observers = tasks
            .Where(task => task is not null)
            .Select(task => ObserveTaskFailureAsync(task!));

        try
        {
            await Task.WhenAll(observers).WaitAsync(InterleavingTimeout);
        }
        catch (TimeoutException)
        {
            // The test's diagnostic wait reports a stalled production path.
            // Cleanup itself stays bounded and must not replace that failure.
        }
    }

    private static async Task ObserveTaskFailureAsync(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
            // The test body owns assertions about task outcomes. This observer
            // only prevents cleanup from masking them or leaving faults unseen.
        }
    }

    private sealed class BlockingWriteInterleaving : IProjectFileWriteInterleaving
    {
        public TaskCompletionSource<bool> BeforeWriterLockReached { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> AfterExpectedRevisionCheckReached { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> ReleaseSignal { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void BeforeWriterLock(string fullPath)
        {
            BeforeWriterLockReached.TrySetResult(true);
        }

        public void AfterExpectedRevisionCheck(string fullPath)
        {
            AfterExpectedRevisionCheckReached.TrySetResult(true);
            ReleaseSignal.Task.GetAwaiter().GetResult();
        }

        public void Release()
        {
            ReleaseSignal.TrySetResult(true);
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
