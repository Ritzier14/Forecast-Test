using System.Collections.ObjectModel;
using System.ComponentModel;
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

public sealed partial class MainWindowViewModel : NotifyObject
{
    private static readonly string AppVersionValue =
        typeof(MainWindowViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(MainWindowViewModel).Assembly.GetName().Version?.ToString(3)
        ?? "0.0.0";

    public const string ForecastFreezeTaskKey = "Task";
    public const string ForecastFreezeResourceKey = "Resource";
    public const string ForecastFreezeCategoryKey = "Category";
    public const string ForecastFreezeCostToDateKey = "CTD";
    public const string ForecastFreezeMonthCostKey = "MonthCost";
    public const string ForecastFreezeLastForecastKey = "LastForecast";
    public const string ForecastFreezeMonthVarianceKey = "MonthVariance";
    public const string ForecastFreezeCtcKey = "CTC";
    public const string ForecastFreezeFccKey = "FCC";
    public const string ForecastFreezeBudgetKey = "Budget";
    public const string ForecastFreezeBudgetVarianceKey = "BudgetVariance";
    public const string DefaultForecastFreezeColumnKey = ForecastFreezeCategoryKey;
    public const string ForecastGroupByNoneKey = "None";
    public const string ForecastGroupByTaskKey = "Task";
    public const string ForecastGroupByResourceKey = "Resource";
    public const string ForecastGroupByCategoryKey = "Category";

    private const double LedgerChartMinWidth = 920;
    private const double LedgerChartHeight = 300;
    private const double LedgerChartLeftPadding = 58;
    private const double LedgerChartTopPadding = 18;
    private const double LedgerChartRightPadding = 20;
    private const double LedgerChartBottomPadding = 34;
    private const double DefaultLedgerChartMonthSpacing = 36;

    private readonly CalculationService _calculationService = new();
    private readonly ProjectFileService _projectFileService = new();
    private readonly CsvTransactionService _csvTransactionService = new();
    private readonly ValidationService _validationService = new();
    private readonly UserPreferencesService _userPreferencesService = new();
    private ProjectDataset _dataset;
    private ForecastLine? _selectedForecastLine;
    private ForecastLine? _hoveredForecastLine;
    private ResourceSummary? _selectedResourceSummary;
    private string _searchText = string.Empty;
    private string _selectedProjectCode = "All";
    private string _selectedPeriod = "All";
    private string _projectFilePath = string.Empty;
    private string _statusText = string.Empty;
    private string _ledgerChartStatusText = "Select a resource to see the spend curve.";
    private string _monthlyReportFiscalYear1Label = "FY 1";
    private string _monthlyReportFiscalYear2Label = "FY 2";
    private string _monthlyReportFiscalYear3Label = "FY 3";
    private string _activeRiskStatusText = "No active risks / financial events captured in this project file yet.";
    private string _pluggedRatesStatusText = "No plugged rates captured in this project file yet.";
    private bool _showOnlyLinesWithActualCost;
    private bool _showCostThisMonthOnly;
    private bool _showOnlyLinesWithRemainingForecast;
    private string _forecastGroupByKey = ForecastGroupByTaskKey;
    private string _selectedMonthlyVarianceFilter = "All";
    private string _selectedBudgetVarianceFilter = "All";
    private string _activeWorkspaceKey = "CTC Forecast";
    private WorkspaceViewTab? _selectedWorkspaceView;
    private string _activeDetailWorkspaceKey = "Ledger Costs";
    private WorkspaceViewTab? _selectedDetailWorkspaceView;
    private bool _isDirty;
    private PointCollection _ledgerActualChartPoints = [];
    private PointCollection _ledgerForecastChartPoints = [];
    private Geometry _ledgerActualChartGeometry = Geometry.Empty;
    private Geometry _ledgerForecastChartGeometry = Geometry.Empty;
    private double _ledgerChartCanvasWidth = LedgerChartMinWidth;
    private DateOnly? _forecastEditLockCutoffDate;
    private bool _viewRefreshQueued;
    private readonly DispatcherTimer _searchRefreshTimer;
    private bool _forecastGroupingQueued;
    private bool _ledgerRefreshQueued;
    private bool _suppressPivotRefresh;
    private bool _suppressFilterRefresh;
    private bool _pivotFilterValuesDirty = true;
    private readonly Dictionary<string, ObservableCollection<WorkspaceViewTab>> _workspaceViews = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _selectedWorkspaceViewNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ObservableCollection<WorkspaceViewTab>> _detailWorkspaceViews = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _selectedDetailWorkspaceViewNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _projectCodeByTaskNumber = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _taskNumbersByProjectCode = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<MonthlyForecast, ForecastLine> _forecastLineByMonthlyForecast = [];
    private IReadOnlyList<CostTransaction> _activeLedgerTransactions = [];
    private IReadOnlyList<MonthlyForecast> _activeLedgerForecastEntries = [];
    private AppTotals _totals = new();
    private LedgerTotals _ledgerTotals = new();
    private int _nextKpiPillId = 1;
    private int _spreadsheetEditBatchDepth;
    private bool _spreadsheetEditBatchChanged;
    private bool _showCtcMonthForecastColumns = true;
    private bool _showMonthNameAboveFiscalPeriod;
    private bool _showCtcMonthForecastYearTotals;
    private bool _showCurrencySymbols;
    private int _forecastMonthMillionDecimals = 2;
    private bool _keepColumnHighlightsAcrossTabs;
    private int _selectedCtcMonthForecastYear;
    private readonly HashSet<int> _selectedCtcMonthForecastYears = [];
    private string _forecastFreezeColumnKey = DefaultForecastFreezeColumnKey;
    private CategorySortOption? _selectedCategorySortOption;
    private LedgerChartRangeOption? _selectedLedgerChartRangeOption;
    private PivotFieldDefinition? _selectedPivotField;
    private PivotAreaField? _selectedPivotRowField;
    private PivotAreaField? _selectedPivotColumnField;
    private PivotAreaField? _selectedPivotValueField;
    private PivotAreaField? _selectedPivotFilterField;
    private AppUserPreferences _userPreferences;
    private bool _suppressPreferenceSave;

    public MainWindowViewModel()
    {
        _searchRefreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(180)
        };
        _searchRefreshTimer.Tick += (_, _) =>
        {
            _searchRefreshTimer.Stop();
            RefreshSearchViews();
        };
        _dataset = new SampleDataService().Load();
        _userPreferences = _userPreferencesService.Load();
        _userPreferences.KpiIconKeys ??= new(StringComparer.OrdinalIgnoreCase);
        _userPreferences.KpiIconColorHexes ??= new(StringComparer.OrdinalIgnoreCase);
        _userPreferences.WorkspaceTabIconKeys ??= new(StringComparer.OrdinalIgnoreCase);
        _userPreferences.WorkspaceTabIconColorHexes ??= new(StringComparer.OrdinalIgnoreCase);
        _suppressPreferenceSave = true;

        ForecastLines = CreateCollection<ForecastLine>();
        ManagementResources = CreateCollection<ManagementResource>();
        ManagementResourceAllocationRows = CreateCollection<ManagementResourceTableRow>();
        ManagementResourceHoursRows = CreateCollection<ManagementResourceTableRow>();
        ManagementResourceCostRows = CreateCollection<ManagementResourceTableRow>();
        Transactions = CreateCollection<CostTransaction>();
        CategorySummaries = CreateCollection<CategorySummary>();
        ContingencyEntries = CreateCollection<ContingencyEntry>();
        Phases = CreateCollection<PhaseItem>();
        SavedMonthSnapshots = CreateCollection<SavedMonthSnapshot>();
        UnmatchedImportCombinations = CreateCollection<UnmatchedImportCombination>();
        ActivePeriodWarnings = CreateCollection<string>();
        ResourceSummaries = CreateCollection<ResourceSummary>();
        FiscalYearReportLines = CreateCollection<FiscalYearReportLine>();
        ActualsPeriodSummaries = CreateCollection<ActualsPeriodSummary>();
        AuditEvents = CreateCollection<AuditEvent>();
        ValidationIssues = CreateCollection<ValidationIssue>();
        MonthlyReportFiscalSummaryRows = CreateCollection<MonthlyReportFiscalSummaryRow>();
        MonthlyReportCategoryRows = CreateCollection<MonthlyReportCategoryRow>();
        MonthlyReportVarianceCommentRows = CreateCollection<MonthlyReportVarianceCommentRow>();
        MonthlyReportRiskItems = CreateCollection<MonthlyReportRiskItem>();
        MonthlyReportPluggedRateItems = CreateCollection<MonthlyReportPluggedRateItem>();
        LedgerChartGridLines = CreateCollection<ChartLineSegment>();
        LedgerChartXAxisLabels = CreateCollection<ChartLabel>();
        LedgerChartYAxisLabels = CreateCollection<ChartLabel>();
        AvailableProjectCodes = CreateCollection<string>();
        AvailablePeriods = CreateCollection<string>();
        MonthlyVarianceFilters = new ObservableCollection<string> { "All", "Negative only", "Positive only", "Any variance" };
        BudgetVarianceFilters = new ObservableCollection<string> { "All", "Over budget", "Under budget", "Any variance" };
        CategorySortOptions = new ObservableCollection<CategorySortOption>
        {
            new() { Key = "Alphabetical", Name = "A-Z" },
            new() { Key = "TotalCost", Name = "Total cost" },
            new() { Key = "MonthCost", Name = "Cost for month" }
        };
        LedgerChartRangeOptions = new ObservableCollection<LedgerChartRangeOption>
        {
            new() { Key = "Last24", Name = "Last 24 months", VisibleMonths = 24, MonthSpacing = 36 },
            new() { Key = "Last12", Name = "Last 12 months", VisibleMonths = 12, MonthSpacing = 54 },
            new() { Key = "Last36", Name = "Last 36 months", VisibleMonths = 36, MonthSpacing = 28 },
            new() { Key = "All", Name = "All months", VisibleMonths = null, MonthSpacing = 32 }
        };
        KpiOptions = new ObservableCollection<KpiOption>
        {
            new() { Key = "TotalForecastCtc", Name = "Total Forecast CTC" },
            new() { Key = "TotalCostToDate", Name = "Cost to Date" },
            new() { Key = "PlannedCostFcc", Name = "Planned Cost FCC" },
            new() { Key = "TotalBudget", Name = "Budget" },
            new() { Key = "TotalBudgetVariance", Name = "Budget Variance" },
            new() { Key = "CurrentMonthCost", Name = "This Month Cost" },
            new() { Key = "RemainingForecast", Name = "Remaining Forecast" },
            new() { Key = "MonthlyVariance", Name = "Monthly Variance" }
        };
        ActualsMonthlyPivotRows = CreateCollection<MonthlyPivotRow>();
        ForecastMonthlyPivotRows = CreateCollection<MonthlyPivotRow>();
        CategoryMonthlyPivotRows = CreateCollection<MonthlyPivotRow>();
        RawTransactionsMonthlyPivotRows = CreateCollection<MonthlyPivotRow>();
        LedgerMonthlyPivotRows = CreateCollection<MonthlyPivotRow>();
        MonthlyPivotPeriods = CreateCollection<string>();
        RawTransactionsMonthlyPivotPeriods = CreateCollection<string>();
        LedgerMonthlyPivotPeriods = CreateCollection<string>();
        KpiPills = CreateCollection<KpiPill>();
        CurrentWorkspaceViews = CreateCollection<WorkspaceViewTab>();
        CurrentDetailWorkspaceViews = CreateCollection<WorkspaceViewTab>();
        CtcMonthForecastColumns = CreateCollection<ForecastMonthColumnDefinition>();
        AvailableCtcMonthForecastYears = CreateCollection<int>();
        PivotFields = CreateCollection<PivotFieldDefinition>(CreatePivotFieldDefinitions());
        PivotRowFields = CreateCollection<PivotAreaField>();
        PivotColumnFields = CreateCollection<PivotAreaField>();
        PivotValueFields = CreateCollection<PivotAreaField>();
        PivotFilterFields = CreateCollection<PivotAreaField>();
        PivotResultColumns = CreateCollection<PivotResultColumn>();
        PivotResultRows = CreateCollection<PivotResultRow>();
        SelectedLedgerChartRangeOption = LedgerChartRangeOptions.FirstOrDefault();

        ForecastLinesView = CollectionViewSource.GetDefaultView(ForecastLines);
        ForecastLinesView.Filter = FilterForecastLine;

        RawTransactionsView = CollectionViewSource.GetDefaultView(Transactions);
        RawTransactionsView.Filter = FilterTransaction;

        LedgerTransactionRows = CreateCollection<CostTransaction>();
        LedgerTransactionsView = CollectionViewSource.GetDefaultView(LedgerTransactionRows);

        ResourceSummariesView = CollectionViewSource.GetDefaultView(ResourceSummaries);
        CategorySummariesView = CollectionViewSource.GetDefaultView(CategorySummaries);

        ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
        ClearAllCommand = new RelayCommand(_ => ClearAllRecords());
        OpenProjectCommand = new RelayCommand(_ => OpenProject());
        SaveProjectCommand = new RelayCommand(_ => SaveProject(), _ => true);
        SaveProjectAsCommand = new RelayCommand(_ => SaveProjectAs());
        ImportCsvCommand = new RelayCommand(_ => ImportCsv());
        ExportTransactionsCommand = new RelayCommand(_ => ExportTransactions());
        RecalculateCommand = new RelayCommand(_ => RecalculateAndRefresh(markDirty: true, reason: "Manual recalculation"));
        AddWorkspaceViewCommand = new RelayCommand(_ => AddWorkspaceView());
        AddDetailWorkspaceViewCommand = new RelayCommand(_ => AddDetailWorkspaceView());
        NewMonthCommand = new RelayCommand(_ => SetupNewMonth());
        ViewSavedMonthCommand = new RelayCommand(_ => OpenSavedMonthViewer());
        ViewUnmatchedImportsCommand = new RelayCommand(_ => OpenUnmatchedImportViewer(), _ => UnmatchedImportCombinations.Count > 0);
        AddPivotRowFieldCommand = new RelayCommand(_ => AddSelectedPivotField(PivotRowFields, requireNumeric: false));
        AddPivotColumnFieldCommand = new RelayCommand(_ => AddSelectedPivotField(PivotColumnFields, requireNumeric: false));
        AddPivotValueFieldCommand = new RelayCommand(_ => AddSelectedPivotField(PivotValueFields, requireNumeric: true));
        AddPivotFilterFieldCommand = new RelayCommand(_ => AddSelectedPivotField(PivotFilterFields, requireNumeric: false));
        RemovePivotRowFieldCommand = new RelayCommand(_ => RemovePivotField(PivotRowFields, SelectedPivotRowField));
        RemovePivotColumnFieldCommand = new RelayCommand(_ => RemovePivotField(PivotColumnFields, SelectedPivotColumnField));
        RemovePivotValueFieldCommand = new RelayCommand(_ => RemovePivotField(PivotValueFields, SelectedPivotValueField));
        RemovePivotFilterFieldCommand = new RelayCommand(_ => RemovePivotField(PivotFilterFields, SelectedPivotFilterField));
        ClearPivotLayoutCommand = new RelayCommand(_ => ClearPivotLayout());

        SeedDefaultKpiPills();
        SeedDefaultPivotLayout();
        InitializeWorkspaceViews(_dataset.WorkspaceViews);
        RefreshCurrentWorkspaceViews();
        RefreshCurrentDetailWorkspaceViews();
        SelectedCategorySortOption = CategorySortOptions.FirstOrDefault();

        LoadDataset(_dataset, markDirty: false);
        StatusText = "Loaded workbook seed data. Totals recalculated from transactions.";
    }

    public ProjectHeader Header => _dataset.Header;
    public ObservableCollection<ForecastLine> ForecastLines { get; }
    public ObservableCollection<ManagementResource> ManagementResources { get; }
    public ObservableCollection<ManagementResourceTableRow> ManagementResourceAllocationRows { get; }
    public ObservableCollection<ManagementResourceTableRow> ManagementResourceHoursRows { get; }
    public ObservableCollection<ManagementResourceTableRow> ManagementResourceCostRows { get; }
    public ObservableCollection<CostTransaction> Transactions { get; }
    public ObservableCollection<CategorySummary> CategorySummaries { get; }
    public ObservableCollection<ContingencyEntry> ContingencyEntries { get; }
    public ObservableCollection<PhaseItem> Phases { get; }
    public ObservableCollection<SavedMonthSnapshot> SavedMonthSnapshots { get; }
    public ObservableCollection<UnmatchedImportCombination> UnmatchedImportCombinations { get; }
    public ObservableCollection<string> ActivePeriodWarnings { get; }
    public ObservableCollection<ResourceSummary> ResourceSummaries { get; }
    public ObservableCollection<FiscalYearReportLine> FiscalYearReportLines { get; }
    public ObservableCollection<ActualsPeriodSummary> ActualsPeriodSummaries { get; }
    public ObservableCollection<AuditEvent> AuditEvents { get; }
    public ObservableCollection<ValidationIssue> ValidationIssues { get; }
    public ObservableCollection<MonthlyReportFiscalSummaryRow> MonthlyReportFiscalSummaryRows { get; }
    public ObservableCollection<MonthlyReportCategoryRow> MonthlyReportCategoryRows { get; }
    public ObservableCollection<MonthlyReportVarianceCommentRow> MonthlyReportVarianceCommentRows { get; }
    public ObservableCollection<MonthlyReportRiskItem> MonthlyReportRiskItems { get; }
    public ObservableCollection<MonthlyReportPluggedRateItem> MonthlyReportPluggedRateItems { get; }
    public ObservableCollection<ChartLineSegment> LedgerChartGridLines { get; }
    public ObservableCollection<ChartLabel> LedgerChartXAxisLabels { get; }
    public ObservableCollection<ChartLabel> LedgerChartYAxisLabels { get; }
    public ObservableCollection<string> AvailableProjectCodes { get; }
    public ObservableCollection<string> AvailablePeriods { get; }
    public ObservableCollection<string> MonthlyVarianceFilters { get; }
    public ObservableCollection<string> BudgetVarianceFilters { get; }
    public ObservableCollection<CategorySortOption> CategorySortOptions { get; }
    public ObservableCollection<LedgerChartRangeOption> LedgerChartRangeOptions { get; }
    public ObservableCollection<KpiOption> KpiOptions { get; }
    public ObservableCollection<KpiPill> KpiPills { get; }
    public ObservableCollection<MonthlyPivotRow> ActualsMonthlyPivotRows { get; }
    public ObservableCollection<MonthlyPivotRow> ForecastMonthlyPivotRows { get; }
    public ObservableCollection<MonthlyPivotRow> CategoryMonthlyPivotRows { get; }
    public ObservableCollection<MonthlyPivotRow> RawTransactionsMonthlyPivotRows { get; }
    public ObservableCollection<MonthlyPivotRow> LedgerMonthlyPivotRows { get; }
    public ObservableCollection<string> MonthlyPivotPeriods { get; }
    public ObservableCollection<string> RawTransactionsMonthlyPivotPeriods { get; }
    public ObservableCollection<string> LedgerMonthlyPivotPeriods { get; }
    public ObservableCollection<WorkspaceViewTab> CurrentWorkspaceViews { get; }
    public ObservableCollection<WorkspaceViewTab> CurrentDetailWorkspaceViews { get; }
    public ObservableCollection<CostTransaction> LedgerTransactionRows { get; }
    public ObservableCollection<ForecastMonthColumnDefinition> CtcMonthForecastColumns { get; }
    public ObservableCollection<int> AvailableCtcMonthForecastYears { get; }
    public ObservableCollection<PivotFieldDefinition> PivotFields { get; }
    public ObservableCollection<PivotAreaField> PivotRowFields { get; }
    public ObservableCollection<PivotAreaField> PivotColumnFields { get; }
    public ObservableCollection<PivotAreaField> PivotValueFields { get; }
    public ObservableCollection<PivotAreaField> PivotFilterFields { get; }
    public ObservableCollection<PivotResultColumn> PivotResultColumns { get; }
    public ObservableCollection<PivotResultRow> PivotResultRows { get; }
    public ICollectionView ForecastLinesView { get; }
    public ICollectionView RawTransactionsView { get; }
    public ICollectionView LedgerTransactionsView { get; }
    public ICollectionView ResourceSummariesView { get; }
    public ICollectionView CategorySummariesView { get; }
    public ICommand ClearFiltersCommand { get; }
    public ICommand ClearAllCommand { get; }
    public ICommand OpenProjectCommand { get; }
    public ICommand SaveProjectCommand { get; }
    public ICommand SaveProjectAsCommand { get; }
    public ICommand ImportCsvCommand { get; }
    public ICommand ExportTransactionsCommand { get; }
    public ICommand RecalculateCommand { get; }
    public ICommand AddWorkspaceViewCommand { get; }
    public ICommand AddDetailWorkspaceViewCommand { get; }
    public ICommand AddPivotRowFieldCommand { get; }
    public ICommand AddPivotColumnFieldCommand { get; }
    public ICommand AddPivotValueFieldCommand { get; }
    public ICommand AddPivotFilterFieldCommand { get; }
    public ICommand RemovePivotRowFieldCommand { get; }
    public ICommand RemovePivotColumnFieldCommand { get; }
    public ICommand RemovePivotValueFieldCommand { get; }
    public ICommand RemovePivotFilterFieldCommand { get; }
    public ICommand ClearPivotLayoutCommand { get; }

    public IReadOnlyList<string> WorkspaceTabOrder => _dataset.WorkspaceTabOrder ?? [];

    public IReadOnlyList<string> DetailWorkspaceTabOrder => _dataset.DetailWorkspaceTabOrder ?? [];

    public bool StartInFullScreen => _userPreferences.StartMaximized;

    public bool IsDetailPanelCollapsed => _userPreferences.DetailPanelCollapsed;

    public PivotFieldDefinition? SelectedPivotField
    {
        get => _selectedPivotField;
        set => SetProperty(ref _selectedPivotField, value);
    }

    public PivotAreaField? SelectedPivotRowField
    {
        get => _selectedPivotRowField;
        set => SetProperty(ref _selectedPivotRowField, value);
    }

    public PivotAreaField? SelectedPivotColumnField
    {
        get => _selectedPivotColumnField;
        set => SetProperty(ref _selectedPivotColumnField, value);
    }

    public PivotAreaField? SelectedPivotValueField
    {
        get => _selectedPivotValueField;
        set => SetProperty(ref _selectedPivotValueField, value);
    }

    public PivotAreaField? SelectedPivotFilterField
    {
        get => _selectedPivotFilterField;
        set => SetProperty(ref _selectedPivotFilterField, value);
    }
    public ICommand NewMonthCommand { get; }
    public ICommand ViewSavedMonthCommand { get; }
    public ICommand ViewUnmatchedImportsCommand { get; }

    public ForecastLine? SelectedForecastLine
    {
        get => _selectedForecastLine;
        set
        {
            if (SetProperty(ref _selectedForecastLine, value))
            {
                if (value != null)
                {
                    _selectedResourceSummary = null;
                    OnPropertyChanged(nameof(SelectedResourceSummary));
                }
                QueueLedgerChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public ResourceSummary? SelectedResourceSummary
    {
        get => _selectedResourceSummary;
        set
        {
            if (SetProperty(ref _selectedResourceSummary, value))
            {
                if (value != null)
                {
                    _selectedForecastLine = null;
                    OnPropertyChanged(nameof(SelectedForecastLine));
                }
                QueueLedgerChanged();
            }
        }
    }

    public ForecastLine? HoveredForecastLine => _hoveredForecastLine;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _searchRefreshTimer.Stop();
                _searchRefreshTimer.Start();
            }
        }
    }

    public string SelectedProjectCode
    {
        get => _selectedProjectCode;
        set
        {
            if (SetProperty(ref _selectedProjectCode, value))
            {
                RefreshForecastAndTransactionViews();
                SaveUserPreferences();
            }
        }
    }

    public string SelectedPeriod
    {
        get => _selectedPeriod;
        set
        {
            if (SetProperty(ref _selectedPeriod, value))
            {
                RefreshRawTransactionsView();
                SaveUserPreferences();
            }
        }
    }

    public string ProjectFilePath
    {
        get => _projectFilePath;
        set
        {
            if (SetProperty(ref _projectFilePath, value))
            {
                OnPropertyChanged(nameof(DisplayProjectFilePath));
            }
        }
    }

    public string DisplayProjectFilePath => string.IsNullOrWhiteSpace(ProjectFilePath)
        ? "Workbook seed data from 1.Mar 26.xlsm"
        : ProjectFilePath;

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string LedgerChartStatusText
    {
        get => _ledgerChartStatusText;
        private set => SetProperty(ref _ledgerChartStatusText, value);
    }

    public string MonthlyReportFiscalYear1Label
    {
        get => _monthlyReportFiscalYear1Label;
        private set => SetProperty(ref _monthlyReportFiscalYear1Label, value);
    }

    public string MonthlyReportFiscalYear2Label
    {
        get => _monthlyReportFiscalYear2Label;
        private set => SetProperty(ref _monthlyReportFiscalYear2Label, value);
    }

    public string MonthlyReportFiscalYear3Label
    {
        get => _monthlyReportFiscalYear3Label;
        private set => SetProperty(ref _monthlyReportFiscalYear3Label, value);
    }

    public string ActiveRiskStatusText
    {
        get => _activeRiskStatusText;
        private set => SetProperty(ref _activeRiskStatusText, value);
    }

    public string PluggedRatesStatusText
    {
        get => _pluggedRatesStatusText;
        private set => SetProperty(ref _pluggedRatesStatusText, value);
    }

    public PointCollection LedgerActualChartPoints
    {
        get => _ledgerActualChartPoints;
        private set => SetProperty(ref _ledgerActualChartPoints, value);
    }

    public PointCollection LedgerForecastChartPoints
    {
        get => _ledgerForecastChartPoints;
        private set => SetProperty(ref _ledgerForecastChartPoints, value);
    }

    public Geometry LedgerActualChartGeometry
    {
        get => _ledgerActualChartGeometry;
        private set => SetProperty(ref _ledgerActualChartGeometry, value);
    }

    public Geometry LedgerForecastChartGeometry
    {
        get => _ledgerForecastChartGeometry;
        private set => SetProperty(ref _ledgerForecastChartGeometry, value);
    }

    public double LedgerChartCanvasWidth
    {
        get => _ledgerChartCanvasWidth;
        private set => SetProperty(ref _ledgerChartCanvasWidth, value);
    }

    public LedgerChartRangeOption? SelectedLedgerChartRangeOption
    {
        get => _selectedLedgerChartRangeOption;
        set
        {
            if (SetProperty(ref _selectedLedgerChartRangeOption, value))
            {
                RebuildLedgerChart();
                SaveUserPreferences();
            }
        }
    }

    public DateOnly? ForecastEditLockCutoffDate => _forecastEditLockCutoffDate;

    public bool ShowOnlyLinesWithActualCost
    {
        get => _showOnlyLinesWithActualCost;
        set
        {
            if (SetProperty(ref _showOnlyLinesWithActualCost, value))
            {
                RefreshForecastLinesView();
                SaveUserPreferences();
            }
        }
    }

    public bool ShowCostThisMonthOnly
    {
        get => _showCostThisMonthOnly;
        set
        {
            if (SetProperty(ref _showCostThisMonthOnly, value))
            {
                RefreshForecastAndTransactionViews();
                SaveUserPreferences();
            }
        }
    }

    public bool ShowOnlyLinesWithRemainingForecast
    {
        get => _showOnlyLinesWithRemainingForecast;
        set
        {
            if (SetProperty(ref _showOnlyLinesWithRemainingForecast, value))
            {
                RefreshForecastLinesView();
                SaveUserPreferences();
            }
        }
    }

    public bool GroupForecastLinesByTask
    {
        get => string.Equals(ForecastGroupByKey, ForecastGroupByTaskKey, StringComparison.OrdinalIgnoreCase);
        set => ForecastGroupByKey = value ? ForecastGroupByTaskKey : ForecastGroupByNoneKey;
    }

    public string ForecastGroupByKey
    {
        get => _forecastGroupByKey;
        set
        {
            var normalized = NormalizeForecastGroupByKey(value);
            if (SetProperty(ref _forecastGroupByKey, normalized))
            {
                if (SelectedWorkspaceView is not null)
                {
                    SelectedWorkspaceView.ForecastGroupByKey = normalized;
                    SelectedWorkspaceView.GroupForecastLinesByTask = string.Equals(normalized, ForecastGroupByTaskKey, StringComparison.OrdinalIgnoreCase);
                    IsDirty = true;
                }

                OnPropertyChanged(nameof(GroupForecastLinesByTask));
                ApplyForecastGrouping();
            }
        }
    }

    public bool ShowForecastZeroAsBlank => SelectedWorkspaceView?.ShowZeroAsBlank ?? true;

    public void SetSelectedForecastShowZeroAsBlank(bool showZeroAsBlank)
    {
        if (SelectedWorkspaceView is null || SelectedWorkspaceView.ShowZeroAsBlank == showZeroAsBlank)
        {
            return;
        }

        SelectedWorkspaceView.ShowZeroAsBlank = showZeroAsBlank;
        IsDirty = true;
        OnPropertyChanged(nameof(ShowForecastZeroAsBlank));
    }

    public string SelectedMonthlyVarianceFilter
    {
        get => _selectedMonthlyVarianceFilter;
        set
        {
            if (SetProperty(ref _selectedMonthlyVarianceFilter, value))
            {
                RefreshForecastLinesView();
                SaveUserPreferences();
            }
        }
    }

    public string SelectedBudgetVarianceFilter
    {
        get => _selectedBudgetVarianceFilter;
        set
        {
            if (SetProperty(ref _selectedBudgetVarianceFilter, value))
            {
                RefreshForecastLinesView();
                SaveUserPreferences();
            }
        }
    }

    public string ActiveWorkspaceKey
    {
        get => _activeWorkspaceKey;
        set
        {
            if (SetProperty(ref _activeWorkspaceKey, value))
            {
                RefreshCurrentWorkspaceViews();
                OnPropertyChanged(nameof(ShowActualsPivotByMonth));
                OnPropertyChanged(nameof(ShowActualsPivotByPeriodRows));
                OnPropertyChanged(nameof(ShowForecastPivotByMonth));
                OnPropertyChanged(nameof(ShowRawTransactionsGroupedByMonth));
                OnPropertyChanged(nameof(ShowRawTransactionsPivotByMonth));
                OnPropertyChanged(nameof(ShowSummaryViewByMonth));
                ApplyRawTransactionGrouping();
            }
        }
    }

    public string ActiveDetailWorkspaceKey
    {
        get => _activeDetailWorkspaceKey;
        set
        {
            if (SetProperty(ref _activeDetailWorkspaceKey, value))
            {
                RefreshCurrentDetailWorkspaceViews();
            }
        }
    }

    public WorkspaceViewTab? SelectedWorkspaceView
    {
        get => _selectedWorkspaceView;
        set
        {
            if (SetProperty(ref _selectedWorkspaceView, value))
            {
                if (value is not null)
                {
                    _selectedWorkspaceViewNames[ActiveWorkspaceKey] = value.Name;
                }

                var groupByKey = NormalizeForecastGroupByKey(value?.ForecastGroupByKey);
                if (string.Equals(groupByKey, ForecastGroupByNoneKey, StringComparison.OrdinalIgnoreCase) && value?.GroupForecastLinesByTask == true)
                {
                    groupByKey = ForecastGroupByTaskKey;
                }

                var forecastGroupingChanged = !string.Equals(_forecastGroupByKey, groupByKey, StringComparison.OrdinalIgnoreCase);
                if (forecastGroupingChanged)
                {
                    _forecastGroupByKey = groupByKey;
                    OnPropertyChanged(nameof(ForecastGroupByKey));
                    OnPropertyChanged(nameof(GroupForecastLinesByTask));
                }

                OnPropertyChanged(nameof(ShowActualsPivotByMonth));
                OnPropertyChanged(nameof(ShowActualsPivotByPeriodRows));
                OnPropertyChanged(nameof(ShowForecastPivotByMonth));
                OnPropertyChanged(nameof(ShowRawTransactionsGroupedByMonth));
                OnPropertyChanged(nameof(ShowRawTransactionsPivotByMonth));
                OnPropertyChanged(nameof(ShowSummaryViewByMonth));
                OnPropertyChanged(nameof(ShowForecastZeroAsBlank));
                if (forecastGroupingChanged)
                {
                    ApplyForecastGrouping();
                }

                if (string.Equals(ActiveWorkspaceKey, "Raw Transactions", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyRawTransactionGrouping();
                }
            }
        }
    }

    public WorkspaceViewTab? SelectedDetailWorkspaceView
    {
        get => _selectedDetailWorkspaceView;
        set
        {
            if (SetProperty(ref _selectedDetailWorkspaceView, value))
            {
                if (value is not null)
                {
                    _selectedDetailWorkspaceViewNames[ActiveDetailWorkspaceKey] = value.Name;
                }

                OnPropertyChanged(nameof(ShowLedgerCostsPivotByMonth));
                if (string.Equals(ActiveDetailWorkspaceKey, "Ledger Costs", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyLedgerTransactionGrouping();
                }
            }
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        set => SetProperty(ref _isDirty, value);
    }

    public bool ShowCtcMonthForecastColumns
    {
        get => _showCtcMonthForecastColumns;
        set
        {
            if (SetProperty(ref _showCtcMonthForecastColumns, value))
            {
                OnPropertyChanged(nameof(IsCtcMonthForecastSelectionAvailable));
                SaveUserPreferences();
            }
        }
    }

    public bool ShowMonthNameAboveFiscalPeriod
    {
        get => _showMonthNameAboveFiscalPeriod;
        set
        {
            if (SetProperty(ref _showMonthNameAboveFiscalPeriod, value))
            {
                RebuildCtcMonthForecastColumns();
                SaveUserPreferences();
            }
        }
    }

    public bool KeepColumnHighlightsAcrossTabs
    {
        get => _keepColumnHighlightsAcrossTabs;
        set
        {
            if (SetProperty(ref _keepColumnHighlightsAcrossTabs, value))
            {
                SaveUserPreferences();
            }
        }
    }

    private bool _showVarianceIndicators;

    public bool ShowVarianceIndicators
    {
        get => _showVarianceIndicators;
        set
        {
            if (SetProperty(ref _showVarianceIndicators, value))
            {
                SaveUserPreferences();
            }
        }
    }

    public bool ShowCtcMonthForecastYearTotals
    {
        get => _showCtcMonthForecastYearTotals;
        set
        {
            if (SetProperty(ref _showCtcMonthForecastYearTotals, value))
            {
                _dataset.ShowCtcMonthForecastYearTotals = value;
                RebuildCtcMonthForecastColumns();
                IsDirty = true;
                SaveUserPreferences();
            }
        }
    }

    public bool ShowCurrencySymbols
    {
        get => _showCurrencySymbols;
        set
        {
            if (SetProperty(ref _showCurrencySymbols, value))
            {
                SaveUserPreferences();
            }
        }
    }

    public int ForecastMonthMillionDecimals
    {
        get => _forecastMonthMillionDecimals;
        set
        {
            var normalized = Math.Clamp(value, 0, 4);
            if (SetProperty(ref _forecastMonthMillionDecimals, normalized))
            {
                SaveUserPreferences();
            }
        }
    }

    public int SelectedCtcMonthForecastYear
    {
        get => _selectedCtcMonthForecastYear;
        set
        {
            if (SetProperty(ref _selectedCtcMonthForecastYear, value))
            {
                RebuildCtcMonthForecastColumns();
            }
        }
    }

    public IReadOnlyCollection<int> SelectedCtcMonthForecastYears => _selectedCtcMonthForecastYears;

    public string ForecastFreezeColumnKey
    {
        get => _forecastFreezeColumnKey;
        set
        {
            if (SetProperty(ref _forecastFreezeColumnKey, string.IsNullOrWhiteSpace(value) ? DefaultForecastFreezeColumnKey : value))
            {
                SaveUserPreferences();
            }
        }
    }

    public bool IsCtcMonthForecastSelectionAvailable => AvailableCtcMonthForecastYears.Count > 0;

    public CategorySortOption? SelectedCategorySortOption
    {
        get => _selectedCategorySortOption;
        set
        {
            if (SetProperty(ref _selectedCategorySortOption, value))
            {
                ApplyCategorySorting();
                SaveUserPreferences();
            }
        }
    }

    public decimal TotalForecastCtc => _totals.TotalForecastCtc;
    public decimal TotalCostToDate => _totals.TotalCostToDate;
    public decimal PlannedCostFcc => _totals.PlannedCostFcc;
    public decimal TotalBudget => _totals.TotalBudget;
    public decimal TotalBudgetVariance => _totals.TotalBudgetVariance;
    public decimal CurrentMonthCostTotal => _totals.CurrentMonthCostTotal;
    public decimal RemainingForecastTotal => _totals.RemainingForecastTotal;
    public decimal MonthlyVarianceTotal => _totals.MonthlyVarianceTotal;
    public decimal TotalContingencyRemaining => _totals.TotalContingencyRemaining;
    public int ForecastLineCount => ForecastLines.Count;
    public int TransactionCount => Transactions.Count;
    public int ValidationIssueCount => ValidationIssues.Count;
    public string AppVersion => AppVersionValue;
    public string AppVersionDisplay => $"v{AppVersion}";
    public string AppWindowTitle => $"Project Cost Forecast - {ReleaseStage} - {AppVersionDisplay}";
    public string ReleaseStage => "Release Candidate";
    public decimal FiscalReportSpentTotal => _totals.FiscalReportSpentTotal;
    public decimal FiscalReportCostToCompleteTotal => _totals.FiscalReportCostToCompleteTotal;
    public decimal FiscalReportPlannedCostTotal => _totals.FiscalReportPlannedCostTotal;
    public decimal FiscalReportBudgetTotal => _totals.FiscalReportBudgetTotal;
    public decimal FiscalReportVarianceTotal => _totals.FiscalReportVarianceTotal;
    public decimal ProjectContingencyTotal => _totals.ProjectContingencyTotal;
    public decimal ContingencyExpendedTotal => _totals.ContingencyExpendedTotal;
    public decimal ContingencyProposedTotal => _totals.ContingencyProposedTotal;
    public decimal ContingencyRemainingTotal => _totals.ContingencyRemainingTotal;
    public bool ShowActualsPivotByMonth => string.Equals(ActiveWorkspaceKey, "Actuals Pivot", StringComparison.OrdinalIgnoreCase)
        && string.Equals(SelectedWorkspaceView?.ContentKey, "ByMonth", StringComparison.OrdinalIgnoreCase);
    public bool ShowActualsPivotByPeriodRows => string.Equals(ActiveWorkspaceKey, "Actuals Pivot", StringComparison.OrdinalIgnoreCase)
        && string.Equals(SelectedWorkspaceView?.ContentKey, "ByPeriodRows", StringComparison.OrdinalIgnoreCase);
    public bool ShowForecastPivotByMonth => string.Equals(ActiveWorkspaceKey, "Forecast Pivot", StringComparison.OrdinalIgnoreCase)
        && string.Equals(SelectedWorkspaceView?.ContentKey, "ByMonth", StringComparison.OrdinalIgnoreCase);
    public bool ShowRawTransactionsGroupedByMonth => string.Equals(ActiveWorkspaceKey, "Raw Transactions", StringComparison.OrdinalIgnoreCase)
        && string.Equals(SelectedWorkspaceView?.ContentKey, "GroupByMonth", StringComparison.OrdinalIgnoreCase);
    public bool ShowRawTransactionsPivotByMonth => string.Equals(ActiveWorkspaceKey, "Raw Transactions", StringComparison.OrdinalIgnoreCase)
        && string.Equals(SelectedWorkspaceView?.ContentKey, "PivotByMonth", StringComparison.OrdinalIgnoreCase);
    public bool ShowSummaryViewByMonth => string.Equals(ActiveWorkspaceKey, "Summary View", StringComparison.OrdinalIgnoreCase)
        && string.Equals(SelectedWorkspaceView?.ContentKey, "PivotByMonth", StringComparison.OrdinalIgnoreCase);
    public bool ShowLedgerCostsPivotByMonth => string.Equals(ActiveDetailWorkspaceKey, "Ledger Costs", StringComparison.OrdinalIgnoreCase)
        && string.Equals(SelectedDetailWorkspaceView?.ContentKey, "PivotByMonth", StringComparison.OrdinalIgnoreCase);

    public IEnumerable<MonthlyForecast> SelectedMonthlyForecasts => _activeLedgerForecastEntries;

    public ForecastLine? GetForecastLine(MonthlyForecast? forecast)
    {
        return forecast is not null && _forecastLineByMonthlyForecast.TryGetValue(forecast, out var line)
            ? line
            : null;
    }
    public decimal LedgerForecastTotal => _ledgerTotals.ForecastTotal;
    public decimal LedgerProjectedTotal => _ledgerTotals.ProjectedTotal;

    public IEnumerable<CostTransaction> LedgerTransactions => _activeLedgerTransactions;

    public string LedgerTitle
    {
        get
        {
            var activeForecastLine = GetActiveLedgerForecastLine();
            if (activeForecastLine is not null)
            {
                return $"{activeForecastLine.ResourceName} / {activeForecastLine.TaskNumber}";
            }

            if (SelectedResourceSummary is not null)
            {
                return SelectedResourceSummary.ResourceName;
            }

            return "Select a resource";
        }
    }

    public int LedgerTransactionCount => _ledgerTotals.TransactionCount;
    public decimal LedgerTransactionTotal => _ledgerTotals.TransactionTotal;
    public decimal LedgerUnitsTotal => _ledgerTotals.UnitsTotal;
    public decimal LedgerAverageRate => _ledgerTotals.AverageRate;

    public bool ConfirmClose() => ConfirmDiscardUnsavedChanges();
}
