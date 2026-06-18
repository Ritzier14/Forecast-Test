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
            }
        }
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

    private static decimal CalculateHours(decimal percentage) => StandardMonthlyHours * percentage / 100m;
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
        set => Resource.HourlyRate = value;
    }

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

        OnPropertyChanged("Item[]");
    }
}
