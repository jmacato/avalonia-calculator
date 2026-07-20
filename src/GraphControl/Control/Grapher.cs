using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Graphing;
using Graphing.Analyzer;
using Graphing.Renderer;
using AvaloniaColor = Avalonia.Media.Color;

namespace GraphControl;

public sealed class Grapher : Control, INotifyPropertyChanged, IDisposable
{
    private static readonly TimeSpan EquationPlotDelay = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan InteractionPlotDelay = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan PanInertiaVelocityLifetime = TimeSpan.FromMilliseconds(100);
    private const double MaximumWheelDeltaPerFrame = 2;
    // NInput's default translation deceleration is 0.07 HIMETRIC/ms².
    private const double PanInertiaDeceleration = 0.07 * (96.0 / 2_540.0) * 1_000_000.0;
    private const double PanInertiaMaximumSpeed = 1_800;
    private const double PanInertiaMinimumStartSpeed = 80;
    private const double PanInertiaStopSpeed = 12;
    private const double PanVelocityBlend = 0.45;
    private const double MaximumPanInertiaFrameSeconds = 0.05;
    private const double InteractionCoverageMarginRatio = 0.42;
    private const double MinimumRangeLength = 1e-12;
    private const double MaximumRangeLength = 1e12;
    private static readonly ImmutableSolidColorBrush TraceBrush = new(AvaloniaColor.FromRgb(0x00, 0x63, 0xB1));
    private static readonly ImmutablePen TraceOutline = new(0xFFFFFFFF, 1);
    public static readonly StyledProperty<string> FormulaProperty = AvaloniaProperty.Register<Grapher, string>(nameof(Formula), string.Empty);
    public static readonly StyledProperty<bool> ForceProportionalAxesProperty = AvaloniaProperty.Register<Grapher, bool>(nameof(ForceProportionalAxes), true);
    public static readonly StyledProperty<bool> UseCommaDecimalSeparatorProperty = AvaloniaProperty.Register<Grapher, bool>(nameof(UseCommaDecimalSeparator));
    public static readonly StyledProperty<AvaloniaColor> AxesColorProperty = AvaloniaProperty.Register<Grapher, AvaloniaColor>(nameof(AxesColor), Colors.Black);
    public static readonly StyledProperty<AvaloniaColor> GraphBackgroundProperty = AvaloniaProperty.Register<Grapher, AvaloniaColor>(nameof(GraphBackground), Colors.White);
    public static readonly StyledProperty<AvaloniaColor> GridLinesColorProperty = AvaloniaProperty.Register<Grapher, AvaloniaColor>(nameof(GridLinesColor), AvaloniaColor.FromRgb(0xC6, 0xC6, 0xC6));
    public static readonly StyledProperty<double> LineWidthProperty = AvaloniaProperty.Register<Grapher, double>(nameof(LineWidth), 2);
    private readonly IMathSolver _solver;
    private readonly IGraph _graph;
    private readonly LegacyEquationImporter _legacyEquationImporter;
    private readonly FunctionAnalysisWorker _analysisWorker = new();
    private readonly AvaloniaGraphRenderCache _renderCache = new();
    private readonly DispatcherTimer _equationPlotTimer;
    private readonly AnimationFrameTimer _graphPreparationTimer;
    private readonly DispatcherTimer _interactionSettleTimer;
    private readonly Action<TimeSpan> _interactionFrameCallback;
    private readonly Dictionary<int, GrapherPointerState> _activePointers = [];
    private double _pendingWheelDelta;
    private Point _pendingWheelPosition;
    private bool _prepareGraphOnAttach;
    private bool _isAttached;
    private bool _interactionFrameRequested;
    private bool _viewportRenderPending;
    private long _lastInteractionTimestamp;
    private Vector _panVelocity;
    private long _panVelocitySampleTimestamp;
    private long _panLastMotionTimestamp;
    private long _panInertiaTimestamp;
    private bool _panInertiaEligible;
    private bool _panInertiaActive;
    private bool _resizePreparePending;
    private bool _hasSettledViewport;
    private double _settledXMinimum;
    private double _settledXMaximum;
    private double _settledYMinimum;
    private double _settledYMaximum;
    private bool _hasInteractionViewport;
    private bool _rendererMatchesInteractionViewport;
    private double _interactionXMinimum;
    private double _interactionXMaximum;
    private double _interactionYMinimum;
    private double _interactionYMaximum;
    private bool _settlePreparationPending;
    private bool _activeTracing;
    private Point _traceLocation = new(double.NaN, double.NaN);
    private int _errorCode;
    private int _errorType;
    private bool _updatingFormulaFromCollection;
    private bool _replacingEquations;
    private IReadOnlyList<IEquation> _initializedEquations = Array.Empty<IEquation>();
    private IReadOnlyList<Equation> _parsedEquations = Array.Empty<Equation>();
    private int _disposed;
    static Grapher()
    {
        AffectsRender<Grapher>(FormulaProperty, ForceProportionalAxesProperty, AxesColorProperty, GraphBackgroundProperty, GridLinesColorProperty, LineWidthProperty);
    }

    public Grapher()
    {
        Focusable = true;
        ClipToBounds = true;
        _solver = MathSolver.CreateMathSolver();
        _solver.ParsingOptions().SetFormatType(FormatType.MathML);
        _solver.FormatOptions().SetFormatType(FormatType.MathML);
        _solver.FormatOptions().SetMathMLPrefix("mml");
        _graph = _solver.CreateGrapher();
        _legacyEquationImporter = new LegacyEquationImporter();
        _equationPlotTimer = new DispatcherTimer
        {
            Interval = EquationPlotDelay
        };
        _equationPlotTimer.Tick += OnEquationPlotTimerTick;
        _graphPreparationTimer = new AnimationFrameTimer(OnGraphPreparationFrame);
        _interactionSettleTimer = new DispatcherTimer
        {
            Interval = InteractionPlotDelay
        };
        _interactionSettleTimer.Tick += OnInteractionSettleTimerTick;
        _interactionFrameCallback = OnInteractionFrame;
        Equations = [];
        Equations.CollectionChanged += OnEquationsChanged;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        _isAttached = true;
        if (_prepareGraphOnAttach)
        {
            _prepareGraphOnAttach = false;
            RenderPreparedGraph();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        if (Volatile.Read(ref _disposed) == 0 && _hasInteractionViewport)
        {
            _ = _graph.GetRenderer().SetDisplayRanges(_interactionXMinimum, _interactionXMaximum, _interactionYMinimum, _interactionYMaximum);
            _prepareGraphOnAttach = true;
        }

        _equationPlotTimer.Stop();
        _graphPreparationTimer.Detach();
        _interactionSettleTimer.Stop();
        _interactionFrameRequested = false;
        _viewportRenderPending = false;
        _pendingWheelDelta = 0;
        ResetPanInertia();
        _hasInteractionViewport = false;
        _rendererMatchesInteractionViewport = false;
        _settlePreparationPending = false;
        _resizePreparePending = false;
        _activePointers.Clear();
        _renderCache.Clear();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnSizeChanged(e);
        if (!_isAttached || e.NewSize.Width <= 0 || e.NewSize.Height <= 0)
        {
            return;
        }

        // Record a current-size scene immediately, then rebuild sampled
        // geometry once the resize settles.
        _resizePreparePending = true;
        _viewportRenderPending = true;
        _lastInteractionTimestamp = Stopwatch.GetTimestamp();
        InvalidateVisual();
        RequestInteractionFrame();
        EnsureInteractionSettlement();
    }

    public string Formula { get => GetValue(FormulaProperty); set => SetValue(FormulaProperty, value ?? string.Empty); }
    public bool ForceProportionalAxes { get => GetValue(ForceProportionalAxesProperty); set => SetValue(ForceProportionalAxesProperty, value); }
    public bool UseCommaDecimalSeparator { get => GetValue(UseCommaDecimalSeparatorProperty); set => SetValue(UseCommaDecimalSeparatorProperty, value); }
    public AvaloniaColor AxesColor { get => GetValue(AxesColorProperty); set => SetValue(AxesColorProperty, value); }
    public AvaloniaColor GraphBackground { get => GetValue(GraphBackgroundProperty); set => SetValue(GraphBackgroundProperty, value); }
    public AvaloniaColor GridLinesColor { get => GetValue(GridLinesColorProperty); set => SetValue(GridLinesColorProperty, value); }
    public double LineWidth { get => GetValue(LineWidthProperty); set => SetValue(LineWidthProperty, value); }
    public EquationCollection Equations { get; }
    public IReadOnlyDictionary<string, Variable> Variables { get; private set; } = new Dictionary<string, Variable>(StringComparer.OrdinalIgnoreCase);
    internal bool IsGraphPreparationDeferred => _prepareGraphOnAttach;

    public bool ActiveTracing
    {
        get => _activeTracing;
        set
        {
            if (_activeTracing == value)
            {
                return;
            }

            _activeTracing = value;
            TracingChanged?.Invoke(this, new TracingChangedEventArgs(value));
            OnPropertyChanged();
            InvalidateVisual();
        }
    }

    public Point TraceLocation
    {
        get => _traceLocation;
        private set
        {
            if (_traceLocation == value)
            {
                return;
            }

            _traceLocation = value;
            OnPropertyChanged();
        }
    }

    public int ErrorCode
    {
        get => _errorCode;
        private set
        {
            _errorCode = value;
            OnPropertyChanged();
        }
    }

    public int ErrorType
    {
        get => _errorType;
        private set
        {
            _errorType = value;
            OnPropertyChanged();
        }
    }

    public int TrigUnitMode
    {
        get => (int)_solver.EvalOptions().GetTrigUnitMode();
        set
        {
            _solver.EvalOptions().SetTrigUnitMode((EvalTrigUnitMode)value);
            PlotGraph(keepCurrentView: true);
        }
    }

    public double XAxisMin { get => _graph.GetOptions().GetDefaultXRange().Minimum; set => SetDefaultRange(xAxis: true, minimum: value, maximum: XAxisMax); }
    public double XAxisMax { get => _graph.GetOptions().GetDefaultXRange().Maximum; set => SetDefaultRange(xAxis: true, minimum: XAxisMin, maximum: value); }
    public double YAxisMin { get => _graph.GetOptions().GetDefaultYRange().Minimum; set => SetDefaultRange(xAxis: false, minimum: value, maximum: YAxisMax); }
    public double YAxisMax { get => _graph.GetOptions().GetDefaultYRange().Maximum; set => SetDefaultRange(xAxis: false, minimum: YAxisMin, maximum: value); }

    public event EventHandler<TracingChangedEventArgs>? TracingChanged;
    public event EventHandler<TracingValueChangedEventArgs>? TracingValueChanged;
    public event EventHandler<PointerValueChangedEventArgs>? PointerValueChanged;
    public event EventHandler<GraphViewChangedEventArgs>? GraphViewChanged;
    public event EventHandler? GraphPlotted;
    public event EventHandler? VariablesUpdated;
    public new event PropertyChangedEventHandler? PropertyChanged;
    public void ZoomFromCenter(double scale)
    {
        if (TryScaleInteractionViewport(0, 0, scale))
        {
            MarkInteraction(viewportAlreadyChanged: true);
        }
    }

    public void ResetGrid()
    {
        CancelInteractionViewport();
        if (_graph.GetRenderer().ResetRange().Succeeded)
        {
            RenderPreparedGraph();
            GraphViewChanged?.Invoke(this, new GraphViewChangedEventArgs(GraphViewChangedReason.Reset));
        }
    }

    public void GetDisplayRanges(out double xMin, out double xMax, out double yMin, out double yMax)
    {
        if (_hasInteractionViewport)
        {
            xMin = _interactionXMinimum;
            xMax = _interactionXMaximum;
            yMin = _interactionYMinimum;
            yMax = _interactionYMaximum;
            return;
        }

        _graph.GetRenderer().GetDisplayRanges(out xMin, out xMax, out yMin, out yMax);
    }

    public void SetDisplayRanges(double xMin, double xMax, double yMin, double yMax)
    {
        CancelInteractionViewport();
        if (_graph.GetRenderer().SetDisplayRanges(xMin, xMax, yMin, yMax).Succeeded)
        {
            RenderPreparedGraph();
            GraphViewChanged?.Invoke(this, new GraphViewChangedEventArgs(GraphViewChangedReason.Manipulation));
        }
    }

    public void SetVariable(string variableName, double newValue)
    {
        _graph.SetArgValue(variableName, newValue);
        if (Variables.TryGetValue(variableName, out Variable? variable))
        {
            variable.Value = newValue;
        }

        RenderPreparedGraph();
    }

    public void ReplaceEquations(IEnumerable<Equation> equations)
    {
        ArgumentNullException.ThrowIfNull(equations);
        Equation[] replacement = equations.ToArray();
        _replacingEquations = true;
        try
        {
            Equations.Clear();
            foreach (Equation equation in replacement)
            {
                Equations.Add(equation);
            }
        }
        finally
        {
            _replacingEquations = false;
        }

        SyncFormulaFromEquations();
    }

    public void PlotGraph(bool keepCurrentView)
    {
        CancelInteractionViewport();
        ApplyOptions();
        IExpression? expression = ParseEnabledEquations(out int errorCode, out int errorType);
        if (expression is null)
        {
            SetErrors(errorCode, errorType);
            GraphPlotted?.Invoke(this, EventArgs.Empty);
            return;
        }

        IReadOnlyList<IEquation>? initialized = _graph.TryInitialize(expression);
        if (initialized is null)
        {
            _solver.HRErrorToErrorInfo(_graph.GetInitializationError(), out errorCode, out errorType);
            SetErrors(errorCode, errorType);
            GraphPlotted?.Invoke(this, EventArgs.Empty);
            return;
        }

        _initializedEquations = initialized;
        BindEquations(initialized);
        if (!keepCurrentView)
        {
            _graph.GetRenderer().ResetRange();
        }

        ErrorCode = 0;
        ErrorType = 0;
        UpdateVariables();
        RenderPreparedGraph();
        GraphPlotted?.Invoke(this, EventArgs.Empty);
    }

    public KeyGraphFeaturesInfo? AnalyzeEquation(Equation equation) =>
        FunctionAnalysisWorker.AnalyzeCore(
            CaptureFunctionAnalysisRequest(equation),
            CancellationToken.None);

    public Task<KeyGraphFeaturesInfo> AnalyzeEquationAsync(
        Equation equation,
        CancellationToken cancellationToken = default) =>
        _analysisWorker.AnalyzeAsync(
            CaptureFunctionAnalysisRequest(equation),
            cancellationToken);

    private FunctionAnalysisRequest CaptureFunctionAnalysisRequest(Equation equation)
    {
        ArgumentNullException.ThrowIfNull(equation);
        KeyValuePair<string, double>[] variables = Variables
            .Select(static pair => new KeyValuePair<string, double>(pair.Key, pair.Value.Value))
            .ToArray();
        LocalizationType localization = UseCommaDecimalSeparator
            ? LocalizationType.DecimalCommaAndListSemicolon
            : LocalizationType.DecimalPointAndListComma;
        string mathMl = EquationMathMl(equation);
        return new FunctionAnalysisRequest(
            mathMl,
            _solver.EvalOptions().GetTrigUnitMode(),
            localization,
            variables);
    }

    public KeyGraphFeaturesInfo? AnalyzeFirstEquation()
    {
        if (Equations.FirstOrDefault(equation => !string.IsNullOrWhiteSpace(EquationMathMl(equation))) is { } equation)
        {
            return AnalyzeEquation(equation);
        }

        if (_initializedEquations.Count == 0 || !_initializedEquations[0].TrySelectEquation())
        {
            return null;
        }

        return AnalyzeSelectedEquation();
    }

    private KeyGraphFeaturesInfo? AnalyzeSelectedEquation()
    {
        var analyzer = _graph.GetAnalyzer();
        if (!analyzer.CanFunctionAnalysisBePerformed(out bool variableIsNotX) || variableIsNotX)
        {
            return null;
        }

        if (analyzer.PerformFunctionAnalysis((uint)Graphing.Analyzer.PerformAnalysisType.All) != GraphStatus.Ok)
        {
            return null;
        }

        return new KeyGraphFeaturesInfo(_solver.Analyze(analyzer));
    }

    public async Task<ReadOnlyMemory<byte>> GetGraphBitmapAsync()
    {
        await Task.Yield();
        GraphStatus status = _graph.GetRenderer().GetBitmap(out IBitmap? bitmap, out _);
        return status.Succeeded && bitmap is not null ? bitmap.GetData() : ReadOnlyMemory<byte>.Empty;
    }

    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        GraphPipelineDiagnostics.RecordRender();
        base.Render(context);
        IGraphRenderer renderer = _graph.GetRenderer();
        EnsureSize(renderer);
        using var target = new AvaloniaGraphDrawingTarget(context, _renderCache);
        GraphStatus status = renderer.Draw(target, out _);
        if (status.Failed)
        {
            context.DrawRectangle(_renderCache.Brush(GraphBackground.ToGraphColor()), null, Bounds);
        }

        if (ActiveTracing && double.IsFinite(TraceLocation.X) && double.IsFinite(TraceLocation.Y))
        {
            context.DrawEllipse(TraceBrush, TraceOutline, TraceLocation, 4, 4);
        }

    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == FormulaProperty && !_updatingFormulaFromCollection)
        {
            PlotGraph(keepCurrentView: false);
        }
        else if (change.Property == UseCommaDecimalSeparatorProperty)
        {
            LocalizationType localization = UseCommaDecimalSeparator ? LocalizationType.DecimalCommaAndListSemicolon : LocalizationType.DecimalPointAndListComma;
            _solver.ParsingOptions().SetLocalizationType(localization);
            _solver.FormatOptions().SetLocalizationType(localization);
            _legacyEquationImporter.SetLocalizationType(localization);
            PlotGraph(keepCurrentView: true);
        }
        else if (change.Property == ForceProportionalAxesProperty || change.Property == AxesColorProperty || change.Property == GraphBackgroundProperty || change.Property == GridLinesColorProperty || change.Property == LineWidthProperty)
        {
            ApplyOptions();
            if (change.Property == LineWidthProperty)
            {
                foreach (IEquation equation in _initializedEquations)
                {
                    IEquationOptions options = equation.GetGraphEquationOptions();
                    options.SetLineWidth((float)LineWidth);
                    options.SetSelectedEquationLineWidth((float)(LineWidth + (LineWidth <= 2 ? 1 : 2)));
                }
            }

            RenderPreparedGraph();
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        Dispatcher.UIThread.VerifyAccess();
        base.OnPointerWheelChanged(e);
        if (Bounds.Width <= 0 || Bounds.Height <= 0 || e.Delta.Y == 0 || !double.IsFinite(e.Delta.Y))
        {
            return;
        }

        ResetPanInertia();
        // Browser trackpads can enqueue hundreds of wheel events before Wasm
        // gets another paint. Keep only one bounded display-frame delta. A
        // trailing backlog makes the graph continue moving long after the
        // fingers stopped and can retain a frame storm on Safari.
        _pendingWheelDelta = Math.Clamp(_pendingWheelDelta + e.Delta.Y, -MaximumWheelDeltaPerFrame, MaximumWheelDeltaPerFrame);
        _pendingWheelPosition = e.GetPosition(this);
        MarkInteraction();
        e.PreventGestureRecognition();
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        Dispatcher.UIThread.VerifyAccess();
        base.OnPointerPressed(e);
        if (ApplyPendingPointerManipulation())
        {
            _viewportRenderPending = true;
            _lastInteractionTimestamp = Stopwatch.GetTimestamp();
            RequestInteractionFrame();
        }

        ResetPanInertia();
        _interactionSettleTimer.Stop();
        Point point = e.GetPosition(this);
        _activePointers[e.Pointer.Id] = new GrapherPointerState(point, point);
        ResetPointerBaselines();
        if (_activePointers.Count == 1)
        {
            BeginPanVelocityTracking();
        }

        e.Pointer.Capture(this);
        e.PreventGestureRecognition();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        Dispatcher.UIThread.VerifyAccess();
        base.OnPointerMoved(e);
        Point point = e.GetPosition(this);
        if (_activePointers.TryGetValue(e.Pointer.Id, out GrapherPointerState state))
        {
            _activePointers[e.Pointer.Id] = state with
            {
                Current = point
            };
            MarkInteraction();
            e.PreventGestureRecognition();
            e.Handled = true;
            return;
        }

        if (ActiveTracing)
        {
            PointerValueChanged?.Invoke(this, new PointerValueChangedEventArgs(point));
            UpdateTracing(point);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        Dispatcher.UIThread.VerifyAccess();
        base.OnPointerReleased(e);
        EndPointer(e.Pointer, e.GetPosition(this), allowInertia: true);
        e.PreventGestureRecognition();
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        Dispatcher.UIThread.VerifyAccess();
        base.OnPointerCaptureLost(e);
        EndPointer(e.Pointer, null, allowInertia: false);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnKeyDown(e);
        ResetPanInertia();
        double amount = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 0.1 : 0.02;
        bool changed = e.Key switch
        {
            Key.Left => TryMoveInteractionViewport(-amount, 0),
            Key.Right => TryMoveInteractionViewport(amount, 0),
            Key.Down => TryMoveInteractionViewport(0, -amount),
            Key.Up => TryMoveInteractionViewport(0, amount),
            Key.Add or Key.OemPlus => TryScaleInteractionViewport(0, 0, 0.9),
            Key.Subtract or Key.OemMinus => TryScaleInteractionViewport(0, 0, 1.1),
            Key.Home => _graph.GetRenderer().ResetRange().Succeeded,
            _ => false
        };
        if (changed)
        {
            if (e.Key == Key.Home)
            {
                ClearInteractionViewport();
                RenderPreparedGraph();
                GraphViewChanged?.Invoke(this, new GraphViewChangedEventArgs(GraphViewChangedReason.Reset));
            }
            else
            {
                MarkInteraction(viewportAlreadyChanged: true);
            }

            e.Handled = true;
        }
    }

    private bool ApplyPendingPointerManipulation()
    {
        if (_activePointers.Count >= 2)
        {
            CancelPanVelocityTracking();
            if (!TryGetFirstTwoPointers(out int firstId, out GrapherPointerState first, out int secondId, out GrapherPointerState second))
            {
                return false;
            }

            Point previousCenter = Midpoint(first.Applied, second.Applied);
            Point currentCenter = Midpoint(first.Current, second.Current);
            double previousDistance = Distance(first.Applied, second.Applied);
            double currentDistance = Distance(first.Current, second.Current);
            _activePointers[firstId] = first with
            {
                Applied = first.Current
            };
            _activePointers[secondId] = second with
            {
                Applied = second.Current
            };
            bool changed = TryPanInteractionViewport(currentCenter.X - previousCenter.X, currentCenter.Y - previousCenter.Y);
            if (previousDistance > 0 && currentDistance > 0)
            {
                double centerX = (2 * currentCenter.X / Math.Max(1, Bounds.Width)) - 1;
                double centerY = 1 - (2 * currentCenter.Y / Math.Max(1, Bounds.Height));
                changed |= TryScaleInteractionViewport(centerX, centerY, previousDistance / currentDistance);
            }

            return changed;
        }

        if (_activePointers.Count == 1 && TryGetFirstPointer(out int pointerId, out GrapherPointerState pointer))
        {
            double deltaX = pointer.Current.X - pointer.Applied.X;
            double deltaY = pointer.Current.Y - pointer.Applied.Y;
            _activePointers[pointerId] = pointer with
            {
                Applied = pointer.Current
            };
            bool changed = TryPanInteractionViewport(deltaX, deltaY);
            if (changed && _panInertiaEligible)
            {
                RecordPanVelocity(deltaX, deltaY);
            }

            return changed;
        }

        return false;
    }

    private void EndPointer(IPointer pointer, Point? finalPosition, bool allowInertia)
    {
        bool wasSinglePointer = _activePointers.Count == 1;
        bool hadPointer = _activePointers.TryGetValue(pointer.Id, out GrapherPointerState state);
        if (!hadPointer)
        {
            return;
        }

        if (hadPointer && finalPosition is { } point)
        {
            _activePointers[pointer.Id] = state with
            {
                Current = point
            };
        }

        bool changed = ApplyPendingPointerManipulation();
        _activePointers.Remove(pointer.Id);
        pointer.Capture(null);
        ResetPointerBaselines();
        if (allowInertia && hadPointer && wasSinglePointer && _activePointers.Count == 0)
        {
            StartPanInertia();
        }
        else if (_activePointers.Count == 1)
        {
            BeginPanVelocityTracking();
        }
        else
        {
            CancelPanVelocityTracking();
        }

        if (changed || _hasInteractionViewport)
        {
            MarkInteraction(viewportAlreadyChanged: changed);
        }
    }

    private void ResetPointerBaselines()
    {
        if (_activePointers.Count >= 2 && TryGetFirstTwoPointers(out int firstId, out GrapherPointerState first, out int secondId, out GrapherPointerState second))
        {
            _activePointers[firstId] = first with
            {
                Applied = first.Current
            };
            _activePointers[secondId] = second with
            {
                Applied = second.Current
            };
            return;
        }

        if (_activePointers.Count == 1 && TryGetFirstPointer(out int pointerId, out GrapherPointerState pointer))
        {
            _activePointers[pointerId] = pointer with
            {
                Applied = pointer.Current
            };
        }
    }

    private bool TryGetFirstPointer(out int pointerId, out GrapherPointerState pointer)
    {
        Dictionary<int, GrapherPointerState>.Enumerator enumerator = _activePointers.GetEnumerator();
        if (enumerator.MoveNext())
        {
            pointerId = enumerator.Current.Key;
            pointer = enumerator.Current.Value;
            return true;
        }

        pointerId = default;
        pointer = default;
        return false;
    }

    private bool TryGetFirstTwoPointers(out int firstId, out GrapherPointerState first, out int secondId, out GrapherPointerState second)
    {
        Dictionary<int, GrapherPointerState>.Enumerator enumerator = _activePointers.GetEnumerator();
        if (enumerator.MoveNext())
        {
            firstId = enumerator.Current.Key;
            first = enumerator.Current.Value;
            if (enumerator.MoveNext())
            {
                secondId = enumerator.Current.Key;
                second = enumerator.Current.Value;
                return true;
            }
        }

        firstId = default;
        first = default;
        secondId = default;
        second = default;
        return false;
    }

    private void UpdateTracing(Point point)
    {
        GraphStatus status = _graph.GetRenderer().GetClosePointData(point.X, point.Y, 0.01, out _, out float screenX, out float screenY, out double x, out double y, out _, out _, out _);
        if (status == GraphStatus.Ok)
        {
            TraceLocation = new Point(screenX, screenY);
            TracingValueChanged?.Invoke(this, new TracingValueChangedEventArgs(x, y));
            if (ActiveTracing)
            {
                InvalidateVisual();
            }
        }
        else
        {
            TraceLocation = new Point(double.NaN, double.NaN);
        }
    }

    private void OnEquationsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (Equation equation in e.OldItems)
            {
                equation.PropertyChanged -= OnEquationPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (Equation equation in e.NewItems)
            {
                equation.PropertyChanged += OnEquationPropertyChanged;
                EnsureMathMl(equation);
            }
        }

        if (!_replacingEquations)
        {
            SyncFormulaFromEquations();
        }
    }

    private void OnEquationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not Equation equation)
        {
            return;
        }

        if (e.PropertyName == nameof(Equation.Expression))
        {
            ImportLegacyEquation(equation);
            return;
        }

        if (e.PropertyName == nameof(Equation.MathMl))
        {
            ScheduleFormulaSynchronization();
            return;
        }

        if (e.PropertyName == nameof(Equation.IsLineEnabled))
        {
            SyncFormulaFromEquations();
            return;
        }

        if (e.PropertyName is nameof(Equation.LineColor) or nameof(Equation.EquationStyle))
        {
            ApplyEquationAppearance(equation);
            RenderPreparedGraph();
            return;
        }

        if (e.PropertyName == nameof(Equation.IsSelected))
        {
            if (equation.IsSelected)
            {
                _ = equation.GraphedEquation?.TrySelectEquation();
            }
            else
            {
                _graph.TryResetSelection();
            }

            RenderPreparedGraph();
        }
    }

    private void SyncFormulaFromEquations()
    {
        _equationPlotTimer.Stop();
        _updatingFormulaFromCollection = true;
        try
        {
            Formula = Equations
                .Where(static equation => equation.IsLineEnabled)
                .Select(EquationMathMl)
                .FirstOrDefault(static mathMl => !string.IsNullOrWhiteSpace(mathMl)) ?? string.Empty;
        }
        finally
        {
            _updatingFormulaFromCollection = false;
        }

        PlotGraph(keepCurrentView: true);
    }

    private void ScheduleFormulaSynchronization()
    {
        _equationPlotTimer.Stop();
        _equationPlotTimer.Start();
    }

    private void OnEquationPlotTimerTick(object? sender, EventArgs e)
    {
        _equationPlotTimer.Stop();
        SyncFormulaFromEquations();
    }

    private void StartGraphPreparationPolling()
    {
        if (_isAttached && Volatile.Read(ref _disposed) == 0)
        {
            _ = _graphPreparationTimer.Start(this);
        }
    }

    private void OnGraphPreparationFrame(TimeSpan _)
    {
        if (!_isAttached || Volatile.Read(ref _disposed) != 0)
        {
            _graphPreparationTimer.Stop();
            return;
        }

        IGraphRenderer renderer = _graph.GetRenderer();
        CommitConcurrentPreparation(renderer);
        if (renderer is not IConcurrentGraphRenderer concurrentRenderer || !concurrentRenderer.IsPrepareGraphPending)
        {
            _graphPreparationTimer.Stop();
        }
    }

    private void EnsureInteractionSettlement()
    {
        if (!_interactionSettleTimer.IsEnabled && _isAttached && Volatile.Read(ref _disposed) == 0)
        {
            _interactionSettleTimer.Start();
        }
    }

    private void OnInteractionSettleTimerTick(object? sender, EventArgs e)
    {
        _interactionSettleTimer.Stop();
        GraphPipelineDiagnostics.RecordSettlementTimer();
        if (!_isAttached || Volatile.Read(ref _disposed) != 0 || _settlePreparationPending)
        {
            return;
        }

        // This timer authoritatively requests fresh geometry at the trailing
        // edge of an interaction. Browser RAF callbacks may be coalesced, so
        // final pointer release must not depend on one particular animation
        // callback being delivered.
        if (_activePointers.Count != 0)
        {
            return;
        }

        if (_panInertiaActive)
        {
            RequestInteractionFrame();
            EnsureInteractionSettlement();
            return;
        }

        if (_pendingWheelDelta != 0)
        {
            RequestInteractionFrame();
            EnsureInteractionSettlement();
            return;
        }

        if (Stopwatch.GetElapsedTime(_lastInteractionTimestamp) < InteractionPlotDelay)
        {
            EnsureInteractionSettlement();
            return;
        }

        IGraphRenderer renderer = _graph.GetRenderer();
        CommitConcurrentPreparation(renderer);
        if (_settlePreparationPending)
        {
            return;
        }

        if (_resizePreparePending)
        {
            if (_hasInteractionViewport)
            {
                SettleInteractionViewport();
            }
            else
            {
                RenderPreparedGraph();
            }

            return;
        }

        if (_hasInteractionViewport)
        {
            SettleInteractionViewport();
        }
    }

    private IExpression? ParseEnabledEquations(out int errorCode, out int errorType)
    {
        var expressions = new List<IExpression>();
        var equations = new List<Equation>();
        errorCode = 0;
        errorType = 0;
        foreach (Equation equation in Equations)
        {
            string mathMl = EquationMathMl(equation);
            if (!equation.IsLineEnabled || string.IsNullOrWhiteSpace(mathMl))
            {
                continue;
            }

            IExpression? expression = _solver.ParseInput(mathMl, out errorCode, out errorType);
            if (expression is null)
            {
                _parsedEquations = Array.Empty<Equation>();
                return null;
            }

            if (!expression.IsEmptySet())
            {
                expressions.Add(expression);
                equations.Add(equation);
            }
        }

        _parsedEquations = equations;
        return _solver.CombineExpressions(expressions);
    }

    private void BindEquations(IReadOnlyList<IEquation> initialized)
    {
        if (Equations.Count == 0)
        {
            return;
        }

        foreach (Equation equation in Equations)
        {
            equation.HasGraphError = false;
            equation.GraphedEquation = null;
        }

        for (int graphIndex = 0; graphIndex < _parsedEquations.Count; graphIndex++)
        {
            Equation equation = _parsedEquations[graphIndex];
            if (graphIndex >= initialized.Count)
            {
                equation.HasGraphError = true;
                continue;
            }

            IEquation graphEquation = initialized[graphIndex];
            equation.GraphedEquation = graphEquation;
            ApplyEquationAppearance(equation);
            if (equation.IsSelected)
            {
                graphEquation.TrySelectEquation();
            }
        }
    }

    private void ApplyEquationAppearance(Equation equation)
    {
        if (equation.GraphedEquation is null)
        {
            return;
        }

        IEquationOptions options = equation.GraphedEquation.GetGraphEquationOptions();
        options.SetGraphColor(equation.LineColor.ToGraphColor());
        options.SetLineStyle(equation.EquationStyle.ToGraphLineStyle());
        options.SetLineWidth((float)LineWidth);
        options.SetSelectedEquationLineWidth((float)(LineWidth + (LineWidth <= 2 ? 1 : 2)));
    }

    private void EnsureMathMl(Equation equation)
    {
        if (string.IsNullOrWhiteSpace(equation.MathMl) &&
            !string.IsNullOrWhiteSpace(equation.Expression))
        {
            ImportLegacyEquation(equation);
        }
    }

    private void ImportLegacyEquation(Equation equation)
    {
        if (string.IsNullOrWhiteSpace(equation.Expression))
        {
            equation.MathMl = string.Empty;
            return;
        }

        equation.MathMl = _legacyEquationImporter.TryImport(
            equation.Expression,
            out string mathMl,
            out _,
            out _)
            ? mathMl
            : string.Empty;
    }

    private string EquationMathMl(Equation equation)
    {
        if (!string.IsNullOrWhiteSpace(equation.MathMl))
        {
            return equation.MathMl;
        }

        return _legacyEquationImporter.TryImport(
            equation.Expression,
            out string mathMl,
            out _,
            out _)
            ? mathMl
            : string.Empty;
    }

    private void SetErrors(int errorCode, int errorType)
    {
        ErrorCode = errorCode;
        ErrorType = errorType;
        foreach (Equation equation in Equations.Where(equation => equation.IsLineEnabled))
        {
            equation.HasGraphError = true;
            equation.GraphErrorCode = errorCode;
            equation.GraphErrorType = errorType;
        }
    }

    private void UpdateVariables()
    {
        var updated = new Dictionary<string, Variable>(StringComparer.OrdinalIgnoreCase);
        foreach (IVariable graphVariable in _graph.GetVariables())
        {
            string name = graphVariable.GetVariableName();
            updated[name] = Variables.TryGetValue(name, out Variable? existing) ? existing : new Variable(1);
        }

        Variables = updated;
        foreach ((string name, Variable variable) in updated)
        {
            _graph.SetArgValue(name, variable.Value);
        }

        OnPropertyChanged(nameof(Variables));
        VariablesUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyOptions()
    {
        IGraphingOptions options = _graph.GetOptions();
        options.SetForceProportional(ForceProportionalAxes);
        options.SetAxisColor(AxesColor.ToGraphColor());
        options.SetFontColor(AxesColor.ToGraphColor());
        options.SetBackColor(GraphBackground.ToGraphColor());
        options.SetBoxColor(GraphBackground.ToGraphColor());
        options.SetGridColor(GridLinesColor.ToGraphColor());
    }

    private void RenderPreparedGraph()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        // Styled properties are applied while the graph view is still being
        // constructed inside the navigation selection notification. Preparing
        // a graph here would start renderer work for the detached control's
        // fallback 1x1 size and re-enter the property/event stack. Accumulate
        // those changes and prepare the latest state once Avalonia has attached
        // and measured the control.
        if (!_isAttached)
        {
            _prepareGraphOnAttach = true;
            return;
        }

        IGraphRenderer renderer = _graph.GetRenderer();
        if (_hasInteractionViewport && !_rendererMatchesInteractionViewport)
        {
            _ = renderer.SetDisplayRanges(_interactionXMinimum, _interactionXMaximum, _interactionYMinimum, _interactionYMaximum);
        }

        ClearInteractionViewport();
        _settlePreparationPending = false;
        EnsureSize(renderer);
        if (renderer is IConcurrentGraphRenderer concurrentRenderer)
        {
            _ = concurrentRenderer.RequestPrepareGraph();
            CaptureSettledViewport(renderer);
            _resizePreparePending = false;
            bool preparationPending = concurrentRenderer.IsPrepareGraphPending;
            InvalidateVisual();

            if (preparationPending)
            {
                StartGraphPreparationPolling();
                RequestInteractionFrame();
            }

            return;
        }

        _ = renderer.PrepareGraph();
        CaptureSettledViewport(renderer);
        _resizePreparePending = false;
        InvalidateVisual();
    }

    private void MarkInteraction(bool viewportAlreadyChanged = false)
    {
        bool changed = ApplyPendingPointerManipulation() || viewportAlreadyChanged;
        _lastInteractionTimestamp = Stopwatch.GetTimestamp();
        if (changed)
        {
            _settlePreparationPending = false;
            _viewportRenderPending = true;
        }

        RequestInteractionFrame();
        EnsureInteractionSettlement();
    }

    private void RequestInteractionFrame()
    {
        if (_interactionFrameRequested || !_isAttached || Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        if (TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        _interactionFrameRequested = true;
        topLevel.RequestAnimationFrame(_interactionFrameCallback);
    }

    private void OnInteractionFrame(TimeSpan _) => ProcessInteractionFrame();
    private void ProcessInteractionFrame()
    {
        Dispatcher.UIThread.VerifyAccess();
        _interactionFrameRequested = false;
        if (!_isAttached)
        {
            return;
        }

        IGraphRenderer renderer = _graph.GetRenderer();
        CommitConcurrentPreparation(renderer);

        bool changed = _viewportRenderPending;
        _viewportRenderPending = false;
        changed |= ApplyPendingPointerManipulation();
        changed |= ApplyPanInertia();
        changed |= ApplyPendingWheelZoom();
        if (changed)
        {
            _lastInteractionTimestamp = Stopwatch.GetTimestamp();
        }

        // Resampling is the expensive half of gesture handling. The displayed
        // scene is rebuilt for the current viewport every frame while a single
        // bounded background request refreshes its user-space geometry.
        if (!ShouldRefreshInteractionGeometry() || !RefreshInteractionGeometry())
        {
            SynchronizeInteractionRendererViewport();
        }

        if (changed)
        {
            InvalidateVisual();
        }

        if (_pendingWheelDelta != 0)
        {
            RequestInteractionFrame();
            return;
        }

        if (_panInertiaActive)
        {
            RequestInteractionFrame();
            return;
        }

        if (_resizePreparePending && _activePointers.Count == 0)
        {
            if (Stopwatch.GetElapsedTime(_lastInteractionTimestamp) < InteractionPlotDelay)
            {
                RequestInteractionFrame();
                return;
            }

            if (_hasInteractionViewport)
            {
                SettleInteractionViewport();
            }
            else
            {
                RenderPreparedGraph();
            }

            return;
        }

        // A finger may pause without ending the gesture. Do not resample in the
        // middle of that pause; the next move/release will request another frame.
        if (_activePointers.Count != 0 || !_hasInteractionViewport)
        {
            return;
        }

        if (Stopwatch.GetElapsedTime(_lastInteractionTimestamp) < InteractionPlotDelay)
        {
            RequestInteractionFrame();
            return;
        }

        SettleInteractionViewport();
    }

    private bool ApplyPendingWheelZoom()
    {
        if (_pendingWheelDelta == 0 || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return false;
        }

        double delta = _pendingWheelDelta;
        _pendingWheelDelta = 0;
        double centerX = (2 * _pendingWheelPosition.X / Bounds.Width) - 1;
        double centerY = 1 - (2 * _pendingWheelPosition.Y / Bounds.Height);
        return TryScaleInteractionViewport(centerX, centerY, Math.Pow(1.15, -delta));
    }

    private void SynchronizeInteractionRendererViewport()
    {
        if (!_hasInteractionViewport || _rendererMatchesInteractionViewport)
        {
            return;
        }

        IGraphRenderer renderer = _graph.GetRenderer();
        if (!renderer.SetDisplayRanges(_interactionXMinimum, _interactionXMaximum, _interactionYMinimum, _interactionYMaximum).Succeeded)
        {
            return;
        }

        _rendererMatchesInteractionViewport = true;
    }

    private void SettleInteractionViewport()
    {
        if (!_hasInteractionViewport)
        {
            return;
        }

        GraphPipelineDiagnostics.RecordSettlementRequest();
        IGraphRenderer renderer = _graph.GetRenderer();
        if (!_rendererMatchesInteractionViewport && !renderer.SetDisplayRanges(_interactionXMinimum, _interactionXMaximum, _interactionYMinimum, _interactionYMaximum).Succeeded)
        {
            ClearInteractionViewport();
            return;
        }

        _rendererMatchesInteractionViewport = true;
        EnsureSize(renderer);
        if (renderer is IConcurrentGraphRenderer concurrentRenderer)
        {
            GraphStatus status = concurrentRenderer.RequestPrepareGraph();
            if (status.Failed || !concurrentRenderer.IsPrepareGraphPending)
            {
                CompleteInteractionSettlement(renderer);
                return;
            }

            _settlePreparationPending = true;
            StartGraphPreparationPolling();
            RequestInteractionFrame();
            return;
        }

        _ = renderer.PrepareGraph();
        CompleteInteractionSettlement(renderer);
    }

    private void CompleteInteractionSettlement(IGraphRenderer renderer)
    {
        CaptureSettledViewport(renderer);
        _hasInteractionViewport = false;
        _rendererMatchesInteractionViewport = false;
        _resizePreparePending = false;
        _settlePreparationPending = false;
        InvalidateVisual();
        GraphViewChanged?.Invoke(this, new GraphViewChangedEventArgs(GraphViewChangedReason.Manipulation));
    }

    private void CommitConcurrentPreparation(IGraphRenderer renderer)
    {
        if (renderer is not IConcurrentGraphRenderer concurrentRenderer)
        {
            return;
        }

        GraphStatus status = concurrentRenderer.TryCommitPreparedGraph(out bool completed);
        if (!completed)
        {
            if (concurrentRenderer.IsPrepareGraphPending)
            {
                StartGraphPreparationPolling();
                RequestInteractionFrame();
            }

            return;
        }

        bool matchesInteractionViewport = _hasInteractionViewport && _rendererMatchesInteractionViewport;
        if (_settlePreparationPending && _activePointers.Count == 0 && matchesInteractionViewport)
        {
            if (status.Succeeded)
            {
                CompleteInteractionSettlement(renderer);
            }
            else
            {
                // The current-viewport scene remains valid when a bounded
                // background refresh fails; only its geometry source is older.
                _settlePreparationPending = false;
                _graphPreparationTimer.Stop();
            }

            return;
        }

        if (status.Succeeded)
        {
            CapturePreparedViewport(concurrentRenderer);
            InvalidateVisual();
        }
    }

    private void CaptureSettledViewport(IGraphRenderer renderer)
    {
        renderer.GetDisplayRanges(out _settledXMinimum, out _settledXMaximum, out _settledYMinimum, out _settledYMaximum);
        GetGraphDimensions(out double width, out double height);
        NormalizeProportionalRanges(ref _settledXMinimum, ref _settledXMaximum, ref _settledYMinimum, ref _settledYMaximum, width, height);
        _hasSettledViewport = true;
    }

    private void CapturePreparedViewport(IConcurrentGraphRenderer renderer)
    {
        if (!renderer.TryGetPreparedDisplayRanges(out _settledXMinimum, out _settledXMaximum, out _settledYMinimum, out _settledYMaximum))
        {
            return;
        }

        _hasSettledViewport = true;
    }

    private bool ShouldRefreshInteractionGeometry()
    {
        if (!_hasSettledViewport || !_hasInteractionViewport)
        {
            return false;
        }

        double settledXLength = _settledXMaximum - _settledXMinimum;
        double settledYLength = _settledYMaximum - _settledYMinimum;
        double interactionXLength = _interactionXMaximum - _interactionXMinimum;
        double interactionYLength = _interactionYMaximum - _interactionYMinimum;
        if (settledXLength <= 0 || settledYLength <= 0 || interactionXLength <= 0 || interactionYLength <= 0)
        {
            return false;
        }

        double xMargin = settledXLength * InteractionCoverageMarginRatio;
        double yMargin = settledYLength * InteractionCoverageMarginRatio;
        bool needsCoverage = _interactionXMinimum < _settledXMinimum - xMargin || _interactionXMaximum > _settledXMaximum + xMargin || _interactionYMinimum < _settledYMinimum - yMargin || _interactionYMaximum > _settledYMaximum + yMargin || interactionXLength < settledXLength * 0.55 || interactionYLength < settledYLength * 0.55;
        if (!needsCoverage)
        {
            return false;
        }

        if (_graph.GetRenderer() is IConcurrentGraphRenderer { IsPrepareGraphPending: true })
        {
            return false;
        }

        return true;
    }

    private bool RefreshInteractionGeometry()
    {
        if (!_hasInteractionViewport)
        {
            return false;
        }

        IGraphRenderer renderer = _graph.GetRenderer();
        if (!_rendererMatchesInteractionViewport && !renderer.SetDisplayRanges(_interactionXMinimum, _interactionXMaximum, _interactionYMinimum, _interactionYMaximum).Succeeded)
        {
            return false;
        }

        _rendererMatchesInteractionViewport = true;
        EnsureSize(renderer);
        if (renderer is IConcurrentGraphRenderer concurrentRenderer)
        {
            if (concurrentRenderer.RequestPrepareGraph().Succeeded && concurrentRenderer.IsPrepareGraphPending)
            {
                StartGraphPreparationPolling();
                RequestInteractionFrame();
                return true;
            }

            return false;
        }

        if (!renderer.PrepareGraph().Succeeded)
        {
            return false;
        }

        _rendererMatchesInteractionViewport = true;
        CaptureSettledViewport(renderer);
        InvalidateVisual();

        return true;
    }

    private bool EnsureInteractionViewport()
    {
        if (_hasInteractionViewport)
        {
            return true;
        }

        if (!_hasSettledViewport)
        {
            CaptureSettledViewport(_graph.GetRenderer());
        }

        if (!_hasSettledViewport)
        {
            return false;
        }

        _interactionXMinimum = _settledXMinimum;
        _interactionXMaximum = _settledXMaximum;
        _interactionYMinimum = _settledYMinimum;
        _interactionYMaximum = _settledYMaximum;
        _hasInteractionViewport = true;
        _rendererMatchesInteractionViewport = false;
        return true;
    }

    private bool TryMoveInteractionViewport(double ratioX, double ratioY)
    {
        if ((!double.IsFinite(ratioX) || !double.IsFinite(ratioY)) || (ratioX == 0 && ratioY == 0) || !EnsureInteractionViewport())
        {
            return false;
        }

        double xOffset = ratioX * (_interactionXMaximum - _interactionXMinimum) * 0.5;
        double yOffset = ratioY * (_interactionYMaximum - _interactionYMinimum) * 0.5;
        _interactionXMinimum += xOffset;
        _interactionXMaximum += xOffset;
        _interactionYMinimum += yOffset;
        _interactionYMaximum += yOffset;
        _rendererMatchesInteractionViewport = false;
        return true;
    }

    private bool TryPanInteractionViewport(double deltaX, double deltaY)
    {
        if ((deltaX == 0 && deltaY == 0) || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return false;
        }

        // MoveRangeByRatio interprets +/-1 as half a viewport. Multiplying by
        // two keeps graph pixels locked to the finger instead of moving at half
        // speed like the earlier direct port.
        return TryMoveInteractionViewport(-2 * deltaX / Bounds.Width, 2 * deltaY / Bounds.Height);
    }

    private void BeginPanVelocityTracking()
    {
        _panInertiaActive = false;
        _panInertiaEligible = true;
        _panVelocity = default;
        _panLastMotionTimestamp = 0;
        _panVelocitySampleTimestamp = Stopwatch.GetTimestamp();
    }

    private void CancelPanVelocityTracking()
    {
        _panInertiaEligible = false;
        _panVelocity = default;
        _panLastMotionTimestamp = 0;
        _panVelocitySampleTimestamp = 0;
    }

    private void RecordPanVelocity(double deltaX, double deltaY)
    {
        long now = Stopwatch.GetTimestamp();
        if (_panVelocitySampleTimestamp == 0)
        {
            _panVelocitySampleTimestamp = now;
            return;
        }

        double elapsedSeconds = Stopwatch.GetElapsedTime(_panVelocitySampleTimestamp, now).TotalSeconds;
        _panVelocitySampleTimestamp = now;
        if (elapsedSeconds <= 0 || (deltaX == 0 && deltaY == 0))
        {
            return;
        }

        var instantaneousVelocity = new Vector(deltaX / elapsedSeconds, deltaY / elapsedSeconds);
        double instantaneousSpeed = instantaneousVelocity.Length;
        if (!double.IsFinite(instantaneousSpeed))
        {
            return;
        }

        if (instantaneousSpeed > PanInertiaMaximumSpeed)
        {
            instantaneousVelocity *= PanInertiaMaximumSpeed / instantaneousSpeed;
        }

        bool previousSampleIsFresh = _panLastMotionTimestamp != 0 && Stopwatch.GetElapsedTime(_panLastMotionTimestamp, now) <= PanInertiaVelocityLifetime;
        _panVelocity = previousSampleIsFresh
            ? new Vector(
                (_panVelocity.X * (1 - PanVelocityBlend)) + (instantaneousVelocity.X * PanVelocityBlend),
                (_panVelocity.Y * (1 - PanVelocityBlend)) + (instantaneousVelocity.Y * PanVelocityBlend))
            : instantaneousVelocity;
        _panLastMotionTimestamp = now;
    }

    private void StartPanInertia()
    {
        _panInertiaEligible = false;
        long now = Stopwatch.GetTimestamp();
        if (_panLastMotionTimestamp == 0 || Stopwatch.GetElapsedTime(_panLastMotionTimestamp, now) > PanInertiaVelocityLifetime)
        {
            ResetPanInertia();
            return;
        }

        double speed = _panVelocity.Length;
        if (!double.IsFinite(speed) || speed < PanInertiaMinimumStartSpeed)
        {
            ResetPanInertia();
            return;
        }

        if (speed > PanInertiaMaximumSpeed)
        {
            _panVelocity *= PanInertiaMaximumSpeed / speed;
        }

        _panInertiaTimestamp = now;
        _panInertiaActive = true;
    }

    private bool ApplyPanInertia()
    {
        if (!_panInertiaActive || _activePointers.Count != 0)
        {
            return false;
        }

        long now = Stopwatch.GetTimestamp();
        double elapsedSeconds = Stopwatch.GetElapsedTime(_panInertiaTimestamp, now).TotalSeconds;
        _panInertiaTimestamp = now;
        double speed = _panVelocity.Length;
        if (elapsedSeconds <= 0 || !double.IsFinite(speed) || speed <= PanInertiaStopSpeed)
        {
            if (speed <= PanInertiaStopSpeed || !double.IsFinite(speed))
            {
                ResetPanInertia();
            }

            return false;
        }

        double travelSeconds = Math.Min(Math.Min(elapsedSeconds, MaximumPanInertiaFrameSeconds), speed / PanInertiaDeceleration);
        double distance = (speed * travelSeconds) - (0.5 * PanInertiaDeceleration * travelSeconds * travelSeconds);
        Vector direction = _panVelocity / speed;
        bool changed = TryPanInteractionViewport(direction.X * distance, direction.Y * distance);
        double remainingSpeed = Math.Max(0, speed - (PanInertiaDeceleration * elapsedSeconds));
        _panVelocity = direction * remainingSpeed;
        if (!changed || remainingSpeed <= PanInertiaStopSpeed)
        {
            ResetPanInertia();
        }

        return changed;
    }

    private void ResetPanInertia()
    {
        _panInertiaActive = false;
        CancelPanVelocityTracking();
        _panInertiaTimestamp = 0;
    }

    private bool TryScaleInteractionViewport(double centerX, double centerY, double scale)
    {
        if (!double.IsFinite(centerX) || !double.IsFinite(centerY) || !double.IsFinite(scale) || scale <= 0 || !EnsureInteractionViewport())
        {
            return false;
        }

        double oldXLength = _interactionXMaximum - _interactionXMinimum;
        double oldYLength = _interactionYMaximum - _interactionYMinimum;
        double newXLength = oldXLength * scale;
        double newYLength = oldYLength * scale;
        if (!IsAllowedRangeLength(newXLength) || !IsAllowedRangeLength(newYLength))
        {
            return false;
        }

        centerX = Math.Clamp(centerX, -1, 1);
        centerY = Math.Clamp(centerY, -1, 1);
        double graphCenterX = ((_interactionXMinimum + _interactionXMaximum) * 0.5) + (centerX * oldXLength * 0.5);
        double graphCenterY = ((_interactionYMinimum + _interactionYMaximum) * 0.5) + (centerY * oldYLength * 0.5);
        _interactionXMinimum = graphCenterX + ((_interactionXMinimum - graphCenterX) * scale);
        _interactionXMaximum = graphCenterX + ((_interactionXMaximum - graphCenterX) * scale);
        _interactionYMinimum = graphCenterY + ((_interactionYMinimum - graphCenterY) * scale);
        _interactionYMaximum = graphCenterY + ((_interactionYMaximum - graphCenterY) * scale);
        _rendererMatchesInteractionViewport = false;
        return true;
    }

    private void ClearInteractionViewport()
    {
        _pendingWheelDelta = 0;
        ResetPanInertia();
        _hasInteractionViewport = false;
        _rendererMatchesInteractionViewport = false;
        ResetPointerBaselines();
    }

    private void CancelInteractionViewport()
    {
        ClearInteractionViewport();
        _viewportRenderPending = false;
        _resizePreparePending = false;
        _settlePreparationPending = false;
    }

    private void NormalizeProportionalRanges(ref double xMinimum, ref double xMaximum, ref double yMinimum, ref double yMaximum, double width, double height)
    {
        if (!ForceProportionalAxes)
        {
            return;
        }

        double xLength = xMaximum - xMinimum;
        double yLength = yMaximum - yMinimum;
        double xUnitsPerPixel = xLength / width;
        double yUnitsPerPixel = yLength / height;
        if (xUnitsPerPixel > yUnitsPerPixel)
        {
            double center = (yMinimum + yMaximum) * 0.5;
            double half = xUnitsPerPixel * height * 0.5;
            yMinimum = center - half;
            yMaximum = center + half;
        }
        else
        {
            double center = (xMinimum + xMaximum) * 0.5;
            double half = yUnitsPerPixel * width * 0.5;
            xMinimum = center - half;
            xMaximum = center + half;
        }
    }

    private void EnsureSize(IGraphRenderer renderer)
    {
        GetGraphDimensions(out double width, out double height);
        renderer.SetGraphSize((uint)width, (uint)height);
        double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        renderer.SetDpi((float)(96 * scaling), (float)(96 * scaling));
    }

    private void GetGraphDimensions(out double width, out double height)
    {
        width = Math.Clamp(Math.Round(Bounds.Width), 1, 32_768);
        height = Math.Clamp(Math.Round(Bounds.Height), 1, 32_768);
    }

    private void SetDefaultRange(bool xAxis, double minimum, double maximum)
    {
        bool changed = xAxis ? _graph.GetOptions().SetDefaultXRange(new AxisRange(minimum, maximum)) : _graph.GetOptions().SetDefaultYRange(new AxisRange(minimum, maximum));
        if (changed)
        {
            RenderPreparedGraph();
        }
    }

    private static double Distance(Point left, Point right)
    {
        double x = left.X - right.X;
        double y = left.Y - right.Y;
        return Math.Sqrt((x * x) + (y * y));
    }

    private static Point Midpoint(Point left, Point right) => new((left.X + right.X) * 0.5, (left.Y + right.Y) * 0.5);
    private static bool IsAllowedRangeLength(double length) => double.IsFinite(length) && length >= MinimumRangeLength && length <= MaximumRangeLength;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public void Dispose()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _isAttached = false;
        _equationPlotTimer.Stop();
        _equationPlotTimer.Tick -= OnEquationPlotTimerTick;
        _graphPreparationTimer.Detach();
        _interactionSettleTimer.Stop();
        _interactionSettleTimer.Tick -= OnInteractionSettleTimerTick;
        Equations.CollectionChanged -= OnEquationsChanged;
        foreach (Equation equation in Equations)
        {
            equation.PropertyChanged -= OnEquationPropertyChanged;
        }

        _interactionFrameRequested = false;
        ResetPanInertia();
        _activePointers.Clear();
        _initializedEquations = Array.Empty<IEquation>();
        _renderCache.Clear();
        _analysisWorker.Dispose();
        if (_graph is IDisposable disposableGraph)
        {
            disposableGraph.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
