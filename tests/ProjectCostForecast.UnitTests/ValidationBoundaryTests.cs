using System.IO;
using System.Text.Json;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using ProjectCostForecast.App.ViewModels;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class ValidationBoundaryTests
{
    [Fact]
    public void Validation_report_uses_stable_codes_and_operation_remedy_text()
    {
        var dataset = CreateValidDataset();
        dataset.Header.CurrentPeriod = "26-13";
        dataset.ForecastLines[0].Budget = -1m;
        dataset.ForecastLines.Add(new ForecastLine
        {
            RowNumber = 2,
            TaskNumber = "TASK-1",
            ResourceName = "Resource A",
            ProjectCode = "Category A",
            TransactionProjectCode = "PROJECT-1"
        });
        dataset.Transactions[0].Units = -1m;
        dataset.Transactions.Add(new CostTransaction
        {
            RowNumber = 2,
            FyPeriod = "26-09",
            TaskNumber = "TASK-1",
            ProjectCode = "PROJECT-1",
            ManualName = "Resource A",
            Units = -1m,
            UnitRate = 100m,
            Amount = 100m
        });

        var report = new ValidationService().ValidateReport(dataset);
        var codes = report.Errors.Select(issue => issue.Code).ToHashSet(StringComparer.Ordinal);

        Assert.Contains(ValidationIssueCodes.CurrentPeriodInvalid, codes);
        Assert.Contains(ValidationIssueCodes.ForecastLineBudgetNegative, codes);
        Assert.Contains(ValidationIssueCodes.ForecastLineDuplicateIdentity, codes);
        Assert.Contains(ValidationIssueCodes.TransactionUnitsNegative, codes);
        Assert.Contains(ValidationIssueCodes.TransactionDuplicateIdentity, codes);

        var message = report.BuildBlockingMessage("Save project");
        Assert.Contains("Save project cannot continue", message, StringComparison.Ordinal);
        Assert.Contains("retry save project", message, StringComparison.Ordinal);
        Assert.Contains(ValidationIssueCodes.CurrentPeriodInvalid, message, StringComparison.Ordinal);
        Assert.Contains("Correct", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Warning_does_not_block_a_valid_save()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Root, "warning-project.json");
        var dataset = CreateValidDataset();
        dataset.Transactions[0].Units = 0m;

        var report = new ValidationService().ValidateReport(dataset);

        Assert.False(report.HasErrors);
        Assert.Contains(report.Warnings, issue => issue.Code == ValidationIssueCodes.TransactionUnitsNonPositiveWithAmount);
        new ProjectFileService().Save(path, dataset);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Invalid_period_dates_and_financial_bounds_are_blocking_errors()
    {
        var dataset = CreateValidDataset();
        dataset.ForecastPeriods[0].StartDate = new DateOnly(2026, 4, 1);
        dataset.ForecastLines[0].MonthlyForecasts[0].Amount = ValidationService.MaximumFinancialValue + 1m;
        dataset.Transactions[0].Amount = ValidationService.MaximumFinancialValue + 1m;

        var report = new ValidationService().ValidateReport(dataset);

        Assert.Contains(report.Errors, issue => issue.Code == ValidationIssueCodes.ForecastPeriodDateMismatch);
        Assert.Contains(report.Errors, issue => issue.Code == ValidationIssueCodes.ForecastLineValueOutOfRange);
        Assert.Contains(report.Errors, issue => issue.Code == ValidationIssueCodes.TransactionValueOutOfRange);
        Assert.True(report.HasErrors);
    }

    [Fact]
    public void Project_file_save_rejects_invalid_state_before_creating_a_file()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Root, "invalid-project.json");
        var dataset = CreateValidDataset();
        dataset.ForecastLines[0].ResourceName = string.Empty;

        var exception = Assert.Throws<ProjectValidationException>(() =>
        {
            new ProjectFileService().Save(path, dataset);
        });

        Assert.Contains(ValidationIssueCodes.ForecastLineResourceNameRequired, exception.Message, StringComparison.Ordinal);
        Assert.Contains("Save project", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Project_file_load_rejects_invalid_state_before_returning_a_dataset()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Root, "invalid-project.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new ProjectDataset
        {
            Header = new ProjectHeader
            {
                ProjectTitle = "Invalid load fixture",
                CurrentPeriod = "not-a-period"
            }
        }));

        var exception = Assert.Throws<ProjectValidationException>(() =>
        {
            new ProjectFileService().Load(path);
        });

        Assert.Contains(ValidationIssueCodes.CurrentPeriodInvalid, exception.Message, StringComparison.Ordinal);
        Assert.Contains("Open project", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Save_boundary_blocks_invalid_state_without_calling_file_service_or_clearing_dirty_state()
    {
        var fileService = new RecordingProjectFileService();
        var viewModel = CreateViewModel(fileService);
        viewModel.ProjectFilePath = "validation-save.json";
        viewModel.ForecastLines.Single().ProjectCode = string.Empty;
        viewModel.IsDirty = true;

        var result = viewModel.ConfirmClose(CloseDecision.Save);

        Assert.False(result);
        Assert.True(viewModel.IsDirty);
        Assert.Equal(0, fileService.SaveCalls);
        Assert.Contains(ValidationIssueCodes.ForecastLineProjectCodeRequired, viewModel.StatusText, StringComparison.Ordinal);
        Assert.Contains("Save project", viewModel.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public void New_month_boundary_blocks_invalid_state_before_rollover_or_save()
    {
        var fileService = new RecordingProjectFileService();
        var viewModel = CreateViewModel(fileService);
        viewModel.ProjectFilePath = "validation-month.json";
        viewModel.Header.CurrentPeriod = "bad-period";
        viewModel.IsDirty = true;
        var beforePeriod = viewModel.Header.CurrentPeriod;
        var beforeSnapshots = viewModel.SavedMonthSnapshots.Select(snapshot => snapshot.Period).ToArray();

        var result = viewModel.TryCreateNewMonthBaseline(confirmed: true, showError: false);

        Assert.False(result);
        Assert.Equal(beforePeriod, viewModel.Header.CurrentPeriod);
        Assert.Equal(beforeSnapshots, viewModel.SavedMonthSnapshots.Select(snapshot => snapshot.Period));
        Assert.Equal(0, fileService.SaveCalls);
        Assert.True(viewModel.IsDirty);
        Assert.Contains(ValidationIssueCodes.CurrentPeriodInvalid, viewModel.StatusText, StringComparison.Ordinal);
        Assert.Contains("New month", viewModel.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_boundary_blocks_invalid_rows_without_changing_the_active_project()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Root, "invalid-import.csv");
        File.WriteAllText(path, "FY Period,Task Number,Amount,Manual Name\n26-99,TASK-NEW,10,New Resource\n");

        var fileService = new RecordingProjectFileService();
        var viewModel = CreateViewModel(fileService);
        var transactionCount = viewModel.Transactions.Count;
        var forecastLineCount = viewModel.ForecastLines.Count;
        var auditCount = viewModel.AuditEvents.Count;
        viewModel.IsDirty = false;

        viewModel.ImportTransactionFile(path, showError: false);

        Assert.Equal(transactionCount, viewModel.Transactions.Count);
        Assert.Equal(forecastLineCount, viewModel.ForecastLines.Count);
        Assert.Equal(auditCount, viewModel.AuditEvents.Count);
        Assert.False(viewModel.IsDirty);
        Assert.Equal(0, fileService.SaveCalls);
        Assert.Contains(ValidationIssueCodes.TransactionFyPeriodInvalid, viewModel.StatusText, StringComparison.Ordinal);
        Assert.Contains("Import transactions", viewModel.StatusText, StringComparison.Ordinal);
    }

    private static MainWindowViewModel CreateViewModel(RecordingProjectFileService fileService)
    {
        return new MainWindowViewModel(new MainWindowViewModelDependencies
        {
            ProjectFileService = fileService,
            UserPreferencesService = new InMemoryUserPreferencesService(),
            InitialDatasetFactory = CreateValidDataset
        });
    }

    private static ProjectDataset CreateValidDataset()
    {
        return new ProjectDataset
        {
            Header = new ProjectHeader
            {
                ProjectTitle = "Validation fixture",
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
                    MonthlyForecasts =
                    [
                        new MonthlyForecast { PeriodLabel = "26-09", PeriodStartDate = new DateOnly(2026, 3, 1), Amount = 25m },
                        new MonthlyForecast { PeriodLabel = "26-10", PeriodStartDate = new DateOnly(2026, 4, 1), Amount = 50m }
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
                    Units = 1m,
                    UnitRate = 100m,
                    Amount = 100m
                }
            ]
        };
    }

    private sealed class RecordingProjectFileService : IProjectFileService
    {
        public int SaveCalls { get; private set; }

        public ProjectDataset Load(string path) => CreateValidDataset();

        public void Save(string path, ProjectDataset dataset)
        {
            SaveCalls++;
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

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Root = Path.Combine(Path.GetTempPath(), "ProjectCostForecast.ValidationBoundaryTests", Guid.NewGuid().ToString("N"));
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
