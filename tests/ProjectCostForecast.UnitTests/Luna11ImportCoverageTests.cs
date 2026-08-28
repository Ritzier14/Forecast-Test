using System.Reflection;
using ClosedXML.Excel;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using ProjectCostForecast.App.ViewModels;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna11ImportCoverageTests
{
    [Fact]
    public void View_model_import_and_preview_preserve_resource_and_project_attribution()
    {
        using var directory = new Luna11TemporaryDirectory();
        var importPath = Path.Combine(directory.Root, "auto-import.csv");
        File.WriteAllText(
            importPath,
            "FY-Period,Task Numb,Period,Doc Date,Units,Unit Rate,Amount,Cost Ledg,Cost Acco,Project Co,Parent Pro,Resource C,Resource D,Source,PO Numb,PO Comm,Supplier N,Narrative 1,Narrative 2,Narrative 3,Who,ECM Number" + Environment.NewLine
            + "26-11,WA57102001,11,2026-05-01,1,123,123,26PRJA,10WA402,WA571,WA57P,90001,Auto Match Person,TC,PO-AUTO,Comment Auto,Supplier Auto,AUTO/01/05/2026/1/1,AUTO COST POSTING,,Auto Match Person,ECM-AUTO-001");
        var viewModel = Luna11TestSupport.CreateSeedViewModel();
        var transactionCountBeforeImport = viewModel.Transactions.Count;

        viewModel.ImportTransactionFile(importPath);

        var autoImportedTransaction = viewModel.Transactions.Single(transaction =>
            string.Equals(transaction.EcmNumber, "ECM-AUTO-001", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(transactionCountBeforeImport + 1, viewModel.Transactions.Count);
        Assert.Equal("Auto Match Person", autoImportedTransaction.ManualName);
        Assert.Equal("Auto Match Person", autoImportedTransaction.LedgerResourceName);
        var autoCreatedForecastLine = viewModel.ForecastLines.Single(line =>
            string.Equals(line.TaskNumber, autoImportedTransaction.TaskNumber, StringComparison.OrdinalIgnoreCase)
            && string.Equals(line.ResourceName, autoImportedTransaction.LedgerResourceName, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("WA571", autoCreatedForecastLine.TransactionProjectCode);

        var anchoredManualLine = viewModel.InsertForecastLine(autoCreatedForecastLine, below: true);
        Assert.Equal(autoCreatedForecastLine.TransactionProjectCode, anchoredManualLine.TransactionProjectCode);
        viewModel.DeleteForecastLine(anchoredManualLine);
        var legacyAnchor = viewModel.ForecastLines.First(line => line.TransactionProjectCode is null);
        var legacyAnchoredManualLine = viewModel.InsertForecastLine(legacyAnchor, below: true);
        Assert.Null(legacyAnchoredManualLine.TransactionProjectCode);
        viewModel.DeleteForecastLine(legacyAnchoredManualLine);
        var unanchoredManualLine = viewModel.InsertForecastLine(null, below: true);
        Assert.Null(unanchoredManualLine.TransactionProjectCode);
        viewModel.DeleteForecastLine(unanchoredManualLine);

        var previewRows = viewModel.BuildForecastLineAutoCreatePreviewItems(
        [
            new CostTransaction
            {
                TaskNumber = "WA57109999",
                ProjectCode = "WA571",
                ManualName = "Preview Person",
                Source = "TC",
                Amount = 200m
            },
            new CostTransaction
            {
                TaskNumber = "WA57109999",
                ProjectCode = "WA571",
                ManualName = "Preview Person",
                Source = "AP",
                Amount = 300m
            }
        ]);
        var preview = Assert.Single(previewRows);
        Assert.Equal("WA57109999", preview.TaskNumber);
        Assert.Equal("Preview Person", preview.ManualName);
        Assert.Equal("WA571", preview.ProjectCode);
        Assert.Equal(500m, preview.Amount);
        Assert.Equal(2, preview.TransactionCount);
        Assert.Contains("AP", preview.Source, StringComparison.Ordinal);
        Assert.Contains("TC", preview.Source, StringComparison.Ordinal);

        var unmatchedPath = Path.Combine(directory.Root, "unmatched.json");
        var unmatchedDataset = new ProjectDataset
        {
            Header = new ProjectHeader { ProjectTitle = "Unmatched import fixture" },
            UnmatchedImportCombinations =
            [
                new UnmatchedImportCombination
                {
                    TaskNumber = "WA57109999",
                    ManualName = "Preview Person",
                    ProjectCode = "WA571",
                    Category = "WA571",
                    Source = "AP, TC",
                    Amount = 500m,
                    TransactionCount = 2
                }
            ]
        };
        new ProjectFileService().Save(unmatchedPath, unmatchedDataset);
        var reloadedUnmatched = new ProjectFileService().Load(unmatchedPath);
        Assert.Single(reloadedUnmatched.UnmatchedImportCombinations);
        Assert.Equal("Preview Person", reloadedUnmatched.UnmatchedImportCombinations[0].ManualName);
    }

    [Fact]
    public void Workspace_views_and_header_colours_keep_independent_persisted_layouts()
    {
        var viewModel = Luna11TestSupport.CreateSeedViewModel();
        viewModel.ActiveWorkspaceKey = "Resources";
        var defaultResourceView = viewModel.SelectedWorkspaceView!;
        viewModel.SetSelectedWorkspaceHiddenColumnKeys(["Units", "Amount"]);
        viewModel.SetSelectedWorkspaceColumnLayouts(
        [
            new WorkspaceColumnLayout { Key = "Resource", Width = 215, DisplayIndex = 0 },
            new WorkspaceColumnLayout { Key = "Amount", Width = 124, DisplayIndex = 1 }
        ]);
        Assert.Equal(["Amount", "Units"], defaultResourceView.HiddenColumnKeys, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(215d, defaultResourceView.ColumnLayouts.Single(layout => layout.Key == "Resource").Width);
        Assert.Equal(1, defaultResourceView.ColumnLayouts.Single(layout => layout.Key == "Amount").DisplayIndex);
        viewModel.AddWorkspaceViewCommand.Execute(null);
        var customResourceView = viewModel.SelectedWorkspaceView!;
        Assert.Equal(defaultResourceView.HiddenColumnKeys, customResourceView.HiddenColumnKeys, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(
            defaultResourceView.ColumnLayouts.Select(layout => layout.Key),
            customResourceView.ColumnLayouts.Select(layout => layout.Key),
            StringComparer.OrdinalIgnoreCase);
        viewModel.SetSelectedWorkspaceHiddenColumnKeys(["Tasks"]);
        viewModel.SetSelectedWorkspaceColumnLayouts([new WorkspaceColumnLayout { Key = "Tasks", Width = 301, DisplayIndex = 0 }]);
        viewModel.SelectedWorkspaceView = defaultResourceView;
        Assert.Equal(["Amount", "Units"], defaultResourceView.HiddenColumnKeys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(defaultResourceView.ColumnLayouts, layout => layout.Key == "Resource" && Math.Abs(layout.Width - 215d) < 0.01);
        viewModel.SelectedWorkspaceView = customResourceView;
        Assert.Equal(["Tasks"], customResourceView.HiddenColumnKeys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(customResourceView.ColumnLayouts, layout => layout.Key == "Tasks" && Math.Abs(layout.Width - 301d) < 0.01);

        using var directory = new Luna11TemporaryDirectory();
        var path = Path.Combine(directory.Root, "header-colours.json");
        var headerColourDataset = new ProjectDataset
        {
            Header = new ProjectHeader { ProjectTitle = "Header colour fixture" }
        };
        headerColourDataset.ForecastCalendarYearHeaderColorHexes["Calendar year 2026"] = "#CFE5FA";
        headerColourDataset.ForecastFiscalYearHeaderColorHexes["FY27"] = "#F0D37A";
        headerColourDataset.ForecastGroupHeaderColorHexes["Project Management"] = "#D7ECCF";
        new ProjectFileService().Save(path, headerColourDataset);
        var reloaded = new ProjectFileService().Load(path);
        Assert.Equal("#CFE5FA", reloaded.ForecastCalendarYearHeaderColorHexes["Calendar year 2026"]);
        Assert.Equal("#F0D37A", reloaded.ForecastFiscalYearHeaderColorHexes["FY27"]);
        Assert.Equal("#D7ECCF", reloaded.ForecastGroupHeaderColorHexes["Project Management"]);
    }

    [Fact]
    public void Csv_and_workbook_imports_preserve_field_mapping_and_multiline_records()
    {
        using var directory = new Luna11TemporaryDirectory();
        var csvPath = Path.Combine(directory.Root, "transactions.csv");
        File.WriteAllText(
            csvPath,
            "FY-Period,Task Numb,Period,Doc Date,Units,Unit Rate,Amount,Cost Ledg,Cost Acco,Project Co,Parent Pro,Resource C,Resource D,Source,PO Numb,PO Comm,Supplier N,Narrative 1,Narrative 2,Narrative 3,Who,ECM Number" + Environment.NewLine
            + "26-08,WA57102001,8,2026-02-08,3,150,450,26PRJA,10WA402,WA571,WA57P,10732,Stanley Drake,TC,PO-1,Comment A,Supplier A,10732/08/02/2026/13/1,DETAILED COST POSTING,,Stanley Drake,7597308");
        var imported = new CsvTransactionService().Import(csvPath, 42).Single();
        Assert.Equal(42, imported.RowNumber);
        Assert.Equal("WA57102001", imported.TaskNumber);
        Assert.Equal("WA571", imported.ProjectCode);
        Assert.Equal("WA57P", imported.ParentProjectCode);
        Assert.Equal("10732", imported.ResourceCode);
        Assert.Equal("Stanley Drake", imported.ResourceDescription);
        Assert.Equal("Supplier A", imported.SupplierName);
        Assert.Equal("DETAILED COST POSTING", imported.Narrative2);
        Assert.Equal("7597308", imported.EcmNumber);

        var multilinePath = Path.Combine(directory.Root, "multiline.csv");
        var multilineComment = "First line, with comma" + Environment.NewLine + "Second line with \"quoted text\"";
        var csvService = new CsvTransactionService();
        csvService.ExportTransactions(
            multilinePath,
            [new CostTransaction
            {
                TaskNumber = "CSV-1",
                ProjectCode = "CSV-PROJECT",
                ManualName = "CSV Resource",
                PoComments = multilineComment,
                Amount = 42m
            }]);
        var multilineRoundTrip = csvService.Import(multilinePath, 1).Single();
        Assert.Equal(multilineComment, multilineRoundTrip.PoComments);
        Assert.Equal("CSV Resource", multilineRoundTrip.ManualName);
        Assert.Equal(42m, multilineRoundTrip.Amount);

        var workbookPath = Path.Combine(directory.Root, "transactions.xlsx");
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.Worksheets.Add("Transactions");
            var headers = new[]
            {
                "FY-Period", "Task Numb", "Period", "Doc Date", "Units", "Unit Rate", "Amount", "Cost Ledg", "Cost Acco",
                "Project Co", "Parent Pro", "Resource C", "Resource D", "Source", "PO Numb", "PO Comm", "Supplier N",
                "Narrative 1", "Narrative 2", "Narrative 3", "Who", "ECM Number"
            };
            for (var column = 0; column < headers.Length; column++)
            {
                sheet.Cell(1, column + 1).Value = headers[column];
            }
            var values = new object?[]
            {
                "26-08", "WA57102001", 8, new DateTime(2026, 2, 8), 3, 150, 450, "26PRJA", "10WA402", "WA571", "WA57P",
                "10732", "Stanley Drake", "TC", "PO-1", "Comment A", "Supplier A", "10732/08/02/2026/13/1", "DETAILED COST POSTING",
                string.Empty, "Stanley Drake", "7597308"
            };
            for (var column = 0; column < values.Length; column++)
            {
                var cell = sheet.Cell(2, column + 1);
                switch (values[column])
                {
                    case DateTime date:
                        cell.Value = date;
                        break;
                    case int integer:
                        cell.Value = integer;
                        break;
                    case decimal decimalValue:
                        cell.Value = decimalValue;
                        break;
                    default:
                        cell.Value = values[column]?.ToString() ?? string.Empty;
                        break;
                }
            }
            sheet.Cell(2, 4).Style.DateFormat.Format = "yyyy-MM-dd";
            workbook.SaveAs(workbookPath);
        }
        var importedWorkbook = new CsvTransactionService().Import(workbookPath, 77).Single();
        Assert.Equal(77, importedWorkbook.RowNumber);
        Assert.Equal(imported.TaskNumber, importedWorkbook.TaskNumber);
        Assert.Equal(imported.ProjectCode, importedWorkbook.ProjectCode);
        Assert.Equal(imported.ParentProjectCode, importedWorkbook.ParentProjectCode);
        Assert.Equal(imported.ResourceCode, importedWorkbook.ResourceCode);
        Assert.Equal(imported.ResourceDescription, importedWorkbook.ResourceDescription);
        Assert.Equal(imported.SupplierName, importedWorkbook.SupplierName);
        Assert.Equal(imported.Narrative2, importedWorkbook.Narrative2);
        Assert.Equal(imported.EcmNumber, importedWorkbook.EcmNumber);

        var sameNamedCost = new CostTransaction
        {
            FyPeriod = imported.FyPeriod,
            TaskNumber = imported.TaskNumber,
            Period = imported.Period,
            DocDate = imported.DocDate,
            Units = imported.Units,
            UnitRate = imported.UnitRate,
            Amount = imported.Amount,
            CostLedger = imported.CostLedger,
            CostAccount = imported.CostAccount,
            ProjectCode = imported.ProjectCode,
            ParentProjectCode = imported.ParentProjectCode,
            ResourceCode = imported.ResourceCode,
            ResourceDescription = imported.ResourceDescription,
            Source = imported.Source,
            PoNumber = imported.PoNumber,
            PoComments = imported.PoComments,
            SupplierName = imported.SupplierName,
            Narrative1 = imported.Narrative1,
            Narrative2 = imported.Narrative2,
            Narrative3 = imported.Narrative3,
            Who = imported.Who,
            EcmNumber = "DIFFERENT"
        };
        Assert.Equal(CsvTransactionService.BuildNameMappingKey(imported), CsvTransactionService.BuildNameMappingKey(sameNamedCost));
        Assert.NotEqual(CsvTransactionService.BuildDuplicateKey(imported), CsvTransactionService.BuildDuplicateKey(sameNamedCost), StringComparer.OrdinalIgnoreCase);

        var differentSupplierSamePerson = new CostTransaction
        {
            ResourceDescription = imported.ResourceDescription,
            SupplierName = "Different supplier",
            Narrative1 = imported.Narrative1,
            Narrative2 = imported.Narrative2,
            Narrative3 = imported.Narrative3,
            Who = imported.Who
        };
        Assert.Equal(CsvTransactionService.BuildNameMappingKey(imported), CsvTransactionService.BuildNameMappingKey(differentSupplierSamePerson));
        var differentResourceDescription = new CostTransaction
        {
            ResourceDescription = "Someone Else",
            Narrative1 = imported.Narrative1,
            Narrative2 = imported.Narrative2,
            Narrative3 = imported.Narrative3,
            Who = imported.Who
        };
        Assert.NotEqual(
            CsvTransactionService.BuildNameMappingKey(imported),
            CsvTransactionService.BuildNameMappingKey(differentResourceDescription),
            StringComparer.OrdinalIgnoreCase);
        var differentNarrative1SamePerson = new CostTransaction
        {
            ResourceDescription = imported.ResourceDescription,
            Narrative1 = "1406/30/07/2017/4/1",
            Narrative2 = imported.Narrative2,
            Narrative3 = imported.Narrative3,
            Who = imported.Who
        };
        Assert.Equal(CsvTransactionService.BuildNameMappingKey(imported), CsvTransactionService.BuildNameMappingKey(differentNarrative1SamePerson));
        var dashPlaceholderRow = new CostTransaction
        {
            ResourceDescription = imported.ResourceDescription,
            Narrative1 = imported.Narrative1,
            Narrative2 = imported.Narrative2,
            Narrative3 = "-",
            Who = imported.Who
        };
        var blankPlaceholderRow = new CostTransaction
        {
            ResourceDescription = imported.ResourceDescription,
            Narrative1 = imported.Narrative1,
            Narrative2 = imported.Narrative2,
            Narrative3 = string.Empty,
            Who = imported.Who
        };
        Assert.Equal(CsvTransactionService.BuildNameMappingKey(dashPlaceholderRow), CsvTransactionService.BuildNameMappingKey(blankPlaceholderRow));
    }

    [Fact]
    public void Cost_centre_association_suggestions_prioritise_real_mapped_names()
    {
        var scoreMethod = typeof(MainWindowViewModel).GetMethod(
            "ScoreCostCenterAssociation",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not find ScoreCostCenterAssociation.");
        var candidateMethod = typeof(MainWindowViewModel).GetMethod(
            "BuildCostCenterNameCandidates",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Could not find BuildCostCenterNameCandidates.");
        var paulaAnchor = new CostCenterNameMapping
        {
            ManualName = "Paula Wright",
            Who = "Paula Wright",
            Narrative2 = "Pwright",
            Narrative1 = "WrightP"
        };
        var paulaNarrative2Only = new CostTransaction { Narrative2 = "P Wright" };
        var paulaNarrative1Only = new CostTransaction { Narrative1 = "WrightP" };
        var genericNarrativeOnly = new CostTransaction { Narrative2 = "DETAILED COST POSTING" };
        var unrelatedKatie = new CostCenterNameMapping
        {
            ManualName = "Katie Armstrong",
            Who = "Katie Armstrong",
            Narrative2 = "Karmstrong",
            Narrative1 = "ArmstrongK"
        };
        var narrative2Score = (int)(scoreMethod.Invoke(null, [paulaNarrative2Only, paulaAnchor]) ?? 0);
        var narrative1Score = (int)(scoreMethod.Invoke(null, [paulaNarrative1Only, paulaAnchor]) ?? 0);
        var genericScore = (int)(scoreMethod.Invoke(null, [genericNarrativeOnly, paulaAnchor]) ?? 0);
        var unrelatedScore = (int)(scoreMethod.Invoke(null, [paulaNarrative2Only, unrelatedKatie]) ?? 0);
        Assert.True(narrative2Score > 0);
        Assert.True(narrative1Score > 0);
        Assert.Equal(0, genericScore);
        Assert.Equal(0, unrelatedScore);

        var accrualSuggestionRow = new CostTransaction
        {
            Narrative2 = "HM Based on Actual Claim",
            Narrative3 = "FY19 - November 2018 Accrual"
        };
        var accrualCandidates = (candidateMethod.Invoke(
            Luna11TestSupport.CreateSeedViewModel(),
            [accrualSuggestionRow, null]) as IEnumerable<CostCenterNameOption>)?.ToList()
            ?? throw new InvalidOperationException("Expected accrual candidates.");
        Assert.Equal("Accrual", accrualCandidates[0].RawName);

        var opusViewModel = Luna11TestSupport.CreateSeedViewModel();
        opusViewModel.ForecastLines.Add(new ForecastLine
        {
            TaskNumber = "WW32203002",
            ResourceName = "OPUS",
            ProjectCode = "WW322"
        });
        var opusSuggestionRow = new CostTransaction
        {
            ResourceCode = "255",
            ResourceDescription = "Contractors Payments",
            Narrative1 = "WSP OPUS",
            Narrative2 = "Annual Pain Gain",
            Narrative3 = "YTD May 2020"
        };
        var opusCandidates = (candidateMethod.Invoke(opusViewModel, [opusSuggestionRow, null]) as IEnumerable<CostCenterNameOption>)?.ToList()
            ?? throw new InvalidOperationException("Expected OPUS candidates.");
        Assert.Equal("OPUS", opusCandidates[0].RawName);
    }
}
