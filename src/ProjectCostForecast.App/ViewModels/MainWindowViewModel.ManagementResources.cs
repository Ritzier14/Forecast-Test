using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;

namespace ProjectCostForecast.App.ViewModels;

public sealed partial class MainWindowViewModel
{
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

        var matchingTransactions = Transactions
            .Where(transaction => CalculationService.MatchesForecastLine(transaction, line))
            .ToList();
        var units = matchingTransactions.Sum(transaction => transaction.Units);
        var rate = units == 0
            ? matchingTransactions.Where(transaction => transaction.UnitRate != 0).Select(transaction => transaction.UnitRate).DefaultIfEmpty().Average()
            : matchingTransactions.Sum(transaction => transaction.Amount) / units;

        var resource = new ManagementResource
        {
            SourceRowNumber = line.RowNumber,
            TaskNumber = line.TaskNumber,
            ResourceName = line.ResourceName,
            ProjectCode = line.ProjectCode,
            HourlyRate = Math.Max(0, rate),
            SourceLine = line
        };
        resource.EnsurePeriods(_dataset.ForecastPeriods);
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
}
