using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using Graphing;
using Graphing.Renderer;
using GraphingRaster.Skia;
using JsMath.Port;

namespace GraphingImpl;

internal sealed record PreparedEquationGeometry(
    CompiledGraphEquation Definition,
    SampledCurve Boundary,
    InequalityHatchGrid InequalityHatch);

internal readonly record struct InequalityHatchGrid(
    ImmutableArray<ulong> Occupancy,
    int LatticeIntervals)
{
    public static InequalityHatchGrid Empty { get; } = new(
        ImmutableArray<ulong>.Empty,
        0);
}

internal sealed record PreparedGraph(
    GraphSnapshot Snapshot,
    SamplingViewport Viewport,
    ImmutableArray<PreparedEquationGeometry> Equations,
    bool HasMissingData);

internal sealed class ManagedGraphRenderer : IGraphRenderer, IDisposable
{
    private const double MinimumRangeLength = 1e-12;
    private const double MaximumRangeLength = 1e12;
    private const int MaximumRetainedVertices = 65_536;
    private const int MaximumVerticesPerEquation = 16_384;
    private static readonly string[] TickFormats =
    [
        "0",
        "0.#",
        "0.##",
        "0.###",
        "0.####",
        "0.#####",
        "0.######",
        "0.#######",
        "0.########",
        "0.#########",
        "0.##########",
        "0.###########",
        "0.############"
    ];
    private readonly Lock _lock = new();
    private readonly ManagedGraphingOptions _options;
    private readonly EvaluationOptions _evaluationOptions;
    private GraphSnapshot _snapshot = GraphSnapshot.Empty;
    private AxisRange _xRange;
    private AxisRange _yRange;
    private uint _width = 800;
    private uint _height = 600;
    private float _dpiX = 96;
    private float _dpiY = 96;
    private long _generation;
    private CancellationTokenSource? _samplingCancellation;
    private PreparedGraph? _prepared;
    private GraphFrame? _frame;
    private PreparedGraph? _equationCommandSource;
    private ImmutableArray<GraphFrameCommand> _equationCommands = ImmutableArray<GraphFrameCommand>.Empty;
    private PreparedGraph? _contentCommandSource;
    private ImmutableArray<GraphFrameCommand> _contentCommands = ImmutableArray<GraphFrameCommand>.Empty;
    private bool _disposed;

    public ManagedGraphRenderer(ManagedGraphingOptions options, EvaluationOptions evaluationOptions)
    {
        _options = options;
        _evaluationOptions = evaluationOptions;
        _xRange = options.GetDefaultXRange();
        _yRange = options.GetDefaultYRange();
    }

    public GraphFrame? CurrentFrame
    {
        get
        {
            lock (_lock)
            {
                if (_frame is null &&
                    _prepared is not null &&
                    _prepared.Viewport == EffectiveViewportLocked())
                {
                    _frame = BuildFrame(_prepared);
                }

                return _frame;
            }
        }
    }

    public GraphStatus SetGraphSize(uint width, uint height)
    {
        if (width == 0 || height == 0 || width > 32_768 || height > 32_768)
        {
            return GraphStatus.InvalidArgument;
        }

        lock (_lock)
        {
            ThrowIfDisposed();
            if (_width == width && _height == height)
            {
                return GraphStatus.Ok;
            }

            _width = width;
            _height = height;
            InvalidateGeometryLocked();
        }

        return GraphStatus.Ok;
    }

    public GraphStatus SetDpi(float dpiX, float dpiY)
    {
        if (!float.IsFinite(dpiX) || dpiX <= 0 || !float.IsFinite(dpiY) || dpiY <= 0)
        {
            return GraphStatus.InvalidArgument;
        }

        lock (_lock)
        {
            ThrowIfDisposed();
            if (_dpiX == dpiX && _dpiY == dpiY)
            {
                return GraphStatus.Ok;
            }

            _dpiX = dpiX;
            _dpiY = dpiY;
            RebuildFrameFromPreparedLocked();
        }

        return GraphStatus.Ok;
    }

    public GraphStatus Draw(IGraphDrawingTarget drawingTarget, out bool hasSomeMissingData)
    {
        ArgumentNullException.ThrowIfNull(drawingTarget);
        GraphFrame? frame = CurrentFrame;
        GraphStatus status = GraphStatus.Ok;
        if (frame is null)
        {
            status = PrepareGraph();
            frame = CurrentFrame;
        }

        hasSomeMissingData = frame?.HasSomeMissingData ?? false;
        if (frame is null || status.Failed)
        {
            return status.Failed ? status : GraphStatus.Fail;
        }

        try
        {
            drawingTarget.BeginFrame(frame);
            foreach (GraphFrameCommand command in frame.Commands)
            {
                drawingTarget.Draw(command);
            }

            drawingTarget.EndFrame();
            return GraphStatus.Ok;
        }
        catch (OperationCanceledException)
        {
            return GraphStatus.Cancelled;
        }
        catch (Exception)
        {
            return GraphStatus.Fail;
        }
    }

    public GraphStatus GetClosePointData(
        double screenPointX,
        double screenPointY,
        double precision,
        out int formulaId,
        out float screenX,
        out float screenY,
        out double x,
        out double y,
        out double rho,
        out double theta,
        out double t)
    {
        SetUnavailable(out formulaId, out screenX, out screenY, out x, out y, out rho, out theta, out t);
        if (!double.IsFinite(screenPointX) || !double.IsFinite(screenPointY) || !double.IsFinite(precision) || precision < 0)
        {
            return GraphStatus.InvalidArgument;
        }

        PreparedGraph? prepared;
        SamplingViewport viewport;
        lock (_lock)
        {
            ThrowIfDisposed();
            prepared = _prepared;
            viewport = EffectiveViewportLocked();
        }

        if (prepared is null)
        {
            GraphStatus prepareStatus = PrepareGraph();
            if (prepareStatus.Failed)
            {
                return prepareStatus;
            }

            lock (_lock)
            {
                prepared = _prepared;
                viewport = EffectiveViewportLocked();
            }
        }

        if (prepared is null)
        {
            return GraphStatus.False;
        }

        double maximumDistance = Math.Clamp(
            Math.Max(6, precision * viewport.Width / viewport.XRange.Length),
            6,
            32);
        var target = new GraphPoint(screenPointX, screenPointY);
        ClosestCandidate best = ClosestCandidate.None;
        for (int equationIndex = 0; equationIndex < prepared.Equations.Length; equationIndex++)
        {
            PreparedEquationGeometry geometry = prepared.Equations[equationIndex];
            foreach (SampledComponent component in geometry.Boundary.Components)
            {
                SearchComponent(
                    component.Points,
                    geometry.Definition.Kind,
                    viewport,
                    target,
                    equationIndex,
                    ref best);
            }
        }

        if (!best.Available || Math.Sqrt(best.DistanceSquared) > maximumDistance)
        {
            return GraphStatus.False;
        }

        formulaId = best.EquationIndex;
        screenX = (float)best.Screen.X;
        screenY = (float)best.Screen.Y;
        x = best.User.X;
        y = best.User.Y;
        rho = Hypotenuse(x, y);
        theta = Math.Atan2(y, x);
        t = best.Parameter;
        return GraphStatus.Ok;
    }

    public GraphStatus ScaleRange(double centerX, double centerY, double scale)
    {
        if (!double.IsFinite(centerX) || !double.IsFinite(centerY) ||
            !double.IsFinite(scale) || scale <= 0)
        {
            return GraphStatus.InvalidArgument;
        }

        lock (_lock)
        {
            ThrowIfDisposed();
            double graphCenterX = _xRange.Center + (Math.Clamp(centerX, -1, 1) * _xRange.Length * 0.5);
            double graphCenterY = _yRange.Center + (Math.Clamp(centerY, -1, 1) * _yRange.Length * 0.5);
            AxisRange x = Scale(_xRange, graphCenterX, scale);
            AxisRange y = Scale(_yRange, graphCenterY, scale);
            if (!IsAllowed(x) || !IsAllowed(y))
            {
                return GraphStatus.False;
            }

            _xRange = x;
            _yRange = y;
            InvalidateGeometryLocked();
        }

        return GraphStatus.Ok;
    }

    public GraphStatus ChangeRange(ChangeRangeAction action)
    {
        return action switch
        {
            ChangeRangeAction.ZoomIn => ScaleRange(0, 0, 0.8),
            ChangeRangeAction.ZoomOut => ScaleRange(0, 0, 1.25),
            ChangeRangeAction.WidenX => ScaleSingleAxis(xAxis: true, 1.25),
            ChangeRangeAction.ShrinkX => ScaleSingleAxis(xAxis: true, 0.8),
            ChangeRangeAction.WidenY => ScaleSingleAxis(xAxis: false, 1.25),
            ChangeRangeAction.ShrinkY => ScaleSingleAxis(xAxis: false, 0.8),
            ChangeRangeAction.MoveNegativeX => MoveRangeByRatio(-0.2, 0),
            ChangeRangeAction.MovePositiveX => MoveRangeByRatio(0.2, 0),
            ChangeRangeAction.MoveNegativeY => MoveRangeByRatio(0, -0.2),
            ChangeRangeAction.MovePositiveY => MoveRangeByRatio(0, 0.2),
            ChangeRangeAction.SmoothZoomIn => ScaleRange(0, 0, 0.95),
            ChangeRangeAction.SmoothZoomOut => ScaleRange(0, 0, 1.05),
            ChangeRangeAction.PinchZoomIn => ScaleRange(0, 0, 0.9),
            ChangeRangeAction.PinchZoomOut => ScaleRange(0, 0, 1.1),
            ChangeRangeAction.WidenZ or
            ChangeRangeAction.ShrinkZ or
            ChangeRangeAction.MoveNegativeZ or
            ChangeRangeAction.MovePositiveZ => GraphStatus.False,
            _ => GraphStatus.InvalidArgument
        };
    }

    public GraphStatus MoveRangeByRatio(double ratioX, double ratioY)
    {
        if (!double.IsFinite(ratioX) || !double.IsFinite(ratioY))
        {
            return GraphStatus.InvalidArgument;
        }

        lock (_lock)
        {
            ThrowIfDisposed();
            double xOffset = ratioX * _xRange.Length * 0.5;
            double yOffset = ratioY * _yRange.Length * 0.5;
            _xRange = new AxisRange(_xRange.Minimum + xOffset, _xRange.Maximum + xOffset);
            _yRange = new AxisRange(_yRange.Minimum + yOffset, _yRange.Maximum + yOffset);
            InvalidateGeometryLocked();
        }

        return GraphStatus.Ok;
    }

    public GraphStatus ResetRange()
    {
        AxisRange x = _options.GetDefaultXRange();
        AxisRange y = _options.GetDefaultYRange();
        lock (_lock)
        {
            ThrowIfDisposed();
            _xRange = x;
            _yRange = y;
            InvalidateGeometryLocked();
        }

        return GraphStatus.Ok;
    }

    public GraphStatus GetDisplayRanges(out double xMin, out double xMax, out double yMin, out double yMax)
    {
        lock (_lock)
        {
            ThrowIfDisposed();
            xMin = _xRange.Minimum;
            xMax = _xRange.Maximum;
            yMin = _yRange.Minimum;
            yMax = _yRange.Maximum;
        }

        return GraphStatus.Ok;
    }

    public GraphStatus SetDisplayRanges(double xMin, double xMax, double yMin, double yMax)
    {
        var x = new AxisRange(xMin, xMax);
        var y = new AxisRange(yMin, yMax);
        if (!IsAllowed(x) || !IsAllowed(y))
        {
            return GraphStatus.InvalidArgument;
        }

        lock (_lock)
        {
            ThrowIfDisposed();
            _xRange = x;
            _yRange = y;
            InvalidateGeometryLocked();
        }

        return GraphStatus.Ok;
    }

    public GraphStatus PrepareGraph()
    {
        GraphSnapshot snapshot;
        SamplingViewport viewport;
        long generation;
        CancellationTokenSource samplingCancellation;
        CancellationToken cancellationToken;
        lock (_lock)
        {
            ThrowIfDisposed();
            snapshot = _snapshot;
            viewport = EffectiveViewportLocked();
            if (_prepared is not null &&
                _prepared.Snapshot.Revision == snapshot.Revision &&
                _prepared.Viewport == viewport)
            {
                _frame ??= BuildFrame(_prepared);
                return GraphStatus.Ok;
            }

            generation = _generation;
            CancelSamplingLocked();
            samplingCancellation = new CancellationTokenSource();
            _samplingCancellation = samplingCancellation;
            cancellationToken = samplingCancellation.Token;
        }

        ulong maximumMilliseconds = _options.GetMaxExecutionTime();
        CancellationTokenSource? timeout = null;
        CancellationToken effectiveCancellationToken = cancellationToken;
        if (maximumMilliseconds > 0)
        {
            timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(Math.Min(maximumMilliseconds, int.MaxValue)));
            effectiveCancellationToken = timeout.Token;
        }

        PreparedGraph prepared;
        try
        {
            prepared = SampleSnapshot(snapshot, viewport, effectiveCancellationToken);
        }
        catch (OperationCanceledException)
        {
            ReleaseSampling(samplingCancellation);
            return cancellationToken.IsCancellationRequested ? GraphStatus.Cancelled : GraphStatus.Timeout;
        }
        catch
        {
            ReleaseSampling(samplingCancellation);
            throw;
        }
        finally
        {
            timeout?.Dispose();
        }

        lock (_lock)
        {
            ReleaseSamplingLocked(samplingCancellation);
            if (_disposed || generation != _generation || snapshot.Revision != _snapshot.Revision)
            {
                return GraphStatus.Cancelled;
            }

            _prepared = prepared;
            ClearEquationCommandCacheLocked();
            _frame = BuildFrame(prepared);
        }

        return GraphStatus.Ok;
    }

    public GraphStatus GetBitmap(out IBitmap? bitmap, out bool hasSomeMissingData)
    {
        bitmap = null;
        GraphFrame? frame = CurrentFrame;
        if (frame is null)
        {
            GraphStatus status = PrepareGraph();
            if (status.Failed)
            {
                hasSomeMissingData = false;
                return status;
            }

            frame = CurrentFrame;
        }

        hasSomeMissingData = frame?.HasSomeMissingData ?? false;
        if (frame is null)
        {
            return GraphStatus.Fail;
        }

        try
        {
            bitmap = new PngGraphBitmap(SkiaGraphFrameRasterizer.EncodePng(frame));
            return GraphStatus.Ok;
        }
        catch (OutOfMemoryException)
        {
            return GraphStatus.OutOfMemory;
        }
        catch (Exception)
        {
            return GraphStatus.Fail;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CancelSamplingLocked();
            _frame = null;
            _prepared = null;
            ClearEquationCommandCacheLocked();
        }
    }

    internal void UpdateSnapshot(GraphSnapshot snapshot)
    {
        lock (_lock)
        {
            ThrowIfDisposed();
            _snapshot = snapshot;
            InvalidateGeometryLocked();
        }
    }

    internal void InvalidateStyle()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            ClearEquationCommandCacheLocked();
            // A theme/style update commonly changes several options back to
            // back. Invalidate once and rebuild lazily for the next requested
            // frame instead of materializing every intermediate style state.
            _frame = null;
        }
    }

    private PreparedGraph SampleSnapshot(
        GraphSnapshot snapshot,
        SamplingViewport viewport,
        CancellationToken cancellationToken)
    {
        var geometries = ImmutableArray.CreateBuilder<PreparedEquationGeometry>(snapshot.Definitions.Length);
        double[] values = ArrayPool<double>.Shared.Rent(Math.Max(1, snapshot.Values.Length));
        int totalVertices = 0;
        bool missing = false;
        try
        {
            for (int index = 0; index < snapshot.Definitions.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                snapshot.Values.AsSpan().CopyTo(values);
                CompiledGraphEquation definition = snapshot.Definitions[index];
                int remaining = Math.Max(2, MaximumRetainedVertices - totalVertices);
                if (remaining <= 2)
                {
                    missing = true;
                    break;
                }

                SampledCurve boundary = SampleBoundary(
                    definition,
                    values,
                    viewport,
                    Math.Min(MaximumVerticesPerEquation, remaining),
                    cancellationToken);
                InequalityHatchGrid hatch = definition.IsInequality
                    ? SampleInequalityHatch(
                        definition,
                        values,
                        viewport,
                        cancellationToken)
                    : InequalityHatchGrid.Empty;
                totalVertices += boundary.VertexCount;
                missing |= boundary.HasMissingData;
                geometries.Add(new PreparedEquationGeometry(definition, boundary, hatch));
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(values, clearArray: false);
        }

        missing |= geometries.Count != snapshot.Definitions.Length;
        return new PreparedGraph(snapshot, viewport, geometries.ToImmutable(), missing);
    }

    private SampledCurve SampleBoundary(
        CompiledGraphEquation definition,
        double[] values,
        SamplingViewport viewport,
        int maximumVertices,
        CancellationToken cancellationToken)
    {
        EvalTrigUnitMode trigMode = _evaluationOptions.GetTrigUnitMode();
        if (definition.Kind == GraphEquationKind.Implicit)
        {
            return ImplicitCurveTracer.Trace(
                (x, y) => EvaluateBoundaryField(definition, values, x, y, trigMode),
                viewport,
                new ImplicitTraceOptions(MaximumVertices: maximumVertices),
                cancellationToken);
        }

        bool inverse = definition.Kind == GraphEquationKind.InverseX;
        CurveEvaluator evaluator = parameter =>
        {
            if (inverse)
            {
                SetVariable(values, definition.YIndex, parameter);
            }
            else
            {
                SetVariable(values, definition.XIndex, parameter);
            }

            EvaluationValue result = definition.BoundaryProgram.Evaluate(values, trigMode);
            return result.IsFinite
                ? new CurveSample(
                    parameter,
                    inverse ? result.Value : parameter,
                    inverse ? parameter : result.Value,
                    SampleState.Finite)
                : CurveSample.Undefined(parameter, ToSampleState(result.State));
        };

        AxisRange parameterRange = inverse ? viewport.YRange : viewport.XRange;
        return AdaptiveCurveSampler.Sample(
            evaluator,
            parameterRange.Minimum,
            parameterRange.Maximum,
            viewport,
            SamplingOptions.Settled with { MaximumVertices = maximumVertices },
            cancellationToken);
    }

    private GraphFrame BuildFrame(PreparedGraph prepared)
    {
        SamplingViewport viewport = EffectiveViewportLocked();
        if (prepared.Viewport != viewport)
        {
            throw new InvalidOperationException("Prepared graph geometry must match the current viewport.");
        }

        return new GraphFrame(
            _width,
            _height,
            _dpiX,
            _dpiY,
            prepared.Snapshot.Revision,
            _options.GetBackColor(),
            GetContentCommandsLocked(prepared),
            prepared.HasMissingData);
    }

    private ImmutableArray<GraphFrameCommand> GetContentCommandsLocked(PreparedGraph prepared)
    {
        if (ReferenceEquals(_contentCommandSource, prepared))
        {
            return _contentCommands;
        }

        var commands = ImmutableArray.CreateBuilder<GraphFrameCommand>();
        var labels = ImmutableArray.CreateBuilder<GlyphCommand>();
        commands.Add(new PushClipCommand(new GraphRect(
            0,
            0,
            prepared.Viewport.Width,
            prepared.Viewport.Height)));
        AppendGridAndAxes(commands, labels, prepared.Viewport);
        commands.AddRange(GetEquationCommandsLocked(prepared));
        commands.AddRange(labels);
        commands.Add(new PopClipCommand());
        _contentCommands = commands.ToImmutable();
        _contentCommandSource = prepared;
        _equationCommandSource = null;
        _equationCommands = ImmutableArray<GraphFrameCommand>.Empty;
        return _contentCommands;
    }

    private ImmutableArray<GraphFrameCommand> GetEquationCommandsLocked(PreparedGraph prepared)
    {
        if (ReferenceEquals(_equationCommandSource, prepared))
        {
            return _equationCommands;
        }

        var commands = ImmutableArray.CreateBuilder<GraphFrameCommand>();
        AppendEquationGeometry(commands, prepared, prepared.Viewport);
        _equationCommands = commands.ToImmutable();
        _equationCommandSource = prepared;
        return _equationCommands;
    }

    private void ClearEquationCommandCacheLocked()
    {
        _equationCommandSource = null;
        _equationCommands = ImmutableArray<GraphFrameCommand>.Empty;
        _contentCommandSource = null;
        _contentCommands = ImmutableArray<GraphFrameCommand>.Empty;
    }

    private void AppendGridAndAxes(
        ImmutableArray<GraphFrameCommand>.Builder commands,
        ImmutableArray<GlyphCommand>.Builder labels,
        SamplingViewport viewport)
    {
        double xStep = NiceStep(viewport.XRange.Length);
        double yStep = NiceStep(viewport.YRange.Length);
        Color gridColor = _options.GetGridColor();
        var majorGridPaint = new GraphPaint(gridColor, 1);
        var minorGridPaint = new GraphPaint(
            new Color(gridColor.R, gridColor.G, gridColor.B, (byte)(gridColor.A / 2)),
            1);
        if (_options.GetShowGrid())
        {
            AppendVerticalGridLines(commands, viewport, xStep, majorGridPaint, minorGridPaint);
            AppendHorizontalGridLines(commands, viewport, yStep, majorGridPaint, minorGridPaint);
        }

        if (_options.GetShowAxis())
        {
            var axisPaint = new GraphPaint(_options.GetAxisColor(), 1);
            GraphPaint fontPaint = new(_options.GetFontColor());
            GraphPaint backgroundPaint = new(_options.GetBackColor());
            bool hasVerticalAxis = viewport.XRange.Minimum <= 0 && viewport.XRange.Maximum >= 0;
            bool hasHorizontalAxis = viewport.YRange.Minimum <= 0 && viewport.YRange.Maximum >= 0;
            double verticalAxisX = hasVerticalAxis ? viewport.ToScreen(0, 0).X : double.NaN;
            double horizontalAxisY = hasHorizontalAxis ? viewport.ToScreen(0, 0).Y : double.NaN;

            if (hasHorizontalAxis)
            {
                AppendHorizontalAxis(commands, viewport, horizontalAxisY, axisPaint);
                AppendHorizontalLabels(
                    commands,
                    labels,
                    viewport,
                    xStep,
                    horizontalAxisY,
                    verticalAxisX,
                    hasVerticalAxis,
                    fontPaint,
                    backgroundPaint);
            }

            if (hasVerticalAxis)
            {
                AppendVerticalAxis(commands, viewport, verticalAxisX, axisPaint);
                AppendVerticalLabels(
                    commands,
                    labels,
                    viewport,
                    yStep,
                    verticalAxisX,
                    fontPaint,
                    backgroundPaint);
            }
        }

        if (_options.GetShowBox())
        {
            commands.Add(new StrokePathCommand(
                new GraphPath(
                [
                    new GraphPoint(0, 0),
                    new GraphPoint(viewport.Width, 0),
                    new GraphPoint(viewport.Width, viewport.Height),
                    new GraphPoint(0, viewport.Height)
                ],
                isClosed: true),
                new GraphPaint(_options.GetBoxColor(), 1)));
        }
    }

    private void AppendEquationGeometry(
        ImmutableArray<GraphFrameCommand>.Builder commands,
        PreparedGraph prepared,
        SamplingViewport viewport)
    {
        for (int index = 0; index < prepared.Equations.Length; index++)
        {
            PreparedEquationGeometry geometry = prepared.Equations[index];
            ManagedEquation equation = prepared.Snapshot.Equations[index];
            IEquationOptions equationOptions = equation.GetGraphEquationOptions();
            Color color = equationOptions.GetGraphColor();
            bool selected = equation.IsEquationSelected();
            float width = selected
                ? equationOptions.GetSelectedEquationLineWidth()
                : equationOptions.GetLineWidth();

            if (geometry.Definition.IsInequality)
            {
                LineStyle boundaryStyle = geometry.Definition.Relation is RelationKind.Less or RelationKind.Greater
                    ? LineStyle.Dash
                    : LineStyle.Solid;
                var boundaryPaint = new GraphPaint(color, width, boundaryStyle);
                foreach (SampledComponent component in geometry.Boundary.Components)
                {
                    if (component.Points.Length >= 2)
                    {
                        GraphPath path = ToScreenPath(component.Points, viewport, isClosed: false);
                        // Calculator emits the inequality boundary through both
                        // inequality graph parts. Replaying both paths preserves
                        // its antialiasing and selected-width appearance.
                        commands.Add(new StrokePathCommand(path, boundaryPaint));
                        commands.Add(new StrokePathCommand(path, boundaryPaint));
                    }
                }

                AppendInequalityHatch(
                    commands,
                    geometry.InequalityHatch,
                    prepared.Viewport,
                    color);
                continue;
            }

            var linePaint = new GraphPaint(color, width, equationOptions.GetLineStyle());
            foreach (SampledComponent component in geometry.Boundary.Components)
            {
                if (component.Points.Length < 2)
                {
                    continue;
                }

                commands.Add(new StrokePathCommand(
                    ToScreenPath(component.Points, viewport, isClosed: false),
                    linePaint));
                AppendFeatureMarkers(commands, component.Points, viewport, color);
            }
        }
    }

    private void AppendFeatureMarkers(
        ImmutableArray<GraphFrameCommand>.Builder commands,
        ImmutableArray<GraphPoint> points,
        SamplingViewport viewport,
        Color equationColor)
    {
        if (!_options.GetMarkZeros() && !_options.GetMarkYIntercept())
        {
            return;
        }

        for (int index = 1; index < points.Length; index++)
        {
            GraphPoint left = points[index - 1];
            GraphPoint right = points[index];
            if (_options.GetMarkZeros() && left.Y * right.Y <= 0 && left.Y != right.Y)
            {
                double amount = -left.Y / (right.Y - left.Y);
                double x = left.X + ((right.X - left.X) * amount);
                commands.Add(new MarkerCommand(
                    viewport.ToScreen(x, 0),
                    3,
                    GraphMarkerShape.Circle,
                    new GraphPaint(_options.GetZerosColor()),
                    new GraphPaint(equationColor, 1)));
            }

            if (_options.GetMarkYIntercept() && left.X * right.X <= 0 && left.X != right.X)
            {
                double amount = -left.X / (right.X - left.X);
                double y = left.Y + ((right.Y - left.Y) * amount);
                commands.Add(new MarkerCommand(
                    viewport.ToScreen(0, y),
                    3,
                    GraphMarkerShape.Circle,
                    new GraphPaint(_options.GetZerosColor()),
                    new GraphPaint(equationColor, 1)));
            }
        }
    }

    private InequalityHatchGrid SampleInequalityHatch(
        CompiledGraphEquation definition,
        double[] values,
        SamplingViewport viewport,
        CancellationToken cancellationToken)
    {
        const int latticeIntervals = 58;
        EvalTrigUnitMode trigMode = _evaluationOptions.GetTrigUnitMode();
        int sampleCount = latticeIntervals * (latticeIntervals - 1);
        var occupancy = ImmutableArray.CreateBuilder<ulong>((sampleCount + 63) / 64);
        occupancy.Count = occupancy.Capacity;
        for (int column = 0; column < latticeIntervals; column++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            double sampleX = column * viewport.Width / latticeIntervals;
            for (int row = 1; row < latticeIntervals; row++)
            {
                double sampleY = row * viewport.Height / latticeIntervals;
                GraphPoint point = viewport.ToUser(sampleX, sampleY);
                double result = EvaluateRegion(definition, values, point.X, point.Y, trigMode);
                if (!IsInequalitySatisfied(definition.Relation, result))
                {
                    continue;
                }

                int bitIndex = (column * (latticeIntervals - 1)) + row - 1;
                occupancy[bitIndex >> 6] |= 1UL << (bitIndex & 63);
            }
        }

        return new InequalityHatchGrid(occupancy.MoveToImmutable(), latticeIntervals);
    }

    private static void AppendInequalityHatch(
        ImmutableArray<GraphFrameCommand>.Builder commands,
        InequalityHatchGrid hatch,
        SamplingViewport sourceViewport,
        Color color)
    {
        if (hatch.Occupancy.IsDefaultOrEmpty)
        {
            return;
        }

        var paint = new GraphPaint(color, 1, LineStyle.Solid, AntiAlias: false);
        commands.Add(new HatchGridCommand(
            hatch.Occupancy,
            hatch.LatticeIntervals,
            sourceViewport.Width,
            sourceViewport.Height,
            1,
            paint));
    }

    private static bool IsInequalitySatisfied(RelationKind relation, double value) =>
        double.IsFinite(value) && relation switch
        {
            RelationKind.Less => value < 0,
            RelationKind.LessOrEqual => value < 0,
            RelationKind.Greater => value > 0,
            RelationKind.GreaterOrEqual => value > 0,
            _ => false
        };

    private static void AppendVerticalGridLines(
        ImmutableArray<GraphFrameCommand>.Builder commands,
        SamplingViewport viewport,
        double majorStep,
        GraphPaint majorPaint,
        GraphPaint minorPaint)
    {
        double minorStep = majorStep / 5;
        double first = Math.Floor(viewport.XRange.Minimum / minorStep) * minorStep;
        double last = Math.Ceiling(viewport.XRange.Maximum / minorStep) * minorStep;
        for (int count = 0; count < 512; count++)
        {
            double value = first + (count * minorStep);
            if (value > last + (minorStep * 1e-10))
            {
                break;
            }

            double x = viewport.ToScreen(value, 0).X;
            AppendLine(
                commands,
                new GraphPoint(x, 0),
                new GraphPoint(x, viewport.Height),
                IsMajorGridValue(value, majorStep) ? majorPaint : minorPaint);
        }
    }

    private static void AppendHorizontalGridLines(
        ImmutableArray<GraphFrameCommand>.Builder commands,
        SamplingViewport viewport,
        double majorStep,
        GraphPaint majorPaint,
        GraphPaint minorPaint)
    {
        double minorStep = majorStep / 5;
        double first = Math.Floor(viewport.YRange.Minimum / minorStep) * minorStep;
        double last = Math.Ceiling(viewport.YRange.Maximum / minorStep) * minorStep;
        for (int count = 0; count < 512; count++)
        {
            double value = first + (count * minorStep);
            if (value > last + (minorStep * 1e-10))
            {
                break;
            }

            double y = viewport.ToScreen(0, value).Y;
            AppendLine(
                commands,
                new GraphPoint(0, y),
                new GraphPoint(viewport.Width, y),
                IsMajorGridValue(value, majorStep) ? majorPaint : minorPaint);
        }
    }

    private static bool IsMajorGridValue(double value, double majorStep)
    {
        double quotient = value / majorStep;
        return Math.Abs(quotient - Math.Round(quotient)) <= 1e-9;
    }

    private static void AppendHorizontalAxis(
        ImmutableArray<GraphFrameCommand>.Builder commands,
        SamplingViewport viewport,
        double y,
        GraphPaint paint)
    {
        double left = Math.Min(2, viewport.Width * 0.5);
        double right = Math.Max(left, viewport.Width - 2);
        AppendLine(commands, new GraphPoint(left, y), new GraphPoint(right, y), paint);
        AppendLine(commands, new GraphPoint(right, y), new GraphPoint(right - 6, y - 6), paint);
        AppendLine(commands, new GraphPoint(right, y), new GraphPoint(right - 6, y + 6), paint);
    }

    private static void AppendVerticalAxis(
        ImmutableArray<GraphFrameCommand>.Builder commands,
        SamplingViewport viewport,
        double x,
        GraphPaint paint)
    {
        double top = Math.Min(2, viewport.Height * 0.5);
        double bottom = Math.Max(top, viewport.Height - 2);
        AppendLine(commands, new GraphPoint(x, bottom), new GraphPoint(x, top), paint);
        AppendLine(commands, new GraphPoint(x, top), new GraphPoint(x - 6, top + 6), paint);
        AppendLine(commands, new GraphPoint(x, top), new GraphPoint(x + 6, top + 6), paint);
    }

    private void AppendHorizontalLabels(
        ImmutableArray<GraphFrameCommand>.Builder commands,
        ImmutableArray<GlyphCommand>.Builder labels,
        SamplingViewport viewport,
        double step,
        double axisY,
        double verticalAxisX,
        bool hasVerticalAxis,
        GraphPaint fontPaint,
        GraphPaint backgroundPaint)
    {
        if (viewport.Width < 48 || viewport.Height < 24)
        {
            return;
        }

        AppendLabel(
            commands,
            labels,
            _options.GetAliasX(),
            new GraphPoint(viewport.Width - 7, axisY + 5),
            GraphTextAlignment.Center,
            fontPaint,
            backgroundPaint);

        double first = Math.Ceiling(viewport.XRange.Minimum / step) * step;
        for (int count = 0; count < 128; count++)
        {
            double value = first + (count * step);
            if (value > viewport.XRange.Maximum + (step * 1e-10))
            {
                break;
            }

            bool zero = Math.Abs(value) <= step * 1e-10;
            GraphPoint origin = zero && hasVerticalAxis
                ? new GraphPoint(verticalAxisX - 4, axisY + 2)
                : new GraphPoint(viewport.ToScreen(value, 0).X, axisY + 2);
            AppendLabel(
                commands,
                labels,
                FormatTick(zero ? 0 : value, step),
                origin,
                zero && hasVerticalAxis ? GraphTextAlignment.End : GraphTextAlignment.Center,
                fontPaint,
                backgroundPaint);
        }
    }

    private void AppendVerticalLabels(
        ImmutableArray<GraphFrameCommand>.Builder commands,
        ImmutableArray<GlyphCommand>.Builder labels,
        SamplingViewport viewport,
        double step,
        double axisX,
        GraphPaint fontPaint,
        GraphPaint backgroundPaint)
    {
        if (viewport.Width < 48 || viewport.Height < 24)
        {
            return;
        }

        AppendLabel(
            commands,
            labels,
            _options.GetAliasY(),
            new GraphPoint(axisX - 11, -0.98046875),
            GraphTextAlignment.End,
            fontPaint,
            backgroundPaint);

        double first = Math.Ceiling(viewport.YRange.Minimum / step) * step;
        for (int count = 0; count < 128; count++)
        {
            double value = first + (count * step);
            if (value > viewport.YRange.Maximum + (step * 1e-10))
            {
                break;
            }

            if (Math.Abs(value) <= step * 1e-10)
            {
                continue;
            }

            AppendLabel(
                commands,
                labels,
                FormatTick(value, step),
                new GraphPoint(axisX - 4, viewport.ToScreen(0, value).Y - 7.98046875),
                GraphTextAlignment.End,
                fontPaint,
                backgroundPaint);
        }
    }

    private static void AppendLabel(
        ImmutableArray<GraphFrameCommand>.Builder commands,
        ImmutableArray<GlyphCommand>.Builder labels,
        string text,
        GraphPoint origin,
        GraphTextAlignment alignment,
        GraphPaint fontPaint,
        GraphPaint backgroundPaint)
    {
        var glyph = new GlyphCommand(
            text,
            origin,
            "Segoe UI",
            12,
            fontPaint,
            alignment,
            GraphFontStyle.Italic);
        commands.Add(new GlyphBackgroundCommand(glyph, backgroundPaint));
        labels.Add(glyph);
    }

    private static void AppendLine(
        ImmutableArray<GraphFrameCommand>.Builder commands,
        GraphPoint start,
        GraphPoint end,
        GraphPaint paint) =>
        commands.Add(new StrokePathCommand(new GraphPath([start, end]), paint));

    private GraphStatus ScaleSingleAxis(bool xAxis, double scale)
    {
        lock (_lock)
        {
            ThrowIfDisposed();
            AxisRange candidate = Scale(xAxis ? _xRange : _yRange, xAxis ? _xRange.Center : _yRange.Center, scale);
            if (!IsAllowed(candidate))
            {
                return GraphStatus.False;
            }

            if (xAxis)
            {
                _xRange = candidate;
            }
            else
            {
                _yRange = candidate;
            }

            InvalidateGeometryLocked();
        }

        return GraphStatus.Ok;
    }

    private void InvalidateGeometryLocked()
    {
        _generation++;
        CancelSamplingLocked();
        _prepared = null;
        _frame = null;
        ClearEquationCommandCacheLocked();
    }

    private void CancelSamplingLocked()
    {
        CancellationTokenSource? cancellation = _samplingCancellation;
        _samplingCancellation = null;
        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        cancellation.Dispose();
    }

    private void ReleaseSampling(CancellationTokenSource samplingCancellation)
    {
        lock (_lock)
        {
            ReleaseSamplingLocked(samplingCancellation);
        }
    }

    private void ReleaseSamplingLocked(CancellationTokenSource samplingCancellation)
    {
        if (ReferenceEquals(_samplingCancellation, samplingCancellation))
        {
            _samplingCancellation = null;
        }

        samplingCancellation.Dispose();
    }

    private void RebuildFrameFromPreparedLocked()
    {
        _frame = _prepared is not null && _prepared.Viewport == EffectiveViewportLocked()
            ? BuildFrame(_prepared)
            : null;
    }

    private SamplingViewport EffectiveViewportLocked()
    {
        AxisRange x = _xRange;
        AxisRange y = _yRange;
        if (_options.GetForceProportional())
        {
            double xUnitsPerPixel = x.Length / _width;
            double yUnitsPerPixel = y.Length / _height;
            if (xUnitsPerPixel > yUnitsPerPixel)
            {
                double half = xUnitsPerPixel * _height * 0.5;
                y = new AxisRange(y.Center - half, y.Center + half);
            }
            else
            {
                double half = yUnitsPerPixel * _width * 0.5;
                x = new AxisRange(x.Center - half, x.Center + half);
            }
        }

        return new SamplingViewport(x, y, _width, _height);
    }

    private static void SearchComponent(
        ImmutableArray<GraphPoint> points,
        GraphEquationKind kind,
        SamplingViewport viewport,
        GraphPoint target,
        int equationIndex,
        ref ClosestCandidate best)
    {
        for (int index = 1; index < points.Length; index++)
        {
            GraphPoint userLeft = points[index - 1];
            GraphPoint userRight = points[index];
            double parameterLeft = TraceParameter(kind, userLeft);
            double parameterRight = TraceParameter(kind, userRight);
            SearchSegment(
                userLeft,
                userRight,
                parameterLeft,
                parameterRight,
                viewport,
                target,
                equationIndex,
                ref best);
        }
    }

    private static double TraceParameter(GraphEquationKind kind, GraphPoint point) => kind switch
    {
        GraphEquationKind.ExplicitY => point.X,
        GraphEquationKind.InverseX => point.Y,
        _ => double.NaN
    };

    private static void SearchSegment(
        GraphPoint userLeft,
        GraphPoint userRight,
        double parameterLeft,
        double parameterRight,
        SamplingViewport viewport,
        GraphPoint target,
        int equationIndex,
        ref ClosestCandidate best)
    {
        GraphPoint left = viewport.ToScreen(userLeft.X, userLeft.Y);
        GraphPoint right = viewport.ToScreen(userRight.X, userRight.Y);
        double dx = right.X - left.X;
        double dy = right.Y - left.Y;
        double denominator = (dx * dx) + (dy * dy);
        double amount = denominator <= double.Epsilon
            ? 0
            : Math.Clamp((((target.X - left.X) * dx) + ((target.Y - left.Y) * dy)) / denominator, 0, 1);
        GraphPoint screen = new(left.X + (amount * dx), left.Y + (amount * dy));
        double distanceX = screen.X - target.X;
        double distanceY = screen.Y - target.Y;
        double distanceSquared = (distanceX * distanceX) + (distanceY * distanceY);
        if (distanceSquared >= best.DistanceSquared)
        {
            return;
        }

        best = new ClosestCandidate(
            true,
            equationIndex,
            distanceSquared,
            screen,
            new GraphPoint(
                userLeft.X + (amount * (userRight.X - userLeft.X)),
                userLeft.Y + (amount * (userRight.Y - userLeft.Y))),
            double.IsNaN(parameterLeft)
                ? double.NaN
                : parameterLeft + (amount * (parameterRight - parameterLeft)));
    }

    private static GraphPath ToScreenPath(
        ImmutableArray<GraphPoint> points,
        SamplingViewport viewport,
        bool isClosed)
    {
        var screenPoints = ImmutableArray.CreateBuilder<GraphPoint>(points.Length);
        foreach (GraphPoint point in points)
        {
            screenPoints.Add(viewport.ToScreen(point.X, point.Y));
        }

        return new GraphPath(screenPoints.MoveToImmutable(), isClosed);
    }

    private static double EvaluateBoundaryField(
        CompiledGraphEquation definition,
        double[] values,
        double x,
        double y,
        EvalTrigUnitMode trigMode)
    {
        SetVariable(values, definition.XIndex, x);
        SetVariable(values, definition.YIndex, y);
        EvaluationValue result = definition.BoundaryProgram.Evaluate(values, trigMode);
        return result.IsFinite ? result.Value : double.NaN;
    }

    private static double EvaluateRegion(
        CompiledGraphEquation definition,
        double[] values,
        double x,
        double y,
        EvalTrigUnitMode trigMode)
    {
        SetVariable(values, definition.XIndex, x);
        SetVariable(values, definition.YIndex, y);
        ExpressionProgram regionProgram = definition.RegionProgram ??
            throw new InvalidOperationException("A region evaluator is required for an inequality.");
        EvaluationValue result = regionProgram.Evaluate(values, trigMode);
        return result.IsFinite ? result.Value : double.NaN;
    }

    private static void SetVariable(double[] values, int index, double value)
    {
        if (index >= 0)
        {
            values[index] = value;
        }
    }

    private static SampleState ToSampleState(EvaluationState state) => state switch
    {
        EvaluationState.NonReal => SampleState.NonReal,
        EvaluationState.Overflow => SampleState.Overflow,
        EvaluationState.BudgetExceeded => SampleState.BudgetExceeded,
        _ => SampleState.Undefined
    };

    private static AxisRange Scale(AxisRange range, double center, double scale) => new(
        center + ((range.Minimum - center) * scale),
        center + ((range.Maximum - center) * scale));

    private static bool IsAllowed(AxisRange range) =>
        range.IsFiniteAndOrdered &&
        range.Length >= MinimumRangeLength &&
        range.Length <= MaximumRangeLength;

    private static double NiceStep(double range)
    {
        double raw = range / 8;
        double exponent = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        double fraction = raw / exponent;
        double nice = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10;
        return nice * exponent;
    }

    private static string FormatTick(double value, double step)
    {
        int decimals = Math.Clamp((int)Math.Ceiling(-Math.Log10(step)), 0, 12);
        return value.ToString(TickFormats[decimals], CultureInfo.InvariantCulture);
    }

    private static double Hypotenuse(double first, double second)
    {
        first = Math.Abs(first);
        second = Math.Abs(second);
        double maximum = Math.Max(first, second);
        if (maximum == 0 || double.IsInfinity(maximum))
        {
            return maximum;
        }

        double ratio = Math.Min(first, second) / maximum;
        return maximum * Math.Sqrt(1 + (ratio * ratio));
    }

    private static void SetUnavailable(
        out int formulaId,
        out float screenX,
        out float screenY,
        out double x,
        out double y,
        out double rho,
        out double theta,
        out double t)
    {
        formulaId = -1;
        screenX = float.NaN;
        screenY = float.NaN;
        x = double.NaN;
        y = double.NaN;
        rho = double.NaN;
        theta = double.NaN;
        t = double.NaN;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private readonly record struct ClosestCandidate(
        bool Available,
        int EquationIndex,
        double DistanceSquared,
        GraphPoint Screen,
        GraphPoint User,
        double Parameter)
    {
        public static ClosestCandidate None { get; } = new(
            false,
            -1,
            double.PositiveInfinity,
            new GraphPoint(double.NaN, double.NaN),
            new GraphPoint(double.NaN, double.NaN),
            double.NaN);
    }
}
