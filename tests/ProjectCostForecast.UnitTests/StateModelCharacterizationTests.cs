using System.Text.Json;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using ProjectCostForecast.App.ViewModels;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class StateModelCharacterizationTests
{
    [Fact]
    public void Project_file_round_trip_preserves_editable_collections_and_nested_items()
    {
        using var directory = new Luna11TemporaryDirectory();
        var path = Path.Combine(directory.Root, "state-model.json");
        var dataset = CreateDataset();
        var forecastLine = Assert.Single(dataset.ForecastLines);
        forecastLine.CostToDate = 123m;
        forecastLine.TotalForecastCtc = 456m;
        forecastLine.MonthlyForecasts[0].ActualCostAmount = 999m;
        forecastLine.SetResolvedTaskMetadata("Derived task name", "Derived category");

        var scheduleActivity = Assert.Single(dataset.Schedule.Activities);
        scheduleActivity.EarlyStart = new DateOnly(2026, 3, 2);
        scheduleActivity.ScheduleNote = "Derived schedule note";
        dataset.BudgetLines[0].IsActive = true;

        new ProjectFileService().Save(path, dataset);
        var json = File.ReadAllText(path);
        var reopened = new ProjectFileService().Load(path);

        Assert.Equal("State model fixture", reopened.Header.ProjectTitle);
        Assert.Equal(["26-09", "26-10"], reopened.ForecastPeriods.Select(period => period.Label));
        Assert.Equal("Construction", Assert.Single(reopened.Phases).Name);
        Assert.Equal(500m, Assert.Single(reopened.FiscalYearBudgets).Budget);
        Assert.Equal("LTP_AP", reopened.ActiveBudgetLineKey);
        Assert.Equal(500m, Assert.Single(Assert.Single(reopened.BudgetLines).Amounts).Amount);

        var reopenedLine = Assert.Single(reopened.ForecastLines);
        Assert.Equal("TASK-1", reopenedLine.TaskNumber);
        Assert.Equal("Resource A", reopenedLine.ResourceName);
        Assert.Equal("PROJECT-1", reopenedLine.TransactionProjectCode);
        Assert.Equal(500m, reopenedLine.Budget);
        Assert.Equal("Variance comment", reopenedLine.CommentsOnTotalBudgetVariance);
        Assert.Equal(42m, reopenedLine.MonthlyForecasts[0].Amount);
        Assert.True(reopenedLine.MonthlyForecasts[0].IsLocked);
        Assert.Equal(123m, reopenedLine.CostToDate);
        Assert.Equal(456m, reopenedLine.TotalForecastCtc);

        Assert.Equal("TASK-1", Assert.Single(reopened.ProjectTaskCodes).SystemCode);
        Assert.Equal("Category A", Assert.Single(reopened.ProjectCategories).Name);
        var reopenedResource = Assert.Single(reopened.ManagementResources);
        Assert.Equal(120m, reopenedResource.HourlyRate);
        Assert.Equal(115m, reopenedResource.CalculatedHourlyRate);
        Assert.Equal(35m, reopenedResource.MonthlyAllocations.Single(item => item.PeriodLabel == "26-09").Percentage);
        Assert.Contains(reopenedResource.MonthlyAllocations, item => item.PeriodLabel == "26-10");
        Assert.Equal(100m, Assert.Single(reopened.Transactions).Amount);
        Assert.Equal("Manual Resource", Assert.Single(reopened.UnmatchedImportCombinations).ManualName);
        Assert.Equal(75m, Assert.Single(reopened.ContingencyEntries).RemainingContingency);
        Assert.Equal("Category A", Assert.Single(reopened.CategorySummaries).ProjectCode);
        Assert.Equal("mapping-1", Assert.Single(reopened.CostCenterNameMappings).Key);

        var snapshot = Assert.Single(reopened.SavedMonthSnapshots);
        Assert.Equal("26-08", snapshot.Period);
        Assert.Equal("TASK-1", Assert.Single(snapshot.ForecastLines).TaskNumber);
        Assert.Equal("audit-1", Assert.Single(reopened.AuditEvents).AuditId);

        var workspace = Assert.Single(reopened.WorkspaceViews);
        Assert.Equal("Forecast", workspace.ContentKey);
        Assert.Equal("canvas-1", Assert.Single(workspace.ReportCanvasObjects).Id);
        Assert.Equal(["Forecast", "Schedule"], reopened.WorkspaceTabOrder);
        Assert.Equal([2026], reopened.SelectedCtcMonthForecastYears);
        Assert.True(reopened.ShowCtcMonthForecastYearTotals);
        Assert.Equal("#123456", reopened.ForecastGroupHeaderColorHexes["Task"]);

        Assert.Equal("CAL-1", reopened.Schedule.DefaultCalendarId);
        Assert.Equal("A-1", Assert.Single(reopened.Schedule.Activities).Id);
        Assert.Equal("A-1", Assert.Single(reopened.Schedule.Links).PredecessorId);
        Assert.Equal("Baseline 1", Assert.Single(reopened.Schedule.Baselines).Name);

        Assert.DoesNotContain("ActualCostAmount", json, StringComparison.Ordinal);
        Assert.DoesNotContain("EarlyStart", json, StringComparison.Ordinal);
        Assert.DoesNotContain("ScheduleNote", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Total\":", json, StringComparison.Ordinal);
        Assert.DoesNotContain("IconPreview", json, StringComparison.Ordinal);
        Assert.Null(Assert.Single(reopened.Schedule.Activities).EarlyStart);
        Assert.Equal(0m, reopenedLine.MonthlyForecasts[0].ActualCostAmount);
        Assert.False(Assert.Single(reopened.BudgetLines).IsActive);
    }

    [Fact]
    public void Monthly_forecast_item_edit_recalculates_and_marks_the_project_dirty()
    {
        var viewModel = CreateViewModel();
        var line = Assert.Single(viewModel.ForecastLines);
        var forecast = line.MonthlyForecasts.Single(item => item.PeriodLabel == "26-10");
        var priorTotal = line.TotalForecastCtc;
        var priorAuditCount = viewModel.AuditEvents.Count;

        viewModel.IsDirty = false;
        forecast.Amount = 42m;

        Assert.True(viewModel.IsDirty);
        Assert.NotEqual(priorTotal, line.TotalForecastCtc);
        Assert.Equal(42m, forecast.Amount);
        Assert.True(viewModel.AuditEvents.Count > priorAuditCount);
        Assert.Equal("26-10", viewModel.AuditEvents.First().FieldName);
    }

    [Fact]
    public void Contingency_collection_and_item_edits_refresh_totals_and_mark_dirty()
    {
        var viewModel = CreateViewModel();
        viewModel.ContingencyEntries.Clear();
        var entry = new ContingencyEntry
        {
            ContingencyExpended = 25m,
            RemainingContingency = 75m,
            ProposedExpenditure = 10m
        };

        viewModel.IsDirty = false;
        viewModel.ContingencyEntries.Add(entry);
        Assert.True(viewModel.IsDirty);
        Assert.Equal(75m, viewModel.TotalContingencyRemaining);

        viewModel.IsDirty = false;
        entry.RemainingContingency = 60m;

        Assert.True(viewModel.IsDirty);
        Assert.Equal(60m, viewModel.TotalContingencyRemaining);
    }

    [Fact]
    public void Budget_amount_item_edit_syncs_budget_views_and_marks_dirty()
    {
        var viewModel = CreateViewModel();
        var budgetLine = viewModel.BudgetLines.Single(line => line.Key == MainWindowViewModel.LtpApBudgetLineKey);
        var amount = budgetLine.Amounts.First();
        var newAmount = amount.Amount + 25m;

        viewModel.IsDirty = false;
        amount.Amount = newAmount;

        Assert.True(viewModel.IsDirty);
        Assert.Equal(newAmount, budgetLine.Total);
        Assert.Equal(
            newAmount,
            viewModel.FiscalYearReportLines.Single(line => line.FiscalYear == amount.FiscalYear).Budget);
    }

    [Fact]
    public void Schedule_calculation_owns_derived_outputs_without_persisting_them()
    {
        var calendar = new ScheduleCalendar
        {
            Id = "CAL-1",
            WorkingDays = [false, true, true, true, true, true, false]
        };
        var schedule = new ScheduleData
        {
            ProjectStart = new DateOnly(2026, 3, 2),
            DefaultCalendarId = calendar.Id,
            Calendars = [calendar],
            Activities =
            [
                new ScheduleActivity
                {
                    Id = "A-1",
                    Name = "Design",
                    CalendarId = calendar.Id,
                    DurationDays = 2
                }
            ]
        };

        new SchedulingService().Recalculate(schedule);

        var activity = Assert.Single(schedule.Activities);
        Assert.Equal(new DateOnly(2026, 3, 2), activity.EarlyStart);
        Assert.NotNull(activity.EarlyFinish);

        using var directory = new Luna11TemporaryDirectory();
        var path = Path.Combine(directory.Root, "schedule-state.json");
        new ProjectFileService().Save(path, new ProjectDataset
        {
            Header = new ProjectHeader { ProjectTitle = "Schedule state fixture" },
            Schedule = schedule
        });

        var json = File.ReadAllText(path);
        var reopened = new ProjectFileService().Load(path);
        var reopenedActivity = Assert.Single(reopened.Schedule.Activities);

        Assert.Equal("Design", reopenedActivity.Name);
        Assert.Null(reopenedActivity.EarlyStart);
        Assert.DoesNotContain("EarlyStart", json, StringComparison.Ordinal);
        Assert.DoesNotContain("TotalFloatDays", json, StringComparison.Ordinal);
        Assert.DoesNotContain("ScheduleNote", json, StringComparison.Ordinal);
    }

    private static MainWindowViewModel CreateViewModel()
    {
        return new MainWindowViewModel(new MainWindowViewModelDependencies
        {
            InitialDatasetFactory = CreateDataset,
            UserPreferencesService = new InMemoryUserPreferencesService()
        });
    }

    private static ProjectDataset CreateDataset()
    {
        var firstPeriodDate = new DateOnly(2026, 3, 1);
        var secondPeriodDate = new DateOnly(2026, 4, 1);
        var line = new ForecastLine
        {
            RowNumber = 1,
            TaskNumber = "TASK-1",
            ResourceName = "Resource A",
            ProjectCode = "Category A",
            ReportingCategoryOverride = "Category A",
            TransactionProjectCode = "PROJECT-1",
            Budget = 500m,
            LastMonthPlannedCost = 480m,
            LastMonthForecast = 125m,
            CommentsOnTotalBudgetVariance = "Variance comment",
            ResourceCommentMetrics =
            [
                new ResourceCommentMetricPreference
                {
                    Key = "CostToDate",
                    Label = "Cost to date",
                    IsVisible = true,
                    DisplayOrder = 1
                }
            ],
            MonthlyCommentHistory =
            [
                new ForecastMonthlyComment
                {
                    PeriodLabel = "26-09",
                    MonthLabel = "March",
                    ResourceName = "Resource A",
                    Text = "Comment history",
                    RecordedAt = new DateTimeOffset(2026, 3, 2, 10, 0, 0, TimeSpan.Zero)
                }
            ],
            MonthlyForecasts =
            [
                new MonthlyForecast
                {
                    PeriodLabel = "26-09",
                    PeriodStartDate = firstPeriodDate,
                    Amount = 42m,
                    IsLocked = true
                },
                new MonthlyForecast
                {
                    PeriodLabel = "26-10",
                    PeriodStartDate = secondPeriodDate,
                    Amount = 20m
                }
            ],
            TaskPhases =
            [
                new ForecastTaskPhase
                {
                    Name = "Design",
                    StartPeriodLabel = "26-09",
                    EndPeriodLabel = "26-10"
                }
            ],
            TaskCostLines =
            [
                new ForecastTaskCostLine
                {
                    Name = "Design package",
                    Amount = 50m,
                    StartPeriodLabel = "26-09",
                    EndPeriodLabel = "26-10",
                    IsAwarded = true
                }
            ]
        };

        var calendar = new ScheduleCalendar
        {
            Id = "CAL-1",
            Name = "Five day",
            WorkingDays = [false, true, true, true, true, true, false]
        };

        return new ProjectDataset
        {
            Header = new ProjectHeader
            {
                ProjectTitle = "State model fixture",
                ReportTitle = "State model report",
                CurrentPeriod = "26-09",
                SourceWorkbook = "source.xlsm",
                ImportNotes = "Characterization fixture"
            },
            Phases =
            [
                new PhaseItem
                {
                    Name = "Construction",
                    Start = firstPeriodDate,
                    End = secondPeriodDate
                }
            ],
            ForecastPeriods =
            [
                new ForecastPeriod { Label = "26-09", StartDate = firstPeriodDate },
                new ForecastPeriod { Label = "26-10", StartDate = secondPeriodDate }
            ],
            FiscalYearBudgets =
            [
                new FiscalYearBudget { FiscalYear = "FY26", Budget = 500m }
            ],
            BudgetLines =
            [
                new FiscalYearBudgetLine
                {
                    Key = MainWindowViewModel.LtpApBudgetLineKey,
                    Name = "LTP/AP",
                    Amounts =
                    [
                        new FiscalYearBudgetAmount { FiscalYear = "FY26", Amount = 500m }
                    ]
                }
            ],
            ActiveBudgetLineKey = MainWindowViewModel.LtpApBudgetLineKey,
            ForecastLines = [line],
            ProjectTaskCodes =
            [
                new ProjectTaskCode
                {
                    SystemCode = "TASK-1",
                    TaskName = "Design task",
                    IsManualCode = true,
                    DisplayOrder = 1,
                    IconKey = "ic_task_16.png",
                    IconColorHex = "#123456",
                    HeaderColorHex = "#654321"
                }
            ],
            ProjectCategories =
            [
                new ProjectCategory
                {
                    Name = "Category A",
                    ColorHex = "#123456",
                    IconKey = "ic_category_16.png",
                    DisplayOrder = 1
                }
            ],
            ManagementResources =
            [
                new ManagementResource
                {
                    SourceRowNumber = 7,
                    TaskNumber = "TASK-1",
                    ResourceName = "Resource A",
                    ProjectCode = "Category A",
                    HourlyRate = 120m,
                    CalculatedHourlyRate = 115m,
                    MonthlyHours = 160m,
                    MonthlyAllocations =
                    [
                        new ManagementResourceAllocation
                        {
                            PeriodLabel = "26-09",
                            PeriodStartDate = firstPeriodDate,
                            Percentage = 35m
                        }
                    ]
                }
            ],
            Transactions =
            [
                new CostTransaction
                {
                    RowNumber = 10,
                    FyPeriod = "26-09",
                    TaskNumber = "TASK-1",
                    Period = 9,
                    DocDate = firstPeriodDate,
                    Units = 1m,
                    UnitRate = 100m,
                    Amount = 100m,
                    ProjectCode = "PROJECT-1",
                    ResourceCode = "RES-1",
                    ResourceDescription = "Resource A",
                    Source = "Ledger",
                    ManualName = "Resource A"
                }
            ],
            UnmatchedImportCombinations =
            [
                new UnmatchedImportCombination
                {
                    RecordedAt = new DateTimeOffset(2026, 3, 3, 10, 0, 0, TimeSpan.Zero),
                    TaskNumber = "TASK-1",
                    ManualName = "Manual Resource",
                    ProjectCode = "PROJECT-1",
                    Category = "Category A",
                    Source = "Ledger",
                    Amount = 10m,
                    TransactionCount = 1
                }
            ],
            ContingencyEntries =
            [
                new ContingencyEntry
                {
                    Date = firstPeriodDate,
                    ContingencyExpended = 25m,
                    RemainingContingency = 75m,
                    ProposedExpenditure = 10m,
                    Reason = "Risk",
                    Status = "Open"
                }
            ],
            CategorySummaries =
            [
                new CategorySummary
                {
                    ProjectCode = "Category A",
                    TotalForecast = 456m,
                    CostToDate = 123m,
                    CurrentMonthCost = 10m,
                    PlannedCost = 456m,
                    Budget = 500m,
                    TotalBudgetVariance = 44m,
                    MonthForecastVariance = 2m
                }
            ],
            CostCenterNameMappings =
            [
                new CostCenterNameMapping
                {
                    Key = "mapping-1",
                    ResourceCode = "RES-1",
                    ResourceDescription = "Resource A",
                    ManualName = "Manual Resource",
                    UseCount = 2,
                    LastUsedAt = new DateTimeOffset(2026, 3, 3, 10, 0, 0, TimeSpan.Zero)
                }
            ],
            SavedMonthSnapshots =
            [
                new SavedMonthSnapshot
                {
                    Period = "26-08",
                    SavedAt = new DateTimeOffset(2026, 2, 28, 10, 0, 0, TimeSpan.Zero),
                    CostToDate = 100m,
                    CostToComplete = 200m,
                    FinalForecast = 300m,
                    TotalBudgetVariance = 200m,
                    ForecastLines =
                    [
                        new SavedMonthForecastLine
                        {
                            RowNumber = 1,
                            TaskNumber = "TASK-1",
                            ResourceName = "Resource A",
                            ProjectCode = "Category A",
                            CostToDate = 100m,
                            CurrentPeriodForecast = 20m,
                            CostToComplete = 200m,
                            FinalForecast = 300m,
                            Budget = 500m,
                            TotalBudgetVariance = 200m,
                            VarianceFromPreviousMonth = 5m,
                            MonthlyForecasts =
                            [
                                new SavedMonthPeriodAmount
                                {
                                    PeriodLabel = "26-08",
                                    PeriodStartDate = new DateOnly(2026, 2, 1),
                                    Amount = 20m
                                }
                            ]
                        }
                    ]
                }
            ],
            AuditEvents =
            [
                new AuditEvent
                {
                    AuditId = "audit-1",
                    EntityType = "ForecastLine",
                    EntityId = "1",
                    FieldName = "Budget",
                    OldValue = "400",
                    NewValue = "500",
                    ChangedAt = new DateTimeOffset(2026, 3, 3, 10, 0, 0, TimeSpan.Zero),
                    Reason = "Fixture"
                }
            ],
            WorkspaceViews =
            [
                new WorkspaceViewLayout
                {
                    WorkspaceKey = "Forecast",
                    ContentKey = "Forecast",
                    Name = "Forecast view",
                    IconKey = "ic_tab_forecast_16.png",
                    IconColorHex = "#123456",
                    HiddenColumnKeys = ["Comments"],
                    ColumnLayouts = [new WorkspaceColumnLayout { Key = "Budget", Width = 120, DisplayIndex = 1 }],
                    ShowZeroAsBlank = false,
                    GroupForecastLinesByTask = true,
                    ForecastGroupByKey = "Task",
                    ReportCanvasInitialized = true,
                    ReportCanvasObjects =
                    [
                        new ReportCanvasObjectLayout
                        {
                            Id = "canvas-1",
                            ObjectType = "Chart",
                            X = 10,
                            Y = 20,
                            Width = 300,
                            Height = 180,
                            ChartKind = "Forecast",
                            FromDate = firstPeriodDate,
                            ToDate = secondPeriodDate
                        }
                    ]
                }
            ],
            WorkspaceTabOrder = ["Forecast", "Schedule"],
            DetailWorkspaceTabOrder = ["Ledger Monthly Forecast"],
            ForecastGroupHeaderIconKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Task"] = "ic_task_16.png"
            },
            ForecastGroupHeaderIconColorHexes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Task"] = "#123456"
            },
            ForecastCalendarYearHeaderColorHexes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["2026"] = "#123456"
            },
            ForecastFiscalYearHeaderColorHexes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["FY26"] = "#654321"
            },
            ForecastGroupHeaderColorHexes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Task"] = "#123456"
            },
            SelectedCtcMonthForecastYears = [2026],
            ShowCtcMonthForecastYearTotals = true,
            Schedule = new ScheduleData
            {
                ProjectStart = firstPeriodDate,
                MustFinishBy = secondPeriodDate,
                DefaultCalendarId = calendar.Id,
                ActiveBaselineName = "Baseline 1",
                Calendars = [calendar],
                Activities =
                [
                    new ScheduleActivity
                    {
                        Id = "A-1",
                        Name = "Design",
                        CalendarId = calendar.Id,
                        DurationDays = 2
                    }
                ],
                Links =
                [
                    new ActivityLink
                    {
                        PredecessorId = "A-1",
                        SuccessorId = "A-1",
                        Type = ActivityLinkType.FinishToStart,
                        LagDays = 0
                    }
                ],
                Baselines =
                [
                    new ScheduleBaseline
                    {
                        Name = "Baseline 1",
                        CapturedAt = new DateTimeOffset(2026, 3, 3, 10, 0, 0, TimeSpan.Zero),
                        Entries =
                        [
                            new ScheduleBaselineEntry
                            {
                                ActivityId = "A-1",
                                Start = firstPeriodDate,
                                Finish = secondPeriodDate
                            }
                        ]
                    }
                ]
            }
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
}
