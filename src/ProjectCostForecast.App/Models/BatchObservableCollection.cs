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
        ArgumentNullException.ThrowIfNull(items);

        if (ReferenceEquals(items, this))
        {
            return;
        }

        if (Enumerable.TryGetNonEnumeratedCount(items, out var sourceCount) && sourceCount == 0)
        {
            if (Count == 0)
            {
                return;
            }

            CheckReentrancy();
            Items.Clear();
            RaiseReset();
            return;
        }

        if (items is IReadOnlyList<T> readOnlyList && HasSameItems(readOnlyList))
        {
            return;
        }

        if (items is IList<T> list && HasSameItems(list))
        {
            return;
        }

        var snapshot = GetStableItems(items);
        if (HasSameItems(snapshot))
        {
            return;
        }

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
        ArgumentNullException.ThrowIfNull(items);

        if (Enumerable.TryGetNonEnumeratedCount(items, out var sourceCount) && sourceCount == 0)
        {
            return;
        }

        var snapshot = ReferenceEquals(items, this)
            ? Items.ToList()
            : GetStableItems(items);
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

    private static IReadOnlyList<T> GetStableItems(IEnumerable<T> items)
    {
        return items switch
        {
            T[] array => array,
            List<T> list => list,
            _ => items.ToList()
        };
    }

    private bool HasSameItems(IReadOnlyList<T> source)
    {
        if (source.Count != Count)
        {
            return false;
        }

        for (var index = 0; index < source.Count; index++)
        {
            if (!IsSameItem(Items[index], source[index]))
            {
                return false;
            }
        }

        return true;
    }

    private bool HasSameItems(IList<T> source)
    {
        if (source.Count != Count)
        {
            return false;
        }

        for (var index = 0; index < source.Count; index++)
        {
            if (!IsSameItem(Items[index], source[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSameItem(T current, T replacement)
    {
        // Replacing an equal-but-distinct mutable model can carry meaningful subscriptions or
        // state. Only immutable strings and value types use value equality; other references
        // must be the exact same objects for the operation to be a safe no-op.
        return typeof(T).IsValueType || typeof(T) == typeof(string)
            ? EqualityComparer<T>.Default.Equals(current, replacement)
            : ReferenceEquals(current, replacement);
    }

    private void RaiseReset()
    {
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
