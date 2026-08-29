using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ProjectCostForecast.App.Models;

/// <summary>
/// Owns the lifetime of item and collection subscriptions for a mutable
/// observable state collection. Replacing the collection or its contents is
/// idempotent: removed items are detached and each current reference is
/// attached at most once.
/// </summary>
public sealed class CollectionSubscriptionTracker<T> : IDisposable
    where T : class, INotifyPropertyChanged
{
    private readonly PropertyChangedEventHandler _itemPropertyChanged;
    private readonly Action<NotifyCollectionChangedEventArgs> _collectionChanged;
    private readonly Action<T, PropertyChangedEventArgs> _propertyChanged;
    private readonly HashSet<T> _trackedItems = new(ReferenceEqualityComparer.Instance);
    private INotifyCollectionChanged? _collection;
    private bool _disposed;

    public CollectionSubscriptionTracker(
        Action<T, PropertyChangedEventArgs> propertyChanged,
        Action<NotifyCollectionChangedEventArgs> collectionChanged)
    {
        _propertyChanged = propertyChanged ?? throw new ArgumentNullException(nameof(propertyChanged));
        _collectionChanged = collectionChanged ?? throw new ArgumentNullException(nameof(collectionChanged));
        _itemPropertyChanged = ItemPropertyChanged;
    }

    public void SetCollection(INotifyCollectionChanged collection, IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(items);
        ThrowIfDisposed();

        if (!ReferenceEquals(_collection, collection))
        {
            DetachCollection();
            _collection = collection;
            _collection.CollectionChanged += CollectionChanged;
        }

        Synchronize(items);
    }

    public void Synchronize(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        ThrowIfDisposed();

        var currentItems = new HashSet<T>(items, ReferenceEqualityComparer.Instance);
        foreach (var removed in _trackedItems.Where(item => !currentItems.Contains(item)).ToList())
        {
            removed.PropertyChanged -= _itemPropertyChanged;
            _trackedItems.Remove(removed);
        }

        foreach (var added in currentItems.Where(item => _trackedItems.Add(item)))
        {
            added.PropertyChanged += _itemPropertyChanged;
        }
    }

    public void Detach()
    {
        if (_disposed)
        {
            return;
        }

        DetachCollection();
        _disposed = true;
    }

    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_disposed || _collection is null)
        {
            return;
        }

        if (sender is IEnumerable items)
        {
            Synchronize(items.Cast<T>());
        }

        _collectionChanged(e);
    }

    private void ItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_disposed && sender is T item && _trackedItems.Contains(item))
        {
            _propertyChanged(item, e);
        }
    }

    private void DetachCollection()
    {
        if (_collection is not null)
        {
            _collection.CollectionChanged -= CollectionChanged;
            _collection = null;
        }

        foreach (var item in _trackedItems)
        {
            item.PropertyChanged -= _itemPropertyChanged;
        }

        _trackedItems.Clear();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose() => Detach();
}
