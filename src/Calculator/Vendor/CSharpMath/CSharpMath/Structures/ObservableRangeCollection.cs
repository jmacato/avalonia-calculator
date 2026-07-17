using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace CSharpMath.Structures;
// No need to scream at helper "disposables"
public class ObservableRangeCollection<T> : ObservableCollection<T>
{
    public ObservableRangeCollection() { }
    public ObservableRangeCollection(IEnumerable<T> collection) : base(collection) { }
    private bool _isBatching;

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        // intercept this when it gets called inside the AddRange method.
        if (!_isBatching) base.OnCollectionChanged(e);
    }

    public void AddRange(IEnumerable<T> items)
    {
        var enumerable = items as T[] ?? items.ToArray();
        RunBatch(() =>
        {
            foreach (var item in enumerable) Add(item);
        });
        base.OnCollectionChanged(
            new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Add,
                items is IList l ? l : enumerable.ToList()
            )
        );
    }
    public void RemoveRange(IEnumerable<T> items)
    {
        var enumerable = items as T[] ?? items.ToArray();
        RunBatch(() =>
        {
            foreach (var item in enumerable) Remove(item);
        });
        base.OnCollectionChanged(
            new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Remove,
                items is IList l ? l : enumerable.ToList()
            )
        );
    }
    private void RunBatch(Action operation)
    {
        bool wasBatching = _isBatching;
        _isBatching = true;
        try
        {
            operation();
        }
        finally
        {
            _isBatching = wasBatching;
        }
    }
}
