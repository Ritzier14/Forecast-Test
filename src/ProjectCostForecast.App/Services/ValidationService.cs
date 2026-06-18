using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App.Services;

public sealed class ValidationService
{
    public List<ValidationIssue> Validate(ProjectDataset dataset)
    {
        var issues = new List<ValidationIssue>();

        foreach (var line in dataset.ForecastLines)
        {
            AddRequired(issues, "ForecastLine", line.RowNumber.ToString(), nameof(line.TaskNumber), line.TaskNumber, "Task number is required.");
            AddRequired(issues, "ForecastLine", line.RowNumber.ToString(), nameof(line.ResourceName), line.ResourceName, "Resource name is required.");
            AddRequired(issues, "ForecastLine", line.RowNumber.ToString(), nameof(line.ProjectCode), line.ProjectCode, "Project category is required.");

            if (line.Budget == 0 && line.PlannedCostFcc != 0)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "Warning",
                    EntityType = "ForecastLine",
                    EntityId = line.RowNumber.ToString(),
                    FieldName = nameof(line.Budget),
                    Message = $"{line.ResourceName} has planned cost but no budget."
                });
            }
        }

        foreach (var transaction in dataset.Transactions)
        {
            AddRequired(issues, "Transaction", transaction.RowNumber.ToString(), nameof(transaction.FyPeriod), transaction.FyPeriod, "FY period is required.");
            AddRequired(issues, "Transaction", transaction.RowNumber.ToString(), nameof(transaction.TaskNumber), transaction.TaskNumber, "Task number is required.");

            if (string.IsNullOrWhiteSpace(transaction.ManualName) && string.IsNullOrWhiteSpace(transaction.ResourceDescription))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "Warning",
                    EntityType = "Transaction",
                    EntityId = transaction.RowNumber.ToString(),
                    FieldName = nameof(transaction.ManualName),
                    Message = "Manual name and resource description are both blank."
                });
            }

            if (transaction.Units <= 0 && transaction.Amount != 0)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "Warning",
                    EntityType = "Transaction",
                    EntityId = transaction.RowNumber.ToString(),
                    FieldName = nameof(transaction.Units),
                    Message = "Units are zero or negative while amount is non-zero."
                });
            }
        }

        var duplicateResourceCodes = dataset.Transactions
            .Where(t => !string.IsNullOrWhiteSpace(t.ResourceCode) && !string.IsNullOrWhiteSpace(t.LedgerResourceName))
            .GroupBy(t => t.ResourceCode)
            .Where(group => group.Select(t => CalculationService.Normalise(t.LedgerResourceName)).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1);

        foreach (var duplicate in duplicateResourceCodes)
        {
            issues.Add(new ValidationIssue
            {
                Severity = "Warning",
                EntityType = "Resource",
                EntityId = duplicate.Key,
                FieldName = nameof(CostTransaction.ResourceCode),
                Message = $"Resource code {duplicate.Key} maps to multiple resource names."
            });
        }

        return issues;
    }

    private static void AddRequired(
        ICollection<ValidationIssue> issues,
        string entityType,
        string entityId,
        string fieldName,
        string value,
        string message)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        issues.Add(new ValidationIssue
        {
            Severity = "Error",
            EntityType = entityType,
            EntityId = entityId,
            FieldName = fieldName,
            Message = message
        });
    }
}
