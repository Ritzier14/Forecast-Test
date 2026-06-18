namespace ProjectCostForecast.App.Models;

public sealed class ProjectDataset
{
    public ProjectHeader Header { get; set; } = new();
    public List<PhaseItem> Phases { get; set; } = [];
    public List<ForecastPeriod> ForecastPeriods { get; set; } = [];
    public List<FiscalYearBudget> FiscalYearBudgets { get; set; } = [];
    public List<ForecastLine> ForecastLines { get; set; } = [];
    public List<ManagementResource> ManagementResources { get; set; } = [];
    public List<CostTransaction> Transactions { get; set; } = [];
    public List<UnmatchedImportCombination> UnmatchedImportCombinations { get; set; } = [];
    public List<ContingencyEntry> ContingencyEntries { get; set; } = [];
    public List<CategorySummary> CategorySummaries { get; set; } = [];
    public List<CostCenterNameMapping> CostCenterNameMappings { get; set; } = [];
    public List<SavedMonthSnapshot> SavedMonthSnapshots { get; set; } = [];
    public List<AuditEvent> AuditEvents { get; set; } = [];
    public List<WorkspaceViewLayout> WorkspaceViews { get; set; } = [];
    public List<string> WorkspaceTabOrder { get; set; } = [];
    public List<string> DetailWorkspaceTabOrder { get; set; } = [];
    public List<int> SelectedCtcMonthForecastYears { get; set; } = [];
    public bool ShowCtcMonthForecastYearTotals { get; set; }
    public ScheduleData Schedule { get; set; } = new();
}

public sealed class ProjectHeader
{
    public string ProjectTitle { get; set; } = string.Empty;
    public string ReportTitle { get; set; } = string.Empty;
    public string CurrentPeriod { get; set; } = string.Empty;
    public string SourceWorkbook { get; set; } = string.Empty;
    public string ImportNotes { get; set; } = string.Empty;
}

public sealed class PhaseItem
{
    public string Name { get; set; } = string.Empty;
    public DateOnly? Start { get; set; }
    public DateOnly? End { get; set; }
}

public sealed class ForecastPeriod
{
    public string Column { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public DateOnly? StartDate { get; set; }
}

public sealed class FiscalYearBudget
{
    public string FiscalYear { get; set; } = string.Empty;
    public decimal Budget { get; set; }
}

public sealed class CostCenterNameMapping
{
    public string Key { get; set; } = string.Empty;
    public string ResourceCode { get; set; } = string.Empty;
    public string ResourceDescription { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string Narrative1 { get; set; } = string.Empty;
    public string Narrative2 { get; set; } = string.Empty;
    public string Narrative3 { get; set; } = string.Empty;
    public string Who { get; set; } = string.Empty;
    public string ManualName { get; set; } = string.Empty;
    public int UseCount { get; set; }
    public DateTime LastUsedAt { get; set; } = DateTime.Now;
}

public sealed class CostCenterNameOption
{
    public string RawName { get; set; } = string.Empty;
    public string SourceLabel { get; set; } = string.Empty;
    public bool IsExistingName { get; set; }
}

public sealed class ImportAutoCreatePreviewItem : ObservableModel
{
    private string _manualName = string.Empty;
    private bool _isExpanded;

    public string OriginalTaskNumber { get; set; } = string.Empty;
    public string OriginalManualName { get; set; } = string.Empty;
    public string OriginalProjectCode { get; set; } = string.Empty;
    public string TaskNumber { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int TransactionCount { get; set; }
    public List<ImportAutoCreatePreviewTransactionDetail> Transactions { get; set; } = [];

    public string ManualName
    {
        get => _manualName;
        set => SetProperty(ref _manualName, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }
}

public sealed class ImportAutoCreatePreviewTransactionDetail
{
    public string FyPeriod { get; set; } = string.Empty;
    public string TaskNumber { get; set; } = string.Empty;
    public string ResourceDescription { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string Narrative1 { get; set; } = string.Empty;
    public string Narrative2 { get; set; } = string.Empty;
    public string Narrative3 { get; set; } = string.Empty;
    public string Who { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public sealed class UnmatchedImportCombination
{
    public DateTime RecordedAt { get; set; } = DateTime.Now;
    public string TaskNumber { get; set; } = string.Empty;
    public string ManualName { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int TransactionCount { get; set; }
}

public sealed class SavedMonthSnapshot
{
    public string Period { get; set; } = string.Empty;
    public DateTime SavedAt { get; set; } = DateTime.Now;
    public decimal CostToDate { get; set; }
    public decimal CostToComplete { get; set; }
    public decimal FinalForecast { get; set; }
    public decimal TotalBudgetVariance { get; set; }
    public List<SavedMonthForecastLine> ForecastLines { get; set; } = [];
}

public sealed class SavedMonthForecastLine
{
    public int RowNumber { get; set; }
    public string TaskNumber { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;
    public decimal CostToDate { get; set; }
    public decimal CurrentPeriodForecast { get; set; }
    public decimal CostToComplete { get; set; }
    public decimal FinalForecast { get; set; }
    public decimal Budget { get; set; }
    public decimal TotalBudgetVariance { get; set; }
    public decimal VarianceFromPreviousMonth { get; set; }
    public List<SavedMonthPeriodAmount> MonthlyForecasts { get; set; } = [];
}

public sealed class SavedMonthPeriodAmount
{
    public string PeriodLabel { get; set; } = string.Empty;
    public DateOnly? PeriodStartDate { get; set; }
    public decimal Amount { get; set; }
}

public sealed class WorkspaceViewLayout
{
    public string WorkspaceKey { get; set; } = string.Empty;
    public string ContentKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> HiddenColumnKeys { get; set; } = [];
    public List<WorkspaceColumnLayout> ColumnLayouts { get; set; } = [];
    public bool ShowZeroAsBlank { get; set; } = true;
    public bool GroupForecastLinesByTask { get; set; }
    public string ForecastGroupByKey { get; set; } = string.Empty;
}

public sealed class WorkspaceColumnLayout
{
    public string Key { get; set; } = string.Empty;
    public double Width { get; set; }
    public int DisplayIndex { get; set; }
}
