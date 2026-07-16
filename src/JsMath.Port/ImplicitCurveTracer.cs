using System.Collections.Immutable;
using Graphing;

namespace JsMath.Port;

/// <summary>
/// Euler predictor/Newton corrector tracer for regular implicit curves.
/// Critical points are reported as missing data instead of being bridged.
/// </summary>
[PortedFrom(
    "JSXGraph",
    "src/math/implicitplot.js (searchLine, traceComponent, tracing)",
    "d4f153470e249a698a46d6e8078c1d68f0cbe2cd",
    "MIT",
    "sha256:cef005ac495f6ddbd67dc27a8dfbe8662484d496487090f34d63b09ce0fb68ad")]
public static class ImplicitCurveTracer
{
    public static SampledCurve Trace(
        ImplicitEvaluator function,
        SamplingViewport viewport,
        ImplicitTraceOptions options = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(function);
        if (!viewport.IsValid)
        {
            throw new ArgumentException("The sampling viewport must be finite and non-empty.", nameof(viewport));
        }

        options = Normalize(options);
        var components = new List<SampledComponent>();
        var seedIndex = new PointQuadtree<int>(new GraphRect(0, 0, viewport.Width, viewport.Height));
        int evaluations = 0;
        int vertices = 0;
        bool missing = false;

        foreach (GraphPoint seed in FindSeeds(function, viewport, options, ref evaluations, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            GraphPoint seedScreen = viewport.ToScreen(seed.X, seed.Y);
            if (seedIndex.Any(new GraphRect(seedScreen.X - 3, seedScreen.Y - 3, 6, 6)))
            {
                continue;
            }

            List<GraphPoint> forward = TraceDirection(
                function, seed, 1, viewport, options, ref evaluations, ref missing, cancellationToken);
            List<GraphPoint> backward = TraceDirection(
                function, seed, -1, viewport, options, ref evaluations, ref missing, cancellationToken);

            backward.Reverse();
            if (backward.Count > 0)
            {
                backward.RemoveAt(backward.Count - 1);
            }

            backward.AddRange(forward);
            if (backward.Count < 2)
            {
                continue;
            }

            int remaining = options.MaximumVertices - vertices;
            if (backward.Count > remaining)
            {
                backward.RemoveRange(remaining, backward.Count - remaining);
                missing = true;
            }

            foreach (GraphPoint point in backward)
            {
                seedIndex.Insert(viewport.ToScreen(point.X, point.Y), components.Count);
            }

            vertices += backward.Count;
            components.Add(new SampledComponent(backward.ToImmutableArray()));
            if (vertices >= options.MaximumVertices)
            {
                break;
            }
        }

        bool budgetExceeded = vertices >= options.MaximumVertices;
        return new SampledCurve(
            components.ToImmutableArray(),
            evaluations,
            vertices,
            missing || budgetExceeded,
            budgetExceeded);
    }

    private static ImplicitTraceOptions Normalize(ImplicitTraceOptions options) => new(
        options.SeedColumns <= 0 ? 48 : options.SeedColumns,
        options.SeedRows <= 0 ? 48 : options.SeedRows,
        options.MaximumVertices <= 0 ? 65_536 : options.MaximumVertices,
        options.MaximumNewtonSteps <= 0 ? 8 : options.MaximumNewtonSteps,
        options.NewtonTolerance <= 0 ? 1e-7 : options.NewtonTolerance,
        options.StepInPixels <= 0 ? 2.5 : options.StepInPixels,
        options.LoopDistanceFactor <= 0 ? 0.09 : options.LoopDistanceFactor,
        options.LoopDirectionDot is <= 0 or > 1 ? 0.99 : options.LoopDirectionDot);

    private static IEnumerable<GraphPoint> FindSeeds(
        ImplicitEvaluator function,
        SamplingViewport viewport,
        ImplicitTraceOptions options,
        ref int evaluations,
        CancellationToken cancellationToken)
    {
        var seeds = new List<GraphPoint>();
        double dx = viewport.XRange.Length / options.SeedColumns;
        double dy = viewport.YRange.Length / options.SeedRows;

        for (int ix = 0; ix <= options.SeedColumns; ix++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            double x = viewport.XRange.Minimum + (ix * dx);
            double previousY = viewport.YRange.Minimum;
            double previous = Evaluate(function, x, previousY, ref evaluations);
            for (int iy = 1; iy <= options.SeedRows; iy++)
            {
                double y = viewport.YRange.Minimum + (iy * dy);
                double current = Evaluate(function, x, y, ref evaluations);
                if (HasRootBracket(previous, current))
                {
                    seeds.Add(BisectVertical(function, x, previousY, y, previous, current, ref evaluations));
                }

                previousY = y;
                previous = current;
            }
        }

        for (int iy = 0; iy <= options.SeedRows; iy++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            double y = viewport.YRange.Minimum + (iy * dy);
            double previousX = viewport.XRange.Minimum;
            double previous = Evaluate(function, previousX, y, ref evaluations);
            for (int ix = 1; ix <= options.SeedColumns; ix++)
            {
                double x = viewport.XRange.Minimum + (ix * dx);
                double current = Evaluate(function, x, y, ref evaluations);
                if (HasRootBracket(previous, current))
                {
                    seeds.Add(BisectHorizontal(function, previousX, x, y, previous, current, ref evaluations));
                }

                previousX = x;
                previous = current;
            }
        }

        return seeds;
    }

    private static List<GraphPoint> TraceDirection(
        ImplicitEvaluator function,
        GraphPoint seed,
        int direction,
        SamplingViewport viewport,
        ImplicitTraceOptions options,
        ref int evaluations,
        ref bool missing,
        CancellationToken cancellationToken)
    {
        var points = new List<GraphPoint> { seed };
        GraphPoint current = seed;
        GraphPoint? initialTangent = null;
        double userStep = options.StepInPixels * Math.Min(
            viewport.XRange.Length / viewport.Width,
            viewport.YRange.Length / viewport.Height);

        for (int step = 1; step < options.MaximumVertices; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            (double gx, double gy) = Gradient(function, current, viewport, ref evaluations);
            double gradientLength = Hypotenuse(gx, gy);
            if (!double.IsFinite(gradientLength) || gradientLength < 1e-12)
            {
                missing = true;
                break;
            }

            GraphPoint tangent = new(direction * (-gy / gradientLength), direction * (gx / gradientLength));
            if (initialTangent is null)
            {
                initialTangent = tangent;
            }
            else if (points.Count > 1)
            {
                GraphPoint previous = points[^1];
                GraphPoint beforePrevious = points[^2];
                double vx = previous.X - beforePrevious.X;
                double vy = previous.Y - beforePrevious.Y;
                if ((vx * tangent.X) + (vy * tangent.Y) < 0)
                {
                    tangent = new GraphPoint(-tangent.X, -tangent.Y);
                }
            }

            GraphPoint corrected = new(
                current.X + (userStep * tangent.X),
                current.Y + (userStep * tangent.Y));
            bool converged = false;
            for (int iteration = 0; iteration < options.MaximumNewtonSteps; iteration++)
            {
                double value = Evaluate(function, corrected.X, corrected.Y, ref evaluations);
                if (!double.IsFinite(value))
                {
                    break;
                }

                if (Math.Abs(value) <= options.NewtonTolerance)
                {
                    converged = true;
                    break;
                }

                (gx, gy) = Gradient(function, corrected, viewport, ref evaluations);
                double denominator = (gx * gx) + (gy * gy);
                if (!double.IsFinite(denominator) || denominator < 1e-24)
                {
                    break;
                }

                corrected = new GraphPoint(
                    corrected.X - ((value * gx) / denominator),
                    corrected.Y - ((value * gy) / denominator));
            }

            if (!converged || !Inside(viewport, corrected))
            {
                if (!converged)
                {
                    missing = true;
                }

                break;
            }

            current = corrected;
            points.Add(current);

            if (points.Count > 8)
            {
                GraphPoint startScreen = viewport.ToScreen(seed.X, seed.Y);
                GraphPoint currentScreen = viewport.ToScreen(current.X, current.Y);
                double distance = Hypotenuse(startScreen.X - currentScreen.X, startScreen.Y - currentScreen.Y);
                if (distance < options.LoopDistanceFactor * options.StepInPixels)
                {
                    (gx, gy) = Gradient(function, current, viewport, ref evaluations);
                    gradientLength = Hypotenuse(gx, gy);
                    if (gradientLength > 1e-12)
                    {
                        GraphPoint closingTangent = new(
                            direction * (-gy / gradientLength),
                            direction * (gx / gradientLength));
                        double dot = (closingTangent.X * initialTangent.Value.X) +
                                     (closingTangent.Y * initialTangent.Value.Y);
                        if (dot >= options.LoopDirectionDot)
                        {
                            points.Add(seed);
                            break;
                        }
                    }
                }
            }
        }

        return points;
    }

    private static (double X, double Y) Gradient(
        ImplicitEvaluator function,
        GraphPoint point,
        SamplingViewport viewport,
        ref int evaluations)
    {
        double hx = Math.Max(viewport.XRange.Length * 1e-7, 1e-10);
        double hy = Math.Max(viewport.YRange.Length * 1e-7, 1e-10);
        double gx = (Evaluate(function, point.X + hx, point.Y, ref evaluations) -
                     Evaluate(function, point.X - hx, point.Y, ref evaluations)) / (2 * hx);
        double gy = (Evaluate(function, point.X, point.Y + hy, ref evaluations) -
                     Evaluate(function, point.X, point.Y - hy, ref evaluations)) / (2 * hy);
        return (gx, gy);
    }

    private static GraphPoint BisectVertical(
        ImplicitEvaluator function,
        double x,
        double minimum,
        double maximum,
        double atMinimum,
        double atMaximum,
        ref int evaluations)
    {
        for (int i = 0; i < 48; i++)
        {
            double midpoint = minimum + ((maximum - minimum) * 0.5);
            double value = Evaluate(function, x, midpoint, ref evaluations);
            if (Math.Abs(value) < 1e-12)
            {
                return new GraphPoint(x, midpoint);
            }

            if (HasRootBracket(atMinimum, value))
            {
                maximum = midpoint;
                atMaximum = value;
            }
            else
            {
                minimum = midpoint;
                atMinimum = value;
            }
        }

        _ = atMaximum;
        return new GraphPoint(x, minimum + ((maximum - minimum) * 0.5));
    }

    private static GraphPoint BisectHorizontal(
        ImplicitEvaluator function,
        double minimum,
        double maximum,
        double y,
        double atMinimum,
        double atMaximum,
        ref int evaluations)
    {
        for (int i = 0; i < 48; i++)
        {
            double midpoint = minimum + ((maximum - minimum) * 0.5);
            double value = Evaluate(function, midpoint, y, ref evaluations);
            if (Math.Abs(value) < 1e-12)
            {
                return new GraphPoint(midpoint, y);
            }

            if (HasRootBracket(atMinimum, value))
            {
                maximum = midpoint;
                atMaximum = value;
            }
            else
            {
                minimum = midpoint;
                atMinimum = value;
            }
        }

        _ = atMaximum;
        return new GraphPoint(minimum + ((maximum - minimum) * 0.5), y);
    }

    private static bool HasRootBracket(double left, double right) =>
        double.IsFinite(left) &&
        double.IsFinite(right) &&
        (left == 0 || right == 0 || Math.Sign(left) != Math.Sign(right));

    private static double Evaluate(ImplicitEvaluator function, double x, double y, ref int evaluations)
    {
        evaluations++;
        return function(x, y);
    }

    private static bool Inside(SamplingViewport viewport, GraphPoint point) =>
        point.X >= viewport.XRange.Minimum && point.X <= viewport.XRange.Maximum &&
        point.Y >= viewport.YRange.Minimum && point.Y <= viewport.YRange.Maximum;

    private static double Hypotenuse(double x, double y)
    {
        x = Math.Abs(x);
        y = Math.Abs(y);
        double maximum = Math.Max(x, y);
        if (maximum == 0 || double.IsInfinity(maximum))
        {
            return maximum;
        }

        double ratio = Math.Min(x, y) / maximum;
        return maximum * Math.Sqrt(1 + (ratio * ratio));
    }
}
