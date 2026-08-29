using System.Text;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using ProjectCostForecast.App.ViewModels;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna15ImportExportWorkflowTests
{
    [Fact]
    public void Import_file_picker_cancellation_leaves_the_active_session_unchanged()
    {
        var interaction = new RecordingImportExportInteraction();
        var viewModel = CreateViewModel(interaction);
        var statusBefore = viewModel.StatusText;

        viewModel.ImportCsvCommand.Execute(null);

        Assert.Equal(1, interaction.OpenFileCalls);
        Assert.Empty(viewModel.Transactions);
        Assert.Equal(statusBefore, viewModel.StatusText);
        Assert.Empty(interaction.Errors);
        Assert.Empty(interaction.InformationMessages);
    }

    [Fact]
    public void Unsupported_import_is_reported_through_the_headless_interaction()
    {
        using var directory = new TemporaryDirectory();
        var interaction = new RecordingImportExportInteraction
        {
            OpenPath = Path.Combine(directory.Root, "transactions.txt")
        };
        var viewModel = CreateViewModel(interaction);

        viewModel.ImportCsvCommand.Execute(null);

        Assert.Empty(viewModel.Transactions);
        Assert.Contains("supported import files", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
        var message = Assert.Single(interaction.InformationMessages);
        Assert.Equal("Import failed", message.Title);
        Assert.Empty(interaction.Errors);
    }

    [Fact]
    public void Malformed_import_is_reported_without_exposing_partial_rows()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Root, "malformed.csv");
        File.WriteAllText(
            path,
            "FY Period,Task Number,Amount\n26-09,TASK-NEW,\"42\n",
            Encoding.UTF8);
        var interaction = new RecordingImportExportInteraction { OpenPath = path };
        var viewModel = CreateViewModel(interaction);

        viewModel.ImportCsvCommand.Execute(null);

        Assert.Empty(viewModel.Transactions);
        Assert.Contains("Import failed", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
        var error = Assert.Single(interaction.Errors);
        Assert.Equal("Import failed", error.Title);
    }

    [Fact]
    public void Import_validation_failure_uses_the_import_error_boundary_without_mutating_rows()
    {
        using var directory = new TemporaryDirectory();
        var path = WriteTransactionFile(directory, new CostTransaction
        {
            Amount = 42m,
            ResourceDescription = "Invalid row"
        });
        var interaction = new RecordingImportExportInteraction { OpenPath = path };
        var viewModel = CreateViewModel(interaction);

        viewModel.ImportCsvCommand.Execute(null);

        Assert.Empty(viewModel.Transactions);
        Assert.Equal("Import transactions blocked", Assert.Single(interaction.Errors).Title);
    }

    [Fact]
    public void Mapping_cancellation_commits_no_transaction_or_mapping_state()
    {
        using var directory = new TemporaryDirectory();
        var path = WriteTransactionFile(directory, CreateUnresolvedTransaction());
        var interaction = new RecordingImportExportInteraction
        {
            CanShowCostCenterMapping = true,
            MappingResult = CancelledMapping
        };
        var viewModel = CreateViewModel(interaction);
        var initialForecastLineCount = viewModel.ForecastLines.Count;
        var dirtyBefore = viewModel.IsDirty;

        viewModel.ImportTransactionFile(path);

        Assert.Equal(1, interaction.MappingCalls);
        Assert.Empty(viewModel.Transactions);
        Assert.Equal(initialForecastLineCount, viewModel.ForecastLines.Count);
        Assert.Empty(viewModel.UnmatchedImportCombinations);
        Assert.DoesNotContain(viewModel.AuditEvents, item => item.EntityType == "TransactionImport");
        Assert.Equal(dirtyBefore, viewModel.IsDirty);
    }

    [Fact]
    public void Accepted_mapping_is_applied_only_when_the_import_commits()
    {
        using var directory = new TemporaryDirectory();
        var path = WriteTransactionFile(directory, CreateUnresolvedTransaction());
        var interaction = new RecordingImportExportInteraction
        {
            CanShowCostCenterMapping = true,
            MappingResult = new CostCenterMappingPromptResult(
                true,
                "Contractors",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                Array.Empty<string>())
        };
        var viewModel = CreateViewModel(interaction);

        viewModel.ImportTransactionFile(path);

        var imported = Assert.Single(viewModel.Transactions);
        Assert.Equal("Contractors", imported.ManualName);
        Assert.Equal(1, interaction.MappingCalls);
        Assert.Contains(viewModel.ForecastLines, line => line.ResourceName == "Contractors");
        Assert.True(viewModel.IsDirty);
    }

    [Fact]
    public void Preview_cancellation_routes_the_staged_rows_to_unmatched_without_importing_them()
    {
        using var directory = new TemporaryDirectory();
        var path = WriteTransactionFile(directory, CreateAutoMappedTransaction());
        var interaction = new RecordingImportExportInteraction
        {
            CanShowAutoCreatePreview = true,
            CanShowUnmatchedImports = true,
            PreviewResultFactory = prompt => new ImportAutoCreatePreviewResult(
                false,
                prompt.ShowPreviewNextTime,
                prompt.PreviewItems)
        };
        var viewModel = CreateViewModel(interaction);
        var initialForecastLineCount = viewModel.ForecastLines.Count;

        viewModel.ImportTransactionFile(path);

        Assert.Equal(1, interaction.PreviewCalls);
        Assert.Equal(1, interaction.ShowUnmatchedCalls);
        Assert.Empty(viewModel.Transactions);
        Assert.Equal(initialForecastLineCount, viewModel.ForecastLines.Count);
        var unmatched = Assert.Single(viewModel.UnmatchedImportCombinations);
        Assert.Equal("TASK-PREVIEW", unmatched.TaskNumber);
        Assert.Contains(viewModel.AuditEvents, item =>
            item.EntityType == "TransactionImport"
            && item.FieldName == "Cancelled"
            && item.EntityId == "AutoCreatePreview");
    }

    [Fact]
    public void Accepted_preview_edits_are_applied_to_the_committed_transaction_and_forecast_line()
    {
        using var directory = new TemporaryDirectory();
        var path = WriteTransactionFile(directory, CreateAutoMappedTransaction());
        var interaction = new RecordingImportExportInteraction
        {
            CanShowAutoCreatePreview = true,
            PreviewResultFactory = prompt =>
            {
                var item = Assert.Single(prompt.PreviewItems);
                item.ManualName = "Reviewed Person";
                return new ImportAutoCreatePreviewResult(true, prompt.ShowPreviewNextTime, prompt.PreviewItems);
            }
        };
        var viewModel = CreateViewModel(interaction);

        viewModel.ImportTransactionFile(path);

        var imported = Assert.Single(viewModel.Transactions);
        Assert.Equal("Reviewed Person", imported.ManualName);
        Assert.Contains(viewModel.ForecastLines, line => line.ResourceName == "Reviewed Person");
        Assert.Empty(viewModel.UnmatchedImportCombinations);
    }

    [Fact]
    public void Export_cancellation_and_failure_are_headless_and_do_not_mutate_the_project()
    {
        using var directory = new TemporaryDirectory();
        var transaction = CreateAutoMappedTransaction();

        var cancelledInteraction = new RecordingImportExportInteraction();
        var cancelledViewModel = CreateViewModel(cancelledInteraction);
        cancelledViewModel.Transactions.Add(transaction);
        var cancelledCount = cancelledViewModel.Transactions.Count;

        cancelledViewModel.ExportTransactionsCommand.Execute(null);

        Assert.Equal(1, cancelledInteraction.SaveFileCalls);
        Assert.Equal(cancelledCount, cancelledViewModel.Transactions.Count);
        Assert.Empty(cancelledInteraction.Errors);

        var failureInteraction = new RecordingImportExportInteraction
        {
            SavePath = Path.Combine(directory.Root, "missing", "transactions.csv")
        };
        var failureViewModel = CreateViewModel(failureInteraction);
        failureViewModel.Transactions.Add(CreateAutoMappedTransaction());
        var failureCount = failureViewModel.Transactions.Count;

        failureViewModel.ExportTransactionsCommand.Execute(null);

        Assert.Equal(failureCount, failureViewModel.Transactions.Count);
        Assert.Contains("Export failed", failureViewModel.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Export failed", Assert.Single(failureInteraction.Errors).Title);
    }

    [Fact]
    public void Project_io_contains_no_direct_file_dialog_dependency()
    {
        var path = Path.Combine(
            Luna11TestSupport.RepositoryRoot,
            "src",
            "ProjectCostForecast.App",
            "ViewModels",
            "MainWindowViewModel.ProjectIO.cs");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("OpenFileDialog", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveFileDialog", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ImportAutoCreatePreviewWindow", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CostCenterMappingWindow", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UnmatchedImportWindow", source, StringComparison.Ordinal);
    }

    private static MainWindowViewModel CreateViewModel(
        RecordingImportExportInteraction interaction,
        bool showPreview = true,
        Func<ProjectDataset>? initialDatasetFactory = null)
    {
        return new MainWindowViewModel(new MainWindowViewModelDependencies
        {
            ImportExportInteraction = interaction,
            UserPreferencesService = new InMemoryUserPreferencesService
            {
                Preferences = new AppUserPreferences { ShowImportAutoCreatePreview = showPreview }
            },
            InitialDatasetFactory = initialDatasetFactory ?? CreateDataset
        });
    }

    private static ProjectDataset CreateDataset()
    {
        return new ProjectDataset
        {
            Header = new ProjectHeader
            {
                ProjectTitle = "Import interaction fixture",
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
            ForecastLines =
            [
                new ForecastLine
                {
                    RowNumber = 1,
                    TaskNumber = "TASK-1",
                    ResourceName = "Resource A",
                    ProjectCode = "PROJECT-1",
                    TransactionProjectCode = "PROJECT-1",
                    MonthlyForecasts =
                    [
                        new MonthlyForecast
                        {
                            PeriodLabel = "26-09",
                            PeriodStartDate = new DateOnly(2026, 3, 1)
                        }
                    ]
                }
            ],
            Transactions = [],
            CostCenterNameMappings = []
        };
    }

    private static CostTransaction CreateUnresolvedTransaction()
    {
        return new CostTransaction
        {
            FyPeriod = "26-09",
            TaskNumber = "TASK-MAPPING",
            Period = 1,
            Amount = 125m,
            ProjectCode = "PROJECT-MAPPING",
            ResourceCode = "255",
            ResourceDescription = "Contractors Payments",
            Who = "Contractor",
            Source = "TC"
        };
    }

    private static CostTransaction CreateAutoMappedTransaction()
    {
        return new CostTransaction
        {
            FyPeriod = "26-09",
            TaskNumber = "TASK-PREVIEW",
            Period = 1,
            Amount = 200m,
            ProjectCode = "PROJECT-PREVIEW",
            ResourceCode = "256",
            ResourceDescription = "Preview Person",
            Who = "Preview Person",
            Source = "TC"
        };
    }

    private static string WriteTransactionFile(TemporaryDirectory directory, CostTransaction transaction)
    {
        var path = Path.Combine(directory.Root, "transactions.csv");
        new CsvTransactionService().ExportTransactions(path, [transaction]);
        return path;
    }

    private static CostCenterMappingPromptResult CancelledMapping => new(
        false,
        string.Empty,
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        Array.Empty<string>());

    private sealed class RecordingImportExportInteraction : IImportExportInteraction
    {
        public string? OpenPath { get; init; }
        public string? SavePath { get; init; }
        public bool CanShowCostCenterMapping { get; init; }
        public bool CanShowAutoCreatePreview { get; init; }
        public bool CanShowUnmatchedImports { get; init; }
        public CostCenterMappingPromptResult MappingResult { get; init; } = new(
            true,
            "Imported resource",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Array.Empty<string>());
        public Func<ImportAutoCreatePreviewPrompt, ImportAutoCreatePreviewResult>? PreviewResultFactory { get; init; }
        public int OpenFileCalls { get; private set; }
        public int SaveFileCalls { get; private set; }
        public int MappingCalls { get; private set; }
        public int PreviewCalls { get; private set; }
        public int ShowUnmatchedCalls { get; private set; }
        public List<(string Title, string Message)> Errors { get; } = [];
        public List<(string Title, string Message)> InformationMessages { get; } = [];

        public string? PickOpenFile(string title, string filter)
        {
            OpenFileCalls++;
            return OpenPath;
        }

        public string? PickSaveFile(string title, string filter, string suggestedFileName)
        {
            SaveFileCalls++;
            return SavePath;
        }

        public CostCenterMappingPromptResult ChooseCostCenterMapping(CostCenterMappingPrompt prompt)
        {
            MappingCalls++;
            return MappingResult;
        }

        public ImportAutoCreatePreviewResult ReviewAutoCreatePreview(ImportAutoCreatePreviewPrompt prompt)
        {
            PreviewCalls++;
            return PreviewResultFactory?.Invoke(prompt)
                ?? new ImportAutoCreatePreviewResult(true, prompt.ShowPreviewNextTime, prompt.PreviewItems);
        }

        public void ShowUnmatchedImports(IReadOnlyCollection<UnmatchedImportCombination> items)
        {
            ShowUnmatchedCalls++;
        }

        public void ShowInformation(string title, string message)
        {
            InformationMessages.Add((title, message));
        }

        public void ShowError(string title, string message)
        {
            Errors.Add((title, message));
        }
    }

    private sealed class InMemoryUserPreferencesService : IUserPreferencesService
    {
        public AppUserPreferences Preferences { get; set; } = new();

        public AppUserPreferences Load() => Preferences;

        public void Save(AppUserPreferences preferences)
        {
            Preferences = preferences;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Root = Path.Combine(Path.GetTempPath(), "ProjectCostForecast.UnitTests", Guid.NewGuid().ToString("N"));
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
