using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

namespace ProjectCostForecast.App.Models;

public sealed class CategorySummary
{
    public string ProjectCode { get; set; } = string.Empty;
    public decimal TotalForecast { get; set; }
    public decimal CostToDate { get; set; }
    public decimal CurrentMonthCost { get; set; }
    public decimal PlannedCost { get; set; }
    public decimal Budget { get; set; }
    public decimal TotalBudgetVariance { get; set; }
    public decimal MonthForecastVariance { get; set; }
}

public sealed class ContingencyEntry
{
    public DateOnly? Date { get; set; }
    public decimal ContingencyExpended { get; set; }
    public decimal RemainingContingency { get; set; }
    public decimal ProposedExpenditure { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class ResourceSummary
{
    public string ResourceName { get; init; } = string.Empty;
    public string ProjectCodeList { get; init; } = string.Empty;
    public string ResourceCodeList { get; init; } = string.Empty;
    public string TaskNumberList { get; init; } = string.Empty;
    public string SourceList { get; init; } = string.Empty;
    public int TransactionCount { get; init; }
    public decimal Units { get; init; }
    public decimal Amount { get; init; }
    public decimal AverageRate => Units == 0 ? 0 : Amount / Units;
}

public sealed class FiscalYearReportLine
{
    public string FiscalYear { get; init; } = string.Empty;
    public decimal SpentToDate { get; init; }
    public decimal CostToComplete { get; init; }
    public decimal PlannedCost { get; init; }
    public decimal Budget { get; init; }
    public decimal Variance { get; init; }
}

public sealed class ActualsPeriodSummary
{
    public string TaskNumber { get; init; } = string.Empty;
    public string ResourceName { get; init; } = string.Empty;
    public string FyPeriod { get; init; } = string.Empty;
    public int TransactionCount { get; init; }
    public decimal Units { get; init; }
    public decimal Amount { get; init; }
}

public sealed class MonthlyPivotRow
{
    private readonly Dictionary<string, decimal> _periodAmounts;

    public MonthlyPivotRow(Dictionary<string, decimal> periodAmounts)
    {
        _periodAmounts = periodAmounts;
    }

    public string TaskNumber { get; init; } = string.Empty;
    public string ResourceName { get; init; } = string.Empty;
    public string ProjectCode { get; init; } = string.Empty;
    public decimal Total => _periodAmounts.Values.Sum();

    public decimal this[string period] => _periodAmounts.GetValueOrDefault(period);
}

public sealed class PivotFieldDefinition
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsNumeric { get; init; }
}

public sealed class PivotAreaField : ObservableModel
{
    private string _selectedFilterValue = PivotBuilderAllFilterValue;

    public const string PivotBuilderAllFilterValue = "(All)";

    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsNumeric { get; init; }
    public ObservableCollection<string> FilterValues { get; } = new BatchObservableCollection<string>([PivotBuilderAllFilterValue]);

    public string SelectedFilterValue
    {
        get => _selectedFilterValue;
        set => SetProperty(ref _selectedFilterValue, string.IsNullOrWhiteSpace(value) ? PivotBuilderAllFilterValue : value);
    }
}

public sealed class PivotResultColumn
{
    public string Key { get; init; } = string.Empty;
    public string Header { get; init; } = string.Empty;
    public bool IsNumeric { get; init; }
}

public sealed class PivotResultRow
{
    private readonly Dictionary<string, object> _values = new(StringComparer.OrdinalIgnoreCase);

    public object this[string key]
    {
        get => _values.GetValueOrDefault(key, string.Empty);
        set => _values[key] = value;
    }
}

public sealed class KpiOption
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public sealed class CategorySortOption
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public sealed class LedgerChartRangeOption
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int? VisibleMonths { get; init; }
    public double MonthSpacing { get; init; } = 36;
}

public sealed class KpiPill : ObservableModel
{
    private string _key = string.Empty;
    private string _name = string.Empty;
    private string _valueText = string.Empty;
    private string _subtext = string.Empty;
    private string _comparisonText = string.Empty;
    private string _comparisonDirection = string.Empty;
    private string _iconPath = string.Empty;
    private ImageSource? _iconSource;
    private Visibility _comparisonVisibility = Visibility.Collapsed;

    public int Id { get; init; }

    public string Key
    {
        get => _key;
        set => SetProperty(ref _key, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string ValueText
    {
        get => _valueText;
        set => SetProperty(ref _valueText, value);
    }

    public string Subtext
    {
        get => _subtext;
        set => SetProperty(ref _subtext, value);
    }

    public string ComparisonText
    {
        get => _comparisonText;
        set => SetProperty(ref _comparisonText, value);
    }

    public string ComparisonDirection
    {
        get => _comparisonDirection;
        set => SetProperty(ref _comparisonDirection, value);
    }

    public string IconPath
    {
        get => _iconPath;
        set => SetProperty(ref _iconPath, value);
    }

    public ImageSource? IconSource
    {
        get => _iconSource;
        set => SetProperty(ref _iconSource, value);
    }

    public Visibility ComparisonVisibility
    {
        get => _comparisonVisibility;
        set => SetProperty(ref _comparisonVisibility, value);
    }
}

public sealed class WorkspaceViewTab : ObservableModel
{
    private string _name = string.Empty;
    private string _editName = string.Empty;
    private bool _isEditing;
    private string _contentKey = string.Empty;

    public string WorkspaceKey { get; init; } = string.Empty;
    public string ContentKey
    {
        get => _contentKey;
        set => SetProperty(ref _contentKey, value);
    }
    public List<string> HiddenColumnKeys { get; set; } = [];
    public List<WorkspaceColumnLayout> ColumnLayouts { get; set; } = [];
    public bool ShowZeroAsBlank { get; set; } = true;
    public bool GroupForecastLinesByTask { get; set; }
    public string ForecastGroupByKey { get; set; } = string.Empty;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string EditName
    {
        get => _editName;
        set => SetProperty(ref _editName, value);
    }

    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    public string RenameRestoreName { get; set; } = string.Empty;
    public string DefaultName { get; set; } = string.Empty;
    public bool IsNewlyCreated { get; set; }
}

public sealed class ForecastMonthColumnDefinition
{
    public string Key { get; init; } = string.Empty;
    public string YearLabel { get; init; } = string.Empty;
    public string PrimaryLabel { get; init; } = string.Empty;
    public string SecondaryLabel { get; init; } = string.Empty;
    public Brush PrimaryBackground { get; init; } = Brushes.White;
    public Brush SecondaryBackground { get; init; } = Brushes.White;
    public Brush ValueBackground { get; init; } = Brushes.White;
    public Brush ValueForeground { get; init; } = Brushes.Black;
    public Visibility LeftSolidSeparatorVisibility { get; init; } = Visibility.Collapsed;
    public Visibility RightSolidSeparatorVisibility { get; init; } = Visibility.Collapsed;
    public Visibility LeftDashedSeparatorVisibility { get; init; } = Visibility.Collapsed;
    public Visibility RightDashedSeparatorVisibility { get; init; } = Visibility.Collapsed;
    public bool IsEditable { get; init; } = true;
    public bool IsTotal { get; init; }
}

public sealed class ChartLineSegment
{
    public double X1 { get; init; }
    public double Y1 { get; init; }
    public double X2 { get; init; }
    public double Y2 { get; init; }
}

public sealed class ChartLabel
{
    public double X { get; init; }
    public double Y { get; init; }
    public string Text { get; init; } = string.Empty;
}

public sealed class MonthlyReportFiscalSummaryRow
{
    public string Label { get; init; } = string.Empty;
    public string Year1Value { get; init; } = string.Empty;
    public string Year2Value { get; init; } = string.Empty;
    public string Year3Value { get; init; } = string.Empty;
    public string TotalValue { get; init; } = string.Empty;
}

public sealed class MonthlyReportCategoryRow
{
    public string ProjectCode { get; init; } = string.Empty;
    public string TotalForecastDisplay { get; init; } = string.Empty;
    public string CostToDateDisplay { get; init; } = string.Empty;
    public string PlannedCostDisplay { get; init; } = string.Empty;
    public string InitialBudgetDisplay { get; init; } = string.Empty;
    public string TotalBudgetVarianceDisplay { get; init; } = string.Empty;
    public string LastMonthPlannedCostDisplay { get; init; } = string.Empty;
    public string VarianceFromLastMonthDisplay { get; init; } = string.Empty;
}

public sealed class MonthlyReportVarianceCommentRow
{
    public string ProjectCode { get; init; } = string.Empty;
    public string TotalBudgetVarianceDisplay { get; init; } = string.Empty;
    public string VarianceLastMonthDisplay { get; init; } = string.Empty;
    public string MonthVarianceComment { get; init; } = string.Empty;
    public string TotalBudgetVarianceComment { get; init; } = string.Empty;
    public string AllMonthComments { get; init; } = string.Empty;
}

public sealed class MonthlyReportRiskItem
{
    public string Item { get; init; } = string.Empty;
    public string ProjectCode { get; init; } = string.Empty;
    public string CurrentValue { get; init; } = string.Empty;
    public string RiskRange { get; init; } = string.Empty;
    public string Comments { get; init; } = string.Empty;
}

public sealed class MonthlyReportPluggedRateItem
{
    public string Item { get; init; } = string.Empty;
    public string ProjectCode { get; init; } = string.Empty;
    public string CurrentValue { get; init; } = string.Empty;
    public string Comments { get; init; } = string.Empty;
}
