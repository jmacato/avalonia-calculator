using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Channels;
using Graphing;
using Graphing.Renderer;
using GraphingRaster.Skia;
using JsMath.Port;

namespace GraphingImpl;

internal sealed class ManagedGraphRenderer : IGraphRenderer, IConcurrentGraphRenderer, IDisposable
{
    private const double MinimumRangeLength = 1e-12;
    private const double MaximumRangeLength = 1e12;
    private const int MaximumRetainedVertices = 65_536;
    private const int MaximumVerticesPerEquation = 16_384;
    private const int MinimumVerticesPerEquation = 2_048;
    private const int VerticesPerViewportPerimeterPixel = 4;
    private const double SamplingOverscanScale = 2;
    private const double StandardDpi = 96;
    private const double MaximumSamplingScale = 4;
    private static readonly string[] TickFormats = ["0", "0.#", "0.##", "0.###", "0.####", "0.#####", "0.######", "0.#######", "0.########", "0.#########", "0.##########", "0.###########", "0.############"];
    private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;
    private readonly ManagedGraphingOptions _options;
    private readonly EvaluationOptions _evaluationOptions;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly Channel<ManagedGraphPrepareRequest> _prepareRequests = Channel.CreateBounded<ManagedGraphPrepareRequest>(new BoundedChannelOptions(1)
    {
        AllowSynchronousContinuations = false,
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
        SingleWriter = true
    });
    private readonly Channel<ManagedGraphPrepareResult> _prepareResults = Channel.CreateBounded<ManagedGraphPrepareResult>(new BoundedChannelOptions(1)
    {
        AllowSynchronousContinuations = false,
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
        SingleWriter = true
    });
    private GraphSnapshot _snapshot = GraphSnapshot.Empty;
    private AxisRange _xRange;
    private AxisRange _yRange;
    private uint _width = 800;
    private uint _height = 600;
    private float _dpiX = 96;
    private float _dpiY = 96;
    private double _samplingScale = 1;
    private PreparedGraph? _prepared;
    private GraphFrame? _frame;
    private PreparedGraph? _equationCommandSource;
    private ImmutableArray<GraphFrameCommand> _equationCommands = ImmutableArray<GraphFrameCommand>.Empty;
    private PreparedGraph? _contentCommandSource;
    private ImmutableArray<GraphFrameCommand> _contentCommands = ImmutableArray<GraphFrameCommand>.Empty;
    private readonly ImmutableArray<GraphFrameCommand>.Builder _interactionCommands = ImmutableArray.CreateBuilder<GraphFrameCommand>(96);
    private readonly ImmutableArray<GraphFrameCommand>.Builder _interactionTickLabels = ImmutableArray.CreateBuilder<GraphFrameCommand>(48);
    private readonly ImmutableArray<GraphFrameCommand>.Builder _interactionAxisAliases = ImmutableArray.CreateBuilder<GraphFrameCommand>(4);
    private Task? _prepareWorker;
    private long _prepareGeneration;
    private long _latestRequestedGeneration;
    private long _latestCompletedGeneration;
    private int _lastCompletedStatus = GraphStatus.Ok.Value;
    private long _lastRequestedSnapshotRevision = -1;
    private SamplingViewport _lastRequestedViewport;
    private double _lastRequestedSamplingScale = double.NaN;
    private int _disposed;
    private int _lifetimeCancellationDisposed;
    public ManagedGraphRenderer(ManagedGraphingOptions options, EvaluationOptions evaluationOptions)
    {
        _options = options;
        _evaluationOptions = evaluationOptions;
        _xRange = options.GetDefaultXRange();
        _yRange = options.GetDefaultYRange();
        GraphPipelineDiagnostics.RecordRendererCreated();
    }

    public GraphFrame? CurrentFrame
    {
        get
        {
            VerifyAccess();
            if (_frame is null &&
                _prepared is { } prepared &&
                prepared.SamplingScale == _samplingScale)
            {
                _frame = BuildFrame(prepared);
            }

            return _frame;
        }
    }

    public bool IsPrepareGraphPending => Volatile.Read(ref _disposed) == 0 && Volatile.Read(ref _latestCompletedGeneration) < Volatile.Read(ref _latestRequestedGeneration);

    public GraphStatus SetGraphSize(uint width, uint height)
    {
        if (width == 0 || height == 0 || width > 32_768 || height > 32_768)
        {
            return GraphStatus.InvalidArgument;
        }

        VerifyAccess();
        if (_width == width && _height == height)
        {
            return GraphStatus.Ok;
        }

        _width = width;
        _height = height;
        InvalidateGeometry(transformExisting: true);
        return GraphStatus.Ok;
    }

    public GraphStatus SetDpi(float dpiX, float dpiY)
    {
        if (!float.IsFinite(dpiX) || dpiX <= 0 || !float.IsFinite(dpiY) || dpiY <= 0)
        {
            return GraphStatus.InvalidArgument;
        }

        VerifyAccess();
        if (_dpiX == dpiX && _dpiY == dpiY)
        {
            return GraphStatus.Ok;
        }

        _dpiX = dpiX;
        _dpiY = dpiY;
        _samplingScale = Math.Clamp(
            Math.Max(dpiX, dpiY) / StandardDpi,
            1,
            MaximumSamplingScale);
        // The prepared curve was tessellated for the previous device scale.
        // Do not publish it with only new DPI metadata: that leaves coarse
        // logical-pixel segments visible on high-density displays. The next
        // prepare/draw will sample at the new physical-pixel tolerance.
        _frame = null;
        return GraphStatus.Ok;
    }

    public GraphStatus Draw(IGraphDrawingTarget drawingTarget, out bool hasSomeMissingData)
    {
        ArgumentNullException.ThrowIfNull(drawingTarget);
        GraphFrame? frame = CurrentFrame;
        GraphStatus status = GraphStatus.Ok;
        if (frame is null)
        {
            if (IsPrepareGraphPending)
            {
                frame = BuildPendingFrame();
                _frame = frame;
            }
            else
            {
                status = PrepareGraph();
                frame = CurrentFrame;
            }
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
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or NotSupportedException)
        {
            return GraphStatus.Fail;
        }
    }

    public GraphStatus GetClosePointData(ref ClosePointRequest request)
    {
        request.Result = ClosestPointData.Unavailable;
        if (!double.IsFinite(request.ScreenPointX) ||
            !double.IsFinite(request.ScreenPointY) ||
            !double.IsFinite(request.Precision) ||
            request.Precision < 0)
        {
            return GraphStatus.InvalidArgument;
        }

        VerifyAccess();
        PreparedGraph? prepared = _prepared;
        SamplingViewport viewport = EffectiveViewport();
        if (prepared is null)
        {
            GraphStatus prepareStatus = PrepareGraph();
            if (prepareStatus.Failed)
            {
                return prepareStatus;
            }

            prepared = _prepared;
            viewport = EffectiveViewport();
        }

        if (prepared is null)
        {
            return GraphStatus.False;
        }

        double maximumDistance = Math.Clamp(Math.Max(6, request.Precision * viewport.Width / viewport.XRange.Length), 6, 32);
        var target = new GraphPoint(request.ScreenPointX, request.ScreenPointY);
        ManagedGraphRendererClosestCandidate best = ManagedGraphRendererClosestCandidate.None;
        for (int equationIndex = 0; equationIndex < prepared.Equations.Length; equationIndex++)
        {
            PreparedEquationGeometry geometry = prepared.Equations[equationIndex];
            foreach (SampledComponent component in geometry.Boundary.Components)
            {
                SearchComponent(component.Points, geometry.Definition.Kind, viewport, target, equationIndex, ref best);
            }
        }

        if (!best.Available || Math.Sqrt(best.DistanceSquared) > maximumDistance)
        {
            return GraphStatus.False;
        }

        double x = best.User.X;
        double y = best.User.Y;
        request.Result = new ClosestPointData(
            best.EquationIndex,
            (float)best.Screen.X,
            (float)best.Screen.Y,
            x,
            y,
            Hypotenuse(x, y),
            Math.Atan2(y, x),
            best.Parameter);
        return GraphStatus.Ok;
    }

    public GraphStatus ScaleRange(double centerX, double centerY, double scale)
    {
        if (!double.IsFinite(centerX) || !double.IsFinite(centerY) || !double.IsFinite(scale) || scale <= 0)
        {
            return GraphStatus.InvalidArgument;
        }

        VerifyAccess();
        double graphCenterX = _xRange.Center + Math.Clamp(centerX, -1, 1) * _xRange.Length * 0.5;
        double graphCenterY = _yRange.Center + Math.Clamp(centerY, -1, 1) * _yRange.Length * 0.5;
        AxisRange x = Scale(_xRange, graphCenterX, scale);
        AxisRange y = Scale(_yRange, graphCenterY, scale);
        if (!IsAllowed(x) || !IsAllowed(y))
        {
            return GraphStatus.False;
        }

        _xRange = x;
        _yRange = y;
        InvalidateGeometry(transformExisting: true);
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
            ChangeRangeAction.WidenZ or ChangeRangeAction.ShrinkZ or ChangeRangeAction.MoveNegativeZ or ChangeRangeAction.MovePositiveZ => GraphStatus.False,
            _ => GraphStatus.InvalidArgument
        };
    }

    public GraphStatus MoveRangeByRatio(double ratioX, double ratioY)
    {
        if (!double.IsFinite(ratioX) || !double.IsFinite(ratioY))
        {
            return GraphStatus.InvalidArgument;
        }

        VerifyAccess();
        double xOffset = ratioX * _xRange.Length * 0.5;
        double yOffset = ratioY * _yRange.Length * 0.5;
        _xRange = new AxisRange(_xRange.Minimum + xOffset, _xRange.Maximum + xOffset);
        _yRange = new AxisRange(_yRange.Minimum + yOffset, _yRange.Maximum + yOffset);
        InvalidateGeometry(transformExisting: true);
        return GraphStatus.Ok;
    }

    public GraphStatus ResetRange()
    {
        AxisRange x = _options.GetDefaultXRange();
        AxisRange y = _options.GetDefaultYRange();
        VerifyAccess();
        _xRange = x;
        _yRange = y;
        InvalidateGeometry(transformExisting: true);
        return GraphStatus.Ok;
    }

    public GraphStatus GetDisplayRanges(out double xMin, out double xMax, out double yMin, out double yMax)
    {
        VerifyAccess();
        xMin = _xRange.Minimum;
        xMax = _xRange.Maximum;
        yMin = _yRange.Minimum;
        yMax = _yRange.Maximum;
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

        VerifyAccess();
        _xRange = x;
        _yRange = y;
        InvalidateGeometry(transformExisting: true);
        return GraphStatus.Ok;
    }

    public GraphStatus PrepareGraph()
    {
        VerifyAccess();
        if (Volatile.Read(ref _disposed) != 0)
        {
            return GraphStatus.Cancelled;
        }

        GraphSnapshot snapshot = _snapshot;
        SamplingViewport viewport = EffectiveViewport();
        double samplingScale = _samplingScale;
        if (_prepared is not null &&
            _prepared.Snapshot.Revision == snapshot.Revision &&
            _prepared.Viewport == viewport &&
            _prepared.SamplingScale == samplingScale)
        {
            _frame ??= BuildFrame(_prepared);
            return GraphStatus.Ok;
        }

        ulong maximumMilliseconds = _options.GetMaxExecutionTime();
        CancellationTokenSource? timeout = null;
        CancellationToken effectiveCancellationToken = _lifetimeCancellation.Token;
        if (maximumMilliseconds > 0)
        {
            timeout = CancellationTokenSource.CreateLinkedTokenSource(effectiveCancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(Math.Min(maximumMilliseconds, int.MaxValue)));
            effectiveCancellationToken = timeout.Token;
        }

        PreparedGraph prepared;
        try
        {
            prepared = SampleSnapshot(
                snapshot,
                viewport,
                samplingScale,
                _evaluationOptions.GetTrigUnitMode(),
                0,
                effectiveCancellationToken);
        }
        catch (OperationCanceledException)
        {
            return _lifetimeCancellation.IsCancellationRequested ? GraphStatus.Cancelled : GraphStatus.Timeout;
        }
        finally
        {
            timeout?.Dispose();
        }

        if (snapshot.Revision != _snapshot.Revision || samplingScale != _samplingScale)
        {
            return GraphStatus.Cancelled;
        }

        _prepared = prepared;
        ClearCommandCache();
        _frame = BuildFrame(prepared);
        return GraphStatus.Ok;
    }

    public GraphStatus RequestPrepareGraph()
    {
        VerifyAccess();
        if (Volatile.Read(ref _disposed) != 0)
        {
            return GraphStatus.Cancelled;
        }

        GraphSnapshot snapshot = _snapshot;
        SamplingViewport viewport = EffectiveViewport();
        double samplingScale = _samplingScale;
        if (_prepared is not null &&
            _prepared.Snapshot.Revision == snapshot.Revision &&
            _prepared.Viewport == viewport &&
            _prepared.SamplingScale == samplingScale)
        {
            _frame ??= BuildFrame(_prepared);
            return GraphStatus.Ok;
        }

        long requested = Volatile.Read(ref _latestRequestedGeneration);
        long completed = Volatile.Read(ref _latestCompletedGeneration);
        if (requested != 0 &&
            completed == requested &&
            _lastRequestedSnapshotRevision == snapshot.Revision &&
            _lastRequestedViewport == viewport &&
            _lastRequestedSamplingScale == samplingScale)
        {
            return new GraphStatus(Volatile.Read(ref _lastCompletedStatus));
        }

        if (completed < requested &&
            _lastRequestedSnapshotRevision == snapshot.Revision &&
            _lastRequestedViewport == viewport &&
            _lastRequestedSamplingScale == samplingScale)
        {
            return GraphStatus.Ok;
        }

        long generation = Interlocked.Increment(ref _prepareGeneration);
        var request = new ManagedGraphPrepareRequest(
            generation,
            snapshot,
            viewport,
            samplingScale,
            _evaluationOptions.GetTrigUnitMode(),
            _options.GetMaxExecutionTime());
        _lastRequestedSnapshotRevision = snapshot.Revision;
        _lastRequestedViewport = viewport;
        _lastRequestedSamplingScale = samplingScale;
        Volatile.Write(ref _latestRequestedGeneration, generation);
        EnsurePrepareWorkerStarted();
        if (!_prepareRequests.Writer.TryWrite(request))
        {
            Volatile.Write(ref _lastCompletedStatus, GraphStatus.Fail.Value);
            Volatile.Write(ref _latestCompletedGeneration, generation);
            return GraphStatus.Fail;
        }

        GraphPipelineDiagnostics.RecordRequest(generation);
        return GraphStatus.Ok;
    }

    public GraphStatus TryCommitPreparedGraph(out bool completed)
    {
        VerifyAccess();
        completed = false;
        if (Volatile.Read(ref _disposed) != 0)
        {
            return GraphStatus.Cancelled;
        }

        ManagedGraphPrepareResult? newest = null;
        while (_prepareResults.Reader.TryRead(out ManagedGraphPrepareResult? candidate))
        {
            if (newest is null || candidate.Generation > newest.Generation)
            {
                newest = candidate;
            }
        }

        if (newest is null || newest.Generation != Volatile.Read(ref _latestRequestedGeneration))
        {
            GraphPipelineDiagnostics.RecordCommitMiss();
            return GraphStatus.Ok;
        }

        completed = true;
        Volatile.Write(ref _latestCompletedGeneration, newest.Generation);
        if (newest.Status.Failed || newest.Prepared is null)
        {
            GraphStatus failure = newest.Status.Failed ? newest.Status : GraphStatus.Fail;
            Volatile.Write(ref _lastCompletedStatus, failure.Value);
            GraphPipelineDiagnostics.RecordCommit(newest.Generation, failure);
            return failure;
        }

        PreparedGraph prepared = newest.Prepared;
        if (prepared.Snapshot.Revision != _snapshot.Revision ||
            prepared.SamplingScale != _samplingScale)
        {
            Volatile.Write(ref _lastCompletedStatus, GraphStatus.Cancelled.Value);
            GraphPipelineDiagnostics.RecordCommit(newest.Generation, GraphStatus.Cancelled);
            return GraphStatus.Cancelled;
        }

        _prepared = prepared;
        ClearCommandCache();
        _frame = BuildFrame(prepared);
        Volatile.Write(ref _lastCompletedStatus, GraphStatus.Ok.Value);
        GraphPipelineDiagnostics.RecordCommit(newest.Generation, GraphStatus.Ok);
        return GraphStatus.Ok;
    }

    public bool TryGetPreparedDisplayRanges(out double xMinimum, out double xMaximum, out double yMinimum, out double yMaximum)
    {
        VerifyAccess();
        if (_prepared is not { } prepared)
        {
            xMinimum = 0;
            xMaximum = 0;
            yMinimum = 0;
            yMaximum = 0;
            return false;
        }

        xMinimum = prepared.Viewport.XRange.Minimum;
        xMaximum = prepared.Viewport.XRange.Maximum;
        yMinimum = prepared.Viewport.YRange.Minimum;
        yMaximum = prepared.Viewport.YRange.Maximum;
        return true;
    }

    public void ReleasePreparedResources()
    {
        VerifyAccess();
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        // Supersede any in-flight sampling without terminating the reusable
        // worker. Sampling checks the generation throughout its hot loops and
        // abandons the old result promptly. A later attachment can request a
        // fresh generation without allocating a new renderer or worker.
        long generation = Interlocked.Increment(ref _prepareGeneration);
        Volatile.Write(ref _latestRequestedGeneration, generation);
        Volatile.Write(ref _latestCompletedGeneration, generation);
        Volatile.Write(ref _lastCompletedStatus, GraphStatus.Cancelled.Value);
        _lastRequestedSnapshotRevision = -1;
        _lastRequestedSamplingScale = double.NaN;
        while (_prepareRequests.Reader.TryRead(out _))
        {
        }

        while (_prepareResults.Reader.TryRead(out _))
        {
        }

        _prepared = null;
        _frame = null;
        ClearCommandCache();
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
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or NotSupportedException)
        {
            return GraphStatus.Fail;
        }
    }

    internal void UpdateSnapshot(GraphSnapshot snapshot)
    {
        VerifyAccess();
        if (snapshot.Revision < _snapshot.Revision)
        {
            return;
        }

        _snapshot = snapshot;
        _prepared = null;
        _frame = null;
        ClearCommandCache();
        InvalidateGeometry(transformExisting: false);
    }

    internal void InvalidateStyle()
    {
        VerifyAccess();
        ClearCommandCache();
        // A theme/style update commonly changes several options back to
        // back. Invalidate once and rebuild lazily for the next requested
        // frame instead of materializing every intermediate style state.
        _frame = null;
    }

    private PreparedGraph SampleSnapshot(
        GraphSnapshot snapshot,
        SamplingViewport viewport,
        double samplingScale,
        EvalTrigUnitMode trigMode,
        long generation,
        CancellationToken cancellationToken)
    {
        SamplingViewport samplingViewport = CreateOverscanViewport(viewport);
        var geometries = ImmutableArray.CreateBuilder<PreparedEquationGeometry>(snapshot.Definitions.Length);
        double[] values = ArrayPool<double>.Shared.Rent(Math.Max(1, snapshot.Values.Length));
        int viewportVertexBudget = ViewportVertexBudget(viewport, samplingScale);
        int totalVertices = 0;
        bool missing = false;
        try
        {
            for (int index = 0; index < snapshot.Definitions.Length; index++)
            {
                ThrowIfSamplingCancelled(generation, cancellationToken);
                snapshot.Values.AsSpan().CopyTo(values);
                CompiledGraphEquation definition = snapshot.Definitions[index];
                int remaining = Math.Max(2, MaximumRetainedVertices - totalVertices);
                if (remaining <= 2)
                {
                    missing = true;
                    break;
                }

                int definitionsRemaining = snapshot.Definitions.Length - index;
                int equationBudget = Math.Max(2, Math.Min(viewportVertexBudget, remaining / definitionsRemaining));
                SamplingViewport boundaryViewport = definition.IsInequality && IsAffineBoundary(definition.BoundarySyntax) ? viewport : samplingViewport;
                SampledCurve boundary = SampleBoundary(
                    definition,
                    values,
                    boundaryViewport,
                    samplingScale,
                    equationBudget,
                    trigMode,
                    generation,
                    cancellationToken);
                InequalityHatchGrid hatch = definition.IsInequality ? SampleInequalityHatch(definition, values, viewport, trigMode, generation, cancellationToken) : InequalityHatchGrid.Empty;
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
        return new PreparedGraph(snapshot, viewport, samplingScale, geometries.ToImmutable(), missing);
    }

    private static int ViewportVertexBudget(SamplingViewport viewport, double samplingScale)
    {
        // A sampled point is retained in user coordinates, copied into a
        // screen-space GraphPath, and then consumed by the platform geometry.
        // A fixed 32K ceiling therefore creates several megabytes of transient
        // data for a curve whose output is only a few hundred pixels wide.
        // Four vertices per viewport-perimeter pixel remains deliberately
        // oversampled for cusps and implicit curves while bounding that cost.
        double requested =
            (viewport.Width + viewport.Height) *
            VerticesPerViewportPerimeterPixel *
            samplingScale;
        return (int)Math.Clamp(Math.Ceiling(requested), MinimumVerticesPerEquation, MaximumVerticesPerEquation);
    }

    private SampledCurve SampleBoundary(
        CompiledGraphEquation definition,
        double[] values,
        SamplingViewport viewport,
        double samplingScale,
        int maximumVertices,
        EvalTrigUnitMode trigMode,
        long generation,
        CancellationToken cancellationToken)
    {
        if (definition.Kind == GraphEquationKind.Implicit)
        {
            return ImplicitCurveTracer.Trace((x, y) =>
            {
                ThrowIfSamplingCancelled(generation, cancellationToken);
                return EvaluateBoundaryField(definition, values, x, y, trigMode);
            }, viewport, new ImplicitTraceOptions(
                MaximumVertices: maximumVertices,
                StepInPixels: 2.5 / samplingScale), cancellationToken);
        }

        bool inverse = definition.Kind == GraphEquationKind.InverseX;
        CurveEvaluator evaluator = parameter =>
        {
            ThrowIfSamplingCancelled(generation, cancellationToken);
            if (inverse)
            {
                SetVariable(values, definition.YIndex, parameter);
            }
            else
            {
                SetVariable(values, definition.XIndex, parameter);
            }

            EvaluationValue result = definition.BoundaryProgram.Evaluate(values, trigMode);
            return result.IsFinite ? new CurveSample(parameter, inverse ? result.Value : parameter, inverse ? parameter : result.Value, SampleState.Finite) : CurveSample.Undefined(parameter, ToSampleState(result.State));
        };
        AxisRange parameterRange = inverse ? viewport.YRange : viewport.XRange;
        return AdaptiveCurveSampler.Sample(
            evaluator,
            parameterRange.Minimum,
            parameterRange.Maximum,
            viewport,
            SamplingOptions.Settled with
            {
                MaximumVertices = maximumVertices,
                FlatnessTolerance = SamplingOptions.Settled.FlatnessTolerance / samplingScale,
                MaximumSegmentLength = SamplingOptions.Settled.MaximumSegmentLength / samplingScale
            },
            cancellationToken);
    }

    private GraphFrame BuildFrame(PreparedGraph prepared)
    {
        SamplingViewport viewport = EffectiveViewport();
        ImmutableArray<GraphFrameCommand> commands;
        if (prepared.Viewport == viewport)
        {
            commands = GetContentCommands(prepared);
        }
        else
        {
            // Interaction frames keep the expensive sampled equation paths but
            // rebuild the cheap viewport furniture at its true coordinates.
            // This avoids blank edges, stretched labels and scaled grid strokes
            // while panning/zooming without evaluating the function again.
            _interactionCommands.Clear();
            _interactionTickLabels.Clear();
            _interactionAxisAliases.Clear();
            try
            {
                _interactionCommands.Add(new PushClipCommand(new GraphRect(0, 0, viewport.Width, viewport.Height)));
                AppendGridAndAxes(_interactionCommands, _interactionTickLabels, _interactionAxisAliases, viewport, compactLines: true);
                _interactionCommands.AddRange(_interactionTickLabels);
                _interactionCommands.AddRange(_interactionAxisAliases);
                _interactionCommands.Add(new PushCoordinateTransformCommand(CoordinateTransform(prepared.Viewport, viewport)));
                _interactionCommands.Add(new CommandGroupCommand(GetEquationCommands(prepared)));
                _interactionCommands.Add(new PopCoordinateTransformCommand());
                _interactionCommands.Add(new PopClipCommand());
                commands = _interactionCommands.ToImmutable();
            }
            finally
            {
                // The immutable frame owns its copied command array. Clear the
                // retained builders immediately so they neither allocate on the
                // next pan frame nor keep the previous frame alive.
                _interactionCommands.Clear();
                _interactionTickLabels.Clear();
                _interactionAxisAliases.Clear();
            }
        }

        return new GraphFrame(_width, _height, _dpiX, _dpiY, prepared.Snapshot.Revision, _options.GetBackColor(), commands, prepared.HasMissingData);
    }

    private GraphFrame BuildPendingFrame()
    {
        var pending = new PreparedGraph(
            _snapshot,
            EffectiveViewport(),
            _samplingScale,
            ImmutableArray<PreparedEquationGeometry>.Empty,
            HasMissingData: true);
        return BuildFrame(pending);
    }

    private ImmutableArray<GraphFrameCommand> GetContentCommands(PreparedGraph prepared)
    {
        if (ReferenceEquals(_contentCommandSource, prepared))
        {
            return _contentCommands;
        }

        var commands = ImmutableArray.CreateBuilder<GraphFrameCommand>();
        var tickLabels = ImmutableArray.CreateBuilder<GraphFrameCommand>();
        var axisAliases = ImmutableArray.CreateBuilder<GraphFrameCommand>();
        commands.Add(new PushClipCommand(new GraphRect(0, 0, prepared.Viewport.Width, prepared.Viewport.Height)));
        AppendGridAndAxes(commands, tickLabels, axisAliases, prepared.Viewport);
        commands.AddRange(tickLabels);
        commands.AddRange(axisAliases);
        commands.AddRange(GetEquationCommands(prepared));
        commands.Add(new PopClipCommand());
        _contentCommands = commands.ToImmutable();
        _contentCommandSource = prepared;
        return _contentCommands;
    }

    private ImmutableArray<GraphFrameCommand> GetEquationCommands(PreparedGraph prepared)
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

    private void ClearCommandCache()
    {
        _equationCommandSource = null;
        _equationCommands = ImmutableArray<GraphFrameCommand>.Empty;
        _contentCommandSource = null;
        _contentCommands = ImmutableArray<GraphFrameCommand>.Empty;
    }

    private static GraphCoordinateTransform CoordinateTransform(SamplingViewport source, SamplingViewport target)
    {
        return new GraphCoordinateTransform(target.Width * source.XRange.Length / (source.Width * target.XRange.Length),
            target.Height * source.YRange.Length / (source.Height * target.YRange.Length),
            (source.XRange.Minimum - target.XRange.Minimum) * target.Width / target.XRange.Length,
            (target.YRange.Maximum - source.YRange.Maximum) * target.Height / target.YRange.Length);
    }

    private void AppendGridAndAxes(ImmutableArray<GraphFrameCommand>.Builder commands, ImmutableArray<GraphFrameCommand>.Builder tickLabels, ImmutableArray<GraphFrameCommand>.Builder axisAliases, SamplingViewport viewport, bool compactLines = false)
    {
        double xStep = NiceStep(viewport.XRange.Length);
        double yStep = NiceStep(viewport.YRange.Length);
        Color gridColor = _options.GetGridColor();
        var majorGridPaint = new GraphPaint(gridColor, 1);
        var minorGridPaint = new GraphPaint(new Color(gridColor.R, gridColor.G, gridColor.B, (byte)(gridColor.A / 2)), 1);
        if (_options.GetShowGrid())
        {
            AppendVerticalGridLines(commands, viewport, xStep, majorGridPaint, minorGridPaint, compactLines);
            AppendHorizontalGridLines(commands, viewport, yStep, majorGridPaint, minorGridPaint, compactLines);
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
                AppendHorizontalAxis(commands, viewport, horizontalAxisY, axisPaint, compactLines);
                AppendHorizontalLabels(tickLabels, axisAliases, viewport, xStep, horizontalAxisY, verticalAxisX, hasVerticalAxis, fontPaint, backgroundPaint);
            }

            if (hasVerticalAxis)
            {
                AppendVerticalAxis(commands, viewport, verticalAxisX, axisPaint, compactLines);
                AppendVerticalLabels(tickLabels, axisAliases, viewport, yStep, verticalAxisX, fontPaint, backgroundPaint);
            }
        }

        if (_options.GetShowBox())
        {
            var boxPaint = new GraphPaint(_options.GetBoxColor(), 1);
            if (compactLines)
            {
                var topLeft = new GraphPoint(0, 0);
                var topRight = new GraphPoint(viewport.Width, 0);
                var bottomRight = new GraphPoint(viewport.Width, viewport.Height);
                var bottomLeft = new GraphPoint(0, viewport.Height);
                AppendLine(commands, topLeft, topRight, boxPaint, compact: true);
                AppendLine(commands, topRight, bottomRight, boxPaint, compact: true);
                AppendLine(commands, bottomRight, bottomLeft, boxPaint, compact: true);
                AppendLine(commands, bottomLeft, topLeft, boxPaint, compact: true);
            }
            else
            {
                commands.Add(new StrokePathCommand(
                    new GraphPath(
                    [
                        new GraphPoint(0, 0),
                        new GraphPoint(viewport.Width, 0),
                        new GraphPoint(viewport.Width, viewport.Height),
                        new GraphPoint(0, viewport.Height),
                    ],
                    isClosed: true),
                    boxPaint));
            }
        }
    }

    private void AppendEquationGeometry(ImmutableArray<GraphFrameCommand>.Builder commands, PreparedGraph prepared, SamplingViewport viewport)
    {
        for (int index = 0; index < prepared.Equations.Length; index++)
        {
            PreparedEquationGeometry geometry = prepared.Equations[index];
            ManagedEquation equation = prepared.Snapshot.Equations[index];
            IEquationOptions equationOptions = equation.GetGraphEquationOptions();
            Color color = equationOptions.GetGraphColor();
            bool selected = equation.IsEquationSelected();
            float width = selected ? equationOptions.GetSelectedEquationLineWidth() : equationOptions.GetLineWidth();
            if (geometry.Definition.IsInequality)
            {
                LineStyle boundaryStyle = geometry.Definition.Relation is RelationKind.Less or RelationKind.Greater ? LineStyle.Dash : LineStyle.Solid;
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

                AppendInequalityHatch(commands, geometry.InequalityHatch, prepared.Viewport, color);
                continue;
            }

            var linePaint = new GraphPaint(color, width, equationOptions.GetLineStyle());
            foreach (SampledComponent component in geometry.Boundary.Components)
            {
                if (component.Points.Length < 2)
                {
                    continue;
                }

                commands.Add(new StrokePathCommand(ToScreenPath(component.Points, viewport, isClosed: false), linePaint));
                AppendFeatureMarkers(commands, component.Points, viewport, color);
            }
        }
    }

    private void AppendFeatureMarkers(ImmutableArray<GraphFrameCommand>.Builder commands, ImmutableArray<GraphPoint> points, SamplingViewport viewport, Color equationColor)
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
                double x = left.X + (right.X - left.X) * amount;
                commands.Add(new MarkerCommand(viewport.ToScreen(x, 0), 3, GraphMarkerShape.Circle, new GraphPaint(_options.GetZerosColor()), new GraphPaint(equationColor, 1)));
            }

            if (_options.GetMarkYIntercept() && left.X * right.X <= 0 && left.X != right.X)
            {
                double amount = -left.X / (right.X - left.X);
                double y = left.Y + (right.Y - left.Y) * amount;
                commands.Add(new MarkerCommand(viewport.ToScreen(0, y), 3, GraphMarkerShape.Circle, new GraphPaint(_options.GetZerosColor()), new GraphPaint(equationColor, 1)));
            }
        }
    }

    private InequalityHatchGrid SampleInequalityHatch(CompiledGraphEquation definition, double[] values, SamplingViewport viewport, EvalTrigUnitMode trigMode, long generation, CancellationToken cancellationToken)
    {
        const int latticeIntervals = 58;
        int sampleCount = latticeIntervals * (latticeIntervals - 1);
        var occupancy = ImmutableArray.CreateBuilder<ulong>((sampleCount + 63) / 64);
        occupancy.Count = occupancy.Capacity;
        for (int column = 0; column < latticeIntervals; column++)
        {
            ThrowIfSamplingCancelled(generation, cancellationToken);
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

                int bitIndex = column * (latticeIntervals - 1) + row - 1;
                occupancy[bitIndex >> 6] |= 1UL << (bitIndex & 63);
            }
        }

        return new InequalityHatchGrid(occupancy.MoveToImmutable(), latticeIntervals);
    }

    private static void AppendInequalityHatch(ImmutableArray<GraphFrameCommand>.Builder commands, InequalityHatchGrid hatch, SamplingViewport sourceViewport, Color color)
    {
        if (hatch.Occupancy.IsDefaultOrEmpty)
        {
            return;
        }

        var paint = new GraphPaint(color, 1, LineStyle.Solid, AntiAlias: false);
        commands.Add(new HatchGridCommand(hatch.Occupancy, hatch.LatticeIntervals, sourceViewport.Width, sourceViewport.Height, 1, paint));
    }

    private static SamplingViewport CreateOverscanViewport(SamplingViewport viewport)
    {
        double xHalf = viewport.XRange.Length * SamplingOverscanScale * 0.5;
        double yHalf = viewport.YRange.Length * SamplingOverscanScale * 0.5;
        return new SamplingViewport(new AxisRange(viewport.XRange.Center - xHalf, viewport.XRange.Center + xHalf), new AxisRange(viewport.YRange.Center - yHalf, viewport.YRange.Center + yHalf), viewport.Width * SamplingOverscanScale, viewport.Height * SamplingOverscanScale);
    }

    private static bool IsAffineBoundary(AstNode node)
    {
        return node.Kind switch
        {
            AstKind.Number or AstKind.Variable => true,
            AstKind.Negate => IsAffineBoundary(node.Children[0]),
            AstKind.Add or AstKind.Subtract => IsAffineBoundary(node.Children[0]) && IsAffineBoundary(node.Children[1]),
            AstKind.Multiply => IsConstantBoundary(node.Children[0]) && IsAffineBoundary(node.Children[1]) ||
                                IsAffineBoundary(node.Children[0]) && IsConstantBoundary(node.Children[1]),
            AstKind.Divide => IsAffineBoundary(node.Children[0]) && IsConstantBoundary(node.Children[1]),
            _ => false
        };
    }

    private static bool IsConstantBoundary(AstNode node)
    {
        return node.Kind switch
        {
            AstKind.Number => true,
            AstKind.Negate => IsConstantBoundary(node.Children[0]),
            AstKind.Add or AstKind.Subtract or AstKind.Multiply or AstKind.Divide =>
                IsConstantBoundary(node.Children[0]) && IsConstantBoundary(node.Children[1]),
            _ => false
        };
    }

    private static bool IsInequalitySatisfied(RelationKind relation, double value)
    {
        return double.IsFinite(value) && relation switch
        {
            RelationKind.Less => value < 0,
            RelationKind.LessOrEqual => value < 0,
            RelationKind.Greater => value > 0,
            RelationKind.GreaterOrEqual => value > 0,
            _ => false
        };
    }

    private static void AppendVerticalGridLines(ImmutableArray<GraphFrameCommand>.Builder commands, SamplingViewport viewport, double majorStep, GraphPaint majorPaint, GraphPaint minorPaint, bool compactLines)
    {
        double minorStep = majorStep / 5;
        double first = Math.Floor(viewport.XRange.Minimum / minorStep) * minorStep;
        double last = Math.Ceiling(viewport.XRange.Maximum / minorStep) * minorStep;
        if (compactLines)
        {
            int lineCount = GridLineCount(first, last, minorStep);
            if (lineCount > 0)
            {
                commands.Add(new GridLineSeriesCommand(viewport.XRange, viewport.Width, viewport.Height, first, minorStep, majorStep, lineCount, IsVertical: true, majorPaint, minorPaint));
            }

            return;
        }

        for (int count = 0; count < 512; count++)
        {
            double value = first + count * minorStep;
            if (value > last + minorStep * 1e-10)
            {
                break;
            }

            double x = viewport.ToScreen(value, 0).X;
            AppendLine(commands, new GraphPoint(x, 0), new GraphPoint(x, viewport.Height), IsMajorGridValue(value, majorStep) ? majorPaint : minorPaint, compactLines);
        }
    }

    private static void AppendHorizontalGridLines(ImmutableArray<GraphFrameCommand>.Builder commands, SamplingViewport viewport, double majorStep, GraphPaint majorPaint, GraphPaint minorPaint, bool compactLines)
    {
        double minorStep = majorStep / 5;
        double first = Math.Floor(viewport.YRange.Minimum / minorStep) * minorStep;
        double last = Math.Ceiling(viewport.YRange.Maximum / minorStep) * minorStep;
        if (compactLines)
        {
            int lineCount = GridLineCount(first, last, minorStep);
            if (lineCount > 0)
            {
                commands.Add(new GridLineSeriesCommand(viewport.YRange, viewport.Width, viewport.Height, first, minorStep, majorStep, lineCount, IsVertical: false, majorPaint, minorPaint));
            }

            return;
        }

        for (int count = 0; count < 512; count++)
        {
            double value = first + count * minorStep;
            if (value > last + minorStep * 1e-10)
            {
                break;
            }

            double y = viewport.ToScreen(0, value).Y;
            AppendLine(commands, new GraphPoint(0, y), new GraphPoint(viewport.Width, y), IsMajorGridValue(value, majorStep) ? majorPaint : minorPaint, compactLines);
        }
    }

    private static bool IsMajorGridValue(double value, double majorStep)
    {
        double quotient = value / majorStep;
        return Math.Abs(quotient - Math.Round(quotient)) <= 1e-9;
    }

    private static int GridLineCount(double first, double last, double minorStep)
    {
        int count = 0;
        while (count < 512 && first + count * minorStep <= last + minorStep * 1e-10)
        {
            count++;
        }

        return count;
    }

    private static void AppendHorizontalAxis(ImmutableArray<GraphFrameCommand>.Builder commands, SamplingViewport viewport, double y, GraphPaint paint, bool compactLines)
    {
        double left = Math.Min(2, viewport.Width * 0.5);
        double right = Math.Max(left, viewport.Width - 2);
        AppendLine(commands, new GraphPoint(left, y), new GraphPoint(right, y), paint, compactLines);
        AppendLine(commands, new GraphPoint(right, y), new GraphPoint(right - 6, y - 6), paint, compactLines);
        AppendLine(commands, new GraphPoint(right, y), new GraphPoint(right - 6, y + 6), paint, compactLines);
    }

    private static void AppendVerticalAxis(ImmutableArray<GraphFrameCommand>.Builder commands, SamplingViewport viewport, double x, GraphPaint paint, bool compactLines)
    {
        double top = Math.Min(2, viewport.Height * 0.5);
        double bottom = Math.Max(top, viewport.Height - 2);
        AppendLine(commands, new GraphPoint(x, bottom), new GraphPoint(x, top), paint, compactLines);
        AppendLine(commands, new GraphPoint(x, top), new GraphPoint(x - 6, top + 6), paint, compactLines);
        AppendLine(commands, new GraphPoint(x, top), new GraphPoint(x + 6, top + 6), paint, compactLines);
    }

    private void AppendHorizontalLabels(ImmutableArray<GraphFrameCommand>.Builder tickLabels, ImmutableArray<GraphFrameCommand>.Builder axisAliases, SamplingViewport viewport, double step, double axisY, double verticalAxisX, bool hasVerticalAxis, GraphPaint fontPaint, GraphPaint backgroundPaint)
    {
        if (viewport.Width < 48 || viewport.Height < 24)
        {
            return;
        }

        AppendLabel(axisAliases, _options.GetAliasX(), new GraphPoint(viewport.Width - 7, axisY + 5), GraphTextAlignment.Center, fontPaint, backgroundPaint);
        double first = Math.Ceiling(viewport.XRange.Minimum / step) * step;
        for (int count = 0; count < 128; count++)
        {
            double value = first + count * step;
            if (value > viewport.XRange.Maximum + step * 1e-10)
            {
                break;
            }

            bool zero = Math.Abs(value) <= step * 1e-10;
            GraphPoint origin = zero && hasVerticalAxis ? new GraphPoint(verticalAxisX - 4, axisY + 2) : new GraphPoint(viewport.ToScreen(value, 0).X, axisY + 2);
            AppendLabel(tickLabels, FormatTick(zero ? 0 : value, step), origin, zero && hasVerticalAxis ? GraphTextAlignment.End : GraphTextAlignment.Center, fontPaint, backgroundPaint);
        }
    }

    private void AppendVerticalLabels(ImmutableArray<GraphFrameCommand>.Builder tickLabels, ImmutableArray<GraphFrameCommand>.Builder axisAliases, SamplingViewport viewport, double step, double axisX, GraphPaint fontPaint, GraphPaint backgroundPaint)
    {
        if (viewport.Width < 48 || viewport.Height < 24)
        {
            return;
        }

        AppendLabel(axisAliases, _options.GetAliasY(), new GraphPoint(axisX - 11, -0.98046875), GraphTextAlignment.End, fontPaint, backgroundPaint);
        double first = Math.Ceiling(viewport.YRange.Minimum / step) * step;
        for (int count = 0; count < 128; count++)
        {
            double value = first + count * step;
            if (value > viewport.YRange.Maximum + step * 1e-10)
            {
                break;
            }

            if (Math.Abs(value) <= step * 1e-10)
            {
                continue;
            }

            AppendLabel(tickLabels, FormatTick(value, step), new GraphPoint(axisX - 4, viewport.ToScreen(0, value).Y - 7.98046875), GraphTextAlignment.End, fontPaint, backgroundPaint);
        }
    }

    private static void AppendLabel(ImmutableArray<GraphFrameCommand>.Builder commands, string text, GraphPoint origin, GraphTextAlignment alignment, GraphPaint fontPaint, GraphPaint backgroundPaint)
    {
        var glyph = new GlyphCommand(text, origin, "Segoe UI", 12, fontPaint, alignment, GraphFontStyle.Italic);
        commands.Add(new GlyphBackgroundCommand(glyph, backgroundPaint));
        commands.Add(glyph);
    }

    private static void AppendLine(ImmutableArray<GraphFrameCommand>.Builder commands, GraphPoint start, GraphPoint end, GraphPaint paint, bool compact = false)
    {
        commands.Add(compact
            ? new StrokeLineCommand(start, end, paint)
            : new StrokePathCommand(new GraphPath([start, end]), paint));
    }

    private GraphStatus ScaleSingleAxis(bool xAxis, double scale)
    {
        VerifyAccess();
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

        InvalidateGeometry(transformExisting: true);
        return GraphStatus.Ok;
    }

    private void InvalidateGeometry(bool transformExisting)
    {
        if (!transformExisting)
        {
            _prepared = null;
        }

        // Range changes can arrive much faster than the compositor paints. Keep
        // the sampled user-space geometry and build the current screen-space
        // scene lazily instead of allocating for every wheel or pointer event.
        _frame = null;
    }

    private void RebuildFrameFromPrepared()
    {
        _frame = _prepared is null ? null : BuildFrame(_prepared);
    }

    private SamplingViewport EffectiveViewport()
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

    private static void SearchComponent(ImmutableArray<GraphPoint> points, GraphEquationKind kind, SamplingViewport viewport, GraphPoint target, int equationIndex, ref ManagedGraphRendererClosestCandidate best)
    {
        for (int index = 1; index < points.Length; index++)
        {
            GraphPoint userLeft = points[index - 1];
            GraphPoint userRight = points[index];
            double parameterLeft = TraceParameter(kind, userLeft);
            double parameterRight = TraceParameter(kind, userRight);
            SearchSegment(userLeft, userRight, parameterLeft, parameterRight, viewport, target, equationIndex, ref best);
        }
    }

    private static double TraceParameter(GraphEquationKind kind, GraphPoint point)
    {
        return kind switch
        {
            GraphEquationKind.ExplicitY => point.X,
            GraphEquationKind.InverseX => point.Y,
            _ => double.NaN
        };
    }

    private static void SearchSegment(GraphPoint userLeft, GraphPoint userRight, double parameterLeft, double parameterRight, SamplingViewport viewport, GraphPoint target, int equationIndex, ref ManagedGraphRendererClosestCandidate best)
    {
        GraphPoint left = viewport.ToScreen(userLeft.X, userLeft.Y);
        GraphPoint right = viewport.ToScreen(userRight.X, userRight.Y);
        double dx = right.X - left.X;
        double dy = right.Y - left.Y;
        double denominator = dx * dx + dy * dy;
        double amount = denominator <= double.Epsilon ? 0 : Math.Clamp(((target.X - left.X) * dx + (target.Y - left.Y) * dy) / denominator, 0, 1);
        GraphPoint screen = new(left.X + amount * dx, left.Y + amount * dy);
        double distanceX = screen.X - target.X;
        double distanceY = screen.Y - target.Y;
        double distanceSquared = distanceX * distanceX + distanceY * distanceY;
        if (distanceSquared >= best.DistanceSquared)
        {
            return;
        }

        best = new ManagedGraphRendererClosestCandidate(true, equationIndex, distanceSquared, screen, new GraphPoint(userLeft.X + amount * (userRight.X - userLeft.X), userLeft.Y + amount * (userRight.Y - userLeft.Y)), double.IsNaN(parameterLeft) ? double.NaN : parameterLeft + amount * (parameterRight - parameterLeft));
    }

    private static GraphPath ToScreenPath(ImmutableArray<GraphPoint> points, SamplingViewport viewport, bool isClosed)
    {
        var screenPoints = ImmutableArray.CreateBuilder<GraphPoint>(points.Length);
        foreach (GraphPoint point in points)
        {
            screenPoints.Add(viewport.ToScreen(point.X, point.Y));
        }

        return new GraphPath(screenPoints.MoveToImmutable(), isClosed);
    }

    private static double EvaluateBoundaryField(CompiledGraphEquation definition, double[] values, double x, double y, EvalTrigUnitMode trigMode)
    {
        SetVariable(values, definition.XIndex, x);
        SetVariable(values, definition.YIndex, y);
        EvaluationValue result = definition.BoundaryProgram.Evaluate(values, trigMode);
        return result.IsFinite ? result.Value : double.NaN;
    }

    private static double EvaluateRegion(CompiledGraphEquation definition, double[] values, double x, double y, EvalTrigUnitMode trigMode)
    {
        SetVariable(values, definition.XIndex, x);
        SetVariable(values, definition.YIndex, y);
        ExpressionProgram regionProgram = definition.RegionProgram ?? throw new InvalidOperationException("A region evaluator is required for an inequality.");
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

    private static SampleState ToSampleState(EvaluationState state)
    {
        return state switch
        {
            EvaluationState.NonReal => SampleState.NonReal,
            EvaluationState.Overflow => SampleState.Overflow,
            EvaluationState.BudgetExceeded => SampleState.BudgetExceeded,
            _ => SampleState.Undefined
        };
    }

    private static AxisRange Scale(AxisRange range, double center, double scale)
    {
        return new AxisRange(center + (range.Minimum - center) * scale, center + (range.Maximum - center) * scale);
    }

    private static bool IsAllowed(AxisRange range)
    {
        return range.IsFiniteAndOrdered && range.Length >= MinimumRangeLength && range.Length <= MaximumRangeLength;
    }

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
        return maximum * Math.Sqrt(1 + ratio * ratio);
    }

    private void ThrowIfSamplingCancelled(long generation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (generation != 0 && generation != Volatile.Read(ref _latestRequestedGeneration))
        {
            throw new OperationCanceledException("Graph preparation was superseded by a newer viewport.", cancellationToken);
        }
    }

    private async Task ProcessPrepareRequestsAsync()
    {
        try
        {
            while (await _prepareRequests.Reader.WaitToReadAsync(_lifetimeCancellation.Token).ConfigureAwait(false))
            {
                ManagedGraphPrepareRequest? request = null;
                while (_prepareRequests.Reader.TryRead(out ManagedGraphPrepareRequest? candidate))
                {
                    request = candidate;
                }

                if (request is null || request.Generation != Volatile.Read(ref _latestRequestedGeneration))
                {
                    continue;
                }

                GraphPipelineDiagnostics.RecordWorkerStart(request.Generation);
                ManagedGraphPrepareResult result = PrepareInBackground(request);
                GraphPipelineDiagnostics.RecordWorkerCompletion(request.Generation, result.Status);
                if (_prepareResults.Writer.TryWrite(result))
                {
                    GraphPipelineDiagnostics.RecordPublication(request.Generation);
                }
            }
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            while (_prepareRequests.Reader.TryRead(out _))
            {
            }

            DisposeLifetimeCancellation();
        }
    }

    private void EnsurePrepareWorkerStarted()
    {
        if (Volatile.Read(ref _prepareWorker) is not null)
        {
            return;
        }

        // Launch before publishing the request so a synchronously-completed
        // channel read can never execute sampling on the UI caller.
        Volatile.Write(ref _prepareWorker, Task.Run(ProcessPrepareRequestsAsync));
    }

    private ManagedGraphPrepareResult PrepareInBackground(ManagedGraphPrepareRequest request)
    {
        CancellationTokenSource? timeout = null;
        CancellationToken cancellationToken = _lifetimeCancellation.Token;
        if (request.MaximumMilliseconds > 0)
        {
            timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(Math.Min(request.MaximumMilliseconds, int.MaxValue)));
            cancellationToken = timeout.Token;
        }

        try
        {
            PreparedGraph prepared = SampleSnapshot(
                request.Snapshot,
                request.Viewport,
                request.SamplingScale,
                request.TrigMode,
                request.Generation,
                cancellationToken);
            return new ManagedGraphPrepareResult(request.Generation, GraphStatus.Ok, prepared);
        }
        catch (OperationCanceledException)
        {
            GraphStatus status = _lifetimeCancellation.IsCancellationRequested || request.Generation != Volatile.Read(ref _latestRequestedGeneration)
                ? GraphStatus.Cancelled
                : GraphStatus.Timeout;
            return new ManagedGraphPrepareResult(request.Generation, status, null);
        }
        catch (OutOfMemoryException)
        {
            return new ManagedGraphPrepareResult(request.Generation, GraphStatus.OutOfMemory, null);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or ArithmeticException)
        {
            Trace.TraceError("Graph preparation generation {0} failed: {1}", request.Generation, exception);
            return new ManagedGraphPrepareResult(request.Generation, GraphStatus.Fail, null);
        }
        finally
        {
            timeout?.Dispose();
        }
    }

    public void Dispose()
    {
        VerifyAccess();
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _lifetimeCancellation.Cancel();
        _prepareRequests.Writer.TryComplete();
        _prepareResults.Writer.TryComplete();
        while (_prepareResults.Reader.TryRead(out _))
        {
        }

        _snapshot = GraphSnapshot.Empty;
        _prepared = null;
        _frame = null;
        _equationCommandSource = null;
        _equationCommands = ImmutableArray<GraphFrameCommand>.Empty;
        _contentCommandSource = null;
        _contentCommands = ImmutableArray<GraphFrameCommand>.Empty;
        Volatile.Write(ref _latestCompletedGeneration, Volatile.Read(ref _latestRequestedGeneration));
        GraphPipelineDiagnostics.RecordRendererDisposed();
        Task? prepareWorker = Volatile.Read(ref _prepareWorker);
        if (prepareWorker is null || prepareWorker.IsCompleted)
        {
            DisposeLifetimeCancellation();
        }

        GC.SuppressFinalize(this);
    }

    private void DisposeLifetimeCancellation()
    {
        if (Interlocked.Exchange(ref _lifetimeCancellationDisposed, 1) == 0)
        {
            _lifetimeCancellation.Dispose();
        }
    }

    private void VerifyAccess()
    {
        if (Environment.CurrentManagedThreadId != _ownerThreadId)
        {
            throw new InvalidOperationException("Graph renderer access must remain on its owning thread.");
        }
    }
}
