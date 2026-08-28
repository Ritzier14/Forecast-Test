using System.Globalization;
using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App.Services;

public sealed class ValidationService
{
    // This is deliberately a broad operational bound rather than a business
    // limit. It catches corrupt imports and arithmetic overflows while still
    // allowing realistic project values, including credits and adjustments.
    public const decimal MaximumFinancialValue = 1_000_000_000_000m;

    public List<ValidationIssue> Validate(ProjectDataset dataset)
    {
        return ValidateReport(dataset).Issues.ToList();
    }

    public ValidationReport ValidateReport(ProjectDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        var issues = new List<ValidationIssue>();
        var forecastPeriods = dataset.ForecastPeriods ?? [];
        var forecastLines = dataset.ForecastLines ?? [];
        var transactions = dataset.Transactions ?? [];
        var hasOperationalData = forecastPeriods.Count > 0
            || forecastLines.Count > 0
            || transactions.Count > 0
            || (dataset.SavedMonthSnapshots?.Count ?? 0) > 0;

        ValidateHeader(issues, dataset.Header, hasOperationalData, forecastPeriods);
        var configuredPeriodDates = ValidateForecastPeriods(issues, forecastPeriods);
        ValidateForecastLines(issues, forecastLines, configuredPeriodDates);
        ValidateManagementResources(issues, dataset.ManagementResources ?? [], configuredPeriodDates);
        ValidateTransactions(issues, transactions);
        ValidateBudgets(issues, dataset.FiscalYearBudgets ?? [], dataset.BudgetLines ?? []);
        ValidateContingency(issues, dataset.ContingencyEntries ?? []);
        ValidateSnapshots(issues, dataset.SavedMonthSnapshots ?? []);
        ValidatePhases(issues, dataset.Phases ?? []);
        ValidateResourceMappings(issues, transactions);

        return new ValidationReport(issues);
    }

    public ValidationReport ValidateForOperation(ProjectDataset dataset)
    {
        return ValidateReport(dataset);
    }

    private static void ValidateHeader(
        ICollection<ValidationIssue> issues,
        ProjectHeader? header,
        bool hasOperationalData,
        IReadOnlyList<ForecastPeriod> forecastPeriods)
    {
        if (header is null)
        {
            AddIssue(issues, ValidationIssueCodes.ProjectTitleRequired, ValidationSeverities.Error, "Project", string.Empty, nameof(ProjectHeader.ProjectTitle), "Project title is required. Enter a project title before saving or opening this project.", "Project identity");
            if (hasOperationalData)
            {
                AddIssue(issues, ValidationIssueCodes.CurrentPeriodRequired, ValidationSeverities.Error, "Project", string.Empty, nameof(ProjectHeader.CurrentPeriod), "Current period is required when the project contains forecast or transaction data. Select a current FY period before saving or rolling over the month.", "Project period");
            }

            return;
        }

        AddRequired(issues, ValidationIssueCodes.ProjectTitleRequired, "Project", header.ProjectTitle, nameof(ProjectHeader.ProjectTitle), "Project title is required. Enter a project title before saving or opening this project.", "Project identity");

        if (hasOperationalData)
        {
            AddRequired(issues, ValidationIssueCodes.CurrentPeriodRequired, "Project", header.CurrentPeriod, nameof(ProjectHeader.CurrentPeriod), "Current period is required when the project contains forecast or transaction data. Select a current FY period before saving or rolling over the month.", "Project period");
        }

        if (string.IsNullOrWhiteSpace(header.CurrentPeriod))
        {
            return;
        }

        if (!TryParseCanonicalPeriod(header.CurrentPeriod, out _, out _))
        {
            AddIssue(issues, ValidationIssueCodes.CurrentPeriodInvalid, ValidationSeverities.Error, "Project", header.CurrentPeriod, nameof(ProjectHeader.CurrentPeriod), $"Current period '{header.CurrentPeriod}' is not a valid FY period in yy-mm form. Select a valid FY period before saving or rolling over the month.", "Project period");
            return;
        }

        var configuredPeriods = forecastPeriods
            .Where(period => period is not null && TryParseCanonicalPeriod(period.Label, out _, out _))
            .Select(period => FiscalPeriod.NormaliseLabel(period.Label))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (configuredPeriods.Count > 0 && !configuredPeriods.Contains(FiscalPeriod.NormaliseLabel(header.CurrentPeriod)))
        {
            AddIssue(issues, ValidationIssueCodes.CurrentPeriodNotConfigured, ValidationSeverities.Error, "Project", header.CurrentPeriod, nameof(ProjectHeader.CurrentPeriod), $"Current period '{header.CurrentPeriod}' is not present in Forecast periods. Add that period or select a configured period before saving or creating a new month.", "Project period");
        }
    }

    private static Dictionary<string, DateOnly> ValidateForecastPeriods(
        ICollection<ValidationIssue> issues,
        IReadOnlyList<ForecastPeriod> periods)
    {
        var configuredPeriodDates = new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < periods.Count; index++)
        {
            var period = periods[index];
            var entityId = string.IsNullOrWhiteSpace(period?.Label) ? (index + 1).ToString(CultureInfo.InvariantCulture) : period.Label;
            if (period is null)
            {
                AddIssue(issues, ValidationIssueCodes.ForecastPeriodLabelRequired, ValidationSeverities.Error, "ForecastPeriod", entityId, nameof(ForecastPeriod.Label), "Forecast period label is required. Add a valid FY period before saving or importing data.", "Forecast periods");
                continue;
            }

            AddRequired(issues, ValidationIssueCodes.ForecastPeriodLabelRequired, "ForecastPeriod", period.Label, nameof(ForecastPeriod.Label), "Forecast period label is required. Add a valid FY period before saving or importing data.", "Forecast periods", entityId);
            if (!TryParseCanonicalPeriod(period.Label, out _, out _))
            {
                AddIssue(issues, ValidationIssueCodes.ForecastPeriodInvalid, ValidationSeverities.Error, "ForecastPeriod", entityId, nameof(ForecastPeriod.Label), $"Forecast period '{period.Label}' is not a valid FY period in yy-mm form. Rename it to a valid period before saving.", "Forecast periods");
                continue;
            }

            FiscalPeriod.TryGetCalendarMonthStart(period.Label, out var expectedDate);
            if (period.StartDate is null)
            {
                AddIssue(issues, ValidationIssueCodes.ForecastPeriodDateMissing, ValidationSeverities.Error, "ForecastPeriod", entityId, nameof(ForecastPeriod.StartDate), $"Forecast period '{period.Label}' has no calendar start date. Rebuild or assign its canonical month start before saving.", "Forecast periods");
            }
            else if (period.StartDate != expectedDate)
            {
                AddIssue(issues, ValidationIssueCodes.ForecastPeriodDateMismatch, ValidationSeverities.Error, "ForecastPeriod", entityId, nameof(ForecastPeriod.StartDate), $"Forecast period '{period.Label}' starts on {period.StartDate:yyyy-MM-dd}, but its canonical month start is {expectedDate:yyyy-MM-dd}. Correct the date before saving.", "Forecast periods");
            }

            configuredPeriodDates[FiscalPeriod.NormaliseLabel(period.Label)] = expectedDate;
        }

        foreach (var duplicate in periods
                     .Where(period => period is not null && !string.IsNullOrWhiteSpace(period.Label))
                     .GroupBy(period => FiscalPeriod.NormaliseLabel(period.Label), StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            AddIssue(issues, ValidationIssueCodes.ForecastPeriodDuplicate, ValidationSeverities.Error, "ForecastPeriod", duplicate.Key, nameof(ForecastPeriod.Label), $"Forecast period '{duplicate.Key}' appears more than once. Keep one configured period before saving.", "Forecast periods");
        }

        return configuredPeriodDates;
    }

    private static void ValidateForecastLines(
        ICollection<ValidationIssue> issues,
        IReadOnlyList<ForecastLine> lines,
        IReadOnlyDictionary<string, DateOnly> configuredPeriodDates)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var entityId = line is null || line.RowNumber == 0 ? (index + 1).ToString(CultureInfo.InvariantCulture) : line.RowNumber.ToString(CultureInfo.InvariantCulture);
            if (line is null)
            {
                AddIssue(issues, ValidationIssueCodes.ForecastLineTaskNumberRequired, ValidationSeverities.Error, "ForecastLine", entityId, nameof(ForecastLine.TaskNumber), "Forecast line is empty. Add task, resource, and project identifiers before saving.", "Forecast line data");
                continue;
            }

            AddRequired(issues, ValidationIssueCodes.ForecastLineTaskNumberRequired, "ForecastLine", line.TaskNumber, nameof(ForecastLine.TaskNumber), "Task number is required. Enter a task number before saving or importing this row.", "Forecast line data", entityId);
            AddRequired(issues, ValidationIssueCodes.ForecastLineResourceNameRequired, "ForecastLine", line.ResourceName, nameof(ForecastLine.ResourceName), "Resource name is required. Enter a resource name before saving or importing this row.", "Forecast line data", entityId);
            AddRequired(issues, ValidationIssueCodes.ForecastLineProjectCodeRequired, "ForecastLine", line.ProjectCode, nameof(ForecastLine.ProjectCode), "Project category is required. Enter a project category before saving or importing this row.", "Forecast line data", entityId);

            if (line.Budget < 0)
            {
                AddIssue(issues, ValidationIssueCodes.ForecastLineBudgetNegative, ValidationSeverities.Error, "ForecastLine", entityId, nameof(ForecastLine.Budget), "Forecast line budget cannot be negative. Enter zero or a positive budget before saving.", "Financial bounds");
            }

            AddFinancialBound(issues, "ForecastLine", entityId, nameof(ForecastLine.Budget), line.Budget, ValidationIssueCodes.ForecastLineValueOutOfRange);
            foreach (var costLine in line.TaskCostLines ?? [])
            {
                if (costLine is not null)
                {
                    AddFinancialBound(issues, "ForecastTaskCostLine", line.TaskNumber, nameof(ForecastTaskCostLine.Amount), costLine.Amount, ValidationIssueCodes.ForecastLineValueOutOfRange);
                }
            }

            var monthlyForecasts = line.MonthlyForecasts ?? [];
            foreach (var forecast in monthlyForecasts)
            {
                if (forecast is null)
                {
                    AddIssue(issues, ValidationIssueCodes.ForecastPeriodEntryRequired, ValidationSeverities.Error, "MonthlyForecast", entityId, nameof(MonthlyForecast.PeriodLabel), "Monthly forecast entry is empty. Add a configured FY period before saving.", "Forecast periods");
                    continue;
                }

                ValidateConfiguredPeriodEntry(issues, "MonthlyForecast", entityId, forecast.PeriodLabel, forecast.PeriodStartDate, configuredPeriodDates, nameof(MonthlyForecast.PeriodLabel), nameof(MonthlyForecast.PeriodStartDate));
                AddFinancialBound(issues, "MonthlyForecast", entityId, nameof(MonthlyForecast.Amount), forecast.Amount, ValidationIssueCodes.ForecastLineValueOutOfRange);
            }

            foreach (var duplicate in monthlyForecasts
                         .Where(forecast => forecast is not null && !string.IsNullOrWhiteSpace(forecast.PeriodLabel))
                         .GroupBy(forecast => FiscalPeriod.NormaliseLabel(forecast.PeriodLabel), StringComparer.OrdinalIgnoreCase)
                         .Where(group => group.Count() > 1))
            {
                AddIssue(issues, ValidationIssueCodes.ForecastPeriodEntryDuplicate, ValidationSeverities.Error, "MonthlyForecast", entityId, nameof(MonthlyForecast.PeriodLabel), $"Forecast line '{entityId}' has duplicate monthly forecast entries for '{duplicate.Key}'. Keep one value per configured period before saving.", "Forecast periods");
            }

            foreach (var phase in line.TaskPhases ?? [])
            {
                if (phase is not null)
                {
                    ValidateOptionalPeriodLabel(issues, "ForecastTaskPhase", entityId, phase.StartPeriodLabel, nameof(ForecastTaskPhase.StartPeriodLabel));
                    ValidateOptionalPeriodLabel(issues, "ForecastTaskPhase", entityId, phase.EndPeriodLabel, nameof(ForecastTaskPhase.EndPeriodLabel));
                }
            }
        }

        foreach (var duplicate in lines
                     .Where(line => line is not null && !string.IsNullOrWhiteSpace(line.TaskNumber) && !string.IsNullOrWhiteSpace(line.ResourceName) && !string.IsNullOrWhiteSpace(line.ProjectCode))
                     .GroupBy(BuildForecastLineIdentity, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            AddIssue(issues, ValidationIssueCodes.ForecastLineDuplicateIdentity, ValidationSeverities.Error, "ForecastLine", duplicate.Key, nameof(ForecastLine.TaskNumber), "Forecast line identity is duplicated. Keep one line per task, resource, and transaction project scope before saving.", "Forecast line identity");
        }

        foreach (var line in lines.Where(line => line is not null && line.Budget == 0 && line.PlannedCostFcc != 0))
        {
            AddIssue(issues, "FORECAST_LINE_BUDGET_VARIANCE", ValidationSeverities.Warning, "ForecastLine", line.RowNumber.ToString(CultureInfo.InvariantCulture), nameof(ForecastLine.Budget), $"{line.ResourceName} has planned cost but no budget.", "Budget variance");
        }
    }

    private static void ValidateManagementResources(
        ICollection<ValidationIssue> issues,
        IReadOnlyList<ManagementResource> resources,
        IReadOnlyDictionary<string, DateOnly> configuredPeriodDates)
    {
        foreach (var resource in resources)
        {
            if (resource is null)
            {
                continue;
            }

            var entityId = resource.SourceRowNumber.ToString(CultureInfo.InvariantCulture);
            AddFinancialBound(issues, "ManagementResource", entityId, nameof(ManagementResource.HourlyRate), resource.HourlyRate);
            AddFinancialBound(issues, "ManagementResource", entityId, nameof(ManagementResource.CalculatedHourlyRate), resource.CalculatedHourlyRate);
            AddFinancialBound(issues, "ManagementResource", entityId, nameof(ManagementResource.MonthlyHours), resource.MonthlyHours);

            foreach (var allocation in resource.MonthlyAllocations ?? [])
            {
                if (allocation is null)
                {
                    continue;
                }

                ValidateConfiguredPeriodEntry(issues, "ManagementAllocation", entityId, allocation.PeriodLabel, allocation.PeriodStartDate, configuredPeriodDates, nameof(ManagementResourceAllocation.PeriodLabel), nameof(ManagementResourceAllocation.PeriodStartDate));
                if (allocation.Percentage is < 0 or > 100)
                {
                    AddIssue(issues, ValidationIssueCodes.ManagementAllocationOutOfRange, ValidationSeverities.Error, "ManagementAllocation", entityId, nameof(ManagementResourceAllocation.Percentage), "Management allocation must be between 0 and 100 percent. Correct the allocation before saving.", "Management allocation");
                }
            }
        }
    }

    private static void ValidateTransactions(ICollection<ValidationIssue> issues, IReadOnlyList<CostTransaction> transactions)
    {
        for (var index = 0; index < transactions.Count; index++)
        {
            var transaction = transactions[index];
            var entityId = transaction is null || transaction.RowNumber == 0 ? (index + 1).ToString(CultureInfo.InvariantCulture) : transaction.RowNumber.ToString(CultureInfo.InvariantCulture);
            if (transaction is null)
            {
                AddIssue(issues, ValidationIssueCodes.TransactionTaskNumberRequired, ValidationSeverities.Error, "Transaction", entityId, nameof(CostTransaction.TaskNumber), "Transaction row is empty. Add a task number and FY period before importing or saving.", "Imported transaction");
                continue;
            }

            AddRequired(issues, ValidationIssueCodes.TransactionFyPeriodRequired, "Transaction", transaction.FyPeriod, nameof(CostTransaction.FyPeriod), "FY period is required. Provide a valid yy-mm period before importing or saving this transaction.", "Imported transaction", entityId);
            AddRequired(issues, ValidationIssueCodes.TransactionTaskNumberRequired, "Transaction", transaction.TaskNumber, nameof(CostTransaction.TaskNumber), "Task number is required. Provide a task number before importing or saving this transaction.", "Imported transaction", entityId);
            if (!string.IsNullOrWhiteSpace(transaction.FyPeriod) && !TryParseCanonicalPeriod(transaction.FyPeriod, out _, out _))
            {
                AddIssue(issues, ValidationIssueCodes.TransactionFyPeriodInvalid, ValidationSeverities.Error, "Transaction", entityId, nameof(CostTransaction.FyPeriod), $"FY period '{transaction.FyPeriod}' is not valid yy-mm data. Correct the import value before importing or saving this transaction.", "Imported transaction");
            }

            if (transaction.Units < 0)
            {
                AddIssue(issues, ValidationIssueCodes.TransactionUnitsNegative, ValidationSeverities.Error, "Transaction", entityId, nameof(CostTransaction.Units), "Transaction units cannot be negative. Correct the source row before importing or saving.", "Financial bounds");
            }

            if (transaction.UnitRate < 0)
            {
                AddIssue(issues, ValidationIssueCodes.TransactionUnitRateNegative, ValidationSeverities.Error, "Transaction", entityId, nameof(CostTransaction.UnitRate), "Transaction unit rate cannot be negative. Correct the source row before importing or saving.", "Financial bounds");
            }

            AddFinancialBound(issues, "Transaction", entityId, nameof(CostTransaction.Units), transaction.Units, ValidationIssueCodes.TransactionValueOutOfRange);
            AddFinancialBound(issues, "Transaction", entityId, nameof(CostTransaction.UnitRate), transaction.UnitRate, ValidationIssueCodes.TransactionValueOutOfRange);
            AddFinancialBound(issues, "Transaction", entityId, nameof(CostTransaction.Amount), transaction.Amount, ValidationIssueCodes.TransactionValueOutOfRange);

            if (string.IsNullOrWhiteSpace(transaction.ManualName) && string.IsNullOrWhiteSpace(transaction.ResourceDescription))
            {
                AddIssue(issues, ValidationIssueCodes.TransactionNameMappingMissing, ValidationSeverities.Warning, "Transaction", entityId, nameof(CostTransaction.ManualName), "Manual name and resource description are both blank. Map a resource name before reviewing the imported cost.", "Imported name mapping");
            }

            if (transaction.Units <= 0 && transaction.Amount != 0)
            {
                AddIssue(issues, ValidationIssueCodes.TransactionUnitsNonPositiveWithAmount, ValidationSeverities.Warning, "Transaction", entityId, nameof(CostTransaction.Units), "Units are zero or negative while amount is non-zero. Review the source row; this warning does not block the operation.", "Imported transaction");
            }
        }

        foreach (var duplicate in transactions
                     .Where(transaction => transaction is not null)
                     .GroupBy(CsvTransactionService.BuildDuplicateKey, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            AddIssue(issues, ValidationIssueCodes.TransactionDuplicateIdentity, ValidationSeverities.Error, "Transaction", duplicate.First().RowNumber.ToString(CultureInfo.InvariantCulture), nameof(CostTransaction.RowNumber), "Transaction identity is duplicated. Remove the duplicate source row before importing or saving.", "Imported transaction");
        }
    }

    private static void ValidateBudgets(ICollection<ValidationIssue> issues, IReadOnlyList<FiscalYearBudget> fiscalYearBudgets, IReadOnlyList<FiscalYearBudgetLine> budgetLines)
    {
        foreach (var budget in fiscalYearBudgets)
        {
            if (budget is not null)
            {
                AddNonNegativeFinancialValue(issues, "FiscalYearBudget", budget.FiscalYear, nameof(FiscalYearBudget.Budget), budget.Budget);
            }
        }

        foreach (var line in budgetLines)
        {
            if (line is null)
            {
                continue;
            }

            foreach (var amount in line.Amounts ?? [])
            {
                if (amount is not null)
                {
                    AddNonNegativeFinancialValue(issues, "FiscalYearBudgetAmount", amount.FiscalYear, nameof(FiscalYearBudgetAmount.Amount), amount.Amount);
                }
            }
        }
    }

    private static void ValidateContingency(ICollection<ValidationIssue> issues, IReadOnlyList<ContingencyEntry> entries)
    {
        foreach (var entry in entries)
        {
            if (entry is not null)
            {
                var entityId = entry.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
                AddFinancialBound(issues, "Contingency", entityId, nameof(ContingencyEntry.ContingencyExpended), entry.ContingencyExpended);
                AddFinancialBound(issues, "Contingency", entityId, nameof(ContingencyEntry.RemainingContingency), entry.RemainingContingency);
                AddFinancialBound(issues, "Contingency", entityId, nameof(ContingencyEntry.ProposedExpenditure), entry.ProposedExpenditure);
            }
        }
    }

    private static void ValidateSnapshots(ICollection<ValidationIssue> issues, IReadOnlyList<SavedMonthSnapshot> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            if (snapshot is null)
            {
                continue;
            }

            AddRequired(issues, ValidationIssueCodes.SnapshotPeriodRequired, "SavedMonthSnapshot", snapshot.Period, nameof(SavedMonthSnapshot.Period), "Saved month period is required. Record a valid baseline period before saving.", "Saved month snapshot");
            if (!string.IsNullOrWhiteSpace(snapshot.Period) && !TryParseCanonicalPeriod(snapshot.Period, out _, out _))
            {
                AddIssue(issues, ValidationIssueCodes.SnapshotPeriodInvalid, ValidationSeverities.Error, "SavedMonthSnapshot", snapshot.Period, nameof(SavedMonthSnapshot.Period), $"Saved month period '{snapshot.Period}' is not valid yy-mm data. Correct the baseline period before saving.", "Saved month snapshot");
            }

            AddFinancialBound(issues, "SavedMonthSnapshot", snapshot.Period, nameof(SavedMonthSnapshot.CostToDate), snapshot.CostToDate, ValidationIssueCodes.SnapshotValueOutOfRange);
            AddFinancialBound(issues, "SavedMonthSnapshot", snapshot.Period, nameof(SavedMonthSnapshot.CostToComplete), snapshot.CostToComplete, ValidationIssueCodes.SnapshotValueOutOfRange);
            AddFinancialBound(issues, "SavedMonthSnapshot", snapshot.Period, nameof(SavedMonthSnapshot.FinalForecast), snapshot.FinalForecast, ValidationIssueCodes.SnapshotValueOutOfRange);
            AddFinancialBound(issues, "SavedMonthSnapshot", snapshot.Period, nameof(SavedMonthSnapshot.TotalBudgetVariance), snapshot.TotalBudgetVariance, ValidationIssueCodes.SnapshotValueOutOfRange);

            foreach (var line in snapshot.ForecastLines ?? [])
            {
                if (line is null)
                {
                    continue;
                }

                var entityId = line.RowNumber.ToString(CultureInfo.InvariantCulture);
                AddRequired(issues, ValidationIssueCodes.ForecastLineTaskNumberRequired, "SavedMonthForecastLine", line.TaskNumber, nameof(SavedMonthForecastLine.TaskNumber), "Saved forecast line task number is required. Repair the baseline line before saving.", "Saved month snapshot", entityId);
                AddRequired(issues, ValidationIssueCodes.ForecastLineResourceNameRequired, "SavedMonthForecastLine", line.ResourceName, nameof(SavedMonthForecastLine.ResourceName), "Saved forecast line resource name is required. Repair the baseline line before saving.", "Saved month snapshot", entityId);
                AddRequired(issues, ValidationIssueCodes.ForecastLineProjectCodeRequired, "SavedMonthForecastLine", line.ProjectCode, nameof(SavedMonthForecastLine.ProjectCode), "Saved forecast line project category is required. Repair the baseline line before saving.", "Saved month snapshot", entityId);
                AddFinancialBound(issues, "SavedMonthForecastLine", entityId, nameof(SavedMonthForecastLine.Budget), line.Budget, ValidationIssueCodes.SnapshotValueOutOfRange);
                AddFinancialBound(issues, "SavedMonthForecastLine", entityId, nameof(SavedMonthForecastLine.CostToDate), line.CostToDate, ValidationIssueCodes.SnapshotValueOutOfRange);
                AddFinancialBound(issues, "SavedMonthForecastLine", entityId, nameof(SavedMonthForecastLine.CostToComplete), line.CostToComplete, ValidationIssueCodes.SnapshotValueOutOfRange);
                AddFinancialBound(issues, "SavedMonthForecastLine", entityId, nameof(SavedMonthForecastLine.FinalForecast), line.FinalForecast, ValidationIssueCodes.SnapshotValueOutOfRange);

                foreach (var amount in line.MonthlyForecasts ?? [])
                {
                    if (amount is not null)
                    {
                        if (!string.IsNullOrWhiteSpace(amount.PeriodLabel) && !TryParseCanonicalPeriod(amount.PeriodLabel, out _, out _))
                        {
                            AddIssue(issues, ValidationIssueCodes.ForecastPeriodEntryInvalid, ValidationSeverities.Error, "SavedMonthPeriodAmount", entityId, nameof(SavedMonthPeriodAmount.PeriodLabel), $"Saved forecast amount period '{amount.PeriodLabel}' is not valid yy-mm data. Correct the baseline entry before saving.", "Saved month snapshot");
                        }

                        AddFinancialBound(issues, "SavedMonthPeriodAmount", entityId, nameof(SavedMonthPeriodAmount.Amount), amount.Amount, ValidationIssueCodes.SnapshotValueOutOfRange);
                    }
                }
            }
        }
    }

    private static void ValidatePhases(ICollection<ValidationIssue> issues, IReadOnlyList<PhaseItem> phases)
    {
        foreach (var phase in phases)
        {
            if (phase?.Start is not null && phase.End is not null && phase.Start > phase.End)
            {
                AddIssue(issues, ValidationIssueCodes.PhaseDateOrderInvalid, ValidationSeverities.Error, "Phase", phase.Name, nameof(PhaseItem.End), "Phase end date cannot be before its start date. Correct the phase dates before saving.", "Project dates");
            }
        }
    }

    private static void ValidateResourceMappings(ICollection<ValidationIssue> issues, IReadOnlyList<CostTransaction> transactions)
    {
        var duplicateResourceCodes = transactions
            .Where(transaction => transaction is not null && !string.IsNullOrWhiteSpace(transaction.ResourceCode) && !string.IsNullOrWhiteSpace(transaction.LedgerResourceName))
            .GroupBy(transaction => transaction.ResourceCode, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(transaction => CalculationService.Normalise(transaction.LedgerResourceName)).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1);

        foreach (var duplicate in duplicateResourceCodes)
        {
            AddIssue(issues, ValidationIssueCodes.ResourceMappingConflict, ValidationSeverities.Warning, "Resource", duplicate.Key, nameof(CostTransaction.ResourceCode), $"Resource code {duplicate.Key} maps to multiple resource names. Review the mapping before using the imported costs.", "Resource mapping");
        }
    }

    private static void ValidateConfiguredPeriodEntry(
        ICollection<ValidationIssue> issues,
        string entityType,
        string entityId,
        string? periodLabel,
        DateOnly? periodStartDate,
        IReadOnlyDictionary<string, DateOnly> configuredPeriodDates,
        string labelFieldName,
        string dateFieldName)
    {
        if (string.IsNullOrWhiteSpace(periodLabel))
        {
            AddIssue(issues, ValidationIssueCodes.ForecastPeriodEntryRequired, ValidationSeverities.Error, entityType, entityId, labelFieldName, "A forecast entry period is required. Add a configured FY period before saving.", "Forecast periods");
            return;
        }

        if (!TryParseCanonicalPeriod(periodLabel, out _, out _))
        {
            AddIssue(issues, ValidationIssueCodes.ForecastPeriodEntryInvalid, ValidationSeverities.Error, entityType, entityId, labelFieldName, $"Forecast entry period '{periodLabel}' is not valid yy-mm data. Correct the period before saving.", "Forecast periods");
            return;
        }

        if (!configuredPeriodDates.TryGetValue(FiscalPeriod.NormaliseLabel(periodLabel), out var expectedDate))
        {
            AddIssue(issues, ValidationIssueCodes.ForecastPeriodEntryRequired, ValidationSeverities.Error, entityType, entityId, labelFieldName, $"Forecast entry period '{periodLabel}' is not configured in Forecast periods. Add the period or remove the entry before saving.", "Forecast periods");
            return;
        }

        if (periodStartDate is null)
        {
            AddIssue(issues, ValidationIssueCodes.ForecastPeriodEntryDateMissing, ValidationSeverities.Error, entityType, entityId, dateFieldName, $"Forecast entry '{periodLabel}' has no calendar start date. Restore the canonical date before saving.", "Forecast periods");
        }
        else if (periodStartDate != expectedDate)
        {
            AddIssue(issues, ValidationIssueCodes.ForecastPeriodEntryDateMismatch, ValidationSeverities.Error, entityType, entityId, dateFieldName, $"Forecast entry '{periodLabel}' starts on {periodStartDate:yyyy-MM-dd}, but its canonical month start is {expectedDate:yyyy-MM-dd}. Correct the date before saving.", "Forecast periods");
        }
    }

    private static void ValidateOptionalPeriodLabel(ICollection<ValidationIssue> issues, string entityType, string entityId, string? periodLabel, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(periodLabel) || TryParseCanonicalPeriod(periodLabel, out _, out _))
        {
            return;
        }

        AddIssue(issues, ValidationIssueCodes.ForecastPeriodEntryInvalid, ValidationSeverities.Error, entityType, entityId, fieldName, $"Period '{periodLabel}' is not valid yy-mm data. Correct the period before saving.", "Forecast periods");
    }

    private static bool TryParseCanonicalPeriod(string? periodLabel, out int year, out int month)
    {
        year = 0;
        month = 0;
        var normalized = FiscalPeriod.NormaliseLabel(periodLabel);
        if (!FiscalPeriod.TryParseLabel(normalized, out year, out month))
        {
            return false;
        }

        return string.Equals(normalized, FiscalPeriod.FormatLabel(year, month), StringComparison.Ordinal);
    }

    private static string BuildForecastLineIdentity(ForecastLine line)
    {
        return string.Join("\u001f", CalculationService.Normalise(line.TaskNumber), CalculationService.Normalise(line.ResourceName), line.TransactionProjectCode is null ? "<legacy>" : CalculationService.Normalise(line.TransactionProjectCode));
    }

    private static void AddRequired(ICollection<ValidationIssue> issues, string code, string entityType, string? value, string fieldName, string message, string category, string? entityId = null)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        AddIssue(issues, code, ValidationSeverities.Error, entityType, entityId ?? string.Empty, fieldName, message, category);
    }

    private static void AddNonNegativeFinancialValue(ICollection<ValidationIssue> issues, string entityType, string entityId, string fieldName, decimal value)
    {
        if (value < 0)
        {
            AddIssue(issues, ValidationIssueCodes.FinancialBudgetNegative, ValidationSeverities.Error, entityType, entityId, fieldName, "Budget values cannot be negative. Enter zero or a positive budget before saving.", "Financial bounds");
        }

        AddFinancialBound(issues, entityType, entityId, fieldName, value);
    }

    private static void AddFinancialBound(ICollection<ValidationIssue> issues, string entityType, string entityId, string fieldName, decimal value, string code = ValidationIssueCodes.FinancialValueOutOfRange)
    {
        if (value <= MaximumFinancialValue && value >= -MaximumFinancialValue)
        {
            return;
        }

        AddIssue(issues, code, ValidationSeverities.Error, entityType, entityId, fieldName, $"{fieldName} is outside the supported financial range of ±{MaximumFinancialValue.ToString("N0", CultureInfo.InvariantCulture)}. Reduce the value or correct the source row before saving or importing.", "Financial bounds");
    }

    private static void AddIssue(ICollection<ValidationIssue> issues, string code, string severity, string entityType, string entityId, string fieldName, string message, string category)
    {
        issues.Add(new ValidationIssue
        {
            Code = code,
            Severity = severity,
            Category = category,
            EntityType = entityType,
            EntityId = entityId,
            FieldName = fieldName,
            Message = message
        });
    }
}
