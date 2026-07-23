using Graphing;

namespace JsMath.Port;

/// <summary>A bounded point quadtree used for loop and nearest-point queries.</summary>
public sealed class PointQuadtree<T>
{
    private readonly PointQuadtreeNode<T> _root;
    private readonly int _bucketSize;
    private readonly int _maximumDepth;

    public PointQuadtree(GraphRect bounds, int bucketSize = 16, int maximumDepth = 16)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bounds));
        }

        if (bucketSize <= 0 || maximumDepth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bucketSize));
        }

        _root = new PointQuadtreeNode<T>(bounds);
        _bucketSize = bucketSize;
        _maximumDepth = maximumDepth;
    }

    public void Insert(GraphPoint point, T value)
    {
        if (!point.IsFinite || !Contains(_root.Bounds, point))
        {
            return;
        }

        Insert(_root, new PointQuadtreeEntry<T>(point, value), 0);
    }

    public void Query(GraphRect area, ICollection<(GraphPoint Point, T Value)> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        Query(_root, area, results);
    }

    public bool Any(GraphRect area)
    {
        return Any(_root, area);
    }

    private void Insert(PointQuadtreeNode<T> node, PointQuadtreeEntry<T> entry, int depth)
    {
        if (node.Children is null && (node.Entries.Count < _bucketSize || depth >= _maximumDepth))
        {
            node.Entries.Add(entry);
            return;
        }

        if (node.Children is null)
        {
            Split(node);
            foreach (PointQuadtreeEntry<T> item in node.Entries)
            {
                Insert(FindChild(node, item.Point), item, depth + 1);
            }

            node.Entries.Clear();
        }

        Insert(FindChild(node, entry.Point), entry, depth + 1);
    }

    private static void Query(PointQuadtreeNode<T> node, GraphRect area, ICollection<(GraphPoint Point, T Value)> results)
    {
        if (!Intersects(node.Bounds, area))
        {
            return;
        }

        foreach (PointQuadtreeEntry<T> entry in node.Entries)
        {
            if (Contains(area, entry.Point))
            {
                results.Add((entry.Point, entry.Value));
            }
        }

        if (node.Children is null)
        {
            return;
        }

        foreach (PointQuadtreeNode<T> child in node.Children)
        {
            Query(child, area, results);
        }
    }

    private static bool Any(PointQuadtreeNode<T> node, GraphRect area)
    {
        if (!Intersects(node.Bounds, area))
        {
            return false;
        }

        foreach (PointQuadtreeEntry<T> entry in node.Entries)
        {
            if (Contains(area, entry.Point))
            {
                return true;
            }
        }

        if (node.Children is null)
        {
            return false;
        }

        foreach (PointQuadtreeNode<T> child in node.Children)
        {
            if (Any(child, area))
            {
                return true;
            }
        }

        return false;
    }

    private static void Split(PointQuadtreeNode<T> node)
    {
        double halfWidth = node.Bounds.Width * 0.5;
        double halfHeight = node.Bounds.Height * 0.5;
        node.Children =
        [
            new PointQuadtreeNode<T>(new GraphRect(node.Bounds.X, node.Bounds.Y, halfWidth, halfHeight)),
            new PointQuadtreeNode<T>(new GraphRect(node.Bounds.X + halfWidth, node.Bounds.Y, halfWidth, halfHeight)),
            new PointQuadtreeNode<T>(new GraphRect(node.Bounds.X, node.Bounds.Y + halfHeight, halfWidth, halfHeight)),
            new PointQuadtreeNode<T>(new GraphRect(node.Bounds.X + halfWidth, node.Bounds.Y + halfHeight, halfWidth, halfHeight))
        ];
    }

    private static PointQuadtreeNode<T> FindChild(PointQuadtreeNode<T> node, GraphPoint point)
    {
        PointQuadtreeNode<T>[] children = node.Children!;
        double centerX = node.Bounds.X + node.Bounds.Width * 0.5;
        double centerY = node.Bounds.Y + node.Bounds.Height * 0.5;
        int index = (point.X >= centerX ? 1 : 0) + (point.Y >= centerY ? 2 : 0);
        return children[index];
    }

    private static bool Contains(GraphRect bounds, GraphPoint point)
    {
        return point.X >= bounds.X && point.X <= bounds.Right &&
               point.Y >= bounds.Y && point.Y <= bounds.Bottom;
    }

    private static bool Intersects(GraphRect left, GraphRect right)
    {
        return left.X <= right.Right && left.Right >= right.X &&
               left.Y <= right.Bottom && left.Bottom >= right.Y;
    }
}
