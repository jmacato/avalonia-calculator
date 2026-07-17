// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Direct managed port of microsoft-ui-xaml's SwipeItems API and collection
// validation from SwipeControl.idl and SwipeItems.cpp at
// commit 3cae15f071f1ab8565f9a7592dbf27f04bafe651.
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;

namespace CalculatorApp.Controls;

public sealed class SwipeItems : AvaloniaObject, IList<SwipeItem>, IReadOnlyList<SwipeItem>, INotifyCollectionChanged
{
    public static readonly StyledProperty<SwipeMode> ModeProperty = AvaloniaProperty.Register<SwipeItems, SwipeMode>(nameof(Mode), SwipeMode.Reveal);
    private readonly ObservableCollection<SwipeItem> _items = [];
    public SwipeItems()
    {
        _items.CollectionChanged += (_, e) => CollectionChanged?.Invoke(this, e);
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public SwipeMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

    public SwipeItem this[int index] { get => _items[index]; set => _items[index] = value; }

    public int Count => _items.Count;
    public bool IsReadOnly => false;

    public void Add(SwipeItem item)
    {
        ThrowIfExecuteAlreadyHasItem();
        _items.Add(item);
    }

    public void Clear() => _items.Clear();
    public bool Contains(SwipeItem item) => _items.Contains(item);
    public void CopyTo(SwipeItem[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    public IEnumerator<SwipeItem> GetEnumerator() => _items.GetEnumerator();
    public int IndexOf(SwipeItem item) => _items.IndexOf(item);
    public void Insert(int index, SwipeItem item)
    {
        ThrowIfExecuteAlreadyHasItem();
        _items.Insert(index, item);
    }

    public bool Remove(SwipeItem item) => _items.Remove(item);
    public void RemoveAt(int index) => _items.RemoveAt(index);
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        System.ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty && change.NewValue is SwipeMode.Execute && Count > 1)
        {
            throw new ArgumentException("Execute items should only have one item.");
        }
    }

    private void ThrowIfExecuteAlreadyHasItem()
    {
        if (Mode == SwipeMode.Execute && Count > 0)
        {
            throw new ArgumentException("Execute items should only have one item.");
        }
    }
}
