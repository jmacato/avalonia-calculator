using System.Buffers;
using Graphing;

namespace JsMath.Port;

internal ref struct MarchingSquaresPooledPointBuffer(int initialCapacity)
{
    private GraphPoint[] _items = ArrayPool<GraphPoint>.Shared.Rent(initialCapacity);

    public int Count { get; private set; }

    public GraphPoint this[int index] => _items[index];
    public void Add(GraphPoint point)
    {
        if (Count == _items.Length)
        {
            GraphPoint[] larger = ArrayPool<GraphPoint>.Shared.Rent(checked(_items.Length * 2));
            _items.AsSpan(0, Count).CopyTo(larger);
            ArrayPool<GraphPoint>.Shared.Return(_items, clearArray: false);
            _items = larger;
        }

        _items[Count++] = point;
    }

    public ReadOnlySpan<GraphPoint> AsSpan()
    {
        return _items.AsSpan(0, Count);
    }

    public void Dispose()
    {
        GraphPoint[] items = _items;
        _items = Array.Empty<GraphPoint>();
        Count = 0;
        ArrayPool<GraphPoint>.Shared.Return(items, clearArray: false);
    }
}
