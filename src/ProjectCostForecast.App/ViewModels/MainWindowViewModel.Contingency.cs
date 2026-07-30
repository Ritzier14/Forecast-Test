using System.Collections.Specialized;
using System.ComponentModel;
using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private readonly HashSet<ContingencyEntry> _trackedContingencyEntries = [];
    private bool _suppressContingencyTracking;

    private void InitializeContingencyTracking()
    {
        ContingencyEntries.CollectionChanged += ContingencyEntriesChanged;
        SynchronizeContingencySubscriptions();
    }

    private void ReplaceContingencyEntries(IEnumerable<ContingencyEntry> entries)
    {
        _suppressContingencyTracking = true;
        try
        {
            ReplaceCollection(ContingencyEntries, entries);
            SynchronizeContingencySubscriptions();
        }
        finally
        {
            _suppressContingencyTracking = false;
        }
    }

    private void ContingencyEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SynchronizeContingencySubscriptions();
        MarkContingencyChanged("Contingency rows changed");
    }

    private void ContingencyEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var field = string.IsNullOrWhiteSpace(e.PropertyName) ? "value" : e.PropertyName;
        MarkContingencyChanged($"Contingency {field} changed");
    }

    private void SynchronizeContingencySubscriptions()
    {
        var currentEntries = new HashSet<ContingencyEntry>(
            ContingencyEntries,
            ReferenceEqualityComparer.Instance);
        foreach (var removed in _trackedContingencyEntries.Where(entry => !currentEntries.Contains(entry)).ToList())
        {
            removed.PropertyChanged -= ContingencyEntryPropertyChanged;
            _trackedContingencyEntries.Remove(removed);
        }

        foreach (var added in currentEntries.Where(entry => _trackedContingencyEntries.Add(entry)))
        {
            added.PropertyChanged += ContingencyEntryPropertyChanged;
        }
    }

    private void MarkContingencyChanged(string status)
    {
        if (_suppressContingencyTracking)
        {
            return;
        }

        NotifyTotalsChanged();
        IsDirty = true;
        StatusText = status;
    }
}
