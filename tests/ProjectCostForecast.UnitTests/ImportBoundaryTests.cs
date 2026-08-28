using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using ClosedXML.Excel;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using ProjectCostForecast.App.ViewModels;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class ImportBoundaryTests
{
    [Fact]
    public void Formula_like_text_is_neutralized_only_in_export_output()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Root, "formula-export.csv");
        var transaction = new CostTransaction
        {
            FyPeriod = "26-09",
            TaskNumber = "@task",
            ProjectCode = " +category",
            ManualName = " \t=HYPERLINK(\"https://example.invalid\")",
            Narrative1 = "-not-a-formula-for-the-domain",
            Amount = -42m
        };
        var canonicalJson = JsonSerializer.Serialize(transaction);

        new CsvTransactionService().ExportTransactions(path, [transaction]);

        var output = File.ReadAllText(path);
        Assert.Contains("'@task", output, StringComparison.Ordinal);
        Assert.Contains("' +category", output, StringComparison.Ordinal);
        Assert.Contains("' \t=HYPERLINK", output, StringComparison.Ordinal);
        Assert.Contains("'-not-a-formula-for-the-domain", output, StringComparison.Ordinal);
        Assert.Contains(",-42,", output, StringComparison.Ordinal);
        Assert.Equal(canonicalJson, JsonSerializer.Serialize(transaction));
        Assert.Equal("@task", transaction.TaskNumber);
        Assert.Equal(" +category", transaction.ProjectCode);
    }

    [Fact]
    public void Multiline_csv_fields_round_trip_and_the_file_handle_is_closed()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Root, "multiline.csv");
        var movedPath = Path.Combine(directory.Root, "multiline-moved.csv");
        var comment = "First line, with comma\r\nSecond line with \"quoted text\"";
        var transaction = new CostTransaction
        {
            FyPeriod = "26-09",
            TaskNumber = "TASK-1",
            PoComments = comment,
            ManualName = "Resource A",
            Amount = 42m
        };
        var service = new CsvTransactionService();

        service.ExportTransactions(path, [transaction]);
        var imported = Assert.Single(service.Import(path, 1));

        Assert.Equal(comment, imported.PoComments);
        Assert.Equal("Resource A", imported.ManualName);
        Assert.Equal(42m, imported.Amount);
        File.Move(path, movedPath);
        Assert.True(File.Exists(movedPath));
    }

    [Theory]
    [InlineData("FY Period,Task Number,Amount\n26-09,TASK-1,\"10\n")]
    [InlineData("FY Period,Task Number,Amount\n26-09,TASK-1,10\"oops\n")]
    public void Malformed_csv_is_rejected_with_a_typed_actionable_failure(string content)
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Root, "malformed.csv");
        File.WriteAllText(path, content);

        var exception = Assert.Throws<ImportBoundaryException>(() => new CsvTransactionService().Import(path, 1));

        Assert.Equal(ImportBoundaryFailureKind.MalformedCsv, exception.FailureKind);
        Assert.Contains("Verify the file", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("10\n", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void File_row_cell_and_character_limits_are_enforced_before_a_batch_is_returned()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Root, "limited.csv");
        File.WriteAllText(
            path,
            "FY Period,Task Number,Amount\n"
            + "26-09,TASK-1,10\n"
            + "26-09,TASK-2,20\n");

        var fileException = Assert.Throws<ImportBoundaryException>(() =>
            new CsvTransactionService(new ImportBoundaryOptions { MaxFileBytes = 10 }).Import(path, 1));
        Assert.Equal(ImportBoundaryFailureKind.FileTooLarge, fileException.FailureKind);
        Assert.Equal(nameof(ImportBoundaryOptions.MaxFileBytes), fileException.LimitName);
        Assert.Contains("Select a smaller input", fileException.Message, StringComparison.Ordinal);

        var rowException = Assert.Throws<ImportBoundaryException>(() =>
            new CsvTransactionService(new ImportBoundaryOptions { MaxRowsPerWorksheet = 2 }).Import(path, 1));
        Assert.Equal(ImportBoundaryFailureKind.RowLimitExceeded, rowException.FailureKind);
        Assert.Equal(3L, rowException.ObservedValue);

        var cellException = Assert.Throws<ImportBoundaryException>(() =>
            new CsvTransactionService(new ImportBoundaryOptions { MaxCellsPerWorksheet = 5 }).Import(path, 1));
        Assert.Equal(ImportBoundaryFailureKind.CellLimitExceeded, cellException.FailureKind);
        Assert.Equal(6L, cellException.ObservedValue);

        var characterPath = Path.Combine(directory.Root, "cell-limit.csv");
        File.WriteAllText(characterPath, "Task Number\nvery-long-value\n");
        var characterException = Assert.Throws<ImportBoundaryException>(() =>
            new CsvTransactionService(new ImportBoundaryOptions { MaxCellCharacters = 4 }).Import(characterPath, 1));
        Assert.Equal(ImportBoundaryFailureKind.CellCharacterLimitExceeded, characterException.FailureKind);
    }

    [Fact]
    public void Unsupported_and_malformed_workbooks_fail_at_the_workbook_boundary()
    {
        using var directory = new TemporaryDirectory();
        var unsupportedPath = Path.Combine(directory.Root, "legacy.xls");
        File.WriteAllText(unsupportedPath, "not a supported workbook");

        var unsupported = Assert.Throws<ImportBoundaryException>(() => new CsvTransactionService().Import(unsupportedPath, 1));
        Assert.Equal(ImportBoundaryFailureKind.UnsupportedFileType, unsupported.FailureKind);
        Assert.Contains(".xlsx", unsupported.Message, StringComparison.Ordinal);

        var malformedPath = Path.Combine(directory.Root, "malformed.xlsx");
        File.WriteAllBytes(malformedPath, [0x50, 0x4B, 0x03, 0x04, 0x00]);

        var malformed = Assert.Throws<ImportBoundaryException>(() => new CsvTransactionService().Import(malformedPath, 1));
        Assert.Equal(ImportBoundaryFailureKind.MalformedWorkbook, malformed.FailureKind);
    }

    [Fact]
    public void Workbook_worksheet_row_and_cell_limits_are_checked_before_materialisation()
    {
        using var directory = new TemporaryDirectory();
        var worksheetPath = Path.Combine(directory.Root, "worksheets.xlsx");
        WriteWorkbook(worksheetPath, worksheetCount: 2, dataRowCount: 1);

        var worksheetException = Assert.Throws<ImportBoundaryException>(() =>
            new CsvTransactionService(new ImportBoundaryOptions { MaxWorksheets = 1 }).Import(worksheetPath, 1));
        Assert.Equal(ImportBoundaryFailureKind.WorksheetLimitExceeded, worksheetException.FailureKind);
        Assert.Equal(2L, worksheetException.ObservedValue);

        var rowPath = Path.Combine(directory.Root, "rows.xlsx");
        WriteWorkbook(rowPath, worksheetCount: 1, dataRowCount: 2);
        var rowException = Assert.Throws<ImportBoundaryException>(() =>
            new CsvTransactionService(new ImportBoundaryOptions { MaxRowsPerWorksheet = 2 }).Import(rowPath, 1));
        Assert.Equal(ImportBoundaryFailureKind.RowLimitExceeded, rowException.FailureKind);

        var cellException = Assert.Throws<ImportBoundaryException>(() =>
            new CsvTransactionService(new ImportBoundaryOptions { MaxCellsPerWorksheet = 3 }).Import(rowPath, 1));
        Assert.Equal(ImportBoundaryFailureKind.CellLimitExceeded, cellException.FailureKind);
    }

    [Theory]
    [InlineData(".xlsx")]
    [InlineData(".xlsm")]
    public void Existing_workbook_imports_remain_supported_and_close_the_workbook_handle(string extension)
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Root, $"supported{extension}");
        var movedPath = Path.Combine(directory.Root, $"supported-moved{extension}");
        WriteWorkbook(path, worksheetCount: 1, dataRowCount: 1);

        var imported = Assert.Single(new CsvTransactionService().Import(path, 77));

        Assert.Equal(77, imported.RowNumber);
        Assert.Equal("TASK-1", imported.TaskNumber);
        Assert.Equal("Resource A", imported.ManualName);
        Assert.Equal(42m, imported.Amount);
        File.Move(path, movedPath);
        Assert.True(File.Exists(movedPath));
    }

    [Fact]
    public void Cancelled_import_does_not_expose_a_partial_batch()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Root, "cancelled.csv");
        File.WriteAllText(path, "FY Period,Task Number,Amount\n26-09,TASK-1,10\n26-09,TASK-2,20\n");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => new CsvTransactionService().Import(path, 1, cancellation.Token));
    }

    [Fact]
    public void Malformed_import_leaves_the_active_view_model_without_partial_rows()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Root, "failed-import.csv");
        File.WriteAllText(path, "FY Period,Task Number,Amount\n26-09,TASK-1,\"10\n");
        var viewModel = CreateViewModel();
        var transactionCount = viewModel.Transactions.Count;
        viewModel.IsDirty = false;

        viewModel.ImportTransactionFile(path, showError: false);

        Assert.Equal(transactionCount, viewModel.Transactions.Count);
        Assert.False(viewModel.IsDirty);
        Assert.Contains("Import failed", viewModel.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public void Reimporting_the_same_source_follows_the_existing_skip_duplicate_policy()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Root, "duplicate-import.csv");
        File.WriteAllText(
            path,
            "FY Period,Task Number,Amount,Project Code,Resource Description,Who,Manual Name\n"
            + "26-09,TASK-1,42,PROJECT-1,Resource A,Resource A,Resource A\n");
        var viewModel = CreateViewModel();

        viewModel.ImportTransactionFile(path, showError: false);
        var importedCount = viewModel.Transactions.Count;
        Assert.True(importedCount == 1, viewModel.StatusText);
        viewModel.IsDirty = false;
        viewModel.ImportTransactionFile(path, showError: false);

        Assert.Equal(1, importedCount);
        Assert.Equal(importedCount, viewModel.Transactions.Count);
        Assert.False(viewModel.IsDirty);
        Assert.Contains("skipped 1 duplicate", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteWorkbook(string path, int worksheetCount, int dataRowCount)
    {
        using var workbook = new XLWorkbook();
        for (var worksheetIndex = 0; worksheetIndex < worksheetCount; worksheetIndex++)
        {
            var worksheet = workbook.Worksheets.Add($"Transactions {worksheetIndex + 1}");
            worksheet.Cell(1, 1).Value = "FY Period";
            worksheet.Cell(1, 2).Value = "Task Number";
            worksheet.Cell(1, 3).Value = "Amount";
            worksheet.Cell(1, 4).Value = "Manual Name";
            for (var row = 0; row < dataRowCount; row++)
            {
                var targetRow = row + 2;
                worksheet.Cell(targetRow, 1).Value = "26-09";
                worksheet.Cell(targetRow, 2).Value = $"TASK-{row + 1}";
                worksheet.Cell(targetRow, 3).Value = 42;
                worksheet.Cell(targetRow, 4).Value = "Resource A";
            }
        }

        workbook.SaveAs(path);
    }

    private static MainWindowViewModel CreateViewModel()
    {
        return new MainWindowViewModel(new MainWindowViewModelDependencies
        {
            ProjectFileService = new RecordingProjectFileService(),
            UserPreferencesService = new InMemoryUserPreferencesService(),
            InitialDatasetFactory = CreateDataset
        });
    }

    private static ProjectDataset CreateDataset()
    {
        return new ProjectDataset
        {
            Header = new ProjectHeader
            {
                ProjectTitle = "Import boundary fixture",
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
            Transactions = []
        };
    }

    private sealed class RecordingProjectFileService : IProjectFileService
    {
        public ProjectDataset Load(string path) => CreateDataset();

        public void Save(string path, ProjectDataset dataset)
        {
        }

        public string CreateBackup(string path) => string.Empty;
    }

    private sealed class InMemoryUserPreferencesService : IUserPreferencesService
    {
        public AppUserPreferences Load() => new();

        public void Save(AppUserPreferences preferences)
        {
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Root = Path.Combine(Path.GetTempPath(), "ProjectCostForecast.ImportBoundaryTests", Guid.NewGuid().ToString("N"));
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
