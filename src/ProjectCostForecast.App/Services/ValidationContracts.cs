using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App.Services;

public static class ValidationSeverities
{
    public const string Error = "Error";
    public const string Warning = "Warning";
}

public static class ValidationIssueCodes
{
    public const string ProjectTitleRequired = "PROJECT_TITLE_REQUIRED";
    public const string CurrentPeriodRequired = "CURRENT_PERIOD_REQUIRED";
    public const string CurrentPeriodInvalid = "CURRENT_PERIOD_INVALID";
    public const string CurrentPeriodNotConfigured = "CURRENT_PERIOD_NOT_CONFIGURED";

    public const string ForecastPeriodLabelRequired = "FORECAST_PERIOD_LABEL_REQUIRED";
    public const string ForecastPeriodInvalid = "FORECAST_PERIOD_INVALID";
    public const string ForecastPeriodDateMissing = "FORECAST_PERIOD_DATE_MISSING";
    public const string ForecastPeriodDateMismatch = "FORECAST_PERIOD_DATE_MISMATCH";
    public const string ForecastPeriodDuplicate = "FORECAST_PERIOD_DUPLICATE";
    public const string ForecastPeriodEntryRequired = "FORECAST_PERIOD_ENTRY_REQUIRED";
    public const string ForecastPeriodEntryInvalid = "FORECAST_PERIOD_ENTRY_INVALID";
    public const string ForecastPeriodEntryDateMissing = "FORECAST_PERIOD_ENTRY_DATE_MISSING";
    public const string ForecastPeriodEntryDateMismatch = "FORECAST_PERIOD_ENTRY_DATE_MISMATCH";
    public const string ForecastPeriodEntryDuplicate = "FORECAST_PERIOD_ENTRY_DUPLICATE";

    public const string ForecastLineTaskNumberRequired = "FORECAST_LINE_TASK_REQUIRED";
    public const string ForecastLineResourceNameRequired = "FORECAST_LINE_RESOURCE_REQUIRED";
    public const string ForecastLineProjectCodeRequired = "FORECAST_LINE_PROJECT_REQUIRED";
    public const string ForecastLineDuplicateIdentity = "FORECAST_LINE_DUPLICATE_IDENTITY";
    public const string ForecastLineBudgetNegative = "FORECAST_LINE_BUDGET_NEGATIVE";
    public const string ForecastLineValueOutOfRange = "FORECAST_LINE_VALUE_OUT_OF_RANGE";

    public const string TransactionFyPeriodRequired = "TRANSACTION_FY_PERIOD_REQUIRED";
    public const string TransactionFyPeriodInvalid = "TRANSACTION_FY_PERIOD_INVALID";
    public const string TransactionTaskNumberRequired = "TRANSACTION_TASK_REQUIRED";
    public const string TransactionDuplicateIdentity = "TRANSACTION_DUPLICATE_IDENTITY";
    public const string TransactionUnitsNegative = "TRANSACTION_UNITS_NEGATIVE";
    public const string TransactionUnitRateNegative = "TRANSACTION_UNIT_RATE_NEGATIVE";
    public const string TransactionValueOutOfRange = "TRANSACTION_VALUE_OUT_OF_RANGE";
    public const string TransactionUnitsNonPositiveWithAmount = "TRANSACTION_UNITS_NON_POSITIVE_WITH_AMOUNT";
    public const string TransactionNameMappingMissing = "TRANSACTION_NAME_MAPPING_MISSING";

    public const string FinancialValueOutOfRange = "FINANCIAL_VALUE_OUT_OF_RANGE";
    public const string FinancialBudgetNegative = "FINANCIAL_BUDGET_NEGATIVE";
    public const string ManagementAllocationOutOfRange = "MANAGEMENT_ALLOCATION_OUT_OF_RANGE";
    public const string ResourceMappingConflict = "RESOURCE_MAPPING_CONFLICT";
    public const string SnapshotPeriodRequired = "SNAPSHOT_PERIOD_REQUIRED";
    public const string SnapshotPeriodInvalid = "SNAPSHOT_PERIOD_INVALID";
    public const string SnapshotValueOutOfRange = "SNAPSHOT_VALUE_OUT_OF_RANGE";
    public const string PhaseDateOrderInvalid = "PHASE_DATE_ORDER_INVALID";
}

public sealed class ValidationReport
{
    public ValidationReport(IEnumerable<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        Issues = issues.ToList().AsReadOnly();
    }

    public IReadOnlyList<ValidationIssue> Issues { get; }

    public IReadOnlyList<ValidationIssue> Errors => Issues
        .Where(issue => string.Equals(issue.Severity, ValidationSeverities.Error, StringComparison.OrdinalIgnoreCase))
        .ToList();

    public IReadOnlyList<ValidationIssue> Warnings => Issues
        .Where(issue => string.Equals(issue.Severity, ValidationSeverities.Warning, StringComparison.OrdinalIgnoreCase))
        .ToList();

    public bool HasErrors => Issues.Any(issue =>
        string.Equals(issue.Severity, ValidationSeverities.Error, StringComparison.OrdinalIgnoreCase));

    public string BuildBlockingMessage(string operation)
    {
        var operationLabel = string.IsNullOrWhiteSpace(operation) ? "This operation" : operation.Trim();
        if (!HasErrors)
        {
            return $"{operationLabel} has no blocking validation errors.";
        }

        var details = Errors.Select(issue =>
            $"[{issue.Code}] {issue.Message} ({issue.EntityType} {issue.EntityId}, field {issue.FieldName})");
        return $"{operationLabel} cannot continue. Fix the listed validation error(s), then retry {operationLabel.ToLowerInvariant()}:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, details);
    }
}

public sealed class ProjectValidationException : Exception
{
    public ProjectValidationException(string message, ValidationReport report)
        : base(message)
    {
        Report = report ?? throw new ArgumentNullException(nameof(report));
    }

    public ValidationReport Report { get; }
}
