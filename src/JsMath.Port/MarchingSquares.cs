using System.Buffers;
using System.Collections.Immutable;
using Graphing;

namespace JsMath.Port;
/// <summary>
/// Deterministic inequality meshing. Ambiguous saddle cells use the bilinear
/// asymptotic decider before clipping the selected triangles.
/// </summary>
[PortedFrom("JSXGraph", "src/math/implicitplot.js (component search and implicit boundary handling)", "d4f153470e249a698a46d6e8078c1d68f0cbe2cd", "MIT", "sha256:cef005ac495f6ddbd67dc27a8dfbe8662484d496487090f34d63b09ce0fb68ad")]
public static class MarchingSquares
{
    public static InequalityMesh Build(ImplicitEvaluator evaluator, InequalityPredicate predicate, SamplingViewport viewport, int columns = 96, int rows = 96, int maximumVertices = 65_536, CancellationToken cancellationToken = default) => BuildCore(evaluator, predicate, viewport, columns, rows, maximumVertices, includeFilledPolygons: true, cancellationToken);
    public static InequalityMesh BuildContours(ImplicitEvaluator evaluator, InequalityPredicate predicate, SamplingViewport viewport, int columns = 96, int rows = 96, int maximumVertices = 65_536, CancellationToken cancellationToken = default) => BuildCore(evaluator, predicate, viewport, columns, rows, maximumVertices, includeFilledPolygons: false, cancellationToken);
    private static InequalityMesh BuildCore(ImplicitEvaluator evaluator, InequalityPredicate predicate, SamplingViewport viewport, int columns, int rows, int maximumVertices, bool includeFilledPolygons, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        ArgumentNullException.ThrowIfNull(predicate);
        if (!viewport.IsValid || columns < 2 || rows < 2 || maximumVertices < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(columns));
        }

        int valueCount = checked((columns + 1) * (rows + 1));
        double[] values = ArrayPool<double>.Shared.Rent(valueCount);
        try
        {
            (int evaluations, bool missing) = EvaluateGrid(evaluator, viewport, columns, rows, values, cancellationToken);
            ImmutableArray<ImmutableArray<GraphPoint>>.Builder? polygons = includeFilledPolygons ? ImmutableArray.CreateBuilder<ImmutableArray<GraphPoint>>() : null;
            MarchingSquaresBoundarySegment[] boundarySegments = ArrayPool<MarchingSquaresBoundarySegment>.Shared.Rent(Math.Min(columns * rows, 512));
            int boundarySegmentCount = 0;
            int polygonVertices = 0;
            int boundaryVertices = 0;
            bool budgetExceeded = false;
            try
            {
                for (int row = 0; row < rows; row++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    for (int column = 0; column < columns; column++)
                    {
                        GraphPoint p00 = Point(viewport, column, row, columns, rows);
                        GraphPoint p10 = Point(viewport, column + 1, row, columns, rows);
                        GraphPoint p11 = Point(viewport, column + 1, row + 1, columns, rows);
                        GraphPoint p01 = Point(viewport, column, row + 1, columns, rows);
                        double f00 = Value(values, columns, column, row);
                        double f10 = Value(values, columns, column + 1, row);
                        double f11 = Value(values, columns, column + 1, row + 1);
                        double f01 = Value(values, columns, column, row + 1);
                        if (!double.IsFinite(f00) || !double.IsFinite(f10) || !double.IsFinite(f11) || !double.IsFinite(f01))
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
                            if (polygons is not null)
                            {
                                if (polygonVertices + 4 <= maximumVertices)
                                {
                                    polygons.Add([p00, p10, p11, p01]);
                                    polygonVertices += 4;
                                }
                                else
                                {
                                    budgetExceeded = true;
                                }
                            }

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
                            AddTriangle(p00, f00, p10, f10, p11, f11, predicate, polygons, ref boundarySegments, ref boundarySegmentCount, ref polygonVertices, ref boundaryVertices, ref budgetExceeded, maximumVertices);
                            AddTriangle(p00, f00, p11, f11, p01, f01, predicate, polygons, ref boundarySegments, ref boundarySegmentCount, ref polygonVertices, ref boundaryVertices, ref budgetExceeded, maximumVertices);
                        }
                        else
                        {
                            AddTriangle(p00, f00, p10, f10, p01, f01, predicate, polygons, ref boundarySegments, ref boundarySegmentCount, ref polygonVertices, ref boundaryVertices, ref budgetExceeded, maximumVertices);
                            AddTriangle(p10, f10, p11, f11, p01, f01, predicate, polygons, ref boundarySegments, ref boundarySegmentCount, ref polygonVertices, ref boundaryVertices, ref budgetExceeded, maximumVertices);
                        }
                    }
                }

                ImmutableArray<ImmutableArray<GraphPoint>> contours = Stitch(boundarySegments.AsSpan(0, boundarySegmentCount), viewport);
                return new InequalityMesh(polygons?.ToImmutable() ?? ImmutableArray<ImmutableArray<GraphPoint>>.Empty, contours, evaluations, includeFilledPolygons ? Math.Max(polygonVertices, boundaryVertices) : boundaryVertices, missing || budgetExceeded);
            }
            finally
            {
                ArrayPool<MarchingSquaresBoundarySegment>.Shared.Return(boundarySegments, clearArray: false);
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(values, clearArray: false);
        }
    }

    private static (int Evaluations, bool HasMissingData) EvaluateGrid(ImplicitEvaluator evaluator, SamplingViewport viewport, int columns, int rows, double[] values, CancellationToken cancellationToken)
    {
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

        return (evaluations, missing);
    }

    private static void AddTriangle(GraphPoint a, double fa, GraphPoint b, double fb, GraphPoint c, double fc, InequalityPredicate predicate, ImmutableArray<ImmutableArray<GraphPoint>>.Builder? polygons, ref MarchingSquaresBoundarySegment[] boundaries, ref int boundaryCount, ref int polygonVertexCount, ref int boundaryVertexCount, ref bool budgetExceeded, int maximumVertices)
    {
        Span<GraphPoint> points = stackalloc GraphPoint[3]
        {
            a,
            b,
            c
        };
        Span<double> values = stackalloc double[3]
        {
            fa,
            fb,
            fc
        };
        Span<bool> inside = stackalloc bool[3]
        {
            predicate(fa),
            predicate(fb),
            predicate(fc)
        };
        ImmutableArray<GraphPoint>.Builder? clipped = polygons is not null ? ImmutableArray.CreateBuilder<GraphPoint>(5) : null;
        Span<GraphPoint> crossings = stackalloc GraphPoint[2];
        int crossingCount = 0;
        for (int index = 0; index < 3; index++)
        {
            int next = (index + 1) % 3;
            if (inside[index])
            {
                clipped?.Add(points[index]);
            }

            if (inside[index] != inside[next])
            {
                GraphPoint crossing = Interpolate(points[index], values[index], points[next], values[next]);
                clipped?.Add(crossing);
                if (crossingCount < 2)
                {
                    crossings[crossingCount++] = crossing;
                }
            }
        }

        if (clipped is { Count: >= 3 })
        {
            if (polygonVertexCount + clipped.Count <= maximumVertices)
            {
                polygons!.Add(clipped.ToImmutable());
                polygonVertexCount += clipped.Count;
            }
            else
            {
                budgetExceeded = true;
            }
        }

        if (crossingCount == 2)
        {
            if (boundaryVertexCount + 2 <= maximumVertices)
            {
                AddBoundary(ref boundaries, ref boundaryCount, new MarchingSquaresBoundarySegment(crossings[0], crossings[1]));
                boundaryVertexCount += 2;
            }
            else
            {
                budgetExceeded = true;
            }
        }
    }

    private static void AddBoundary(ref MarchingSquaresBoundarySegment[] boundaries, ref int count, MarchingSquaresBoundarySegment boundary)
    {
        if (count == boundaries.Length)
        {
            MarchingSquaresBoundarySegment[] larger = ArrayPool<MarchingSquaresBoundarySegment>.Shared.Rent(checked(boundaries.Length * 2));
            boundaries.AsSpan(0, count).CopyTo(larger);
            ArrayPool<MarchingSquaresBoundarySegment>.Shared.Return(boundaries, clearArray: false);
            boundaries = larger;
        }

        boundaries[count++] = boundary;
    }

    private static ImmutableArray<ImmutableArray<GraphPoint>> Stitch(ReadOnlySpan<MarchingSquaresBoundarySegment> segments, SamplingViewport viewport)
    {
        if (segments.Length == 0)
        {
            return ImmutableArray<ImmutableArray<GraphPoint>>.Empty;
        }

        var result = ImmutableArray.CreateBuilder<ImmutableArray<GraphPoint>>();
        double tolerance = Math.Min(viewport.XRange.Length / viewport.Width, viewport.YRange.Length / viewport.Height) * 0.25;
        double toleranceSquared = tolerance * tolerance;
        int nodeCount = checked(segments.Length * 2);
        int[] next = ArrayPool<int>.Shared.Rent(nodeCount);
        bool[] visited = ArrayPool<bool>.Shared.Rent(segments.Length);
        int endpointCapacity = EndpointCapacity(nodeCount);
        MarchingSquaresEndpointKey[] endpointKeys = ArrayPool<MarchingSquaresEndpointKey>.Shared.Rent(endpointCapacity);
        int[] endpointHeads = ArrayPool<int>.Shared.Rent(endpointCapacity);
        int endpointMask = endpointCapacity - 1;
        Array.Fill(next, -1, 0, nodeCount);
        Array.Clear(visited, 0, segments.Length);
        Array.Fill(endpointHeads, -1, 0, endpointCapacity);
        try
        {
            for (int node = 0; node < nodeCount; node++)
            {
                MarchingSquaresBoundarySegment segment = segments[node >> 1];
                (GraphPoint a, GraphPoint b) = (segment.A, segment.B);
                GraphPoint point = (node & 1) == 0 ? a : b;
                MarchingSquaresEndpointKey key = EndpointKeyFor(point, viewport, tolerance);
                int slot = FindEndpointSlot(endpointKeys, endpointHeads, endpointMask, key);
                int previous = endpointHeads[slot];
                if (previous >= 0)
                {
                    next[node] = previous;
                }
                else
                {
                    endpointKeys[slot] = key;
                }

                endpointHeads[slot] = node;
            }

            // Preserve the native port's deterministic contour orientation: the
            // previous implementation consumed the last remaining segment first.
            for (int seed = segments.Length - 1; seed >= 0; seed--)
            {
                if (visited[seed])
                {
                    continue;
                }

                visited[seed] = true;
                MarchingSquaresBoundarySegment segment = segments[seed];
                (GraphPoint a, GraphPoint b) = (segment.A, segment.B);
                var contour = new MarchingSquaresPooledPointBuffer(32);
                var prefix = new MarchingSquaresPooledPointBuffer(8);
                try
                {
                    contour.Add(a);
                    contour.Add(b);
                    ExtendContour(b, ref contour, segments, endpointKeys, endpointHeads, endpointMask, next, visited, viewport, tolerance, toleranceSquared);
                    ExtendContour(a, ref prefix, segments, endpointKeys, endpointHeads, endpointMask, next, visited, viewport, tolerance, toleranceSquared);
                    var joined = ImmutableArray.CreateBuilder<GraphPoint>(prefix.Count + contour.Count);
                    for (int index = prefix.Count - 1; index >= 0; index--)
                    {
                        joined.Add(prefix[index]);
                    }

                    joined.AddRange(contour.AsSpan());
                    result.Add(joined.MoveToImmutable());
                }
                finally
                {
                    prefix.Dispose();
                    contour.Dispose();
                }
            }

            return result.ToImmutable();
        }
        finally
        {
            ArrayPool<int>.Shared.Return(next, clearArray: false);
            ArrayPool<bool>.Shared.Return(visited, clearArray: false);
            ArrayPool<MarchingSquaresEndpointKey>.Shared.Return(endpointKeys, clearArray: false);
            ArrayPool<int>.Shared.Return(endpointHeads, clearArray: false);
        }
    }

    private static void ExtendContour(GraphPoint endpoint, ref MarchingSquaresPooledPointBuffer output, ReadOnlySpan<MarchingSquaresBoundarySegment> segments, MarchingSquaresEndpointKey[] endpointKeys, int[] endpointHeads, int endpointMask, int[] next, bool[] visited, SamplingViewport viewport, double tolerance, double toleranceSquared)
    {
        while (TryTakeConnectedSegment(endpoint, segments, endpointKeys, endpointHeads, endpointMask, next, visited, viewport, tolerance, toleranceSquared, out int segmentIndex, out bool matchedFirst))
        {
            visited[segmentIndex] = true;
            MarchingSquaresBoundarySegment segment = segments[segmentIndex];
            (GraphPoint a, GraphPoint b) = (segment.A, segment.B);
            endpoint = matchedFirst ? b : a;
            output.Add(endpoint);
        }
    }

    private static bool TryTakeConnectedSegment(GraphPoint endpoint, ReadOnlySpan<MarchingSquaresBoundarySegment> segments, MarchingSquaresEndpointKey[] endpointKeys, int[] endpointHeads, int endpointMask, int[] next, bool[] visited, SamplingViewport viewport, double tolerance, double toleranceSquared, out int segmentIndex, out bool matchedFirst)
    {
        MarchingSquaresEndpointKey center = EndpointKeyFor(endpoint, viewport, tolerance);
        int bestSegmentIndex = -1;
        bool bestMatchedFirst = false;
        for (long yOffset = -1; yOffset <= 1; yOffset++)
        {
            for (long xOffset = -1; xOffset <= 1; xOffset++)
            {
                var key = new MarchingSquaresEndpointKey(center.X + xOffset, center.Y + yOffset);
                int slot = FindEndpointSlot(endpointKeys, endpointHeads, endpointMask, key);
                int node = endpointHeads[slot];
                if (node < 0)
                {
                    continue;
                }

                for (; node >= 0; node = next[node])
                {
                    int candidateIndex = node >> 1;
                    if (visited[candidateIndex])
                    {
                        continue;
                    }

                    bool candidateIsFirst = (node & 1) == 0;
                    MarchingSquaresBoundarySegment segment = segments[candidateIndex];
                    (GraphPoint a, GraphPoint b) = (segment.A, segment.B);
                    GraphPoint candidate = candidateIsFirst ? a : b;
                    if (DistanceSquared(endpoint, candidate) > toleranceSquared)
                    {
                        continue;
                    }

                    // The original stitcher scanned the remaining segment list
                    // backwards and tested A before B. Search every neighboring
                    // spatial bucket before choosing so the indexed version keeps
                    // that exact deterministic priority at ambiguous junctions.
                    if (candidateIndex > bestSegmentIndex || (candidateIndex == bestSegmentIndex && candidateIsFirst && !bestMatchedFirst))
                    {
                        bestSegmentIndex = candidateIndex;
                        bestMatchedFirst = candidateIsFirst;
                    }
                }
            }
        }

        segmentIndex = bestSegmentIndex;
        matchedFirst = bestMatchedFirst;
        return bestSegmentIndex >= 0;
    }

    private static MarchingSquaresEndpointKey EndpointKeyFor(GraphPoint point, SamplingViewport viewport, double tolerance) => new((long)Math.Floor((point.X - viewport.XRange.Minimum) / tolerance), (long)Math.Floor((point.Y - viewport.YRange.Minimum) / tolerance));
    private static int EndpointCapacity(int nodeCount)
    {
        int required = checked(nodeCount * 2);
        int capacity = 4;
        while (capacity < required)
        {
            capacity = checked(capacity * 2);
        }

        return capacity;
    }

    private static int FindEndpointSlot(MarchingSquaresEndpointKey[] keys, int[] heads, int mask, MarchingSquaresEndpointKey key)
    {
        int slot = HashCode.Combine(key.X, key.Y) & mask;
        while (heads[slot] >= 0 && keys[slot] != key)
        {
            slot = (slot + 1) & mask;
        }

        return slot;
    }

    private static GraphPoint Point(SamplingViewport viewport, int column, int row, int columns, int rows) => new(viewport.XRange.Minimum + (viewport.XRange.Length * column / columns), viewport.YRange.Minimum + (viewport.YRange.Length * row / rows));
    private static double Value(double[] values, int columns, int column, int row) => values[(row * (columns + 1)) + column];
    private static GraphPoint Interpolate(GraphPoint left, double leftValue, GraphPoint right, double rightValue)
    {
        double denominator = leftValue - rightValue;
        double amount = denominator == 0 ? 0.5 : Math.Clamp(leftValue / denominator, 0, 1);
        return new GraphPoint(left.X + ((right.X - left.X) * amount), left.Y + ((right.Y - left.Y) * amount));
    }

    private static double DistanceSquared(GraphPoint left, GraphPoint right)
    {
        double x = left.X - right.X;
        double y = left.Y - right.Y;
        return (x * x) + (y * y);
    }
}
