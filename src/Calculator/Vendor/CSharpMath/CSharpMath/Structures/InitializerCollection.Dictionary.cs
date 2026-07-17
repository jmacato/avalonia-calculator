using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CSharpMath.Structures;

/// <summary>
/// Funnels collection initializers to a strongly typed add operation.
/// </summary>
/// <example>
/// A derived collection can accept entries such as
/// <code>{ { 1, 2, 3, "1 to 3" }, { Enumerable.Range(7, 10), i => i.ToString() } }</code>.
/// </example>
public abstract class InitializerCollection<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
{
    private const string NotEnumerable = "This type supports collection initializer syntax but does not expose its entries from the base class.";
    private readonly Action<TKey, TValue>? _extraAddAction;

    protected InitializerCollection(Action<TKey, TValue>? extraAddAction = null) =>
        _extraAddAction = extraAddAction;

    protected abstract void OnAdded(TKey key, TValue value);

    public void Add(TKey key, TValue value)
    {
        _extraAddAction?.Invoke(key, value);
        OnAdded(key, value);
    }

    public void Add(TKey key1, TKey key2, TValue value)
    {
        Add(key1, value);
        Add(key2, value);
    }

    public void Add(TKey key1, TKey key2, TKey key3, TValue value)
    {
        Add(key1, value);
        Add(key2, value);
        Add(key3, value);
    }

    public void Add(TKey key1, TKey key2, TKey key3, TKey key4, TValue value)
    {
        Add(key1, value);
        Add(key2, value);
        Add(key3, value);
        Add(key4, value);
    }

    public void Add<TCollection>(TCollection keys, TValue value)
        where TCollection : IEnumerable<TKey>
    {
        foreach (var key in keys)
            Add(key, value);
    }

    public void Add<TCollection>(TCollection keys, Func<TKey, TValue> valueFactory)
        where TCollection : IEnumerable<TKey>
    {
        ArgumentNullException.ThrowIfNull(valueFactory);
        foreach (var key in keys)
            Add(key, valueFactory(key));
    }

    public virtual IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() =>
        throw new NotSupportedException(NotEnumerable);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
