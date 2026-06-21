using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ProjectCostForecast.App;
using Microsoft.Win32;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;

namespace ProjectCostForecast.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void OpenProject()
    {
        if (!ConfirmDiscardUnsavedChanges())
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Open Project Cost Forecast file",
            Filter = "Project Cost Forecast JSON (*.json)|*.json|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            LoadDataset(_projectFileService.Load(dialog.FileName), markDirty: false);
            ProjectFilePath = dialog.FileName;
            StatusText = $"Opened {dialog.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Open failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool SaveProject()
    {
        if (string.IsNullOrWhiteSpace(ProjectFilePath))
        {
            return SaveProjectAs();
        }

        try
        {
            var backupPath = _projectFileService.CreateBackup(ProjectFilePath);
            SyncDatasetFromCollections();
            AddAuditEvent("Project", Header.ProjectTitle, "Saved", string.Empty, ProjectFilePath, "Project saved");
            _projectFileService.Save(ProjectFilePath, _dataset);
            IsDirty = false;
            StatusText = string.IsNullOrWhiteSpace(backupPath)
                ? $"Saved {ProjectFilePath}"
                : $"Saved {ProjectFilePath}; backup created.";
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Save failed", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private bool SaveProjectAs()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save Project Cost Forecast file",
            Filter = "Project Cost Forecast JSON (*.json)|*.json|All files (*.*)|*.*",
            FileName = BuildDefaultProjectFileName()
        };

        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        ProjectFilePath = dialog.FileName;
        return SaveProject();
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

    private void ImportCsv()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import raw transaction file",
            Filter = _csvTransactionService.GetSupportedFileFilter()
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        ImportTransactionFile(dialog.FileName);
    }

    public void ImportTransactionFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!_csvTransactionService.SupportsFile(path))
        {
            MessageBox.Show("Supported import files are .csv, .xlsx, and .xlsm.", "Import failed", MessageBoxButton.OK, MessageBoxImage.Information);
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

            if (!ApplyCostCenterNameMappings(newTransactions))
            {
                StatusText = "Import cancelled before any transaction rows were added.";
                return;
            }

            if (!ReviewForecastLineAutoCreatePreview(newTransactions))
            {
                StatusText = "Import cancelled before any transaction rows were added.";
                return;
            }

            var nextRow = Transactions.Any() ? Transactions.Max(t => t.RowNumber) + 1 : 1;
            foreach (var transaction in newTransactions)
            {
                transaction.RowNumber = nextRow++;
            }

            AddItems(Transactions, newTransactions);
            EnsureForecastLinesForImportedTransactions(newTransactions);

            AddAuditEvent(
                "TransactionImport",
                path,
                "ImportedRows",
                "0",
                newTransactions.Count.ToString(),
                duplicateCount == 0 ? "Imported raw transaction file" : $"Imported raw transaction file; skipped {duplicateCount} duplicate row(s)");
            RecalculateAndRefresh(markDirty: true, reason: duplicateCount == 0
                ? $"Imported {newTransactions.Count} new transaction rows"
                : $"Imported {newTransactions.Count} new transaction rows and skipped {duplicateCount} duplicate row(s)");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Import failed", MessageBoxButton.OK, MessageBoxImage.Error);
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

    private void EnsureForecastLinesForImportedTransactions(IEnumerable<CostTransaction> transactions)
    {
        var existingLineKeys = ForecastLines
            .Select(line => BuildForecastLineMatchKey(line.TaskNumber, line.ResourceName, line.ProjectCode))
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
            if (!existingLineKeys.Add(BuildForecastLineMatchKey(group.Key.Task, group.Key.Resource, group.Key.Project)))
            {
                continue;
            }

            var line = new ForecastLine
            {
                RowNumber = nextRow++,
                TaskNumber = sample.TaskNumber,
                ResourceName = sample.LedgerResourceName,
                ProjectCode = string.IsNullOrWhiteSpace(sample.ProjectCode) ? "Unassigned" : sample.ProjectCode,
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
            .Select(line => BuildForecastLineMatchKey(line.TaskNumber, line.ResourceName, line.ProjectCode))
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
            .Where(group => !existingLineKeys.Contains(BuildForecastLineMatchKey(group.Key.Task, group.Key.Resource, group.Key.Project)))
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

    private bool ReviewForecastLineAutoCreatePreview(IReadOnlyCollection<CostTransaction> transactions)
    {
        var previewItems = BuildForecastLineAutoCreatePreviewItems(transactions);
        if (previewItems.Count == 0)
        {
            return true;
        }

        if (!_userPreferences.ShowImportAutoCreatePreview
            || Thread.CurrentThread.GetApartmentState() != ApartmentState.STA
            || Application.Current is null)
        {
            return true;
        }

        var window = new ImportAutoCreatePreviewWindow(previewItems, _userPreferences.ShowImportAutoCreatePreview)
        {
            Owner = Application.Current.MainWindow
        };

        var importAccepted = window.ShowDialog() == true;
        _userPreferences.ShowImportAutoCreatePreview = window.ShowPreviewNextTime;
        SaveUserPreferences();
        if (!importAccepted)
        {
            RoutePreviewItemsToUnmatchedList(previewItems);
            OpenUnmatchedImportViewer();
            return false;
        }

        ApplyForecastLineAutoCreatePreviewEdits(transactions, window.PreviewItems);
        return true;
    }

    private void ApplyForecastLineAutoCreatePreviewEdits(
        IEnumerable<CostTransaction> transactions,
        IEnumerable<ImportAutoCreatePreviewItem> previewItems)
    {
        var mappingsByKey = (_dataset.CostCenterNameMappings ?? [])
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
                    mapping.LastUsedAt = DateTime.Now;
                }
            }
        }
    }

    private void RoutePreviewItemsToUnmatchedList(IEnumerable<ImportAutoCreatePreviewItem> previewItems)
    {
        var recordedAt = DateTime.Now;
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

    private bool ApplyCostCenterNameMappings(IReadOnlyCollection<CostTransaction> transactions)
    {
        _dataset.CostCenterNameMappings ??= [];
        var mappingsByKey = _dataset.CostCenterNameMappings
            .Where(mapping => !string.IsNullOrWhiteSpace(mapping.Key) && !string.IsNullOrWhiteSpace(mapping.ManualName))
            .GroupBy(mapping => mapping.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(mapping => mapping.LastUsedAt).First(), StringComparer.OrdinalIgnoreCase);
        var newMappings = new List<CostCenterNameMapping>();
        var resolvedGroups = new List<(IReadOnlyCollection<CostTransaction> Rows, CostCenterNameMapping Mapping)>();
        var unresolvedGroups = new List<UnresolvedCostCenterGroup>();

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
            var groupedDetails = seed.SuggestedOption is null
                ? [seed]
                : unresolvedDetails
                    .Where(detail => detail.SuggestedOption is not null
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
            var mappingWindow = new CostCenterMappingWindow(
                seed.Group.Sample,
                combinedRows,
                combinedCandidates,
                seed.SuggestedOption,
                GetExistingCostCenterNames(candidateMappings),
                unresolvedGroups.Count)
            {
                Owner = Application.Current?.MainWindow
            };

            if (mappingWindow.ShowDialog() != true)
            {
                return false;
            }

            foreach (var detail in groupedDetails)
            {
                var mapping = CreateCostCenterNameMapping(detail.Group.Sample, mappingWindow.SelectedManualName);
                newMappings.Add(mapping);
                mappingsByKey[detail.Group.Key] = mapping;
                resolvedGroups.Add((detail.Group.Rows, mapping));
            }

            unresolvedGroups.RemoveAll(group =>
                groupedDetails.Any(detail => string.Equals(detail.Group.Key, group.Key, StringComparison.OrdinalIgnoreCase)));
        }

        foreach (var mapping in newMappings)
        {
            _dataset.CostCenterNameMappings.Add(mapping);
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

    private static CostCenterNameMapping CreateCostCenterNameMapping(CostTransaction sample, string manualName)
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
            LastUsedAt = DateTime.Now
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
        mapping.LastUsedAt = DateTime.Now;
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
        var dialog = new SaveFileDialog
        {
            Title = "Export raw transactions",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = "ProjectCostForecast.transactions.csv"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            _csvTransactionService.ExportTransactions(dialog.FileName, Transactions);
            StatusText = $"Exported {Transactions.Count} transactions to {dialog.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Export failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SetupNewMonth()
    {
        SyncDatasetFromCollections();
        ApplyClosedForecastPeriodRule();
        var currentPeriod = Header.CurrentPeriod;
        if (string.IsNullOrWhiteSpace(currentPeriod))
        {
            MessageBox.Show("Set a current period before creating a new month baseline.", "New month", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _calculationService.Recalculate(_dataset);
        ReplaceCollection(ForecastLines, _dataset.ForecastLines);

        var nextPeriod = GetNextForecastPeriod(currentPeriod);
        var message = string.IsNullOrWhiteSpace(nextPeriod)
            ? $"Confirm you are ready to save the project file and set up a new month. This will save {currentPeriod} as a baseline and roll current forecast values into the previous month fields."
            : $"Confirm you are ready to save the project file and set up a new month. This will save {currentPeriod} as a baseline, roll current forecast values into the previous month fields, and move the current period to {nextPeriod}.";

        if (MessageBox.Show(message, "New month baseline", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        var snapshot = BuildSavedMonthSnapshot(currentPeriod);
        SavedMonthSnapshots.Insert(0, snapshot);

        foreach (var line in ForecastLines)
        {
            line.LastMonthPlannedCost = line.PlannedCostFcc;
            line.LastMonthForecast = line.MonthForecast;
        }

        if (!string.IsNullOrWhiteSpace(nextPeriod))
        {
            Header.CurrentPeriod = nextPeriod;
            OnPropertyChanged(nameof(Header));
        }

        AddAuditEvent("SavedMonth", currentPeriod, "Baseline", string.Empty, snapshot.SavedAt.ToString("s"), "Created new month baseline");
        AddAuditEvent("SavedMonth", currentPeriod, "FutureAction", string.Empty, "UnlockOpenSavedMonth", "Future unlock-open-saved-month action recorded");
        RecalculateAndRefresh(markDirty: true, reason: string.IsNullOrWhiteSpace(nextPeriod)
            ? $"Saved {currentPeriod} baseline"
            : $"Saved {currentPeriod} baseline and moved to {nextPeriod}");
        SaveProject();
    }

    private void OpenUnmatchedImportViewer()
    {
        if (UnmatchedImportCombinations.Count == 0)
        {
            MessageBox.Show("There are no unmatched import combinations.", "Unmatched imports", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var viewer = new UnmatchedImportWindow(UnmatchedImportCombinations)
        {
            Owner = Application.Current.MainWindow
        };
        viewer.ShowDialog();
    }

    private SavedMonthSnapshot BuildSavedMonthSnapshot(string period)
    {
        var lines = ForecastLines.Select(line => new SavedMonthForecastLine
        {
            RowNumber = line.RowNumber,
            TaskNumber = line.TaskNumber,
            ResourceName = line.ResourceName,
            ProjectCode = line.ProjectCode,
            CostToDate = line.CostToDateSummary,
            CurrentPeriodForecast = line.MonthForecast,
            CostToComplete = line.TotalForecastCtc,
            FinalForecast = line.PlannedCostFcc,
            Budget = line.Budget,
            TotalBudgetVariance = line.TotalBudgetVariance,
            VarianceFromPreviousMonth = line.VarianceLastMonthToDate,
            MonthlyForecasts = line.MonthlyForecasts.Select(forecast => new SavedMonthPeriodAmount
            {
                PeriodLabel = forecast.PeriodLabel,
                PeriodStartDate = forecast.PeriodStartDate,
                Amount = forecast.Amount
            }).ToList()
        }).ToList();

        return new SavedMonthSnapshot
        {
            Period = period,
            SavedAt = DateTime.Now,
            CostToDate = lines.Sum(line => line.CostToDate),
            CostToComplete = lines.Sum(line => line.CostToComplete),
            FinalForecast = lines.Sum(line => line.FinalForecast),
            TotalBudgetVariance = lines.Sum(line => line.TotalBudgetVariance),
            ForecastLines = lines
        };
    }

    private string GetNextForecastPeriod(string currentPeriod)
    {
        var periods = _dataset.ForecastPeriods
            .Where(period => !string.IsNullOrWhiteSpace(period.Label))
            .OrderBy(period => period.StartDate ?? DateOnly.MaxValue)
            .ThenBy(period => period.Label)
            .Select(period => period.Label)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var index = periods.FindIndex(period => string.Equals(period, currentPeriod, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < periods.Count ? periods[index + 1] : string.Empty;
    }

    private void OpenSavedMonthViewer()
    {
        var viewer = new SavedMonthSnapshotWindow(SavedMonthSnapshots)
        {
            Owner = Application.Current.MainWindow
        };
        viewer.ShowDialog();
    }
}
