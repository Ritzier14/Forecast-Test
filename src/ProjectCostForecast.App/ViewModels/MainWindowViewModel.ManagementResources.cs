using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;

namespace ProjectCostForecast.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private bool _syncingManagementResources;

    public bool IsManagementResource(ForecastLine line)
    {
        return ManagementResources.Any(resource => MatchesManagementResource(resource, line));
    }

    public ManagementResource AddManagementResource(ForecastLine line)
    {
        var existing = ManagementResources.FirstOrDefault(resource => MatchesManagementResource(resource, line));
        if (existing is not null)
        {
            StatusText = $"{line.ResourceName} is already a management resource.";
            return existing;
        }

        var rate = CalculateManagementResourceDefaultRate(line);

        var resource = new ManagementResource
        {
            SourceRowNumber = line.RowNumber,
            TaskNumber = line.TaskNumber,
            ResourceName = line.ResourceName,
            ProjectCode = line.ProjectCode,
            SourceLine = line
        };
        resource.SetCalculatedHourlyRate(rate, resetCurrentRate: true);
        resource.MonthlyHours = ManagementResource.StandardMonthlyHours;
        resource.EnsurePeriods(_dataset.ForecastPeriods);
        PopulateManagementAllocationFromForecastLine(resource, line);
        resource.PropertyChanged += ManagementResource_PropertyChanged;

        ManagementResources.Add(resource);
        AddManagementResourceRows(resource);
        IsDirty = true;
        AddAuditEvent(
            "ManagementResource",
            line.RowNumber.ToString(),
            "Added",
            string.Empty,
            line.ResourceName,
            "Added forecast resource to management planning");
        StatusText = $"Added {line.ResourceName} to the Resources tab at {resource.HourlyRate:C2} per hour.";
        OnPropertyChanged(nameof(ManagementResources));
        return resource;
    }

    public void RemoveManagementResource(ManagementResource resource)
    {
        if (!ManagementResources.Contains(resource))
        {
            return;
        }

        resource.PropertyChanged -= ManagementResource_PropertyChanged;
        ManagementResources.Remove(resource);
        RemoveManagementResourceRows(ManagementResourceAllocationRows, resource);
        RemoveManagementResourceRows(ManagementResourceHoursRows, resource);
        RemoveManagementResourceRows(ManagementResourceCostRows, resource);
        IsDirty = true;
        AddAuditEvent(
            "ManagementResource",
            resource.SourceRowNumber.ToString(),
            "Removed",
            resource.ResourceName,
            string.Empty,
            "Removed resource from management planning");
        StatusText = $"Removed {resource.ResourceName} from the Resources tab.";
        OnPropertyChanged(nameof(ManagementResources));
    }

    private void LoadManagementResources(IEnumerable<ManagementResource> resources)
    {
        foreach (var resource in ManagementResources)
        {
            resource.PropertyChanged -= ManagementResource_PropertyChanged;
        }

        var loadedResources = resources.ToList();
        foreach (var resource in loadedResources)
        {
            resource.SourceLine = _dataset.ForecastLines.FirstOrDefault(line => MatchesManagementResource(resource, line));
            if (resource.SourceLine is not null)
            {
                resource.SetCalculatedHourlyRate(CalculateManagementResourceDefaultRate(resource.SourceLine), resetCurrentRate: !resource.IsHourlyRateOverridden);
            }

            resource.EnsurePeriods(_dataset.ForecastPeriods);
            resource.PropertyChanged += ManagementResource_PropertyChanged;
        }

        ReplaceCollection(ManagementResources, loadedResources);
        ReplaceCollection(
            ManagementResourceAllocationRows,
            loadedResources.Select(resource => new ManagementResourceTableRow(resource, ManagementResourceMetric.AllocationPercentage)));
        ReplaceCollection(
            ManagementResourceHoursRows,
            loadedResources.Select(resource => new ManagementResourceTableRow(resource, ManagementResourceMetric.Hours)));
        ReplaceCollection(
            ManagementResourceCostRows,
            loadedResources.Select(resource => new ManagementResourceTableRow(resource, ManagementResourceMetric.Cost)));
        OnPropertyChanged(nameof(ManagementResources));
    }

    private void ManagementResource_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not ManagementResource resource)
        {
            return;
        }

        NotifyManagementResourceRows(ManagementResourceAllocationRows, resource, e.PropertyName);
        NotifyManagementResourceRows(ManagementResourceHoursRows, resource, e.PropertyName);
        NotifyManagementResourceRows(ManagementResourceCostRows, resource, e.PropertyName);
        if (!_syncingManagementResources
            && resource.SourceLine is not null
            && (string.Equals(e.PropertyName, "Item[]", StringComparison.Ordinal)
                || string.Equals(e.PropertyName, nameof(ManagementResource.HourlyRate), StringComparison.Ordinal)
                || string.Equals(e.PropertyName, nameof(ManagementResource.MonthlyHours), StringComparison.Ordinal)))
        {
            SyncForecastLineFromManagementResource(resource);
        }

        IsDirty = true;
    }

    private void AddManagementResourceRows(ManagementResource resource)
    {
        ManagementResourceAllocationRows.Add(new ManagementResourceTableRow(resource, ManagementResourceMetric.AllocationPercentage));
        ManagementResourceHoursRows.Add(new ManagementResourceTableRow(resource, ManagementResourceMetric.Hours));
        ManagementResourceCostRows.Add(new ManagementResourceTableRow(resource, ManagementResourceMetric.Cost));
    }

    private static void RemoveManagementResourceRows(
        System.Collections.ObjectModel.ObservableCollection<ManagementResourceTableRow> rows,
        ManagementResource resource)
    {
        var row = rows.FirstOrDefault(item => ReferenceEquals(item.Resource, resource));
        if (row is not null)
        {
            rows.Remove(row);
        }
    }

    private static void NotifyManagementResourceRows(
        IEnumerable<ManagementResourceTableRow> rows,
        ManagementResource resource,
        string? propertyName)
    {
        foreach (var row in rows.Where(item => ReferenceEquals(item.Resource, resource)))
        {
            row.NotifyResourceChanged(propertyName);
        }
    }

    private static bool MatchesManagementResource(ManagementResource resource, ForecastLine line)
    {
        if (resource.SourceRowNumber > 0 && line.RowNumber > 0)
        {
            return resource.SourceRowNumber == line.RowNumber;
        }

        return string.Equals(resource.TaskNumber, line.TaskNumber, StringComparison.OrdinalIgnoreCase)
            && string.Equals(resource.ResourceName, line.ResourceName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(resource.ProjectCode, line.ProjectCode, StringComparison.OrdinalIgnoreCase);
    }

    public decimal CalculateManagementResourceDefaultRate(ForecastLine line)
    {
        var resourceTransactions = Transactions
            .Where(transaction => CalculationService.MatchesForecastResource(transaction, line) && transaction.UnitRate > 0)
            .ToList();
        if (resourceTransactions.Count == 0)
        {
            return 0;
        }

        var periodOrder = _dataset.ForecastPeriods
            .Where(period => !string.IsNullOrWhiteSpace(period.Label))
            .Select((period, index) => new { period.Label, Order = period.StartDate?.DayNumber ?? index })
            .ToDictionary(item => item.Label, item => item.Order, StringComparer.OrdinalIgnoreCase);
        var lastTwoPeriods = resourceTransactions
            .Select(transaction => transaction.FyPeriod)
            .Where(period => !string.IsNullOrWhiteSpace(period))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(period => periodOrder.GetValueOrDefault(period, int.MinValue))
            .Take(2)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (lastTwoPeriods.Count == 0)
        {
            return 0;
        }

        var latestPeriod = lastTwoPeriods
            .OrderByDescending(period => periodOrder.GetValueOrDefault(period, int.MinValue))
            .First();

        return resourceTransactions
            .Where(transaction => lastTwoPeriods.Contains(transaction.FyPeriod))
            .GroupBy(transaction => transaction.UnitRate)
            .Select(group => new
            {
                Rate = group.Key,
                Count = group.Count(),
                LatestCount = group.Count(transaction => string.Equals(transaction.FyPeriod, latestPeriod, StringComparison.OrdinalIgnoreCase))
            })
            .OrderByDescending(group => group.Count)
            .ThenByDescending(group => group.LatestCount)
            .ThenByDescending(group => group.Rate)
            .First()
            .Rate;
    }

    public void ResetManagementResourceRate(ManagementResource resource)
    {
        resource.ResetHourlyRate();
    }

    public void SynchronizeManagementResourcesFromForecastLines(IEnumerable<ForecastLine> lines)
    {
        var changedLines = lines.ToHashSet(ReferenceEqualityComparer.Instance);
        if (changedLines.Count == 0)
        {
            return;
        }

        try
        {
            _syncingManagementResources = true;
            foreach (var resource in ManagementResources.Where(resource => resource.SourceLine is not null && changedLines.Contains(resource.SourceLine)))
            {
                PopulateManagementAllocationFromForecastLine(resource, resource.SourceLine!);
                NotifyManagementResourceRows(ManagementResourceAllocationRows, resource, "Item[]");
                NotifyManagementResourceRows(ManagementResourceHoursRows, resource, "Item[]");
                NotifyManagementResourceRows(ManagementResourceCostRows, resource, "Item[]");
            }
        }
        finally
        {
            _syncingManagementResources = false;
        }
    }

    private void PopulateManagementAllocationFromForecastLine(ManagementResource resource, ForecastLine line)
    {
        foreach (var period in _dataset.ForecastPeriods.Where(period => !string.IsNullOrWhiteSpace(period.Label)))
        {
            var percentage = resource.CalculatePercentageFromCost(line[period.Label]);
            resource.SetAllocation(period.Label, percentage);
        }
    }

    private void SyncForecastLineFromManagementResource(ManagementResource resource)
    {
        if (resource.SourceLine is null)
        {
            return;
        }

        try
        {
            _syncingManagementResources = true;
            foreach (var period in _dataset.ForecastPeriods.Where(period => !string.IsNullOrWhiteSpace(period.Label)))
            {
                resource.SourceLine[period.Label] = Math.Round(resource.CalculateForecastCost(period.Label), 0);
            }

            resource.SourceLine.NotifyMonthForecastValuesChanged();
            RecalculateForecastLinesForSpreadsheetEdit([resource.SourceLine]);
        }
        finally
        {
            _syncingManagementResources = false;
        }
    }
}
