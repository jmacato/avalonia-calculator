using System.Collections.Immutable;
using Graphing;

namespace JsMath.Port;

public delegate bool InequalityPredicate(double value);

public sealed record InequalityMesh(
    ImmutableArray<ImmutableArray<GraphPoint>> FilledPolygons,
    ImmutableArray<ImmutableArray<GraphPoint>> Contours,
    int EvaluationCount,
    int VertexCount,
    bool HasMissingData);

/// <summary>
/// Deterministic inequality meshing. Ambiguous saddle cells use the bilinear
/// asymptotic decider before clipping the selected triangles.
/// </summary>
[PortedFrom(
    "JSXGraph",
    "src/math/implicitplot.js (component search and implicit boundary handling)",
    "d4f153470e249a698a46d6e8078c1d68f0cbe2cd",
    "MIT",
    "sha256:cef005ac495f6ddbd67dc27a8dfbe8662484d496487090f34d63b09ce0fb68ad")]
public static class MarchingSquares
{
    public static InequalityMesh Build(
        ImplicitEvaluator evaluator,
        InequalityPredicate predicate,
        SamplingViewport viewport,
        int columns = 96,
        int rows = 96,
        int maximumVertices = 65_536,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        ArgumentNullException.ThrowIfNull(predicate);
        if (!viewport.IsValid || columns < 2 || rows < 2 || maximumVertices < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(columns));
        }

        var values = new double[(columns + 1) * (rows + 1)];
        int evaluations = 0;
        bool missing = false;
        for (int row = 0; row <= rows; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            double y = viewport.YRange.Minimum + (viewport.YRange.Length * row / rows);
            for (int column = 0; column <= columns; column++)
            {
                double x = viewport.XRange.Minimum + (viewport.XRange.Length * column / columns);
                double value = evaluator(x, y);
                values[(row * (columns + 1)) + column] = value;
                evaluations++;
                missing |= !double.IsFinite(value);
            }
        }

        var polygons = ImmutableArray.CreateBuilder<ImmutableArray<GraphPoint>>();
        var boundarySegments = new List<(GraphPoint A, GraphPoint B)>();
        int vertices = 0;
        for (int row = 0; row < rows && vertices < maximumVertices; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int column = 0; column < columns && vertices < maximumVertices; column++)
            {
                GraphPoint p00 = Point(viewport, column, row, columns, rows);
                GraphPoint p10 = Point(viewport, column + 1, row, columns, rows);
                GraphPoint p11 = Point(viewport, column + 1, row + 1, columns, rows);
                GraphPoint p01 = Point(viewport, column, row + 1, columns, rows);
                double f00 = Value(values, columns, column, row);
                double f10 = Value(values, columns, column + 1, row);
                double f11 = Value(values, columns, column + 1, row + 1);
                double f01 = Value(values, columns, column, row + 1);
                if (!double.IsFinite(f00) || !double.IsFinite(f10) ||
                    !double.IsFinite(f11) || !double.IsFinite(f01))
                {
                    continue;
                }

                bool b00 = predicate(f00);
                bool b10 = predicate(f10);
                bool b11 = predicate(f11);
                bool b01 = predicate(f01);
                int mask = (b00 ? 1 : 0) | (b10 ? 2 : 0) | (b11 ? 4 : 0) | (b01 ? 8 : 0);
                if (mask == 0)
                {
                    continue;
                }

                if (mask == 15)
                {
                    ImmutableArray<GraphPoint> cell = [p00, p10, p11, p01];
                    polygons.Add(cell);
                    vertices += 4;
                    continue;
                }

                // For masks 5 and 10, Q determines which corners share the
                // interior of the bilinear interpolant. The same split is used
                // for fill clipping and contour construction.
                double q = (f00 * f11) - (f10 * f01);
                bool diagonal00To11 = mask switch
                {
                    5 => q >= 0,
                    10 => q < 0,
                    _ => Math.Abs(f00) + Math.Abs(f11) <= Math.Abs(f10) + Math.Abs(f01)
                };

                if (diagonal00To11)
                {
                    AddTriangle(p00, f00, p10, f10, p11, f11, predicate, polygons, boundarySegments, ref vertices, maximumVertices);
                    AddTriangle(p00, f00, p11, f11, p01, f01, predicate, polygons, boundarySegments, ref vertices, maximumVertices);
                }
                else
                {
                    AddTriangle(p00, f00, p10, f10, p01, f01, predicate, polygons, boundarySegments, ref vertices, maximumVertices);
                    AddTriangle(p10, f10, p11, f11, p01, f01, predicate, polygons, boundarySegments, ref vertices, maximumVertices);
                }
            }
        }

        ImmutableArray<ImmutableArray<GraphPoint>> contours = Stitch(boundarySegments, viewport);
        return new InequalityMesh(
            polygons.ToImmutable(),
            contours,
            evaluations,
            vertices,
            missing || vertices >= maximumVertices);
    }

    private static void AddTriangle(
        GraphPoint a,
        double fa,
        GraphPoint b,
        double fb,
        GraphPoint c,
        double fc,
        InequalityPredicate predicate,
        ImmutableArray<ImmutableArray<GraphPoint>>.Builder polygons,
        List<(GraphPoint A, GraphPoint B)> boundaries,
        ref int vertexCount,
        int maximumVertices)
    {
        Span<GraphPoint> points = stackalloc GraphPoint[3] { a, b, c };
        Span<double> values = stackalloc double[3] { fa, fb, fc };
        Span<bool> inside = stackalloc bool[3]
        {
            predicate(fa), predicate(fb), predicate(fc)
        };
        var clipped = ImmutableArray.CreateBuilder<GraphPoint>(5);
        Span<GraphPoint> crossings = stackalloc GraphPoint[2];
        int crossingCount = 0;

        for (int index = 0; index < 3; index++)
        {
            int next = (index + 1) % 3;
            if (inside[index])
            {
                clipped.Add(points[index]);
            }

            if (inside[index] != inside[next])
            {
                GraphPoint crossing = Interpolate(points[index], values[index], points[next], values[next]);
                clipped.Add(crossing);
                if (crossingCount < 2)
                {
                    crossings[crossingCount++] = crossing;
                }
            }
        }

        if (clipped.Count >= 3 && vertexCount + clipped.Count <= maximumVertices)
        {
            polygons.Add(clipped.ToImmutable());
            vertexCount += clipped.Count;
        }

        if (crossingCount == 2)
        {
            boundaries.Add((crossings[0], crossings[1]));
        }
    }

    private static ImmutableArray<ImmutableArray<GraphPoint>> Stitch(
        List<(GraphPoint A, GraphPoint B)> segments,
        SamplingViewport viewport)
    {
        var result = ImmutableArray.CreateBuilder<ImmutableArray<GraphPoint>>();
        double tolerance = Math.Min(viewport.XRange.Length / viewport.Width, viewport.YRange.Length / viewport.Height) * 0.25;
        double toleranceSquared = tolerance * tolerance;
        while (segments.Count > 0)
        {
            (GraphPoint a, GraphPoint b) = segments[^1];
            segments.RemoveAt(segments.Count - 1);
            var contour = new LinkedList<GraphPoint>();
            contour.AddLast(a);
            contour.AddLast(b);
            while (true)
            {
                if (TryExtend(contour, segments, extendTail: true, toleranceSquared) ||
                    TryExtend(contour, segments, extendTail: false, toleranceSquared))
                {
                    continue;
                }

                break;
            }

            result.Add(ImmutableArray.CreateRange(contour));
        }

        return result.ToImmutable();
    }

    private static bool TryExtend(
        LinkedList<GraphPoint> contour,
        List<(GraphPoint A, GraphPoint B)> segments,
        bool extendTail,
        double toleranceSquared)
    {
        GraphPoint endpoint = extendTail ? contour.Last!.Value : contour.First!.Value;
        for (int index = segments.Count - 1; index >= 0; index--)
        {
            (GraphPoint left, GraphPoint right) = segments[index];
            GraphPoint extension;
            if (DistanceSquared(endpoint, left) <= toleranceSquared)
            {
                extension = right;
            }
            else if (DistanceSquared(endpoint, right) <= toleranceSquared)
            {
                extension = left;
            }
            else
            {
                continue;
            }

            if (extendTail)
            {
                contour.AddLast(extension);
            }
            else
            {
                contour.AddFirst(extension);
            }

            segments.RemoveAt(index);
            return true;
        }

        return false;
    }

    private static GraphPoint Point(
        SamplingViewport viewport,
        int column,
        int row,
        int columns,
        int rows) => new(
        viewport.XRange.Minimum + (viewport.XRange.Length * column / columns),
        viewport.YRange.Minimum + (viewport.YRange.Length * row / rows));

    private static double Value(double[] values, int columns, int column, int row) =>
        values[(row * (columns + 1)) + column];

    private static GraphPoint Interpolate(GraphPoint left, double leftValue, GraphPoint right, double rightValue)
    {
        double denominator = leftValue - rightValue;
        double amount = denominator == 0 ? 0.5 : Math.Clamp(leftValue / denominator, 0, 1);
        return new GraphPoint(
            left.X + ((right.X - left.X) * amount),
            left.Y + ((right.Y - left.Y) * amount));
    }

    private static double DistanceSquared(GraphPoint left, GraphPoint right)
    {
        double x = left.X - right.X;
        double y = left.Y - right.Y;
        return (x * x) + (y * y);
    }
}
