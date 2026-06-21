using System.Windows.Media;

namespace ProjectCostForecast.App.Models;

public sealed class ForecastLine : ObservableModel
{
    private decimal _costToDate;
    private decimal _currentMonthCost;
    private decimal _totalForecastCtc;
    private decimal _costToDateSummary;
    private decimal _plannedCostFcc;
    private decimal _monthForecast;
    private decimal _lastMonthPlannedCost;
    private decimal _lastMonthForecast;
    private decimal _varianceLastMonthToDate;
    private decimal _monthForecastVariance;
    private decimal _budget;
    private decimal _totalBudgetVariance;
    private string _manualAllMonthComment = string.Empty;
    private bool _useManualAllMonthComment;
    private string _reportingCategoryOverride = string.Empty;
    private string _taskName = string.Empty;
    private string _reportingCategory = string.Empty;

    public int RowNumber { get; set; }
    public string TaskNumber { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;
    public string ReportingCategoryOverride
    {
        get => _reportingCategoryOverride;
        set => SetProperty(ref _reportingCategoryOverride, value?.Trim() ?? string.Empty);
    }

    public double FormatGroup { get; set; }
    public bool IsManuallyAdded { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string TaskName
    {
        get => _taskName;
        private set => SetProperty(ref _taskName, value);
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public string ReportingCategory
    {
        get => _reportingCategory;
        set
        {
            var next = value?.Trim() ?? string.Empty;
            ReportingCategoryOverride = next;
            SetProperty(ref _reportingCategory, next);
        }
    }

    public decimal CostToDate { get => _costToDate; set => SetProperty(ref _costToDate, value); }
    public decimal TotalBudgetVarianceQuick { get; set; }
    public decimal PlannedCostVsBudgetQuick { get; set; }
    public decimal CurrentMonthCost { get => _currentMonthCost; set => SetProperty(ref _currentMonthCost, value); }
    public decimal LastMonthForecastQuick { get; set; }
    public decimal VarianceActualVsForecastMonthQuick { get; set; }

    public decimal TotalForecastCtc { get => _totalForecastCtc; set => SetProperty(ref _totalForecastCtc, value); }
    public decimal CostToDateSummary { get => _costToDateSummary; set => SetProperty(ref _costToDateSummary, value); }
    public decimal PlannedCostFcc { get => _plannedCostFcc; set => SetProperty(ref _plannedCostFcc, value); }
    public decimal MonthForecast { get => _monthForecast; set => SetProperty(ref _monthForecast, value); }
    public decimal LastMonthPlannedCost { get => _lastMonthPlannedCost; set => SetProperty(ref _lastMonthPlannedCost, value); }
    public decimal LastMonthForecast { get => _lastMonthForecast; set => SetProperty(ref _lastMonthForecast, value); }
    public decimal VarianceLastMonthToDate { get => _varianceLastMonthToDate; set => SetProperty(ref _varianceLastMonthToDate, value); }
    public decimal MonthForecastVariance { get => _monthForecastVariance; set => SetProperty(ref _monthForecastVariance, value); }
    public decimal Budget { get => _budget; set => SetProperty(ref _budget, value); }
    public decimal TotalBudgetVariance { get => _totalBudgetVariance; set => SetProperty(ref _totalBudgetVariance, value); }

    public string CommentsOnMonthForecastVariance { get; set; } = string.Empty;
    public string CommentsOnMonthBudgetVariance { get; set; } = string.Empty;
    public string CommentsOnTotalBudgetVariance { get; set; } = string.Empty;
    public List<ResourceCommentMetricPreference> ResourceCommentMetrics { get; set; } = [];
    public List<ForecastMonthlyComment> MonthlyCommentHistory { get; set; } = [];
    public string ManualAllMonthComment { get => _manualAllMonthComment; set => SetProperty(ref _manualAllMonthComment, value); }
    public bool UseManualAllMonthComment { get => _useManualAllMonthComment; set => SetProperty(ref _useManualAllMonthComment, value); }
    public string ManualCommentPeriodLabel { get; set; } = string.Empty;
    public string ManualCommentMonthLabel { get; set; } = string.Empty;
    public DateTime? ManualCommentRecordedAt { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasManualAllMonthComment => !string.IsNullOrWhiteSpace(ManualAllMonthComment);

    public void SetResolvedTaskMetadata(string taskName, string reportingCategory)
    {
        TaskName = string.IsNullOrWhiteSpace(taskName) ? "Unnamed task" : taskName.Trim();
        SetProperty(ref _reportingCategory, string.IsNullOrWhiteSpace(reportingCategory)
            ? TaskName
            : reportingCategory.Trim(), nameof(ReportingCategory));
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public string AllMonthComments => UseManualAllMonthComment && HasManualAllMonthComment
        ? BuildManualCommentDisplayText()
        : string.Join(
            Environment.NewLine,
            MonthlyCommentHistory
                .OrderByDescending(comment => comment.PeriodSortKey)
                .ThenByDescending(comment => comment.RecordedAt)
                .Select(comment => comment.DisplayText));

    public void NotifyAllMonthCommentsChanged()
    {
        OnPropertyChanged(nameof(AllMonthComments));
        OnPropertyChanged(nameof(HasManualAllMonthComment));
    }

    private string BuildManualCommentDisplayText()
    {
        var period = string.IsNullOrWhiteSpace(ManualCommentMonthLabel)
            ? $"FY {ManualCommentPeriodLabel}"
            : $"{ManualCommentMonthLabel} - FY {ManualCommentPeriodLabel}";
        return $"{period}: {ResourceName}: {ManualAllMonthComment}";
    }

    public List<MonthlyForecast> MonthlyForecasts { get; set; } = [];

    public decimal this[string forecastKey]
    {
        get
        {
            if (string.IsNullOrWhiteSpace(forecastKey))
            {
                return 0;
            }

            if (forecastKey.StartsWith("TOTAL:", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(forecastKey["TOTAL:".Length..], out var calendarYear))
            {
                return GetCalendarYearForecastTotal(calendarYear);
            }

            return MonthlyForecasts
                .Where(forecast => string.Equals(forecast.PeriodLabel, forecastKey, StringComparison.OrdinalIgnoreCase))
                .Sum(forecast => forecast.Amount);
        }
        set
        {
            if (string.IsNullOrWhiteSpace(forecastKey) || forecastKey.StartsWith("TOTAL:", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var forecast = MonthlyForecasts.FirstOrDefault(item =>
                string.Equals(item.PeriodLabel, forecastKey, StringComparison.OrdinalIgnoreCase));

            if (forecast is not null)
            {
                forecast.Amount = value;
            }
        }
    }

    public decimal GetCalendarYearForecastTotal(int calendarYear)
    {
        return MonthlyForecasts
            .Where(forecast => forecast.PeriodStartDate?.Year == calendarYear)
            .Sum(forecast => forecast.Amount);
    }

    public void NotifyMonthForecastValuesChanged()
    {
        OnPropertyChanged("Item[]");
    }

    public void EnsureResourceCommentMetrics()
    {
        ResourceCommentMetrics ??= [];
        if (ResourceCommentMetrics.Count == 0)
        {
            ResourceCommentMetrics.AddRange(ResourceCommentMetricPreference.CreateDefaults());
            return;
        }

        var defaults = ResourceCommentMetricPreference.CreateDefaults();
        foreach (var metric in defaults)
        {
            if (!ResourceCommentMetrics.Any(existing => string.Equals(existing.Key, metric.Key, StringComparison.OrdinalIgnoreCase)))
            {
                ResourceCommentMetrics.Add(metric);
            }
        }

        ResourceCommentMetrics = ResourceCommentMetrics
            .OrderBy(metric => metric.DisplayOrder)
            .ThenBy(metric => metric.Label)
            .ToList();
    }
}

public sealed class MonthlyForecast : ObservableModel
{
    private static readonly Brush LockedBackgroundBrush = BrushFactory.Frozen(0xF3, 0xF4, 0xF6);
    private static readonly Brush LockedForegroundBrush = BrushFactory.Frozen(0x94, 0xA3, 0xB8);
    private decimal _amount;
    private bool _isLocked;
    private Brush _backgroundBrush = Brushes.White;
    private Brush _foregroundBrush = Brushes.Black;

    public event EventHandler<ValueChangedEventArgs<decimal>>? AmountChanged;

    public string PeriodLabel { get; set; } = string.Empty;
    public DateOnly? PeriodStartDate { get; set; }

    public decimal Amount
    {
        get => _amount;
        set
        {
            var oldValue = _amount;
            if (SetProperty(ref _amount, value))
            {
                AmountChanged?.Invoke(this, new ValueChangedEventArgs<decimal>(oldValue, value));
            }
        }
    }

    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            if (SetProperty(ref _isLocked, value))
            {
                BackgroundBrush = value ? LockedBackgroundBrush : Brushes.White;
                ForegroundBrush = value ? LockedForegroundBrush : Brushes.Black;
                OnPropertyChanged(nameof(IsEditable));
            }
        }
    }

    public bool IsEditable => !IsLocked;

    [System.Text.Json.Serialization.JsonIgnore]
    public Brush BackgroundBrush
    {
        get => _backgroundBrush;
        private set => SetProperty(ref _backgroundBrush, value);
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public Brush ForegroundBrush
    {
        get => _foregroundBrush;
        private set => SetProperty(ref _foregroundBrush, value);
    }

}

public sealed class ForecastMonthlyComment
{
    public string PeriodLabel { get; set; } = string.Empty;
    public string MonthLabel { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; } = DateTime.Now;

    [System.Text.Json.Serialization.JsonIgnore]
    public int PeriodSortKey => Services.FiscalPeriod.SortKey(PeriodLabel) == int.MaxValue
        ? 0
        : Services.FiscalPeriod.SortKey(PeriodLabel);

    [System.Text.Json.Serialization.JsonIgnore]
    public string DisplayText
    {
        get
        {
            var period = string.IsNullOrWhiteSpace(MonthLabel)
                ? $"FY {PeriodLabel}"
                : $"{MonthLabel} - FY {PeriodLabel}";
            return $"{period}: {ResourceName}: {Text}";
        }
    }
}

public sealed class ResourceCommentMetricPreference
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsVisible { get; set; } = true;
    public int DisplayOrder { get; set; }

    public static List<ResourceCommentMetricPreference> CreateDefaults()
    {
        return
        [
            new() { Key = "TotalBudget", Label = "Total Budget", IsVisible = true, DisplayOrder = 0 },
            new() { Key = "FinalForecast", Label = "Final Forecast", IsVisible = true, DisplayOrder = 1 },
            new() { Key = "CostToComplete", Label = "Cost to Complete", IsVisible = true, DisplayOrder = 2 },
            new() { Key = "CostToDate", Label = "Cost to Date", IsVisible = true, DisplayOrder = 3 },
            new() { Key = "BudgetVarianceToDate", Label = "Variance from Budget to Date", IsVisible = true, DisplayOrder = 4 },
            new() { Key = "BudgetVarianceThisMonth", Label = "Variance Change from Budget This Month", IsVisible = true, DisplayOrder = 5 },
            new() { Key = "ForecastLastPeriod", Label = "Amount Forecast Last Period", IsVisible = true, DisplayOrder = 6 },
            new() { Key = "ActualCostToDate", Label = "Actual Cost to Date", IsVisible = true, DisplayOrder = 7 },
            new() { Key = "CostVsLastForecast", Label = "Variance in Cost vs Last Month Forecast", IsVisible = true, DisplayOrder = 8 }
        ];
    }
}
