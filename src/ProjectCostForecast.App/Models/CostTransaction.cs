namespace ProjectCostForecast.App.Models;

public sealed class CostTransaction
{
    public int RowNumber { get; set; }
    public string FyPeriod { get; set; } = string.Empty;
    public string TaskNumber { get; set; } = string.Empty;
    public int Period { get; set; }
    public DateOnly? DocDate { get; set; }
    public decimal Units { get; set; }
    public decimal UnitRate { get; set; }
    public decimal Amount { get; set; }
    public string CostLedger { get; set; } = string.Empty;
    public string CostAccount { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;
    public string ParentProjectCode { get; set; } = string.Empty;
    public string ResourceCode { get; set; } = string.Empty;
    public string ResourceDescription { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string PoNumber { get; set; } = string.Empty;
    public string PoComments { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string Narrative1 { get; set; } = string.Empty;
    public string Narrative2 { get; set; } = string.Empty;
    public string Narrative3 { get; set; } = string.Empty;
    public string Who { get; set; } = string.Empty;
    public string EcmNumber { get; set; } = string.Empty;
    public string ManualName { get; set; } = string.Empty;

    public string LedgerResourceName => string.IsNullOrWhiteSpace(ManualName) ? ResourceDescription : ManualName;
}
