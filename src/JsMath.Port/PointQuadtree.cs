using Graphing;

namespace JsMath.Port;

/// <summary>A bounded point quadtree used for loop and nearest-point queries.</summary>
[PortedFrom(
    "JSXGraph",
    "src/math/qdt.js",
    "d4f153470e249a698a46d6e8078c1d68f0cbe2cd",
    "MIT",
    "sha256:a3ffb479a7c2ceb332b8332396d75cc40f3b30fe62864a8a29fb253b1223a454")]
public sealed class PointQuadtree<T>
{
    private readonly Node _root;
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

        _root = new Node(bounds);
        _bucketSize = bucketSize;
        _maximumDepth = maximumDepth;
    }

    public void Insert(GraphPoint point, T value)
    {
        if (!point.IsFinite || !Contains(_root.Bounds, point))
        {
            return;
        }

        Insert(_root, new Entry(point, value), 0);
    }

    public void Query(GraphRect area, List<(GraphPoint Point, T Value)> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        Query(_root, area, results);
    }

    public bool Any(GraphRect area) => Any(_root, area);

    private void Insert(Node node, Entry entry, int depth)
    {
        if (node.Children is null && (node.Entries.Count < _bucketSize || depth >= _maximumDepth))
        {
            node.Entries.Add(entry);
            return;
        }

        if (node.Children is null)
        {
            Split(node);
            foreach (Entry item in node.Entries)
            {
                Insert(FindChild(node, item.Point), item, depth + 1);
            }

            node.Entries.Clear();
        }

        Insert(FindChild(node, entry.Point), entry, depth + 1);
    }

    private static void Query(Node node, GraphRect area, List<(GraphPoint Point, T Value)> results)
    {
        if (!Intersects(node.Bounds, area))
        {
            return;
        }

        foreach (Entry entry in node.Entries)
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

        foreach (Node child in node.Children)
        {
            Query(child, area, results);
        }
    }

    private static bool Any(Node node, GraphRect area)
    {
        if (!Intersects(node.Bounds, area))
        {
            return false;
        }

        foreach (Entry entry in node.Entries)
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

        foreach (Node child in node.Children)
        {
            if (Any(child, area))
            {
                return true;
            }
        }

        return false;
    }

    private static void Split(Node node)
    {
        double halfWidth = node.Bounds.Width * 0.5;
        double halfHeight = node.Bounds.Height * 0.5;
        node.Children =
        [
            new Node(new GraphRect(node.Bounds.X, node.Bounds.Y, halfWidth, halfHeight)),
            new Node(new GraphRect(node.Bounds.X + halfWidth, node.Bounds.Y, halfWidth, halfHeight)),
            new Node(new GraphRect(node.Bounds.X, node.Bounds.Y + halfHeight, halfWidth, halfHeight)),
            new Node(new GraphRect(node.Bounds.X + halfWidth, node.Bounds.Y + halfHeight, halfWidth, halfHeight))
        ];
    }

    private static Node FindChild(Node node, GraphPoint point)
    {
        Node[] children = node.Children!;
        double centerX = node.Bounds.X + (node.Bounds.Width * 0.5);
        double centerY = node.Bounds.Y + (node.Bounds.Height * 0.5);
        int index = (point.X >= centerX ? 1 : 0) + (point.Y >= centerY ? 2 : 0);
        return children[index];
    }

    private static bool Contains(GraphRect bounds, GraphPoint point) =>
        point.X >= bounds.X && point.X <= bounds.Right &&
        point.Y >= bounds.Y && point.Y <= bounds.Bottom;

    private static bool Intersects(GraphRect left, GraphRect right) =>
        left.X <= right.Right && left.Right >= right.X &&
        left.Y <= right.Bottom && left.Bottom >= right.Y;

    private readonly record struct Entry(GraphPoint Point, T Value);

    private sealed class Node(GraphRect bounds)
    {
        public GraphRect Bounds { get; } = bounds;

        public List<Entry> Entries { get; } = [];

        public Node[]? Children { get; set; }
    }
}
