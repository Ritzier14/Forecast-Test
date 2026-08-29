namespace ProjectCostForecast.App.Models;

public sealed class ProjectDataset
{
    // This is the persisted project-file format, independent of the app
    // assembly version. Unversioned files are treated as legacy format 0 by
    // the migration pipeline.
    public int FormatVersion { get; set; } = 1;
    public ProjectHeader Header { get; set; } = new();
    public List<PhaseItem> Phases { get; set; } = [];
    public List<ForecastPeriod> ForecastPeriods { get; set; } = [];
    public List<FiscalYearBudget> FiscalYearBudgets { get; set; } = [];
    public List<FiscalYearBudgetLine> BudgetLines { get; set; } = [];
    public string ActiveBudgetLineKey { get; set; } = "LTP_AP";
    // These collections are observable because they are the canonical live
    // financial state exposed by the view model while a project is open. The
    // JSON shape remains an array; the collection type is an in-process
    // ownership/detail and is not a second projection.
    public BatchObservableCollection<ForecastLine> ForecastLines { get; set; } = [];
    public List<ProjectTaskCode> ProjectTaskCodes { get; set; } = [];
    public List<ProjectCategory> ProjectCategories { get; set; } = [];
    public List<ManagementResource> ManagementResources { get; set; } = [];
    public BatchObservableCollection<CostTransaction> Transactions { get; set; } = [];
    public List<UnmatchedImportCombination> UnmatchedImportCombinations { get; set; } = [];
    public List<ContingencyEntry> ContingencyEntries { get; set; } = [];
    public List<CategorySummary> CategorySummaries { get; set; } = [];
    public List<CostCenterNameMapping> CostCenterNameMappings { get; set; } = [];
    // Saved-month history is a canonical append/freeze collection while a
    // project is open; the view model exposes this same collection instance.
    public BatchObservableCollection<SavedMonthSnapshot> SavedMonthSnapshots { get; set; } = [];
    public List<AuditEvent> AuditEvents { get; set; } = [];
    // Workspace layouts are project-local presentation state. They remain
    // separate from financial inputs even though their persisted collection
    // is owned by the dataset.
    public BatchObservableCollection<WorkspaceViewLayout> WorkspaceViews { get; set; } = [];
    public List<string> WorkspaceTabOrder { get; set; } = [];
    public List<string> DetailWorkspaceTabOrder { get; set; } = [];
    public Dictionary<string, string> ForecastGroupHeaderIconKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> ForecastGroupHeaderIconColorHexes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> ForecastCalendarYearHeaderColorHexes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> ForecastFiscalYearHeaderColorHexes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> ForecastGroupHeaderColorHexes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<int> SelectedCtcMonthForecastYears { get; set; } = [];
    public bool ShowCtcMonthForecastYearTotals { get; set; }
    public ScheduleData Schedule { get; set; } = new();
}

public sealed class ProjectTaskCode : ObservableModel
{
    private string _systemCode = string.Empty;
    private string _taskName = string.Empty;
    private string _iconKey = string.Empty;
    private string _iconColorHex = string.Empty;
    private string _headerColorHex = string.Empty;
    private string _defaultHeaderColorHex = string.Empty;

    public string SystemCode { get => _systemCode; set => SetProperty(ref _systemCode, value?.Trim() ?? string.Empty); }
    public string TaskName { get => _taskName; set => SetProperty(ref _taskName, value?.Trim() ?? string.Empty); }
    public bool IsRawDataCode { get; set; }
    public bool IsManualCode { get; set; }
    public int DisplayOrder { get; set; }
    public string IconKey
    {
        get => _iconKey;
        set => SetProperty(ref _iconKey, value?.Trim() ?? string.Empty);
    }

    public string IconColorHex
    {
        get => _iconColorHex;
        set => SetProperty(ref _iconColorHex, value?.Trim() ?? string.Empty);
    }

    public string HeaderColorHex
    {
        get => _headerColorHex;
        set => SetProperty(ref _headerColorHex, value?.Trim() ?? string.Empty);
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public string DefaultHeaderColorHex
    {
        get => _defaultHeaderColorHex;
        set => SetProperty(ref _defaultHeaderColorHex, value?.Trim() ?? string.Empty);
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool CanEditSystemCode => !IsRawDataCode;

    [System.Text.Json.Serialization.JsonIgnore]
    public bool CanDelete => !IsRawDataCode;
}

public sealed class ProjectCategory : ObservableModel
{
    private string _name = string.Empty;
    private string _colorHex = string.Empty;
    private string _iconKey = string.Empty;
    private string _defaultColorHex = string.Empty;

    public string Name { get => _name; set => SetProperty(ref _name, value?.Trim() ?? string.Empty); }

    public string ColorHex
    {
        get => _colorHex;
        set => SetProperty(ref _colorHex, value?.Trim() ?? string.Empty);
    }

    public string IconKey
    {
        get => _iconKey;
        set => SetProperty(ref _iconKey, value?.Trim() ?? string.Empty);
    }
    public int DisplayOrder { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string DefaultColorHex
    {
        get => _defaultColorHex;
        set => SetProperty(ref _defaultColorHex, value?.Trim() ?? string.Empty);
    }
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

public sealed class FiscalYearBudgetLine : ObservableModel
{
    private bool _isActive;

    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<FiscalYearBudgetAmount> Amounts { get; set; } = [];

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public decimal Total => Amounts.Sum(amount => amount.Amount);

    public void NotifyTotalChanged() => OnPropertyChanged(nameof(Total));
}

public sealed class FiscalYearBudgetAmount : ObservableModel
{
    private decimal _amount;

    public string FiscalYear { get; set; } = string.Empty;
    public decimal Amount { get => _amount; set => SetProperty(ref _amount, value); }
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
    public DateTimeOffset LastUsedAt { get; set; } = DateTimeOffset.UnixEpoch;
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
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UnixEpoch;
    public string TaskNumber { get; set; } = string.Empty;
    public string ManualName { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int TransactionCount { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string RecordedAtDisplay => Services.DateTimeContract.FormatNewZealand(RecordedAt);
}

public sealed class SavedMonthSnapshot
{
    public string Period { get; set; } = string.Empty;
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UnixEpoch;
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
    public string IconKey { get; set; } = string.Empty;
    public string IconColorHex { get; set; } = string.Empty;
    public List<string> HiddenColumnKeys { get; set; } = [];
    public List<WorkspaceColumnLayout> ColumnLayouts { get; set; } = [];
    public bool ShowZeroAsBlank { get; set; } = true;
    public bool GroupForecastLinesByTask { get; set; }
    public string ForecastGroupByKey { get; set; } = string.Empty;
    public bool ReportCanvasInitialized { get; set; }
    public string ReportCanvasPageSize { get; set; } = "A4";
    public string ReportCanvasOrientation { get; set; } = "Portrait";
    public List<ReportCanvasObjectLayout> ReportCanvasObjects { get; set; } = [];
}

public sealed class ReportCanvasObjectLayout
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ObjectType { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string Text { get; set; } = string.Empty;
    public string StyleKey { get; set; } = "Default";
    public string ChartKind { get; set; } = string.Empty;
    public string Grouping { get; set; } = string.Empty;
    public string DataSetKey { get; set; } = string.Empty;
    public bool ReportChartCostCodeFilterEnabled { get; set; }
    public string ReportChartCostCodeFilter { get; set; } = string.Empty;
    public List<string> ReportChartCostCodeFilters { get; set; } = [];
    public bool ReportChartHeadingFilterEnabled { get; set; }
    public bool ReportChartSubHeadingFilterEnabled { get; set; }
    public List<string> ReportChartValueKeys { get; set; } = [];
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public int XAxisTickFrequency { get; set; } = 8;
}

public sealed class WorkspaceColumnLayout
{
    public string Key { get; set; } = string.Empty;
    public double Width { get; set; }
    public int DisplayIndex { get; set; }
}
