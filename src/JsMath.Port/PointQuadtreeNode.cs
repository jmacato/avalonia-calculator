using Graphing;

namespace JsMath.Port;

internal sealed class PointQuadtreeNode<T>(GraphRect bounds)
{
    public GraphRect Bounds { get; } = bounds;

    public List<PointQuadtreeEntry<T>> Entries { get; } = [];

    public PointQuadtreeNode<T>[]? Children { get; set; }
}
