namespace ProjectCostForecast.App.Models;

public sealed class AuditEvent
{
    public string AuditId { get; set; } = Guid.NewGuid().ToString("N");
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string OldValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = Environment.UserName;
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.Now;
    public string Reason { get; set; } = string.Empty;
    public string Source { get; set; } = "Desktop";
}

public sealed class ValidationIssue
{
    public string Severity { get; set; } = "Warning";
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
