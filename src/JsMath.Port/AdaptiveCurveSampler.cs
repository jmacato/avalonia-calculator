using System.Collections.Immutable;
using Graphing;

namespace JsMath.Port;

/// <summary>
/// Deterministic adaptive parametric sampler with finite/non-finite border
/// probing and screen-space cusp/jump refinement.
/// </summary>
[PortedFrom(
    "JSXGraph",
    "src/math/plot.js (updateParametricCurve_v2, _plotRecursive_v2, _borderCase)",
    "d4f153470e249a698a46d6e8078c1d68f0cbe2cd",
    "MIT",
    "sha256:2c0e84dbcca84ec68b70983d3c1a8f55c45f59132edd9a66ea12f6c18d99a651")]
public static class AdaptiveCurveSampler
{
    public static SampledCurve Sample(
        CurveEvaluator evaluator,
        double minimumParameter,
        double maximumParameter,
        SamplingViewport viewport,
        SamplingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        options ??= SamplingOptions.Settled;
        ValidateArguments(minimumParameter, maximumParameter, viewport, options);

        var context = new SamplingContext(evaluator, viewport, options, cancellationToken);
        int segmentCount = Math.Max(1, options.InitialSegments);
        double step = (maximumParameter - minimumParameter) / segmentCount;

        CurveSample left = context.Evaluate(minimumParameter);
        for (int i = 0; i < segmentCount && !context.ShouldStop; i++)
        {
            double rightParameter = i == segmentCount - 1
                ? maximumParameter
                : minimumParameter + ((i + 1) * step);
            CurveSample right = context.Evaluate(rightParameter);
            ProcessInterval(context, left, right, 0);
            left = right;
        }

        return context.Build();
    }

    private static void ValidateArguments(
        double minimumParameter,
        double maximumParameter,
        SamplingViewport viewport,
        SamplingOptions options)
    {
        if (!double.IsFinite(minimumParameter) ||
            !double.IsFinite(maximumParameter) ||
            minimumParameter >= maximumParameter)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumParameter));
        }

        if (!viewport.IsValid)
        {
            throw new ArgumentException("The sampling viewport must be finite and non-empty.", nameof(viewport));
        }

        if (options.InitialSegments <= 0 ||
            options.MinimumDepth < 0 ||
            options.MaximumDepth < options.MinimumDepth ||
            options.MaximumVertices < 2 ||
            options.MaximumEvaluations < 3 ||
            options.FlatnessTolerance <= 0 ||
            options.MaximumSegmentLength <= 0 ||
            options.BorderProbeIterations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
    }

    private static void ProcessInterval(
        SamplingContext context,
        CurveSample left,
        CurveSample right,
        int depth)
    {
        if (context.ShouldStop)
        {
            return;
        }

        double midpointParameter = left.Parameter + ((right.Parameter - left.Parameter) * 0.5);
        CurveSample midpoint = context.Evaluate(midpointParameter);

        if (depth < context.Options.MaximumDepth && ShouldRefine(context, left, midpoint, right, depth))
        {
            ProcessInterval(context, left, midpoint, depth + 1);
            ProcessInterval(context, midpoint, right, depth + 1);
            return;
        }

        EmitInterval(context, left, midpoint, right);
    }

    private static bool ShouldRefine(
        SamplingContext context,
        CurveSample left,
        CurveSample midpoint,
        CurveSample right,
        int depth)
    {
        if (depth < context.Options.MinimumDepth)
        {
            return true;
        }

        int finiteCount = (left.IsFinite ? 1 : 0) +
                          (midpoint.IsFinite ? 1 : 0) +
                          (right.IsFinite ? 1 : 0);
        if (finiteCount is > 0 and < 3)
        {
            return true;
        }

        // Probe undefined islands for a bounded number of levels. This catches
        // narrow valid domains without exhausting the full recursion budget.
        if (finiteCount == 0)
        {
            return depth < context.Options.MinimumDepth + 4;
        }

        GraphPoint a = context.Viewport.ToScreen(left.X, left.Y);
        GraphPoint c = context.Viewport.ToScreen(midpoint.X, midpoint.Y);
        GraphPoint b = context.Viewport.ToScreen(right.X, right.Y);

        double ab = Distance(a, b);
        double ac = Distance(a, c);
        double cb = Distance(c, b);
        double deviation = DistanceToSegment(c, a, b);

        bool allFarOutside = !context.Viewport.ContainsWithMargin(a, context.Options.OffscreenMargin) &&
                             !context.Viewport.ContainsWithMargin(c, context.Options.OffscreenMargin) &&
                             !context.Viewport.ContainsWithMargin(b, context.Options.OffscreenMargin) &&
                             AreOnSameOutsideSide(a, c, b, context.Viewport, context.Options.OffscreenMargin);
        if (allFarOutside)
        {
            return false;
        }

        bool tooLong = Math.Max(ac, cb) > context.Options.MaximumSegmentLength;
        bool notFlat = deviation > context.Options.FlatnessTolerance;
        bool cusp = ab < 0.5 * (ac + cb) && Math.Max(ac, cb) > context.Options.FlatnessTolerance;
        bool possibleJump =
            (ac > 0.99 * (ab + cb) || cb > 0.99 * (ab + ac)) &&
            Math.Max(ac, cb) > context.Viewport.Height * 0.5;

        return tooLong || notFlat || cusp || possibleJump;
    }

    private static void EmitInterval(
        SamplingContext context,
        CurveSample left,
        CurveSample midpoint,
        CurveSample right)
    {
        if (left.IsFinite && midpoint.IsFinite && right.IsFinite)
        {
            GraphPoint a = context.Viewport.ToScreen(left.X, left.Y);
            GraphPoint c = context.Viewport.ToScreen(midpoint.X, midpoint.Y);
            GraphPoint b = context.Viewport.ToScreen(right.X, right.Y);
            double ac = Distance(a, c);
            double cb = Distance(c, b);
            bool unresolvedJump =
                Math.Max(ac, cb) > context.Viewport.Height * 2 &&
                !context.Viewport.ContainsWithMargin(c, context.Options.OffscreenMargin);

            if (unresolvedJump)
            {
                context.Add(left);
                context.Break();
                context.Add(right);
                context.HasMissingData = true;
            }
            else
            {
                context.Add(left);
                context.Add(midpoint);
                context.Add(right);
            }

            return;
        }

        context.HasMissingData = true;
        EmitHalf(context, left, midpoint);
        EmitHalf(context, midpoint, right);
    }

    private static void EmitHalf(SamplingContext context, CurveSample left, CurveSample right)
    {
        if (left.IsFinite && right.IsFinite)
        {
            context.Add(left);
            context.Add(right);
            return;
        }

        if (left.IsFinite == right.IsFinite)
        {
            context.Break();
            return;
        }

        CurveSample finite = left.IsFinite ? left : right;
        CurveSample invalid = left.IsFinite ? right : left;
        bool finiteIsLeft = left.IsFinite;
        for (int i = 0; i < context.Options.BorderProbeIterations && !context.ShouldStop; i++)
        {
            double t = finite.Parameter + ((invalid.Parameter - finite.Parameter) * 0.5);
            CurveSample probe = context.Evaluate(t);
            if (probe.IsFinite)
            {
                finite = probe;
            }
            else
            {
                invalid = probe;
            }
        }

        if (finiteIsLeft)
        {
            context.Add(left);
            context.Add(finite);
            context.Break();
        }
        else
        {
            context.Break();
            context.Add(finite);
            context.Add(right);
        }
    }

    private static bool AreOnSameOutsideSide(
        GraphPoint a,
        GraphPoint b,
        GraphPoint c,
        SamplingViewport viewport,
        double margin) =>
        (a.X < -margin && b.X < -margin && c.X < -margin) ||
        (a.X > viewport.Width + margin && b.X > viewport.Width + margin && c.X > viewport.Width + margin) ||
        (a.Y < -margin && b.Y < -margin && c.Y < -margin) ||
        (a.Y > viewport.Height + margin && b.Y > viewport.Height + margin && c.Y > viewport.Height + margin);

    private static double Distance(GraphPoint a, GraphPoint b) =>
        Hypotenuse(a.X - b.X, a.Y - b.Y);

    private static double DistanceToSegment(GraphPoint p, GraphPoint a, GraphPoint b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double denominator = (dx * dx) + (dy * dy);
        if (denominator <= double.Epsilon)
        {
            return Distance(p, a);
        }

        double t = Math.Clamp((((p.X - a.X) * dx) + ((p.Y - a.Y) * dy)) / denominator, 0, 1);
        return Hypotenuse(p.X - (a.X + (t * dx)), p.Y - (a.Y + (t * dy)));
    }

    private static double Hypotenuse(double x, double y)
    {
        x = Math.Abs(x);
        y = Math.Abs(y);
        double maximum = Math.Max(x, y);
        if (maximum == 0 || double.IsInfinity(maximum))
        {
            return maximum;
        }

        double minimumRatio = Math.Min(x, y) / maximum;
        return maximum * Math.Sqrt(1 + (minimumRatio * minimumRatio));
    }

    private sealed class SamplingContext
    {
        private readonly CurveEvaluator _evaluator;
        private readonly CancellationToken _cancellationToken;
        private readonly List<SampledComponent> _components = [];
        private readonly List<GraphPoint> _current = [];
        private double _lastParameter = double.NaN;
        private int _evaluationCount;
        private int _vertexCount;

        public SamplingContext(
            CurveEvaluator evaluator,
            SamplingViewport viewport,
            SamplingOptions options,
            CancellationToken cancellationToken)
        {
            _evaluator = evaluator;
            Viewport = viewport;
            Options = options;
            _cancellationToken = cancellationToken;
        }

        public SamplingViewport Viewport { get; }

        public SamplingOptions Options { get; }

        public bool HasMissingData { get; set; }

        public bool BudgetExceeded { get; private set; }

        public bool ShouldStop => BudgetExceeded || _cancellationToken.IsCancellationRequested;

        public CurveSample Evaluate(double parameter)
        {
            if (_evaluationCount >= Options.MaximumEvaluations)
            {
                BudgetExceeded = true;
                HasMissingData = true;
                return CurveSample.Undefined(parameter, SampleState.BudgetExceeded);
            }

            _cancellationToken.ThrowIfCancellationRequested();
            CurveSample result = _evaluator(parameter);
            _evaluationCount++;
            if (result.Parameter != parameter)
            {
                result = result with { Parameter = parameter };
            }

            if (result.State == SampleState.Finite &&
                (!double.IsFinite(result.X) || !double.IsFinite(result.Y)))
            {
                result = CurveSample.Undefined(parameter,
                    double.IsInfinity(result.X) || double.IsInfinity(result.Y)
                        ? SampleState.Overflow
                        : SampleState.Undefined);
            }

            return result;
        }

        public void Add(CurveSample point)
        {
            if (!point.IsFinite || BudgetExceeded)
            {
                return;
            }

            if (_current.Count > 0)
            {
                GraphPoint previous = _current[^1];
                if (_lastParameter == point.Parameter && previous.X == point.X && previous.Y == point.Y)
                {
                    return;
                }
            }

            if (_vertexCount >= Options.MaximumVertices)
            {
                BudgetExceeded = true;
                HasMissingData = true;
                return;
            }

            _current.Add(new GraphPoint(point.X, point.Y));
            _lastParameter = point.Parameter;
            _vertexCount++;
        }

        public void Break()
        {
            if (_current.Count >= 2)
            {
                _components.Add(new SampledComponent(_current.ToImmutableArray()));
            }

            _current.Clear();
            _lastParameter = double.NaN;
        }

        public SampledCurve Build()
        {
            Break();
            return new SampledCurve(
                _components.ToImmutableArray(),
                _evaluationCount,
                _vertexCount,
                HasMissingData || _cancellationToken.IsCancellationRequested,
                BudgetExceeded);
        }
    }
}
