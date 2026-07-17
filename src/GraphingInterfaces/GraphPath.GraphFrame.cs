using System.Collections.Immutable;
using Graphing.Renderer;

namespace Graphing;

public sealed record GraphPath
{
    public GraphPath(IEnumerable<GraphPoint> points, bool isClosed = false) : this(points.ToImmutableArray(), isClosed)
    {
    }

    public GraphPath(ImmutableArray<GraphPoint> points, bool isClosed = false)
    {
        Points = points.IsDefault ? ImmutableArray<GraphPoint>.Empty : points;
        IsClosed = isClosed;
    }

    public ImmutableArray<GraphPoint> Points { get; }
    public bool IsClosed { get; }
}
