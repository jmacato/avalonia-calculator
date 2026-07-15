using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Graphing;
using Graphing.Analyzer;
using Graphing.Renderer;
using AvaloniaColor = Avalonia.Media.Color;

namespace GraphControl;

[SuppressMessage("Maintainability", "CA1501:Avoid excessive inheritance", Justification = "Avalonia controls necessarily inherit the framework visual hierarchy.")]
[SuppressMessage("Maintainability", "CA1506:Avoid excessive class coupling", Justification = "This compatibility facade intentionally maps the native Grapher surface to the engine and Avalonia input/rendering types.")]
public sealed class Grapher : Control, INotifyPropertyChanged
{
    public static readonly StyledProperty<string> FormulaProperty =
        AvaloniaProperty.Register<Grapher, string>(nameof(Formula), string.Empty);

    public static readonly StyledProperty<bool> ForceProportionalAxesProperty =
        AvaloniaProperty.Register<Grapher, bool>(nameof(ForceProportionalAxes), true);

    public static readonly StyledProperty<bool> UseCommaDecimalSeparatorProperty =
        AvaloniaProperty.Register<Grapher, bool>(nameof(UseCommaDecimalSeparator));

    public static readonly StyledProperty<AvaloniaColor> AxesColorProperty =
        AvaloniaProperty.Register<Grapher, AvaloniaColor>(nameof(AxesColor), Colors.Black);

    public static readonly StyledProperty<AvaloniaColor> GraphBackgroundProperty =
        AvaloniaProperty.Register<Grapher, AvaloniaColor>(nameof(GraphBackground), Colors.White);

    public static readonly StyledProperty<AvaloniaColor> GridLinesColorProperty =
        AvaloniaProperty.Register<Grapher, AvaloniaColor>(nameof(GridLinesColor), AvaloniaColor.FromRgb(0xC6, 0xC6, 0xC6));

    public static readonly StyledProperty<double> LineWidthProperty =
        AvaloniaProperty.Register<Grapher, double>(nameof(LineWidth), 2);

    private readonly IMathSolver _solver;
    private readonly IGraph _graph;
    private readonly EquationTextCodec _textCodec;
    private readonly Dictionary<int, Point> _activePointers = [];
    private Point? _lastPanPoint;
    private double? _lastPinchDistance;
    private bool _activeTracing;
    private Point _traceLocation = new(double.NaN, double.NaN);
    private int _errorCode;
    private int _errorType;
    private bool _updatingFormulaFromCollection;
    private bool _replacingEquations;
    private IReadOnlyList<IEquation> _initializedEquations = Array.Empty<IEquation>();

    static Grapher()
    {
        AffectsRender<Grapher>(
            FormulaProperty,
            ForceProportionalAxesProperty,
            AxesColorProperty,
            GraphBackgroundProperty,
            GridLinesColorProperty,
            LineWidthProperty);
    }

    public Grapher()
    {
        Focusable = true;
        ClipToBounds = true;
        _solver = MathSolver.CreateMathSolver();
        _solver.ParsingOptions().SetFormatType(FormatType.Linear);
        _solver.FormatOptions().SetFormatType(FormatType.MathML);
        _solver.FormatOptions().SetMathMLPrefix("mml");
        _graph = _solver.CreateGrapher();
        _textCodec = new EquationTextCodec();
        Equations = [];
        Equations.CollectionChanged += OnEquationsChanged;
    }

    public string Formula
    {
        get => GetValue(FormulaProperty);
        set => SetValue(FormulaProperty, value ?? string.Empty);
    }

    public bool ForceProportionalAxes
    {
        get => GetValue(ForceProportionalAxesProperty);
        set => SetValue(ForceProportionalAxesProperty, value);
    }

    public bool UseCommaDecimalSeparator
    {
        get => GetValue(UseCommaDecimalSeparatorProperty);
        set => SetValue(UseCommaDecimalSeparatorProperty, value);
    }

    public AvaloniaColor AxesColor
    {
        get => GetValue(AxesColorProperty);
        set => SetValue(AxesColorProperty, value);
    }

    public AvaloniaColor GraphBackground
    {
        get => GetValue(GraphBackgroundProperty);
        set => SetValue(GraphBackgroundProperty, value);
    }

    public AvaloniaColor GridLinesColor
    {
        get => GetValue(GridLinesColorProperty);
        set => SetValue(GridLinesColorProperty, value);
    }

    public double LineWidth
    {
        get => GetValue(LineWidthProperty);
        set => SetValue(LineWidthProperty, value);
    }

    public EquationCollection Equations { get; }

    public IReadOnlyDictionary<string, Variable> Variables { get; private set; } =
        new Dictionary<string, Variable>(StringComparer.OrdinalIgnoreCase);

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
            TracingChanged?.Invoke(value);
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

    public double XAxisMin
    {
        get => _graph.GetOptions().GetDefaultXRange().Minimum;
        set => SetDefaultRange(xAxis: true, minimum: value, maximum: XAxisMax);
    }

    public double XAxisMax
    {
        get => _graph.GetOptions().GetDefaultXRange().Maximum;
        set => SetDefaultRange(xAxis: true, minimum: XAxisMin, maximum: value);
    }

    public double YAxisMin
    {
        get => _graph.GetOptions().GetDefaultYRange().Minimum;
        set => SetDefaultRange(xAxis: false, minimum: value, maximum: YAxisMax);
    }

    public double YAxisMax
    {
        get => _graph.GetOptions().GetDefaultYRange().Maximum;
        set => SetDefaultRange(xAxis: false, minimum: YAxisMin, maximum: value);
    }

    public event Action<bool>? TracingChanged;

    public event Action<double, double>? TracingValueChanged;

    public event Action<Point>? PointerValueChanged;

    public event EventHandler<GraphViewChangedReason>? GraphViewChanged;

    public event EventHandler? GraphPlotted;

    public event EventHandler? VariablesUpdated;

    public new event PropertyChangedEventHandler? PropertyChanged;

    public void ZoomFromCenter(double scale)
    {
        if (_graph.GetRenderer().ScaleRange(0, 0, scale).Succeeded)
        {
            RenderPreparedGraph();
            GraphViewChanged?.Invoke(this, GraphViewChangedReason.Manipulation);
        }
    }

    public void ResetGrid()
    {
        if (_graph.GetRenderer().ResetRange().Succeeded)
        {
            RenderPreparedGraph();
            GraphViewChanged?.Invoke(this, GraphViewChangedReason.Reset);
        }
    }

    public void GetDisplayRanges(out double xMin, out double xMax, out double yMin, out double yMax) =>
        _graph.GetRenderer().GetDisplayRanges(out xMin, out xMax, out yMin, out yMax);

    public void SetDisplayRanges(double xMin, double xMax, double yMin, double yMax)
    {
        if (_graph.GetRenderer().SetDisplayRanges(xMin, xMax, yMin, yMax).Succeeded)
        {
            RenderPreparedGraph();
            GraphViewChanged?.Invoke(this, GraphViewChangedReason.Manipulation);
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

    public string ConvertToLinear(string mathMl)
    {
        return _textCodec.TryMathMlToLinear(mathMl, out string linear, out _, out _)
            ? linear
            : string.Empty;
    }

    public string FormatMathML(string input)
    {
        return _textCodec.TryLinearToMathMl(input, out string mathMl, out _, out _)
            ? mathMl
            : string.Empty;
    }

    public void PlotGraph(bool keepCurrentView)
    {
        ApplyOptions();
        string formula = BuildFormula();
        IExpression? expression = _solver.ParseInput(formula, out int errorCode, out int errorType);
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

    public KeyGraphFeaturesInfo? AnalyzeEquation(Equation equation)
    {
        ArgumentNullException.ThrowIfNull(equation);
        IExpression? expression = _solver.ParseInput(equation.Expression, out _, out _);
        if (expression is null)
        {
            return new KeyGraphFeaturesInfo(AnalysisErrorType.AnalysisCouldNotBePerformed);
        }

        IGraph analysisGraph = _solver.CreateGrapher();
        if (analysisGraph.TryInitialize(expression) is not { Count: > 0 })
        {
            return new KeyGraphFeaturesInfo(AnalysisErrorType.AnalysisCouldNotBePerformed);
        }

        foreach ((string variableName, Variable variable) in Variables)
        {
            analysisGraph.SetArgValue(variableName, variable.Value);
        }

        IGraphAnalyzer analyzer = analysisGraph.GetAnalyzer();
        if (!analyzer.CanFunctionAnalysisBePerformed(out bool variableIsNotX))
        {
            return new KeyGraphFeaturesInfo(variableIsNotX
                ? AnalysisErrorType.VariableIsNotX
                : AnalysisErrorType.AnalysisNotSupported);
        }

        if (variableIsNotX)
        {
            return new KeyGraphFeaturesInfo(AnalysisErrorType.VariableIsNotX);
        }

        if (analyzer.PerformFunctionAnalysis((uint)Graphing.Analyzer.PerformAnalysisType.All) != GraphStatus.Ok)
        {
            return new KeyGraphFeaturesInfo(AnalysisErrorType.AnalysisCouldNotBePerformed);
        }

        return new KeyGraphFeaturesInfo(_solver.Analyze(analyzer));
    }

    public KeyGraphFeaturesInfo? AnalyzeFirstEquation()
    {
        if (Equations.FirstOrDefault(equation => !string.IsNullOrWhiteSpace(equation.Expression)) is { } equation)
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
        base.Render(context);
        IGraphRenderer renderer = _graph.GetRenderer();
        EnsureSize(renderer);
        using var target = new AvaloniaGraphDrawingTarget(context);
        GraphStatus status = renderer.Draw(target, out _);
        if (status.Failed)
        {
            context.DrawRectangle(new SolidColorBrush(GraphBackground), null, Bounds);
        }

        if (ActiveTracing && double.IsFinite(TraceLocation.X) && double.IsFinite(TraceLocation.Y))
        {
            var brush = new SolidColorBrush(AvaloniaColor.FromRgb(0x00, 0x63, 0xB1));
            context.DrawEllipse(brush, new Pen(Brushes.White, 1), TraceLocation, 4, 4);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FormulaProperty && !_updatingFormulaFromCollection)
        {
            PlotGraph(keepCurrentView: false);
        }
        else if (change.Property == UseCommaDecimalSeparatorProperty)
        {
            LocalizationType localization = UseCommaDecimalSeparator
                ? LocalizationType.DecimalCommaAndListSemicolon
                : LocalizationType.DecimalPointAndListComma;
            _solver.ParsingOptions().SetLocalizationType(localization);
            _solver.FormatOptions().SetLocalizationType(localization);
            PlotGraph(keepCurrentView: true);
        }
        else if (change.Property == ForceProportionalAxesProperty ||
                 change.Property == AxesColorProperty ||
                 change.Property == GraphBackgroundProperty ||
                 change.Property == GridLinesColorProperty ||
                 change.Property == LineWidthProperty)
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
        base.OnPointerWheelChanged(e);
        if (Bounds.Width <= 0 || Bounds.Height <= 0 || e.Delta.Y == 0)
        {
            return;
        }

        double scale = 1 + (Math.Abs(e.Delta.Y) * 0.15);
        if (e.Delta.Y > 0)
        {
            scale = 1 / scale;
        }

        Point position = e.GetPosition(this);
        double centerX = (2 * position.X / Bounds.Width) - 1;
        double centerY = 1 - (2 * position.Y / Bounds.Height);
        if (_graph.GetRenderer().ScaleRange(centerX, centerY, scale).Succeeded)
        {
            RenderPreparedGraph();
            GraphViewChanged?.Invoke(this, GraphViewChangedReason.Manipulation);
            e.Handled = true;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        Point point = e.GetPosition(this);
        _activePointers[e.Pointer.Id] = point;
        _lastPanPoint = point;
        e.Pointer.Capture(this);
        UpdatePinchBaseline();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Point point = e.GetPosition(this);
        PointerValueChanged?.Invoke(point);
        if (_activePointers.ContainsKey(e.Pointer.Id))
        {
            _activePointers[e.Pointer.Id] = point;
            HandlePointerManipulation();
            return;
        }

        UpdateTracing(point);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        EndPointer(e.Pointer);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndPointer(e.Pointer);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        double amount = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 0.1 : 0.02;
        GraphStatus status = e.Key switch
        {
            Key.Left => _graph.GetRenderer().MoveRangeByRatio(-amount, 0),
            Key.Right => _graph.GetRenderer().MoveRangeByRatio(amount, 0),
            Key.Down => _graph.GetRenderer().MoveRangeByRatio(0, -amount),
            Key.Up => _graph.GetRenderer().MoveRangeByRatio(0, amount),
            Key.Add or Key.OemPlus => _graph.GetRenderer().ScaleRange(0, 0, 0.9),
            Key.Subtract or Key.OemMinus => _graph.GetRenderer().ScaleRange(0, 0, 1.1),
            Key.Home => _graph.GetRenderer().ResetRange(),
            _ => GraphStatus.False
        };
        if (status == GraphStatus.Ok)
        {
            RenderPreparedGraph();
            GraphViewChanged?.Invoke(this, e.Key == Key.Home
                ? GraphViewChangedReason.Reset
                : GraphViewChangedReason.Manipulation);
            e.Handled = true;
        }
    }

    private void HandlePointerManipulation()
    {
        if (_activePointers.Count >= 2)
        {
            Point[] points = _activePointers.Values.Take(2).ToArray();
            double distance = Distance(points[0], points[1]);
            if (_lastPinchDistance is > 0 && distance > 0)
            {
                Point center = new((points[0].X + points[1].X) * 0.5, (points[0].Y + points[1].Y) * 0.5);
                double centerX = (2 * center.X / Bounds.Width) - 1;
                double centerY = 1 - (2 * center.Y / Bounds.Height);
                _graph.GetRenderer().ScaleRange(centerX, centerY, _lastPinchDistance.Value / distance);
                RenderPreparedGraph();
                GraphViewChanged?.Invoke(this, GraphViewChangedReason.Manipulation);
            }

            _lastPinchDistance = distance;
            _lastPanPoint = null;
            return;
        }

        if (_activePointers.Count == 1 && _lastPanPoint is { } previous)
        {
            Point current = _activePointers.Values.First();
            double ratioX = (current.X - previous.X) / -Math.Max(1, Bounds.Width);
            double ratioY = (current.Y - previous.Y) / Math.Max(1, Bounds.Height);
            if (ratioX != 0 || ratioY != 0)
            {
                _graph.GetRenderer().MoveRangeByRatio(ratioX, ratioY);
                RenderPreparedGraph();
                GraphViewChanged?.Invoke(this, GraphViewChangedReason.Manipulation);
            }

            _lastPanPoint = current;
        }
    }

    private void EndPointer(IPointer pointer)
    {
        _activePointers.Remove(pointer.Id);
        pointer.Capture(null);
        _lastPanPoint = _activePointers.Count == 1 ? _activePointers.Values.First() : null;
        UpdatePinchBaseline();
    }

    private void UpdatePinchBaseline()
    {
        if (_activePointers.Count >= 2)
        {
            Point[] points = _activePointers.Values.Take(2).ToArray();
            _lastPinchDistance = Distance(points[0], points[1]);
        }
        else
        {
            _lastPinchDistance = null;
        }
    }

    private void UpdateTracing(Point point)
    {
        GraphStatus status = _graph.GetRenderer().GetClosePointData(
            point.X,
            point.Y,
            0.01,
            out _,
            out float screenX,
            out float screenY,
            out double x,
            out double y,
            out _,
            out _,
            out _);
        if (status == GraphStatus.Ok)
        {
            TraceLocation = new Point(screenX, screenY);
            TracingValueChanged?.Invoke(x, y);
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

        if (e.PropertyName is nameof(Equation.Expression) or nameof(Equation.IsLineEnabled))
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
        _updatingFormulaFromCollection = true;
        try
        {
            Formula = BuildFormula();
        }
        finally
        {
            _updatingFormulaFromCollection = false;
        }

        PlotGraph(keepCurrentView: true);
    }

    private string BuildFormula()
    {
        if (Equations.Count == 0)
        {
            return Formula;
        }

        string separator = UseCommaDecimalSeparator ? ";" : ",";
        return string.Join(separator, Equations.Where(equation => equation.IsLineEnabled)
            .Select(equation => equation.Expression)
            .Where(expression => !string.IsNullOrWhiteSpace(expression)));
    }

    private void BindEquations(IReadOnlyList<IEquation> initialized)
    {
        if (Equations.Count == 0)
        {
            return;
        }

        int graphIndex = 0;
        foreach (Equation equation in Equations)
        {
            equation.HasGraphError = false;
            if (!equation.IsLineEnabled || string.IsNullOrWhiteSpace(equation.Expression))
            {
                equation.GraphedEquation = null;
                continue;
            }

            if (graphIndex >= initialized.Count)
            {
                equation.GraphedEquation = null;
                equation.HasGraphError = true;
                continue;
            }

            IEquation graphEquation = initialized[graphIndex++];
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
        IGraphRenderer renderer = _graph.GetRenderer();
        EnsureSize(renderer);
        _ = renderer.PrepareGraph();
        InvalidateVisual();
    }

    private void EnsureSize(IGraphRenderer renderer)
    {
        uint width = (uint)Math.Clamp(Math.Round(Bounds.Width), 1, 32_768);
        uint height = (uint)Math.Clamp(Math.Round(Bounds.Height), 1, 32_768);
        renderer.SetGraphSize(width, height);
        double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        renderer.SetDpi((float)(96 * scaling), (float)(96 * scaling));
    }

    private void SetDefaultRange(bool xAxis, double minimum, double maximum)
    {
        bool changed = xAxis
            ? _graph.GetOptions().SetDefaultXRange(new AxisRange(minimum, maximum))
            : _graph.GetOptions().SetDefaultYRange(new AxisRange(minimum, maximum));
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
