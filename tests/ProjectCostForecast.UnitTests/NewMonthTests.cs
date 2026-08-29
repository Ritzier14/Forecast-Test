using System.IO;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using ProjectCostForecast.App.ViewModels;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class NewMonthTests
{
    [Fact]
    public void Cancelled_new_month_leaves_live_state_unchanged()
    {
        var fileService = new RecordingProjectFileService();
        var viewModel = CreateViewModel(fileService);
        viewModel.ProjectFilePath = "new-month.json";
        viewModel.IsDirty = true;

        var before = CaptureState(viewModel);

        Assert.False(viewModel.TryCreateNewMonthBaseline(confirmed: false));

        AssertStateUnchanged(before, viewModel);
        Assert.Equal(0, fileService.SaveCalls);
    }

    [Fact]
    public void Failed_new_month_save_leaves_period_forecasts_snapshots_audit_selection_and_dirty_state_unchanged()
    {
        var fileService = new RecordingProjectFileService { ThrowOnSave = true };
        var viewModel = CreateViewModel(fileService);
        viewModel.ProjectFilePath = "new-month.json";
        viewModel.IsDirty = true;

        var before = CaptureState(viewModel);

        Assert.False(viewModel.TryCreateNewMonthBaseline(confirmed: true));

        AssertStateUnchanged(before, viewModel);
        Assert.Equal(1, fileService.SaveCalls);
    }

    [Fact]
    public void Successful_new_month_persists_one_baseline_and_advances_one_period()
    {
        var fileService = new RecordingProjectFileService();
        var viewModel = CreateViewModel(fileService);
        viewModel.ProjectFilePath = "new-month.json";
        viewModel.IsDirty = true;
        var lineBefore = Assert.Single(viewModel.ForecastLines);
        var expectedLastMonthPlannedCost = lineBefore.PlannedCostFcc;
        var expectedLastMonthForecast = lineBefore.MonthForecast;

        Assert.True(viewModel.TryCreateNewMonthBaseline(confirmed: true));

        Assert.Equal("26-10", viewModel.Header.CurrentPeriod);
        Assert.Equal(2, viewModel.SavedMonthSnapshots.Count);
        Assert.Equal("26-09", viewModel.SavedMonthSnapshots.First().Period);
        Assert.Equal(expectedLastMonthPlannedCost, Assert.Single(viewModel.ForecastLines).LastMonthPlannedCost);
        Assert.Equal(expectedLastMonthForecast, Assert.Single(viewModel.ForecastLines).LastMonthForecast);
        Assert.Equal(4, viewModel.AuditEvents.Count);
        Assert.False(viewModel.IsDirty);
        Assert.Equal(1, fileService.SaveCalls);
        Assert.NotNull(fileService.SavedDataset);
        Assert.Equal("26-10", fileService.SavedDataset!.Header.CurrentPeriod);
        Assert.Equal(2, fileService.SavedDataset.SavedMonthSnapshots.Count);
        Assert.Equal(4, fileService.SavedDataset.AuditEvents.Count);
    }

    [Fact]
    public void Repeated_new_month_after_a_baseline_exists_does_not_create_a_duplicate()
    {
        var fileService = new RecordingProjectFileService();
        var viewModel = CreateViewModel(fileService, includeNextPeriod: false);
        viewModel.ProjectFilePath = "new-month.json";
        viewModel.IsDirty = true;

        Assert.True(viewModel.TryCreateNewMonthBaseline(confirmed: true));
        var snapshotCount = viewModel.SavedMonthSnapshots.Count;
        var auditCount = viewModel.AuditEvents.Count;

        Assert.False(viewModel.TryCreateNewMonthBaseline(confirmed: true));

        Assert.Equal(snapshotCount, viewModel.SavedMonthSnapshots.Count);
        Assert.Equal(auditCount, viewModel.AuditEvents.Count);
        Assert.Equal(1, fileService.SaveCalls);
    }

    [Fact]
    public void Reentrant_new_month_invocation_is_rejected_while_save_is_in_progress()
    {
        var fileService = new RecordingProjectFileService();
        var viewModel = CreateViewModel(fileService);
        viewModel.ProjectFilePath = "new-month.json";
        viewModel.IsDirty = true;
        var reentrantResult = true;
        fileService.OnSave = () => reentrantResult = viewModel.TryCreateNewMonthBaseline(confirmed: true);

        Assert.True(viewModel.TryCreateNewMonthBaseline(confirmed: true));

        Assert.False(reentrantResult);
        Assert.Equal(1, fileService.SaveCalls);
        Assert.Equal(2, viewModel.SavedMonthSnapshots.Count);
    }

    private static MainWindowViewModel CreateViewModel(
        RecordingProjectFileService fileService,
        bool includeNextPeriod = true)
    {
        return new MainWindowViewModel(new MainWindowViewModelDependencies
        {
            ProjectFileService = fileService,
            UserPreferencesService = new InMemoryUserPreferencesService(),
            InitialDatasetFactory = () => CreateDataset(includeNextPeriod)
        });
    }

    private static ProjectDataset CreateDataset(bool includeNextPeriod)
    {
        var periods = new List<ForecastPeriod>
        {
            new() { Label = "26-09", StartDate = new DateOnly(2026, 3, 1) }
        };
        if (includeNextPeriod)
        {
            periods.Add(new ForecastPeriod { Label = "26-10", StartDate = new DateOnly(2026, 4, 1) });
        }

        var monthlyForecasts = new List<MonthlyForecast>
        {
            new()
            {
                PeriodLabel = "26-09",
                PeriodStartDate = new DateOnly(2026, 3, 1),
                Amount = 10m
            }
        };
        if (includeNextPeriod)
        {
            monthlyForecasts.Add(new MonthlyForecast
            {
                PeriodLabel = "26-10",
                PeriodStartDate = new DateOnly(2026, 4, 1),
                Amount = 20m
            });
        }

        return new ProjectDataset
        {
            Header = new ProjectHeader
            {
                ProjectTitle = "New month fixture",
                CurrentPeriod = "26-09"
            },
            ForecastPeriods = periods,
            ForecastLines =
            [
                new ForecastLine
                {
                    RowNumber = 1,
                    TaskNumber = "TASK-1",
                    ResourceName = "Resource A",
                    ProjectCode = "Category A",
                    ReportingCategoryOverride = "Category A",
                    Budget = 100m,
                    MonthlyForecasts = monthlyForecasts
                }
            ],
            SavedMonthSnapshots =
            [
                new SavedMonthSnapshot
                {
                    Period = "26-08",
                    SavedAt = new DateTime(2026, 2, 1)
                }
            ],
            AuditEvents =
            [
                new AuditEvent
                {
                    AuditId = "existing-audit",
                    EntityType = "Fixture",
                    EntityId = "1",
                    Reason = "Existing state"
                }
            ]
        };
    }

    private static CapturedState CaptureState(MainWindowViewModel viewModel)
    {
        var line = Assert.Single(viewModel.ForecastLines);
        return new CapturedState(
            viewModel.Header.CurrentPeriod,
            line.MonthlyForecasts.Select(forecast => forecast.Amount).ToArray(),
            line.LastMonthPlannedCost,
            line.LastMonthForecast,
            viewModel.SavedMonthSnapshots.Select(snapshot => snapshot.Period).ToArray(),
            viewModel.AuditEvents.Select(audit => audit.AuditId).ToArray(),
            viewModel.SelectedForecastLine,
            viewModel.SelectedResourceSummary,
            viewModel.IsDirty);
    }

    private static void AssertStateUnchanged(CapturedState before, MainWindowViewModel viewModel)
    {
        var line = Assert.Single(viewModel.ForecastLines);
        Assert.Equal(before.CurrentPeriod, viewModel.Header.CurrentPeriod);
        Assert.Equal(before.ForecastAmounts, line.MonthlyForecasts.Select(forecast => forecast.Amount));
        Assert.Equal(before.LastMonthPlannedCost, line.LastMonthPlannedCost);
        Assert.Equal(before.LastMonthForecast, line.LastMonthForecast);
        Assert.Equal(before.SnapshotPeriods, viewModel.SavedMonthSnapshots.Select(snapshot => snapshot.Period));
        Assert.Equal(before.AuditIds, viewModel.AuditEvents.Select(audit => audit.AuditId));
        Assert.Same(before.SelectedForecastLine, viewModel.SelectedForecastLine);
        Assert.Same(before.SelectedResourceSummary, viewModel.SelectedResourceSummary);
        Assert.Equal(before.IsDirty, viewModel.IsDirty);
    }

    private sealed record CapturedState(
        string CurrentPeriod,
        decimal[] ForecastAmounts,
        decimal LastMonthPlannedCost,
        decimal LastMonthForecast,
        string[] SnapshotPeriods,
        string[] AuditIds,
        ForecastLine? SelectedForecastLine,
        ResourceSummary? SelectedResourceSummary,
        bool IsDirty);

    private sealed class RecordingProjectFileService : IProjectFileService
    {
        public int SaveCalls { get; private set; }
        public bool ThrowOnSave { get; init; }
        public Action? OnSave { get; set; }
        public ProjectDataset? SavedDataset { get; private set; }

        public ProjectDataset Load(string path) => new();

        public ProjectFileRevision SaveWithRevision(
            string path,
            ProjectDataset dataset,
            ProjectFileRevision? expectedRevision,
            string operation = "Save project")
        {
            SaveCalls++;
            OnSave?.Invoke();
            if (ThrowOnSave)
            {
                throw new IOException("forced new-month save failure");
            }

            SavedDataset = dataset;
            return new ProjectFileRevision(path, 0, "saved");
        }

        public string CreateBackup(string path) => string.Empty;
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
}
