using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ProjectCostForecast.App.Models;

public sealed class BatchObservableCollection<T> : ObservableCollection<T>
{
    public BatchObservableCollection()
    {
    }

    public BatchObservableCollection(IEnumerable<T> items)
        : base(items)
    {
    }

    public void ReplaceWith(IEnumerable<T> items)
    {
        if (ReferenceEquals(items, this))
        {
            return;
        }

        var snapshot = items.ToList();
        CheckReentrancy();
        Items.Clear();
        foreach (var item in snapshot)
        {
            Items.Add(item);
        }

        RaiseReset();
    }

    public void AddRange(IEnumerable<T> items)
    {
        var snapshot = items.ToList();
        if (snapshot.Count == 0)
        {
            return;
        }

        CheckReentrancy();
        foreach (var item in snapshot)
        {
            Items.Add(item);
        }

        RaiseReset();
    }

    private void RaiseReset()
    {
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
