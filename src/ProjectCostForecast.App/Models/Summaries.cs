using System.Collections.ObjectModel;
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

public sealed class ContingencyEntry : ObservableModel
{
    private DateOnly? _date;
    private decimal _contingencyExpended;
    private decimal _remainingContingency;
    private decimal _proposedExpenditure;
    private string _reason = string.Empty;
    private string _status = string.Empty;

    public DateOnly? Date { get => _date; set => SetProperty(ref _date, value); }
    public decimal ContingencyExpended { get => _contingencyExpended; set => SetProperty(ref _contingencyExpended, value); }
    public decimal RemainingContingency { get => _remainingContingency; set => SetProperty(ref _remainingContingency, value); }
    public decimal ProposedExpenditure { get => _proposedExpenditure; set => SetProperty(ref _proposedExpenditure, value); }
    public string Reason { get => _reason; set => SetProperty(ref _reason, value ?? string.Empty); }
    public string Status { get => _status; set => SetProperty(ref _status, value ?? string.Empty); }
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
    public string Aggregation { get; init; } = string.Empty;
    public string DisplayName => string.Equals(Aggregation, "Sum", StringComparison.OrdinalIgnoreCase)
        ? $"Sum of {Name}"
        : Name;
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

public enum LedgerChartTimeScale
{
    Month,
    Quarter,
    HalfYear,
    Year
}

public sealed class MonthlyForecastAcrossRow : ObservableModel
{
    private readonly Dictionary<string, decimal> _values;
    private readonly Action<string, decimal>? _valueChanged;

    public MonthlyForecastAcrossRow(
        string resourceName,
        string taskNumber,
        string metric,
        IEnumerable<KeyValuePair<string, decimal>> values,
        Action<string, decimal>? valueChanged = null)
    {
        ResourceName = resourceName;
        TaskNumber = taskNumber;
        Metric = metric;
        _values = values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        _valueChanged = valueChanged;
    }

    public string ResourceName { get; }
    public string TaskNumber { get; }
    public string Metric { get; }

    public decimal this[string period]
    {
        get => _values.GetValueOrDefault(period);
        set
        {
            if (string.IsNullOrWhiteSpace(period) || _values.GetValueOrDefault(period) == value)
            {
                return;
            }

            _values[period] = value;
            _valueChanged?.Invoke(period, value);
            OnPropertyChanged("Item[]");
        }
    }

    public IReadOnlyDictionary<string, decimal> Values => _values;
}

public sealed class TaskCodeReviewRow
{
    public string TaskCode { get; init; } = string.Empty;
    public string AssignedName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
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
