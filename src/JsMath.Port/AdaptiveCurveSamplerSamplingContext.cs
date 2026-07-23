using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Graphing;

namespace JsMath.Port;

internal sealed class AdaptiveCurveSamplerSamplingContext(
    CurveEvaluator evaluator,
    SamplingViewport viewport,
    SamplingOptions options,
    CancellationToken cancellationToken)
    : IDisposable
{
    private readonly List<SampledComponent> _components = new(4);
    private GraphPoint[]? _pointBuffer = ArrayPool<GraphPoint>.Shared.Rent(options.MaximumVertices);
    private int _pointCount;
    private double _lastParameter = double.NaN;
    private int _evaluationCount;
    private int _vertexCount;
    private int _segmentVertexLimit;
    private bool _segmentBudgetExceeded;
    private bool _evaluationBudgetExceeded;

    public SamplingViewport Viewport { get; } = viewport;
    public SamplingOptions Options { get; } = options;
    public bool HasMissingData { get; set; }
    public bool BudgetExceeded { get; private set; }
    public bool ShouldStop => _segmentBudgetExceeded || _evaluationBudgetExceeded || cancellationToken.IsCancellationRequested;
    public bool CannotContinue => _evaluationBudgetExceeded || cancellationToken.IsCancellationRequested;

    public void BeginInitialSegment(int index, int segmentCount)
    {
        _segmentVertexLimit = Math.Max(_vertexCount, (int)((long)Options.MaximumVertices * (index + 1) / segmentCount));
        _segmentBudgetExceeded = false;
    }

    public CurveSample Evaluate(double parameter)
    {
        if (_evaluationCount >= Options.MaximumEvaluations)
        {
            _evaluationBudgetExceeded = true;
            BudgetExceeded = true;
            HasMissingData = true;
            return CurveSample.Undefined(parameter, SampleState.BudgetExceeded);
        }

        cancellationToken.ThrowIfCancellationRequested();
        CurveSample result = evaluator(parameter);
        _evaluationCount++;
        if (result.Parameter != parameter)
        {
            result = result with
            {
                Parameter = parameter
            };
        }

        if (result.State == SampleState.Finite && (!double.IsFinite(result.X) || !double.IsFinite(result.Y)))
        {
            result = CurveSample.Undefined(parameter, double.IsInfinity(result.X) || double.IsInfinity(result.Y) ? SampleState.Overflow : SampleState.Undefined);
        }

        return result;
    }

    public void Add(CurveSample point)
    {
        if (!point.IsFinite || _segmentBudgetExceeded || _evaluationBudgetExceeded)
        {
            return;
        }

        if (_pointCount > 0)
        {
            GraphPoint previous = _pointBuffer![_pointCount - 1];
            if (_lastParameter == point.Parameter && previous.X == point.X && previous.Y == point.Y)
            {
                return;
            }
        }

        if (_vertexCount >= _segmentVertexLimit)
        {
            _segmentBudgetExceeded = true;
            BudgetExceeded = true;
            HasMissingData = true;
            return;
        }

        _pointBuffer![_pointCount++] = new GraphPoint(point.X, point.Y);
        _lastParameter = point.Parameter;
        _vertexCount++;
    }

    public void Break()
    {
        if (_pointCount >= 2)
        {
            var points = new GraphPoint[_pointCount];
            _pointBuffer!.AsSpan(0, _pointCount).CopyTo(points);
            _components.Add(new SampledComponent(ImmutableCollectionsMarshal.AsImmutableArray(points)));
        }

        _pointCount = 0;
        _lastParameter = double.NaN;
    }

    public SampledCurve Build()
    {
        Break();
        return new SampledCurve(_components.ToImmutableArray(), _evaluationCount, _vertexCount, HasMissingData || cancellationToken.IsCancellationRequested, BudgetExceeded);
    }

    public void Dispose()
    {
        GraphPoint[]? points = _pointBuffer;
        _pointBuffer = null;
        _pointCount = 0;
        if (points is not null)
        {
            ArrayPool<GraphPoint>.Shared.Return(points, clearArray: false);
        }
    }
}
