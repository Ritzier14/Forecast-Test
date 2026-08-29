using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ProjectCostForecast.App;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;

namespace ProjectCostForecast.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void OpenProject()
    {
        var result = _projectFileWorkflow.Open(IsDirty);
        if (result.IsCancelled)
        {
            return;
        }

        if (!result.IsSuccess || result.Value is null)
        {
            HandleProjectOperationFailure(result, "Open project");
            return;
        }

        try
        {
            ApplyLoadedProject(result.Value.Path, result.Value.LoadedProject);
            StatusText = $"Opened {result.Value.Path}";
        }
        catch (Exception ex)
        {
            HandleProjectOperationFailure(
                new ProjectOperationResult<ProjectFileOpenOutcome>(ProjectOperationStatus.Failed, Error: ex),
                "Open project");
            _projectPrompt.ShowError("Open failed", ex.Message);
        }
    }

    private void ApplyLoadedProject(string path, ProjectFileLoadResult loadedProject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(loadedProject);

        var normalizedDataset = _projectDatasetMigrationPipeline.Normalize(loadedProject.Dataset).Dataset;
        var validationReport = _validationService.ValidateForOperation(normalizedDataset);
        if (validationReport.HasErrors)
        {
            ApplyValidationReport(validationReport);
            throw new ProjectValidationException(validationReport.BuildBlockingMessage("Open project"), validationReport);
        }

        LoadDataset(normalizedDataset, markDirty: false);
        ProjectFilePath = path;
        _projectFileRevision = loadedProject.Revision;
    }

    private bool SaveProject(bool showError = true)
    {
        SyncDatasetFromCollections();
        _calculationService.Recalculate(_dataset);
        ReplaceCollection(CategorySummaries, _dataset.CategorySummaries);
        return SaveDataset(_dataset, showError);
    }

    private bool SaveProjectAs(bool showError = true)
    {
        SyncDatasetFromCollections();
        _calculationService.Recalculate(_dataset);
        ReplaceCollection(CategorySummaries, _dataset.CategorySummaries);
        return SaveDatasetAs(_dataset, showError);
    }

    private void RestoreProjectBackup()
    {
        if (!ConfirmDiscardUnsavedChanges())
        {
            return;
        }

        var backupPath = _importExportInteraction.PickOpenFile(
            "Select a verified project backup",
            "Project Cost Forecast backups (*.bak.json)|*.bak.json|JSON files (*.json)|*.json|All files (*.*)|*.*");
        if (string.IsNullOrWhiteSpace(backupPath))
        {
            return;
        }

        var destinationPath = _importExportInteraction.PickSaveFile(
            "Restore project backup to a new file",
            "Project Cost Forecast JSON (*.json)|*.json|All files (*.*)|*.*",
            BuildDefaultRestoredProjectFileName());
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            return;
        }

        try
        {
            var restore = _projectFileService.RestoreBackup(
                backupPath,
                ProjectFilePath,
                destinationPath);
            var restoredProject = _projectFileService.LoadWithRevision(restore.RestoredPath);
            ApplyLoadedProject(restore.RestoredPath, restoredProject);
            StatusText = string.IsNullOrWhiteSpace(restore.PreRestoreBackupPath)
                ? $"Restored {restore.RestoredPath} from {restore.BackupPath}."
                : $"Restored {restore.RestoredPath} from {restore.BackupPath}; pre-restore backup created at {restore.PreRestoreBackupPath}.";
        }
        catch (Exception ex)
        {
            StatusText = $"Restore failed: {ex.Message}";
            _importExportInteraction.ShowError("Restore failed", ex.Message);
        }
    }

    private bool SaveDataset(
        ProjectDataset dataset,
        bool showError = true,
        string operation = "Save project",
        bool forceSaveAs = false)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        var result = _projectFileWorkflow.Save(
            dataset,
            forceSaveAs ? null : ProjectFilePath,
            forceSaveAs ? null : _projectFileRevision,
            BuildDefaultProjectFileName(),
            operation,
            showError,
            path => PrepareSaveAuditEvent(dataset, path));

        if (!result.IsSuccess || result.Value is null)
        {
            HandleProjectOperationFailure(result, operation);
            return false;
        }

        var outcome = result.Value;
        if (outcome.Kind == ProjectFileSaveOutcomeKind.Reloaded)
        {
            try
            {
                if (outcome.ReloadedProject is null)
                {
                    throw new InvalidOperationException("The reload result did not contain a project.");
                }

                ApplyLoadedProject(outcome.Path, outcome.ReloadedProject);
                StatusText = $"Reloaded {outcome.Path} after an external change. Unsaved changes were discarded.";
            }
            catch (Exception ex)
            {
                HandleProjectOperationFailure(
                    new ProjectOperationResult<ProjectFileSaveOutcome>(ProjectOperationStatus.Failed, Error: ex),
                    "Reload project");
                if (showError)
                {
                    _projectPrompt.ShowError("Reload failed", ex.Message);
                }
            }

            return false;
        }

        ProjectFilePath = outcome.Path;
        _projectFileRevision = outcome.Revision;
        if (ReferenceEquals(dataset, _dataset))
        {
            IsDirty = false;
            StatusText = string.IsNullOrWhiteSpace(outcome.BackupPath)
                ? $"Saved {ProjectFilePath}"
                : $"Saved {ProjectFilePath}; backup created.";
        }

        return true;
    }

    private bool SaveDatasetAs(ProjectDataset dataset, bool showError = true, string operation = "Save project")
    {
        return SaveDataset(dataset, showError, operation, forceSaveAs: true);
    }

    private bool TryBlockOperation(
        string operation,
        ProjectDataset dataset,
        bool showError,
        Action<string, string>? showErrorHandler = null)
    {
        var report = _validationService.ValidateForOperation(dataset);
        if (!report.HasErrors)
        {
            return true;
        }

        ApplyValidationReport(report);
        var message = report.BuildBlockingMessage(operation);
        StatusText = message;

        if (showError)
        {
            (showErrorHandler ?? _projectPrompt.ShowError)($"{operation} blocked", message);
        }

        return false;
    }

    private void HandleProjectOperationFailure<T>(ProjectOperationResult<T> result, string operation)
    {
        if (result.Error is ProjectValidationException validationException)
        {
            ApplyValidationReport(validationException.Report);
            StatusText = validationException.Message;
            return;
        }

        if (result.IsCancelled && result.Error is ProjectFileConflictException conflict)
        {
            StatusText = $"{conflict.Operation} cancelled because the project file changed externally. "
                + "Reload the file or use Save As to preserve these changes.";
            return;
        }

        if (result.Error is not null)
        {
            var operationLabel = string.Equals(operation, "Save project", StringComparison.Ordinal)
                ? "Save"
                : operation;
            StatusText = $"{operationLabel} failed: {result.Error.Message}";
        }
    }

    private void ApplyValidationReport(ValidationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        ReplaceCollection(ValidationIssues, report.Issues);
        OnPropertyChanged(nameof(ValidationIssueCount));
        OnPropertyChanged(nameof(ValidationSummaryText));
    }

    private Action PrepareSaveAuditEvent(ProjectDataset dataset, string path)
    {
        var saveAuditEvent = new AuditEvent
        {
            EntityType = "Project",
            EntityId = Header.ProjectTitle,
            FieldName = "Saved",
            NewValue = path,
            Reason = "Project saved"
        };
        AddSaveAuditEvent(dataset, saveAuditEvent);
        return () => RemoveSaveAuditEvent(dataset, saveAuditEvent);
    }

    private void AddSaveAuditEvent(ProjectDataset dataset, AuditEvent auditEvent)
    {
        if (auditEvent.ChangedAt == DateTimeOffset.UnixEpoch)
        {
            auditEvent.ChangedAt = _clock.UtcNow;
        }

        if (ReferenceEquals(dataset, _dataset))
        {
            AddAuditEvent(auditEvent);
            return;
        }

        dataset.AuditEvents.Insert(0, auditEvent);
    }

    private void RemoveSaveAuditEvent(ProjectDataset dataset, AuditEvent auditEvent)
    {
        dataset.AuditEvents.Remove(auditEvent);
        if (ReferenceEquals(dataset, _dataset))
        {
            AuditEvents.Remove(auditEvent);
            OnPropertyChanged(nameof(AuditEvents));
        }
    }

    private string BuildDefaultProjectFileName()
    {
        var title = Header.ProjectTitle;
        if (string.IsNullOrWhiteSpace(title))
        {
            return "ProjectCostForecast.project.json";
        }

        var invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
        var cleanedTitle = new string(title
            .Select(character => invalidCharacters.Contains(character) ? ' ' : character)
            .Select(character => character is '-' or '_' ? ' ' : character)
            .ToArray());
        var collapsedTitle = string.Join(' ', cleanedTitle
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return string.IsNullOrWhiteSpace(collapsedTitle)
            ? "ProjectCostForecast.project.json"
            : $"{collapsedTitle}.json";
    }

    private string BuildDefaultRestoredProjectFileName()
    {
        var defaultName = BuildDefaultProjectFileName();
        var extension = Path.GetExtension(defaultName);
        var baseName = Path.GetFileNameWithoutExtension(defaultName);
        return $"{baseName}.restored{extension}";
    }

    private void ImportCsv()
    {
        try
        {
            var path = _importExportInteraction.PickOpenFile(
                "Import raw transaction file",
                _csvTransactionService.GetSupportedFileFilter());
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            ImportTransactionFile(path);
        }
        catch (Exception ex)
        {
            StatusText = $"Import failed: {ex.Message}";
            _importExportInteraction.ShowError("Import failed", ex.Message);
        }
    }

    public void ImportTransactionFile(string path, bool showError = true)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!_csvTransactionService.SupportsFile(path))
        {
            StatusText = "Import failed: supported import files are .csv, .xlsx, and .xlsm.";
            if (showError)
            {
                _importExportInteraction.ShowInformation(
                    "Import failed",
                    "Supported import files are .csv, .xlsx, and .xlsm.");
            }

            return;
        }

        try
        {
            var imported = _csvTransactionService.Import(path, 1);
            if (imported.Count == 0)
            {
                StatusText = "Import found no transaction rows.";
                return;
            }

            var newTransactions = GetNewTransactions(imported, out var duplicateCount);
            if (newTransactions.Count == 0)
            {
                StatusText = $"Import skipped {duplicateCount} duplicate transaction row(s). No new costs were added.";
                return;
            }

            SyncDatasetFromCollections();
            var stagedDataset = _projectDatasetCloner.Clone(_dataset);
            stagedDataset.Transactions.AddRange(newTransactions);
            if (!TryBlockOperation("Import transactions", stagedDataset, showError, _importExportInteraction.ShowError))
            {
                return;
            }

            var stagedMappings = stagedDataset.CostCenterNameMappings ??= [];
            if (!ApplyCostCenterNameMappings(newTransactions, stagedMappings))
            {
                StatusText = "Import cancelled before any transaction rows were added.";
                return;
            }

            if (!ReviewForecastLineAutoCreatePreview(newTransactions, stagedMappings))
            {
                StatusText = "Import cancelled before any transaction rows were added.";
                return;
            }

            var nextRow = Transactions.Any() ? Transactions.Max(t => t.RowNumber) + 1 : 1;
            foreach (var transaction in newTransactions)
            {
                transaction.RowNumber = nextRow++;
            }

            if (!TryBlockOperation("Import transactions", stagedDataset, showError, _importExportInteraction.ShowError))
            {
                return;
            }

            _dataset.CostCenterNameMappings = stagedMappings;
            AddItems(Transactions, newTransactions);
            EnsureForecastLinesForImportedTransactions(newTransactions);
            var initialCostLoadSnapshotCount = CreateInitialCostLoadSavedMonths
                ? CreateInitialCostLoadSnapshots(newTransactions)
                : 0;

            AddAuditEvent(
                "TransactionImport",
                path,
                "ImportedRows",
                "0",
                newTransactions.Count.ToString(),
                duplicateCount == 0 ? "Imported raw transaction file" : $"Imported raw transaction file; skipped {duplicateCount} duplicate row(s)");
            var importReason = duplicateCount == 0
                ? $"Imported {newTransactions.Count} new transaction rows"
                : $"Imported {newTransactions.Count} new transaction rows and skipped {duplicateCount} duplicate row(s)";
            if (initialCostLoadSnapshotCount > 0)
            {
                importReason += $"; created {initialCostLoadSnapshotCount} saved month snapshot(s)";
            }

            RecalculateAndRefresh(
                markDirty: true,
                reason: importReason,
                includeRawTransactionsPivot: true);
        }
        catch (Exception ex)
        {
            StatusText = $"Import failed: {ex.Message}";
            if (showError)
            {
                _importExportInteraction.ShowError("Import failed", ex.Message);
            }
        }
    }

    private List<CostTransaction> GetNewTransactions(IEnumerable<CostTransaction> imported, out int duplicateCount)
    {
        var existingKeys = Transactions
            .Select(CsvTransactionService.BuildDuplicateKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var fileKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var newTransactions = new List<CostTransaction>();
        duplicateCount = 0;

        foreach (var transaction in imported)
        {
            var key = CsvTransactionService.BuildDuplicateKey(transaction);
            if (existingKeys.Contains(key) || !fileKeys.Add(key))
            {
                duplicateCount++;
                continue;
            }

            newTransactions.Add(transaction);
        }

        return newTransactions;
    }

    private int CreateInitialCostLoadSnapshots(IReadOnlyCollection<CostTransaction> importedTransactions)
    {
        var periods = importedTransactions
            .Select(transaction => CalculationService.Normalise(transaction.FyPeriod))
            .Where(period => FiscalPeriod.SortKey(period) != int.MaxValue)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(FiscalPeriod.SortKey)
            .ToList();
        if (periods.Count == 0)
        {
            return 0;
        }

        SyncDatasetFromCollections();
        var stagedDataset = _projectDatasetCloner.Clone(_dataset);
        var allTransactions = stagedDataset.Transactions.ToList();
        var snapshots = new List<SavedMonthSnapshot>();

        foreach (var period in periods)
        {
            var cutoffSortKey = FiscalPeriod.SortKey(period);
            stagedDataset.Transactions.ReplaceWith(allTransactions.Where(transaction =>
            {
                var transactionSortKey = FiscalPeriod.SortKey(transaction.FyPeriod);
                return transactionSortKey != int.MaxValue && transactionSortKey <= cutoffSortKey;
            }));
            stagedDataset.Header.CurrentPeriod = period;
            _calculationService.Recalculate(stagedDataset);
            snapshots.Add(_newMonthOperation.BuildSnapshot(period, stagedDataset.ForecastLines));
        }

        foreach (var snapshot in snapshots.OrderBy(snapshot => FiscalPeriod.SortKey(snapshot.Period)))
        {
            SavedMonthSnapshots.Insert(0, snapshot);
            AddAuditEvent(
                "SavedMonth",
                snapshot.Period,
                "InitialCostLoad",
                string.Empty,
                DateTimeContract.FormatUtc(snapshot.SavedAt),
                $"Created saved month from initial cost load through {snapshot.Period}");
        }

        return snapshots.Count;
    }

    private void EnsureForecastLinesForImportedTransactions(IEnumerable<CostTransaction> transactions)
    {
        var existingLineKeys = ForecastLines
            .Where(line => line.TransactionProjectCode is not null)
            .Select(line => BuildForecastLineMatchKey(line.TaskNumber, line.ResourceName, line.TransactionProjectCode))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var legacyLineKeys = ForecastLines
            .Where(line => line.TransactionProjectCode is null)
            .Select(line => BuildLegacyForecastLineMatchKey(line.TaskNumber, line.ResourceName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nextRow = ForecastLines.Any() ? ForecastLines.Max(item => item.RowNumber) + 1 : 1;
        var newLines = new List<ForecastLine>();

        foreach (var group in transactions
                     .Where(transaction => !string.IsNullOrWhiteSpace(transaction.TaskNumber))
                     .Where(transaction => !string.IsNullOrWhiteSpace(transaction.LedgerResourceName))
                     .GroupBy(transaction => new
                     {
                         Task = CalculationService.Normalise(transaction.TaskNumber),
                         Resource = CalculationService.Normalise(transaction.LedgerResourceName),
                         Project = CalculationService.Normalise(transaction.ProjectCode)
                     }))
        {
            var sample = group.First();
            if (legacyLineKeys.Contains(BuildLegacyForecastLineMatchKey(group.Key.Task, group.Key.Resource))
                || !existingLineKeys.Add(BuildForecastLineMatchKey(group.Key.Task, group.Key.Resource, group.Key.Project)))
            {
                continue;
            }

            var line = new ForecastLine
            {
                RowNumber = nextRow++,
                TaskNumber = sample.TaskNumber,
                ResourceName = sample.LedgerResourceName,
                ProjectCode = string.IsNullOrWhiteSpace(sample.ProjectCode) ? "Unassigned" : sample.ProjectCode,
                TransactionProjectCode = sample.ProjectCode,
                Budget = 0
            };

            foreach (var period in _dataset.ForecastPeriods)
            {
                line.MonthlyForecasts.Add(new MonthlyForecast
                {
                    PeriodLabel = period.Label,
                    PeriodStartDate = period.StartDate
                });
            }

            line.EnsureResourceCommentMetrics();
            SubscribeMonthlyForecastEvents(line);
            newLines.Add(line);
        }

        if (newLines.Count > 0)
        {
            AddItems(ForecastLines, newLines);
            AddAuditEvent("ForecastLine", "Import", "AutoCreated", "0", newLines.Count.ToString(), "Created forecast lines for imported transactions");
        }
    }

    public IReadOnlyList<ImportAutoCreatePreviewItem> BuildForecastLineAutoCreatePreviewItems(IEnumerable<CostTransaction> transactions)
    {
        var existingLineKeys = ForecastLines
            .Where(line => line.TransactionProjectCode is not null)
            .Select(line => BuildForecastLineMatchKey(line.TaskNumber, line.ResourceName, line.TransactionProjectCode))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var legacyLineKeys = ForecastLines
            .Where(line => line.TransactionProjectCode is null)
            .Select(line => BuildLegacyForecastLineMatchKey(line.TaskNumber, line.ResourceName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return transactions
            .Where(transaction => !string.IsNullOrWhiteSpace(transaction.TaskNumber))
            .Where(transaction => !string.IsNullOrWhiteSpace(transaction.LedgerResourceName))
            .GroupBy(transaction => new
            {
                Task = CalculationService.Normalise(transaction.TaskNumber),
                Resource = CalculationService.Normalise(transaction.LedgerResourceName),
                Project = CalculationService.Normalise(transaction.ProjectCode)
            })
            .Where(group => !legacyLineKeys.Contains(BuildLegacyForecastLineMatchKey(group.Key.Task, group.Key.Resource))
                && !existingLineKeys.Contains(BuildForecastLineMatchKey(group.Key.Task, group.Key.Resource, group.Key.Project)))
            .Select(group =>
            {
                var sample = group.First();
                return new ImportAutoCreatePreviewItem
                {
                    OriginalTaskNumber = sample.TaskNumber,
                    OriginalManualName = CleanCostCenterName(sample.LedgerResourceName),
                    OriginalProjectCode = string.IsNullOrWhiteSpace(sample.ProjectCode) ? "Unassigned" : sample.ProjectCode,
                    TaskNumber = sample.TaskNumber,
                    ManualName = CleanCostCenterName(sample.LedgerResourceName),
                    ProjectCode = string.IsNullOrWhiteSpace(sample.ProjectCode) ? "Unassigned" : sample.ProjectCode,
                    Category = string.IsNullOrWhiteSpace(sample.ProjectCode) ? "Unassigned" : sample.ProjectCode,
                    Source = string.Join(", ", group.Select(transaction => transaction.Source).Where(source => !string.IsNullOrWhiteSpace(source)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(source => source)),
                    Amount = group.Sum(transaction => transaction.Amount),
                    TransactionCount = group.Count(),
                    Transactions = group
                        .OrderBy(transaction => transaction.FyPeriod, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(transaction => transaction.Source, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(transaction => transaction.Amount)
                        .Select(transaction => new ImportAutoCreatePreviewTransactionDetail
                        {
                            FyPeriod = transaction.FyPeriod,
                            TaskNumber = transaction.TaskNumber,
                            ResourceDescription = transaction.ResourceDescription,
                            SupplierName = transaction.SupplierName,
                            Narrative1 = transaction.Narrative1,
                            Narrative2 = transaction.Narrative2,
                            Narrative3 = transaction.Narrative3,
                            Who = transaction.Who,
                            Source = transaction.Source,
                            Amount = transaction.Amount
                        })
                        .ToList()
                };
            })
            .OrderBy(item => item.TaskNumber, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ManualName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ProjectCode, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private bool ReviewForecastLineAutoCreatePreview(
        IReadOnlyCollection<CostTransaction> transactions,
        IReadOnlyCollection<CostCenterNameMapping> stagedMappings)
    {
        var previewItems = BuildForecastLineAutoCreatePreviewItems(transactions);
        if (previewItems.Count == 0)
        {
            return true;
        }

        if (!_userPreferences.ShowImportAutoCreatePreview
            || !_importExportInteraction.CanShowAutoCreatePreview)
        {
            return true;
        }

        var review = _importExportInteraction.ReviewAutoCreatePreview(
            new ImportAutoCreatePreviewPrompt(previewItems, _userPreferences.ShowImportAutoCreatePreview));
        _userPreferences.ShowImportAutoCreatePreview = review.ShowPreviewNextTime;
        SaveUserPreferences();
        if (!review.Accepted)
        {
            RoutePreviewItemsToUnmatchedList(previewItems);
            OpenUnmatchedImportViewer();
            return false;
        }

        ApplyForecastLineAutoCreatePreviewEdits(transactions, review.PreviewItems, stagedMappings);
        return true;
    }

    private void ApplyForecastLineAutoCreatePreviewEdits(
        IEnumerable<CostTransaction> transactions,
        IEnumerable<ImportAutoCreatePreviewItem> previewItems,
        IEnumerable<CostCenterNameMapping> stagedMappings)
    {
        var mappingsByKey = stagedMappings
            .GroupBy(mapping => mapping.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(mapping => mapping.LastUsedAt).First(), StringComparer.OrdinalIgnoreCase);

        foreach (var previewItem in previewItems)
        {
            var manualName = CalculationService.Normalise(previewItem.ManualName);
            if (string.IsNullOrWhiteSpace(manualName))
            {
                continue;
            }
            manualName = CleanCostCenterName(manualName);

            var matchingTransactions = transactions
                .Where(transaction => string.Equals(CalculationService.Normalise(transaction.TaskNumber), CalculationService.Normalise(previewItem.OriginalTaskNumber), StringComparison.OrdinalIgnoreCase))
                .Where(transaction => string.Equals(CalculationService.Normalise(transaction.LedgerResourceName), CalculationService.Normalise(previewItem.OriginalManualName), StringComparison.OrdinalIgnoreCase))
                .Where(transaction => string.Equals(CalculationService.Normalise(string.IsNullOrWhiteSpace(transaction.ProjectCode) ? "Unassigned" : transaction.ProjectCode), CalculationService.Normalise(previewItem.OriginalProjectCode), StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var transaction in matchingTransactions)
            {
                transaction.ManualName = manualName;
                var mappingKey = CsvTransactionService.BuildNameMappingKey(transaction);
                if (mappingsByKey.TryGetValue(mappingKey, out var mapping))
                {
                    mapping.ManualName = manualName;
                    mapping.LastUsedAt = _clock.UtcNow;
                }
            }
        }
    }

    private void RoutePreviewItemsToUnmatchedList(IEnumerable<ImportAutoCreatePreviewItem> previewItems)
    {
        var recordedAt = _clock.UtcNow;
        var newItems = previewItems.Select(item => new UnmatchedImportCombination
        {
            RecordedAt = recordedAt,
            TaskNumber = item.TaskNumber,
            ManualName = item.ManualName,
            ProjectCode = item.ProjectCode,
            Category = item.Category,
            Source = item.Source,
            Amount = item.Amount,
            TransactionCount = item.TransactionCount
        }).ToList();

        foreach (var item in newItems.OrderByDescending(item => item.RecordedAt))
        {
            UnmatchedImportCombinations.Insert(0, item);
        }

        SyncDatasetFromCollections();
        AddAuditEvent("TransactionImport", "AutoCreatePreview", "Cancelled", "0", newItems.Count.ToString(), "Cancelled import and routed new combinations to unmatched list");
    }

    public ForecastLine InsertForecastLine(ForecastLine? anchor, bool below)
    {
        var line = new ForecastLine
        {
            RowNumber = ForecastLines.Any() ? ForecastLines.Max(item => item.RowNumber) + 1 : 1,
            TaskNumber = anchor?.TaskNumber ?? string.Empty,
            ResourceName = anchor is null ? string.Empty : "New line",
            ProjectCode = anchor?.ProjectCode ?? string.Empty,
            TransactionProjectCode = anchor?.TransactionProjectCode,
            ReportingCategoryOverride = anchor?.ReportingCategoryOverride ?? anchor?.ReportingCategory ?? string.Empty,
            Budget = 0,
            IsManuallyAdded = true
        };

        foreach (var period in _dataset.ForecastPeriods)
        {
            line.MonthlyForecasts.Add(new MonthlyForecast
            {
                PeriodLabel = period.Label,
                PeriodStartDate = period.StartDate
            });
        }

        line.EnsureResourceCommentMetrics();
        SubscribeMonthlyForecastEvents(line);

        var anchorIndex = anchor is null ? -1 : ForecastLines.IndexOf(anchor);
        var insertIndex = anchorIndex < 0 ? ForecastLines.Count : anchorIndex + (below ? 1 : 0);
        ForecastLines.Insert(insertIndex, line);
        InitializeTaskCategoryMetadata();

        AddAuditEvent("ForecastLine", line.RowNumber.ToString(), "Created", string.Empty, line.ResourceName, below ? "Added line below" : "Added line above");
        ApplyForecastPeriodLockStates();
        RecalculateAndRefresh(markDirty: true, reason: "Added forecast line");
        SelectedForecastLine = line;
        return line;
    }

    public void DeleteForecastLine(ForecastLine line)
    {
        if (!ForecastLines.Contains(line))
        {
            return;
        }

        if (!line.IsManuallyAdded)
        {
            StatusText = "Lines that came from imported raw data cannot be deleted.";
            return;
        }

        UnsubscribeMonthlyForecastEvents(line);
        ForecastLines.Remove(line);
        AddAuditEvent("ForecastLine", line.RowNumber.ToString(), "Deleted", line.ResourceName, string.Empty, "Deleted forecast line");
        RecalculateAndRefresh(markDirty: true, reason: "Deleted forecast line");
    }

    private static string BuildForecastLineMatchKey(string? taskNumber, string? resourceName, string? projectCode)
    {
        return string.Join('\u001f',
            CalculationService.Normalise(taskNumber),
            CalculationService.Normalise(resourceName),
            CalculationService.Normalise(projectCode));
    }

    private static string BuildLegacyForecastLineMatchKey(string? taskNumber, string? resourceName)
    {
        return string.Join('\u001f',
            CalculationService.Normalise(taskNumber),
            CalculationService.Normalise(resourceName));
    }

    private bool ApplyCostCenterNameMappings(
        IReadOnlyCollection<CostTransaction> transactions,
        IList<CostCenterNameMapping> stagedMappings)
    {
        ArgumentNullException.ThrowIfNull(stagedMappings);

        var mappingsByKey = stagedMappings
            .Where(mapping => !string.IsNullOrWhiteSpace(mapping.Key) && !string.IsNullOrWhiteSpace(mapping.ManualName))
            .GroupBy(mapping => mapping.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(mapping => mapping.LastUsedAt).First(), StringComparer.OrdinalIgnoreCase);
        var newMappings = new List<CostCenterNameMapping>();
        var resolvedGroups = new List<(IReadOnlyCollection<CostTransaction> Rows, CostCenterNameMapping Mapping)>();
        var unresolvedGroups = new List<UnresolvedCostCenterGroup>();
        var forceIndividualGroupKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in transactions.GroupBy(CsvTransactionService.BuildNameMappingKey, StringComparer.OrdinalIgnoreCase))
        {
            var rows = group.ToList();
            var sample = rows[0];

            if (!mappingsByKey.TryGetValue(group.Key, out var mapping))
            {
                if (CanUseWhoAsCtcName(sample))
                {
                    mapping = CreateCostCenterNameMapping(sample, sample.Who);
                    newMappings.Add(mapping);
                    mappingsByKey[group.Key] = mapping;
                }
                else
                {
                    unresolvedGroups.Add(new UnresolvedCostCenterGroup(group.Key, sample, rows));
                    continue;
                }
            }

            resolvedGroups.Add((rows, mapping));
        }

        while (unresolvedGroups.Count > 0)
        {
            var candidateMappings = mappingsByKey.Values
                .Concat(newMappings)
                .GroupBy(mappingItem => mappingItem.Key, StringComparer.OrdinalIgnoreCase)
                .Select(grouping => grouping.OrderByDescending(mappingItem => mappingItem.LastUsedAt).First())
                .ToList();
            var unresolvedDetails = unresolvedGroups
                .Select(group =>
                {
                    var candidates = BuildCostCenterNameCandidates(group.Sample, candidateMappings);
                    return new UnresolvedCostCenterGroupDetail(
                        group,
                        candidates,
                        GetSuggestedCostCenterOption(candidates));
                })
                .ToList();

            var seed = unresolvedDetails[0];
            var groupedDetails = seed.SuggestedOption is null || forceIndividualGroupKeys.Contains(seed.Group.Key)
                ? [seed]
                : unresolvedDetails
                    .Where(detail => detail.SuggestedOption is not null
                        && !forceIndividualGroupKeys.Contains(detail.Group.Key)
                        && string.Equals(detail.SuggestedOption.RawName, seed.SuggestedOption.RawName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            var combinedRows = groupedDetails
                .SelectMany(detail => detail.Group.Rows)
                .ToList();
            var combinedCandidates = groupedDetails
                .SelectMany(detail => detail.Candidates)
                .GroupBy(option => option.RawName, StringComparer.OrdinalIgnoreCase)
                .Select(grouping => grouping.OrderByDescending(option => option.IsExistingName).First())
                .OrderByDescending(option => seed.SuggestedOption is not null
                    && string.Equals(option.RawName, seed.SuggestedOption.RawName, StringComparison.OrdinalIgnoreCase))
                .ThenBy(option => option.RawName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (!_importExportInteraction.CanShowCostCenterMapping)
            {
                return false;
            }

            var mappingDecision = _importExportInteraction.ChooseCostCenterMapping(
                new CostCenterMappingPrompt(
                    seed.Group.Sample,
                    combinedRows,
                    combinedCandidates,
                    seed.SuggestedOption,
                    GetExistingCostCenterNames(candidateMappings),
                    unresolvedGroups.Count,
                    groupedDetails.Select(detail => detail.Group.Key).ToList()));
            if (!mappingDecision.Accepted)
            {
                return false;
            }

            var completedGroupKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var detail in groupedDetails)
            {
                if (mappingDecision.ExcludedMappingKeys.Contains(detail.Group.Key, StringComparer.OrdinalIgnoreCase))
                {
                    forceIndividualGroupKeys.Add(detail.Group.Key);
                    continue;
                }

                var assignedName = mappingDecision.MappingNameOverrides.TryGetValue(detail.Group.Key, out var overrideName)
                    ? overrideName
                    : mappingDecision.SelectedManualName;
                if (string.IsNullOrWhiteSpace(assignedName))
                {
                    forceIndividualGroupKeys.Add(detail.Group.Key);
                    continue;
                }

                var mapping = CreateCostCenterNameMapping(detail.Group.Sample, assignedName);
                newMappings.Add(mapping);
                mappingsByKey[detail.Group.Key] = mapping;
                resolvedGroups.Add((detail.Group.Rows, mapping));
                completedGroupKeys.Add(detail.Group.Key);
            }

            unresolvedGroups.RemoveAll(group =>
                completedGroupKeys.Contains(group.Key));
        }

        foreach (var mapping in newMappings)
        {
            stagedMappings.Add(mapping);
        }

        foreach (var resolvedGroup in resolvedGroups)
        {
            ApplyCostCenterNameMapping(resolvedGroup.Rows, resolvedGroup.Mapping);
        }

        return true;
    }

    private sealed record UnresolvedCostCenterGroup(
        string Key,
        CostTransaction Sample,
        IReadOnlyCollection<CostTransaction> Rows);

    private sealed record UnresolvedCostCenterGroupDetail(
        UnresolvedCostCenterGroup Group,
        IReadOnlyList<CostCenterNameOption> Candidates,
        CostCenterNameOption? SuggestedOption);

    private CostCenterNameMapping CreateCostCenterNameMapping(CostTransaction sample, string manualName)
    {
        return new CostCenterNameMapping
        {
            Key = CsvTransactionService.BuildNameMappingKey(sample),
            ResourceCode = sample.ResourceCode,
            ResourceDescription = sample.ResourceDescription,
            SupplierName = sample.SupplierName,
            Narrative1 = sample.Narrative1,
            Narrative2 = sample.Narrative2,
            Narrative3 = sample.Narrative3,
            Who = sample.Who,
            ManualName = CleanCostCenterName(manualName),
            LastUsedAt = _clock.UtcNow
        };
    }

    private void ApplyCostCenterNameMapping(IReadOnlyCollection<CostTransaction> rows, CostCenterNameMapping mapping)
    {
        var manualName = CleanCostCenterName(mapping.ManualName);
        foreach (var transaction in rows)
        {
            transaction.ManualName = manualName;
        }

        mapping.UseCount += rows.Count;
        mapping.ManualName = manualName;
        mapping.LastUsedAt = _clock.UtcNow;
    }

    private IReadOnlyList<AssociatedCostCenterMatch> GetAssociatedCostCenterMatches(CostTransaction transaction, IEnumerable<CostCenterNameMapping>? availableMappings = null)
    {
        return (availableMappings ?? _dataset.CostCenterNameMappings)
            .Where(mapping => !string.IsNullOrWhiteSpace(mapping.ManualName))
            .Select(mapping => new AssociatedCostCenterMatch
            {
                Mapping = mapping,
                Score = ScoreCostCenterAssociation(transaction, mapping)
            })
            .Where(match => match.Score > 0)
            .OrderByDescending(match => match.Score)
            .ThenByDescending(match => match.Mapping.LastUsedAt)
            .ToList();
    }

    private static int ScoreCostCenterAssociation(CostTransaction transaction, CostCenterNameMapping mapping)
    {
        var score = 0;
        score += SharesAssociationValue(transaction.Who, mapping.Who) ? 100 : 0;
        score += SharesAssociationValue(transaction.Narrative2, mapping.Narrative2) ? 60 : 0;
        score += SharesAssociationValue(transaction.Narrative1, mapping.Narrative1) ? 40 : 0;
        score += SharesAssociationValue(transaction.Narrative3, mapping.Narrative3) ? 20 : 0;
        return score;
    }

    private IReadOnlyList<CostCenterNameOption> BuildCostCenterNameCandidates(CostTransaction transaction, IEnumerable<CostCenterNameMapping>? availableMappings = null)
    {
        var candidates = new List<CostCenterNameOption>();

        if (ShouldSuggestAccrual(transaction))
        {
            AddCandidate(candidates, "Accrual", "Suggested");
        }

        if (CanUseWhoAsCtcName(transaction))
        {
            AddCandidate(candidates, transaction.Who, "Who matches Resources");
        }

        foreach (var match in GetMentionedExistingCostCenterMatches(transaction, availableMappings))
        {
            AddCandidate(candidates, match.RawName, match.SourceLabel, isExistingName: true);
        }

        foreach (var match in GetAssociatedCostCenterMatches(transaction, availableMappings))
        {
            AddCandidate(candidates, match.Mapping.ManualName, DescribeAssociationSource(transaction, match.Mapping), isExistingName: true);
        }

        AddCandidate(candidates, transaction.ResourceDescription, "Resource Desc");
        AddCandidate(candidates, transaction.SupplierName, "Supplier Name");
        AddCandidate(candidates, transaction.Narrative1, "Narrative 1");
        AddCandidate(candidates, transaction.Narrative2, "Narrative 2");
        AddCandidate(candidates, transaction.Narrative3, "Narrative 3");
        AddCandidate(candidates, transaction.Who, "Who");
        if (candidates.Count == 0)
        {
            AddCandidate(candidates, transaction.ResourceCode, "Resource Code");
        }

        if (candidates.Count == 0)
        {
            candidates.Add(new CostCenterNameOption
            {
                RawName = "Unassigned cost centre",
                SourceLabel = "Default fallback"
            });
        }

        return candidates;
    }

    private IReadOnlyList<CostCenterNameOption> GetMentionedExistingCostCenterMatches(CostTransaction transaction, IEnumerable<CostCenterNameMapping>? availableMappings = null)
    {
        var matches = new List<CostCenterNameOption>();
        var existingNames = GetExistingCostCenterNames(availableMappings)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(name => name.Length)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var existingName in existingNames)
        {
            var sourceLabel = GetMentionedExistingCostCenterSource(transaction, existingName);
            if (string.IsNullOrWhiteSpace(sourceLabel))
            {
                continue;
            }

            matches.Add(new CostCenterNameOption
            {
                RawName = existingName,
                SourceLabel = sourceLabel,
                IsExistingName = true
            });
        }

        return matches;
    }

    private static CostCenterNameOption? GetSuggestedCostCenterOption(IEnumerable<CostCenterNameOption> candidates)
    {
        return candidates.FirstOrDefault(option => string.Equals(option.RawName, "Accrual", StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault(option => option.IsExistingName);
    }

    private static bool ShouldSuggestAccrual(CostTransaction transaction)
    {
        return GetAccrualSuggestionFields(transaction)
            .Any(value => !string.IsNullOrWhiteSpace(value)
                && value.Contains("accrual", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> GetAccrualSuggestionFields(CostTransaction transaction)
    {
        yield return transaction.FyPeriod;
        yield return transaction.TaskNumber;
        yield return transaction.ProjectCode;
        yield return transaction.ParentProjectCode;
        yield return transaction.ResourceCode;
        yield return transaction.ResourceDescription;
        yield return transaction.Source;
        yield return transaction.SupplierName;
        yield return transaction.Narrative1;
        yield return transaction.Narrative2;
        yield return transaction.Narrative3;
        yield return transaction.Who;
        yield return transaction.EcmNumber;
        yield return transaction.ManualName;
    }

    private static bool CanUseWhoAsCtcName(CostTransaction transaction)
    {
        return !string.IsNullOrWhiteSpace(transaction.Who)
            && !string.IsNullOrWhiteSpace(transaction.ResourceDescription)
            && string.Equals(
                CalculationService.Normalise(transaction.Who),
                CalculationService.Normalise(transaction.ResourceDescription),
                StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyCollection<string> GetExistingCostCenterNames(IEnumerable<CostCenterNameMapping>? availableMappings = null)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var manualName in (availableMappings ?? _dataset.CostCenterNameMappings)
                     .Select(mapping => mapping.ManualName)
                     .Where(name => !string.IsNullOrWhiteSpace(name)))
        {
            names.Add(CleanCostCenterName(manualName));
        }

        foreach (var forecastName in ForecastLines
                     .Select(line => line.ResourceName)
                     .Where(name => !string.IsNullOrWhiteSpace(name)))
        {
            names.Add(CleanCostCenterName(forecastName));
        }

        return names.ToList();
    }

    private static string GetMentionedExistingCostCenterSource(CostTransaction transaction, string existingName)
    {
        foreach (var field in EnumerateSuggestionFields(transaction))
        {
            if (ContainsCandidatePhrase(field.Value, existingName))
            {
                return field.Label;
            }
        }

        return string.Empty;
    }

    private static IEnumerable<(string Label, string? Value)> EnumerateSuggestionFields(CostTransaction transaction)
    {
        yield return ("Resource Desc", transaction.ResourceDescription);
        yield return ("Supplier Name", transaction.SupplierName);
        yield return ("Narrative 1", transaction.Narrative1);
        yield return ("Narrative 2", transaction.Narrative2);
        yield return ("Narrative 3", transaction.Narrative3);
        yield return ("Who", transaction.Who);
    }

    private static bool ContainsCandidatePhrase(string? fieldValue, string candidate)
    {
        var normalisedField = NormaliseCandidatePhrase(fieldValue);
        var normalisedCandidate = NormaliseCandidatePhrase(candidate);
        if (string.IsNullOrWhiteSpace(normalisedField) || string.IsNullOrWhiteSpace(normalisedCandidate))
        {
            return false;
        }

        return string.Equals(normalisedField, normalisedCandidate, StringComparison.OrdinalIgnoreCase)
            || normalisedField.StartsWith(normalisedCandidate + " ", StringComparison.OrdinalIgnoreCase)
            || normalisedField.EndsWith(" " + normalisedCandidate, StringComparison.OrdinalIgnoreCase)
            || normalisedField.Contains(" " + normalisedCandidate + " ", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormaliseCandidatePhrase(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(" ", value
            .Split([ ' ', '/', '\\', '-', '_', ',', '.', ':', ';', '(', ')', '[', ']', '{', '}', '"' ], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => new string(part.Where(char.IsLetterOrDigit).ToArray()))
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part.ToUpperInvariant()));
    }

    private static void AddCandidate(ICollection<CostCenterNameOption> candidates, string? value, string sourceLabel, bool isExistingName = false)
    {
        var candidate = CleanCostCenterName(CalculationService.Normalise(value));
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        if (!candidates.Any(existing => string.Equals(existing.RawName, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidates.Add(new CostCenterNameOption
            {
                RawName = candidate,
                SourceLabel = sourceLabel,
                IsExistingName = isExistingName
            });
        }
    }

    private static string DescribeAssociationSource(CostTransaction transaction, CostCenterNameMapping mapping)
    {
        if (SharesAssociationValue(transaction.Who, mapping.Who))
        {
            return "Who";
        }

        if (SharesAssociationValue(transaction.Narrative2, mapping.Narrative2))
        {
            return "Narrative 2";
        }

        if (SharesAssociationValue(transaction.Narrative1, mapping.Narrative1))
        {
            return "Narrative 1";
        }

        if (SharesAssociationValue(transaction.Narrative3, mapping.Narrative3))
        {
            return "Narrative 3";
        }

        return "Existing associated resource";
    }

    private static bool SharesAssociationValue(string? left, string? right)
    {
        var normalisedLeft = NormaliseAssociationValue(left);
        var normalisedRight = NormaliseAssociationValue(right);
        return !string.IsNullOrWhiteSpace(normalisedLeft)
            && string.Equals(normalisedLeft, normalisedRight, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormaliseAssociationValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var compact = new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());

        if (compact.All(character => character == '-'))
        {
            return string.Empty;
        }

        return IsGenericAssociationValue(compact) ? string.Empty : compact;
    }

    private static string CleanCostCenterName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var cleaned = value.Trim();
        if (cleaned.StartsWith("SUGGESTED -- ", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned["SUGGESTED -- ".Length..].TrimStart();
        }

        if (cleaned.StartsWith("EXISTING CTC -- ", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned["EXISTING CTC -- ".Length..].TrimStart();
        }

        while (cleaned.StartsWith('('))
        {
            var closingIndex = cleaned.IndexOf(')');
            if (closingIndex <= 0 || closingIndex + 1 >= cleaned.Length || !char.IsWhiteSpace(cleaned[closingIndex + 1]))
            {
                break;
            }

            cleaned = cleaned[(closingIndex + 1)..].TrimStart();
        }

        if (cleaned.EndsWith(" (existing CTC)", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[..^" (existing CTC)".Length].TrimEnd();
        }

        return cleaned.Trim();
    }

    private static bool IsGenericAssociationValue(string value)
    {
        return value switch
        {
            "DETAILEDCOSTPOSTING" => true,
            "COSTPOSTING" => true,
            "POSTING" => true,
            "INVOICE" => true,
            "PAYMENT" => true,
            "CONSULTANT" => true,
            "CONTRACTOR" => true,
            "CONTRACTORSPAYMENTS" => true,
            _ => false
        };
    }

    private sealed class AssociatedCostCenterMatch
    {
        public CostCenterNameMapping Mapping { get; init; } = new();
        public int Score { get; init; }
    }

    private void ExportTransactions()
    {
        try
        {
            var path = _importExportInteraction.PickSaveFile(
                "Export raw transactions",
                "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                "ProjectCostForecast.transactions.csv");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            _csvTransactionService.ExportTransactions(path, Transactions);
            StatusText = $"Exported {Transactions.Count} transactions to {path}";
        }
        catch (Exception ex)
        {
            StatusText = $"Export failed: {ex.Message}";
            _importExportInteraction.ShowError("Export failed", ex.Message);
        }
    }

    private void SetupNewMonth()
    {
        if (Volatile.Read(ref _newMonthOperationInProgress) != 0)
        {
            return;
        }

        var currentPeriod = Header.CurrentPeriod;
        if (string.IsNullOrWhiteSpace(currentPeriod))
        {
            MessageBox.Show("Set a current period before creating a new month baseline.", "New month", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var nextPeriod = NewMonthOperation.GetNextForecastPeriod(_dataset, currentPeriod);
        var message = string.IsNullOrWhiteSpace(nextPeriod)
            ? $"Confirm you are ready to save the project file and set up a new month. This will save {currentPeriod} as a baseline and roll current forecast values into the previous month fields."
            : $"Confirm you are ready to save the project file and set up a new month. This will save {currentPeriod} as a baseline, roll current forecast values into the previous month fields, and move the current period to {nextPeriod}.";

        if (MessageBox.Show(message, "New month baseline", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        TryCreateNewMonthBaseline(confirmed: true, showError: true);
    }

    public bool TryCreateNewMonthBaseline(bool confirmed, bool showError = false)
    {
        if (!confirmed || Interlocked.CompareExchange(ref _newMonthOperationInProgress, 1, 0) != 0)
        {
            return false;
        }

        var selectedForecastLine = SelectedForecastLine;
        (int RowNumber, string TaskNumber, string ResourceName, string ProjectCode)? selectedForecastLineKey = selectedForecastLine is null
            ? null
            : (selectedForecastLine.RowNumber, selectedForecastLine.TaskNumber, selectedForecastLine.ResourceName, selectedForecastLine.ProjectCode);
        var selectedResourceName = SelectedResourceSummary?.ResourceName;

        try
        {
            SyncDatasetFromCollections();
            if (!TryBlockOperation("New month", _dataset, showError))
            {
                return false;
            }

            var preparation = _newMonthOperation.Prepare(_dataset);
            if (!preparation.IsReady)
            {
                StatusText = preparation.Message;
                if (showError && preparation.Status == NewMonthPreparationStatus.MissingCurrentPeriod)
                {
                    MessageBox.Show(preparation.Message, "New month", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                return false;
            }

            if (!SaveDataset(preparation.StagedDataset!, showError, "New month"))
            {
                return false;
            }

            LoadDataset(preparation.StagedDataset!, markDirty: false);
            RestoreNewMonthSelection(selectedForecastLineKey, selectedResourceName);
            IsDirty = false;
            StatusText = preparation.Message;
            return true;
        }
        catch (Exception ex)
        {
            StatusText = $"New month failed: {ex.Message}";
            if (showError)
            {
                MessageBox.Show(ex.Message, "New month failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return false;
        }
        finally
        {
            Volatile.Write(ref _newMonthOperationInProgress, 0);
        }
    }

    private void RestoreNewMonthSelection(
        (int RowNumber, string TaskNumber, string ResourceName, string ProjectCode)? selectedForecastLineKey,
        string? selectedResourceName)
    {
        if (selectedForecastLineKey is { } key)
        {
            var restoredLine = ForecastLines.FirstOrDefault(line =>
                line.RowNumber == key.RowNumber
                && string.Equals(line.TaskNumber, key.TaskNumber, StringComparison.OrdinalIgnoreCase)
                && string.Equals(line.ResourceName, key.ResourceName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(line.ProjectCode, key.ProjectCode, StringComparison.OrdinalIgnoreCase));
            if (restoredLine is not null)
            {
                SelectedForecastLine = restoredLine;
                return;
            }
        }

        if (!string.IsNullOrWhiteSpace(selectedResourceName))
        {
            SelectedResourceSummary = ResourceSummaries.FirstOrDefault(summary =>
                string.Equals(summary.ResourceName, selectedResourceName, StringComparison.OrdinalIgnoreCase));
            return;
        }

        if (selectedForecastLineKey is null && selectedResourceName is null)
        {
            SelectedForecastLine = null;
        }
    }

    private void OpenUnmatchedImportViewer()
    {
        if (UnmatchedImportCombinations.Count == 0)
        {
            _importExportInteraction.ShowInformation(
                "Unmatched imports",
                "There are no unmatched import combinations.");
            return;
        }

        if (!_importExportInteraction.CanShowUnmatchedImports)
        {
            return;
        }

        _importExportInteraction.ShowUnmatchedImports(UnmatchedImportCombinations.ToList());
    }

    private SavedMonthSnapshot BuildSavedMonthSnapshot(string period)
    {
        return _newMonthOperation.BuildSnapshot(period, ForecastLines);
    }

    private string GetNextForecastPeriod(string currentPeriod)
    {
        return NewMonthOperation.GetNextForecastPeriod(_dataset, currentPeriod);
    }

    private void OpenSavedMonthViewer()
    {
        var viewer = new SavedMonthSnapshotWindow(SavedMonthSnapshots)
        {
            Owner = Application.Current.MainWindow
        };
        if (viewer.ShowDialog() == true && viewer.SelectedSnapshotToOpen is { } snapshot)
        {
            ViewSavedMonthSnapshot(snapshot);
        }
    }
}
