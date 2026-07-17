using System;
using System.Collections.Generic;
using System.Linq;

namespace CSharpMath.Structures;

/// <summary>
/// Represents a many-to-one relationship between <typeparamref name="TFirst"/> values and
/// <typeparamref name="TSecond"/> values, with lookup in both directions.
/// </summary>
public sealed class AliasBiMap<TFirst, TSecond> : InitializerCollection<TFirst, TSecond>
    where TFirst : notnull
    where TSecond : notnull
{
    private readonly Dictionary<TFirst, TSecond> _firstToSecond = new();
    private readonly Dictionary<TSecond, TFirst> _secondToFirst = new();

    public AliasBiMap(Action<TFirst, TSecond>? extraAddAction = null) : base(extraAddAction)
    {
    }

    protected override void OnAdded(TFirst first, TSecond second)
    {
        switch (_firstToSecond.ContainsKey(first), _secondToFirst.ContainsKey(second))
        {
            case (true, _):
                throw new InvalidOperationException($"Key already exists in {nameof(AliasBiMap<TFirst, TSecond>)}.");
            case (false, true):
                _firstToSecond.Add(first, second);
                break;
            case (false, false):
                _firstToSecond.Add(first, second);
                _secondToFirst.Add(second, first);
                break;
        }
    }

    public override IEnumerator<KeyValuePair<TFirst, TSecond>> GetEnumerator() =>
        _firstToSecond.GetEnumerator();

    public bool RemoveByFirst(TFirst first)
    {
        bool exists = _firstToSecond.TryGetValue(first, out var second);
        if (!exists)
            return false;

        _firstToSecond.Remove(first);
        if (!_secondToFirst[second!].Equals(first))
            return true;

        TFirst[] otherFirsts = _firstToSecond
            .Where(pair => EqualityComparer<TSecond>.Default.Equals(pair.Value, second))
            .Select(pair => pair.Key)
            .ToArray();
        if (otherFirsts.IsEmpty())
            _secondToFirst.Remove(second!);
        else
            _secondToFirst[second!] = otherFirsts[0];
        return true;
    }

    public bool RemoveBySecond(TSecond second)
    {
        if (!_secondToFirst.Remove(second))
            return false;

        var firsts = _firstToSecond
            .Where(pair => EqualityComparer<TSecond>.Default.Equals(pair.Value, second))
            .Select(pair => pair.Key)
            .ToArray();
        foreach (var first in firsts)
            _firstToSecond.Remove(first);
        return true;
    }

    public IReadOnlyDictionary<TFirst, TSecond> FirstToSecond => _firstToSecond;
    public IReadOnlyDictionary<TSecond, TFirst> SecondToFirst => _secondToFirst;
}
