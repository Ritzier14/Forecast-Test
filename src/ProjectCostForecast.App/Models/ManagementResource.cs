using System.Text.Json.Serialization;

namespace ProjectCostForecast.App.Models;

public enum ManagementResourceMetric
{
    AllocationPercentage,
    Hours,
    Cost
}

public sealed class ManagementResource : ObservableModel
{
    public const decimal StandardMonthlyHours = 160m;

    private decimal _hourlyRate;
    private decimal _calculatedHourlyRate;
    private decimal _monthlyHours = StandardMonthlyHours;
    private bool _isHourlyRateOverridden;

    public int SourceRowNumber { get; set; }
    public string TaskNumber { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;
    public List<ManagementResourceAllocation> MonthlyAllocations { get; set; } = [];

    [JsonIgnore]
    public ForecastLine? SourceLine { get; set; }

    public decimal HourlyRate
    {
        get => _hourlyRate;
        set
        {
            if (SetProperty(ref _hourlyRate, Math.Max(0, value)))
            {
                OnPropertyChanged("Item[]");
                OnPropertyChanged(nameof(RateStatus));
            }
        }
    }

    public decimal CalculatedHourlyRate
    {
        get => _calculatedHourlyRate;
        set
        {
            if (SetProperty(ref _calculatedHourlyRate, Math.Max(0, value)))
            {
                OnPropertyChanged(nameof(RateStatus));
            }
        }
    }

    public bool IsHourlyRateOverridden
    {
        get => _isHourlyRateOverridden;
        set
        {
            if (SetProperty(ref _isHourlyRateOverridden, value))
            {
                OnPropertyChanged(nameof(RateStatus));
            }
        }
    }

    public decimal MonthlyHours
    {
        get => _monthlyHours <= 0 ? StandardMonthlyHours : _monthlyHours;
        set
        {
            if (SetProperty(ref _monthlyHours, Math.Max(1, value)))
            {
                OnPropertyChanged("Item[]");
            }
        }
    }

    [JsonIgnore]
    public string RateStatus => IsHourlyRateOverridden ? "Overridden" : "Calculated";

    public void SetCalculatedHourlyRate(decimal rate, bool resetCurrentRate)
    {
        CalculatedHourlyRate = rate;
        if (resetCurrentRate || HourlyRate == 0)
        {
            _hourlyRate = Math.Max(0, rate);
            OnPropertyChanged(nameof(HourlyRate));
            OnPropertyChanged("Item[]");
        }
    }

    public void OverrideHourlyRate(decimal rate)
    {
        HourlyRate = rate;
        IsHourlyRateOverridden = true;
    }

    public void ResetHourlyRate()
    {
        IsHourlyRateOverridden = false;
        HourlyRate = CalculatedHourlyRate;
    }

    [JsonIgnore]
    public decimal this[string periodKey]
    {
        get => GetValue(ManagementResourceMetric.AllocationPercentage, periodKey);
        set => SetAllocation(periodKey, value);
    }

    public void EnsurePeriods(IEnumerable<ForecastPeriod> periods)
    {
        MonthlyAllocations ??= [];
        foreach (var period in periods.Where(period => !string.IsNullOrWhiteSpace(period.Label)))
        {
            var existing = MonthlyAllocations.FirstOrDefault(item =>
                string.Equals(item.PeriodLabel, period.Label, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                MonthlyAllocations.Add(new ManagementResourceAllocation
                {
                    PeriodLabel = period.Label,
                    PeriodStartDate = period.StartDate
                });
            }
            else if (!existing.PeriodStartDate.HasValue)
            {
                existing.PeriodStartDate = period.StartDate;
            }
        }
    }

    public decimal GetValue(ManagementResourceMetric metric, string periodKey)
    {
        var allocations = GetAllocations(periodKey);
        return metric switch
        {
            ManagementResourceMetric.AllocationPercentage => allocations.Sum(item => item.Percentage),
            ManagementResourceMetric.Hours => allocations.Sum(item => CalculateHours(item.Percentage)),
            ManagementResourceMetric.Cost => allocations.Sum(item => CalculateHours(item.Percentage) * HourlyRate),
            _ => 0
        };
    }

    public void SetAllocation(string periodKey, decimal percentage)
    {
        if (string.IsNullOrWhiteSpace(periodKey) || periodKey.StartsWith("TOTAL:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var allocation = MonthlyAllocations.FirstOrDefault(item =>
            string.Equals(item.PeriodLabel, periodKey, StringComparison.OrdinalIgnoreCase));
        if (allocation is null)
        {
            allocation = new ManagementResourceAllocation { PeriodLabel = periodKey };
            MonthlyAllocations.Add(allocation);
        }

        var normalized = Math.Clamp(percentage, 0, 100);
        if (allocation.Percentage == normalized)
        {
            return;
        }

        allocation.Percentage = normalized;
        OnPropertyChanged("Item[]");
    }

    private IEnumerable<ManagementResourceAllocation> GetAllocations(string periodKey)
    {
        if (periodKey.StartsWith("TOTAL:", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(periodKey["TOTAL:".Length..], out var calendarYear))
        {
            return MonthlyAllocations.Where(item => item.PeriodStartDate?.Year == calendarYear);
        }

        return MonthlyAllocations.Where(item =>
            string.Equals(item.PeriodLabel, periodKey, StringComparison.OrdinalIgnoreCase));
    }

    public decimal CalculateForecastCost(string periodKey) =>
        GetValue(ManagementResourceMetric.AllocationPercentage, periodKey) * MonthlyHours * HourlyRate / 100m;

    public decimal CalculatePercentageFromCost(decimal amount)
    {
        var capacity = MonthlyHours * HourlyRate;
        return capacity <= 0 ? 0 : Math.Clamp(amount / capacity * 100m, 0, 100);
    }

    private decimal CalculateHours(decimal percentage) => MonthlyHours * percentage / 100m;
}

public sealed class ManagementResourceAllocation
{
    public string PeriodLabel { get; set; } = string.Empty;
    public DateOnly? PeriodStartDate { get; set; }
    public decimal Percentage { get; set; }
}

public sealed class ManagementResourceTableRow : ObservableModel
{
    public ManagementResourceTableRow(ManagementResource resource, ManagementResourceMetric metric)
    {
        Resource = resource;
        Metric = metric;
    }

    public ManagementResource Resource { get; }
    public ManagementResourceMetric Metric { get; }
    public int SourceRowNumber => Resource.SourceRowNumber;
    public string TaskNumber => Resource.TaskNumber;
    public string ResourceName => Resource.ResourceName;
    public string ProjectCode => Resource.ProjectCode;

    public decimal HourlyRate
    {
        get => Resource.HourlyRate;
        set => Resource.OverrideHourlyRate(value);
    }

    public decimal MonthlyHours
    {
        get => Resource.MonthlyHours;
        set => Resource.MonthlyHours = value;
    }

    public string RateStatus => Resource.RateStatus;

    public decimal this[string periodKey]
    {
        get => Resource.GetValue(Metric, periodKey);
        set
        {
            if (Metric == ManagementResourceMetric.AllocationPercentage)
            {
                Resource.SetAllocation(periodKey, value);
            }
        }
    }

    public void NotifyResourceChanged(string? propertyName)
    {
        if (string.Equals(propertyName, nameof(ManagementResource.HourlyRate), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(HourlyRate));
        }

        if (string.Equals(propertyName, nameof(ManagementResource.MonthlyHours), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(MonthlyHours));
        }

        if (string.Equals(propertyName, nameof(ManagementResource.RateStatus), StringComparison.Ordinal)
            || string.Equals(propertyName, nameof(ManagementResource.IsHourlyRateOverridden), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(RateStatus));
        }

        OnPropertyChanged("Item[]");
    }
}
