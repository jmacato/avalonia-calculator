using System;
using System.Collections.Generic;
using Windows.UI;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.System;
using Windows.Storage.Streams;
using Graphing;
using GraphControl;

namespace GraphControl
{
    public sealed class Grapher : UserControl
    {
        public static readonly DependencyProperty ForceProportionalAxesProperty = DependencyProperty.Register(nameof(ForceProportionalAxes), typeof(bool), typeof(Grapher), new PropertyMetadata(true, OnForceProportionalAxesPropertyChanged));
        public static readonly DependencyProperty UseCommaDecimalSeperatorProperty = DependencyProperty.Register(nameof(UseCommaDecimalSeperator), typeof(bool), typeof(Grapher), new PropertyMetadata(false, OnUseCommaDecimalSeperatorPropertyChanged));
        public static readonly DependencyProperty VariablesProperty = DependencyProperty.Register(nameof(Variables), typeof(IDictionary<string, Variable>), typeof(Grapher), new PropertyMetadata(new Dictionary<string, Variable>()));
        public static readonly DependencyProperty EquationsProperty = DependencyProperty.Register(nameof(Equations), typeof(EquationCollection), typeof(Grapher), new PropertyMetadata(null, OnEquationsPropertyChanged));
        public static readonly DependencyProperty AxesColorProperty = DependencyProperty.Register(nameof(AxesColor), typeof(Color), typeof(Grapher), new PropertyMetadata(Colors.Transparent, OnAxesColorPropertyChanged));
        public static readonly DependencyProperty GraphBackgroundProperty = DependencyProperty.Register(nameof(GraphBackground), typeof(Color), typeof(Grapher), new PropertyMetadata(Colors.Transparent, OnGraphBackgroundPropertyChanged));
        public static readonly DependencyProperty GridLinesColorProperty = DependencyProperty.Register(nameof(GridLinesColor), typeof(Color), typeof(Grapher), new PropertyMetadata(Colors.Transparent, OnGridLinesColorPropertyChanged));
        public static readonly DependencyProperty LineWidthProperty = DependencyProperty.Register(nameof(LineWidth), typeof(double), typeof(Grapher), new PropertyMetadata(2.0, OnLineWidthPropertyChanged));
        public static readonly DependencyProperty IsKeepCurrentViewProperty = DependencyProperty.Register(nameof(IsKeepCurrentView), typeof(bool), typeof(Grapher), new PropertyMetadata(false));
        public bool ForceProportionalAxes { get => (bool)GetValue(ForceProportionalAxesProperty); set => SetValue(ForceProportionalAxesProperty, value); }
        public bool UseCommaDecimalSeperator { get => (bool)GetValue(UseCommaDecimalSeperatorProperty); set => SetValue(UseCommaDecimalSeperatorProperty, value); }
        public IDictionary<string, Variable> Variables => (IDictionary<string, Variable>)GetValue(VariablesProperty);
        public EquationCollection Equations => (EquationCollection)GetValue(EquationsProperty);
        public Color AxesColor { get => (Color)GetValue(AxesColorProperty); set => SetValue(AxesColorProperty, value); }
        public Color GraphBackground { get => (Color)GetValue(GraphBackgroundProperty); set => SetValue(GraphBackgroundProperty, value); }
        public Color GridLinesColor { get => (Color)GetValue(GridLinesColorProperty); set => SetValue(GridLinesColorProperty, value); }
        public double LineWidth { get => (double)GetValue(LineWidthProperty); set => SetValue(LineWidthProperty, value); }
        public bool IsKeepCurrentView { get => (bool)GetValue(IsKeepCurrentViewProperty); set => SetValue(IsKeepCurrentViewProperty, value); }

        public bool ActiveTracing
        {
            get => _renderMain != null && _renderMain.ActiveTracing;
            set
            {
                if (_renderMain != null && _renderMain.ActiveTracing != value)
                {
                    _renderMain.ActiveTracing = value;
                    UpdateTracingChanged();
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveTracing)));
                }
            }
        }

        public Point TraceLocation { get => _renderMain != null ? _renderMain.TraceLocation : new Point(); }

        public Point ActiveTraceCursorPosition
        {
            get => _renderMain != null ? _renderMain.ActiveTraceCursorPosition : new Point();
            set
            {
                if (_renderMain != null && _renderMain.ActiveTraceCursorPosition != value)
                {
                    _renderMain.ActiveTraceCursorPosition = value;
                    UpdateTracingChanged();
                }
            }
        }

        public int TrigUnitMode
        {
            get => (int)_solver.EvalOptions().TrigUnitMode;
            set
            {
                if (value != (int)_solver.EvalOptions().TrigUnitMode)
                {
                    _solver.EvalOptions().TrigUnitMode = (Graphing.EvalTrigUnitMode)value;
                    _trigUnitsChanged = true;
                    PlotGraph(true);
                }
            }
        }

        public double XAxisMin
        {
            get => _graph.GetOptions().GetDefaultXRange().Item1;
            set
            {
                var newValue = new Tuple<double, double>(value, XAxisMax);
                if (_graph != null)
                {
                    _graph.GetOptions().SetDefaultXRange(newValue);
                    if (_renderMain != null)
                    {
                        _renderMain.RunRenderPass();
                    }
                }
            }
        }

        public double XAxisMax
        {
            get => _graph.GetOptions().GetDefaultXRange().Item2;
            set
            {
                var newValue = new Tuple<double, double>(XAxisMin, value);
                if (_graph != null)
                {
                    _graph.GetOptions().SetDefaultXRange(newValue);
                    if (_renderMain != null)
                    {
                        _renderMain.RunRenderPass();
                    }
                }
            }
        }

        public double YAxisMin
        {
            get => _graph.GetOptions().GetDefaultYRange().Item1;
            set
            {
                var newValue = new Tuple<double, double>(value, YAxisMax);
                if (_graph != null)
                {
                    _graph.GetOptions().SetDefaultYRange(newValue);
                    if (_renderMain != null)
                    {
                        _renderMain.RunRenderPass();
                    }
                }
            }
        }

        public double YAxisMax
        {
            get => _graph.GetOptions().GetDefaultYRange().Item2;
            set
            {
                var newValue = new Tuple<double, double>(YAxisMin, value);
                if (_graph != null)
                {
                    _graph.GetOptions().SetDefaultYRange(newValue);
                    if (_renderMain != null)
                    {
                        _renderMain.RunRenderPass();
                    }
                }
            }
        }

        public event EventHandler<TracingValueChangedEventArgs>? TracingValueChangedEvent;
        public event EventHandler<PointerValueChangedEventArgs>? PointerValueChangedEvent;
        public event EventHandler<TracingChangedEventArgs>? TracingChangedEvent;
        public event EventHandler<GraphViewChangedEventArgs>? GraphViewChangedEvent;
        public event RoutedEventHandler? GraphPlottedEvent;
        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<VariablesUpdatedEventArgs>? VariablesUpdated;
        private readonly MathSolverImplementation _solver;
        private readonly Graphing.IGraph _graph;
        private RenderMain? _renderMain;
        private bool _calculatedForceProportional;
        private bool _trigUnitsChanged;
        private bool[] _keysPressed = new bool[5]; // Left, Right, Down, Up, Accelerator
        private bool _moving;
        private DispatcherTimer? _tracingTrackingTimer;
        private CoreCursor? _cachedCursor;
        private int _errorType;
        private int _errorCode;
        private bool _resetUsingInitialDisplayRange;
        private bool _rangeUpdatedBySettings;
        private double _initialDisplayRangeXMin;
        private double _initialDisplayRangeXMax;
        private double _initialDisplayRangeYMin;
        private double _initialDisplayRangeYMax;
        public Grapher()
        {
            DefaultStyleKey = typeof(Grapher);
            // TODO: In real implementation, this would instantiate the native solver
            _solver = new MathSolverImplementation(); //Graphing.IMathSolver.CreateMathSolver();
            _graph = _solver.CreateGrapher();
            SetValue(EquationsProperty, new EquationCollection());
            _solver.ParsingOptions().SetFormatType(Graphing.FormatType.MathML);
            _solver.FormatOptions().SetFormatType(Graphing.FormatType.MathML);
            _solver.FormatOptions().SetMathMLPrefix("mml");
            // Set up manipulation modes
            ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateY | ManipulationModes.TranslateInertia | ManipulationModes.Scale | ManipulationModes.ScaleInertia;
            // Register for keyboard events
            var coreWindow = CoreWindow.GetForCurrentThread();
            coreWindow.KeyDown += OnCoreKeyDown;
            coreWindow.KeyUp += OnCoreKeyUp;
            // Register other events
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Get the SwapChainPanel from the template
            var swapChainPanel = GetTemplateChild("GraphSurface") as SwapChainPanel;
            if (swapChainPanel != null)
            {
                swapChainPanel.AllowFocusOnInteraction = true;
                _renderMain = new RenderMain(swapChainPanel);
                _renderMain.BackgroundColor = GraphBackground;
            }

            TryUpdateGraph(false);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            // The rest of initialization is done in OnLoaded
        }

        public void ZoomFromCenter(double scale)
        {
            ScaleRange(0, 0, scale);
            GraphViewChangedEvent?.Invoke(this, new GraphViewChangedEventArgs(GraphViewChangedReason.Manipulation));
        }

        public void ResetGrid()
        {
            if (_graph != null && _renderMain != null)
            {
                var renderer = _graph.GetRenderer();
                if (renderer is not null)
                {
                    if (_resetUsingInitialDisplayRange)
                    {
                        renderer.SetDisplayRanges(_initialDisplayRangeXMin, _initialDisplayRangeXMax, _initialDisplayRangeYMin, _initialDisplayRangeYMax);
                        _resetUsingInitialDisplayRange = false;
                    }
                    else if (_rangeUpdatedBySettings)
                    {
                        IsKeepCurrentView = false;
                        TryPlotGraph(false, false);
                        _rangeUpdatedBySettings = false;
                        GraphViewChangedEvent?.Invoke(this, new GraphViewChangedEventArgs(GraphViewChangedReason.Reset));
                        return;
                    }
                    else
                    {
                        renderer.ResetRange();
                    }

                    _renderMain.RunRenderPass();
                    GraphViewChangedEvent?.Invoke(this, new GraphViewChangedEventArgs(GraphViewChangedReason.Reset));
                }
            }
        }

        public void SetVariable(string variableName, double newValue)
        {
            if (!Variables.ContainsKey(variableName))
            {
                Variables[variableName] = new Variable(newValue);
            }

            if (_graph != null && _renderMain != null)
            {
                _graph.SetArgValue(variableName, newValue);
                _renderMain.RunRenderPass();
            }
        }

        public string ConvertToLinear(string mmlString)
        {
            _solver.FormatOptions().SetFormatType(Graphing.FormatType.LinearInput);
            int errorCode, errorType;
            var expression = _solver.ParseInput(mmlString, out errorCode, out errorType);
            var linearExpression = _solver.Serialize(expression);
            _solver.FormatOptions().SetFormatType(Graphing.FormatType.MathML);
            return linearExpression;
        }

        public string FormatMathML(string mmlString)
        {
            int errorCode, errorType;
            var expression = _solver.ParseInput(mmlString, out errorCode, out errorType);
            return _solver.Serialize(expression);
        }

        public void PlotGraph(bool keepCurrentView)
        {
            TryPlotGraph(keepCurrentView, false);
        }

        public KeyGraphFeaturesInfo? AnalyzeEquation(Equation equation)
        {
            if (equation is null)
            {
                throw new ArgumentNullException(nameof(equation));
            }

            var graph = GetGraph(equation);
            if (graph == null)
                return null;
            SetGraphArgs(graph);
            var analyzer = graph.GetAnalyzer();
            if (analyzer == null)
                return null;
            List<Equation> equationVector = new List<Equation>
            {
                equation
            };
            UpdateGraphOptions(graph.GetOptions(), equationVector);
            bool variableIsNotX;
            if (analyzer.CanFunctionAnalysisBePerformed(out variableIsNotX) && !variableIsNotX)
            {
                if (analyzer.PerformFunctionAnalysis((uint)Graphing.PerformAnalysisType.All) == 0)
                {
                    var functionAnalysisData = _solver.Analyze(analyzer);
                    return KeyGraphFeaturesInfo.Create(functionAnalysisData);
                }
            }
            else if (variableIsNotX)
            {
                return KeyGraphFeaturesInfo.Create((AnalysisErrorType)1); // VariableIsNotX
            }
            else
            {
                return KeyGraphFeaturesInfo.Create((AnalysisErrorType)2); // AnalysisNotSupported
            }

            return KeyGraphFeaturesInfo.Create((AnalysisErrorType)3); // AnalysisCouldNotBePerformed
        }

        public void GetDisplayRanges(out double xMin, out double xMax, out double yMin, out double yMax)
        {
            xMin = xMax = yMin = yMax = 0;
            if (_renderMain != null)
            {
                var render = _graph.GetRenderer();
                if (render != null)
                {
                    render.GetDisplayRanges(out xMin, out xMax, out yMin, out yMax);
                }
            }
        }

        public void SetDisplayRanges(double xMin, double xMax, double yMin, double yMax)
        {
            var render = _graph.GetRenderer();
            if (render != null)
            {
                render.SetDisplayRanges(xMin, xMax, yMin, yMax);
                _rangeUpdatedBySettings = true;
                if (_renderMain != null)
                {
                    _renderMain.RunRenderPass();
                    GraphViewChangedEvent?.Invoke(this, new GraphViewChangedEventArgs(GraphViewChangedReason.Manipulation));
                }
            }
        }

        private void ScaleRange(double centerX, double centerY, double scale)
        {
            if (_graph != null && _renderMain != null)
            {
                var renderer = _graph.GetRenderer();
                if (renderer != null && renderer.ScaleRange(centerX, centerY, scale) == 0)
                {
                    _renderMain.RunRenderPass();
                    GraphViewChangedEvent?.Invoke(this, new GraphViewChangedEventArgs(GraphViewChangedReason.Manipulation));
                }
            }
        }

        private void TryPlotGraph(bool keepCurrentView, bool shouldRetry)
        {
            if (TryUpdateGraph(keepCurrentView))
            {
                SetEquationsAsValid();
            }
            else
            {
                SetEquationErrors();
                // If we failed to plot the graph, try again after the bad equations are flagged.
                if (shouldRetry)
                {
                    TryUpdateGraph(keepCurrentView);
                }
            }

            int valid = 0;
            int invalid = 0;
            foreach (var eq in Equations)
            {
                if (eq.HasGraphError)
                {
                    invalid++;
                }

                if (eq.IsValidated)
                {
                    valid++;
                }
            }

            if (!_trigUnitsChanged)
            {
                // Log equation count changes
                // TraceLogger.Instance.LogEquationCountChanged(valid, invalid);
            }

            _trigUnitsChanged = false;
            GraphPlottedEvent?.Invoke(this, new RoutedEventArgs());
        }

        private bool TryUpdateGraph(bool keepCurrentView)
        {
            bool successful = false;
            _errorCode = 0;
            _errorType = 0;
            if (_renderMain != null && _graph != null)
            {
                Graphing.IExpression? graphExpression = null;
                string request = string.Empty;
                var validEqs = GetGraphableEquations();
                // Will be set to true if the previous graph should be kept in the event of an error
                bool shouldKeepPreviousGraph = false;
                if (validEqs.Count > 0)
                {
                    request = "<mrow><mi>show2d</mi><mfenced separators=\"\">";
                    int numValidEquations = 0;
                    foreach (var eq in validEqs)
                    {
                        if (eq.IsValidated)
                        {
                            shouldKeepPreviousGraph = true;
                        }

                        if (numValidEquations++ > 0)
                        {
                            if (!UseCommaDecimalSeperator)
                            {
                                request += "<mo>,</mo>";
                            }
                            else
                            {
                                request += "<mo>;</mo>";
                            }
                        }

                        var equationRequest = eq.GetRequest();
                        // If the equation request failed, then fail graphing.
                        if (equationRequest == null)
                        {
                            return false;
                        }

                        string parsableEquation = "<mrow><mi>show2d</mi><mfenced separators=\"\">" + equationRequest + "</mfenced></mrow>";
                        // Wire up the corresponding error to an error message in the UI
                        graphExpression = _solver.ParseInput(parsableEquation, out _errorCode, out _errorType);
                        if (graphExpression == null)
                        {
                            return false;
                        }

                        request += equationRequest;
                    }

                    request += "</mfenced></mrow>";
                }

                if (!string.IsNullOrEmpty(request))
                {
                    graphExpression = _solver.ParseInput(request, out _errorCode, out _errorType);
                }

                IReadOnlyList<Graphing.IEquation>? initResult = null;
                if (graphExpression != null)
                {
                    initResult = TryInitializeGraph(keepCurrentView, graphExpression);
                    if (initResult != null)
                    {
                        for (int i = 0; i < validEqs.Count; i++)
                        {
                            validEqs[i].GraphedEquation = initResult[i];
                        }

                        UpdateGraphOptions(_graph.GetOptions(), validEqs);
                        SetGraphArgs(_graph);
                        _renderMain.Graph = _graph;
                        // It is possible that the render fails, in that case fall through to explicit empty initialization
                        bool renderSuccess = _renderMain.RunRenderPass();
                        if (renderSuccess)
                        {
                            UpdateVariables();
                            successful = true;
                        }
                        else
                        {
                            // If we failed to render then we have already lost the previous graph
                            shouldKeepPreviousGraph = false;
                            initResult = null;
                            _solver.HRErrorToErrorInfo(_renderMain.RenderError, out _errorCode, out _errorType);
                        }
                    }
                    else
                    {
                        _solver.HRErrorToErrorInfo(_graph.GetInitializationError(), out _errorCode, out _errorType);
                    }
                }

                // Do not re-initialize the graph to empty if there are still valid equations graphed
                if (initResult == null && !shouldKeepPreviousGraph)
                {
                    initResult = TryInitializeGraph(false, null);
                    if (initResult != null)
                    {
                        UpdateGraphOptions(_graph.GetOptions(), new List<Equation>());
                        SetGraphArgs(_graph);
                        _renderMain.Graph = _graph;
                        _renderMain.RunRenderPass();
                        UpdateVariables();
                        // Initializing an empty graph is only a success if there were no equations to graph.
                        successful = (validEqs.Count == 0);
                    }
                }
            }

            // Return true if we were able to graph and render all graphable equations
            return successful;
        }

        private void SetEquationsAsValid()
        {
            foreach (var eq in GetGraphableEquations())
            {
                eq.IsValidated = true;
            }
        }

        private void SetEquationErrors()
        {
            foreach (var eq in GetGraphableEquations())
            {
                if (!eq.IsValidated)
                {
                    eq.GraphErrorType = (ErrorType)_errorType;
                    eq.GraphErrorCode = _errorCode;
                    eq.HasGraphError = true;
                }
            }
        }

        private void SetGraphArgs(Graphing.IGraph graph)
        {
            if (graph != null && _renderMain != null)
            {
                foreach (var variablePair in Variables)
                {
                    graph.SetArgValue(variablePair.Key, variablePair.Value.Value);
                }
            }
        }

        private Graphing.IGraph? GetGraph(Equation equation)
        {
            Graphing.IGraph graph = _solver.CreateGrapher();
            string request = "<mrow><mi>show2d</mi><mfenced separators=\"\">" + equation.GetRequest() + "</mfenced></mrow>";
            int errorCode, errorType;
            var expr = _solver.ParseInput(request, out errorCode, out errorType);
            if (expr != null)
            {
                var eqs = graph.TryInitialize(expr);
                if (eqs != null && eqs.Count > 0)
                {
                    return graph;
                }
            }

            return null;
        }

        private void UpdateVariables()
        {
            var updatedVariables = new Dictionary<string, Variable>();
            if (_graph != null)
            {
                var graphVariables = _graph.GetVariables();
                foreach (var graphVar in graphVariables)
                {
                    string name = graphVar.VariableName;
                    if (name != "x" && name != "y")
                    {
                        if (!Variables.TryGetValue(name, out Variable variable))
                        {
                            variable = new Variable(1.0);
                        }

                        updatedVariables[name] = variable;
                    }
                }
            }

            if (Variables.Count != updatedVariables.Count)
            {
                // TraceLogger.Instance.LogVariableCountChanged(updatedVariables.Count);
            }

            SetValue(VariablesProperty, updatedVariables);
            VariablesUpdated?.Invoke(this, new VariablesUpdatedEventArgs(Variables));
        }

        private void UpdateGraphOptions(Graphing.IGraphingOptions options, List<Equation> validEqs)
        {
            options.ForceProportional = ForceProportionalAxes;
            if (!options.AllowKeyGraphFeaturesForFunctionsWithParameters)
            {
                options.AllowKeyGraphFeaturesForFunctionsWithParameters = true;
            }

            if (validEqs.Count > 0)
            {
                var graphColors = validEqs.Select(eq => eq.LineColor).ToList();
                options.SetGraphColors(graphColors);
                foreach (var eq in validEqs)
                {
                    if (eq.GraphedEquation != null)
                    {
                        if (!eq.HasGraphError && eq.IsSelected)
                        {
                            eq.GraphedEquation.TrySelectEquation();
                        }

                        eq.GraphedEquation.GetGraphEquationOptions().SetLineStyle((Graphing.LineStyle)eq.EquationStyle);
                        eq.GraphedEquation.GetGraphEquationOptions().SetLineWidth((float)LineWidth);
                        eq.GraphedEquation.GetGraphEquationOptions().SetSelectedEquationLineWidth((float)(LineWidth + ((LineWidth <= 2) ? 1 : 2)));
                    }
                }
            }
        }

        private List<Equation> GetGraphableEquations()
        {
            var validEqs = new List<Equation>();
            foreach (var eq in Equations)
            {
                if (eq.IsGraphableEquation())
                {
                    validEqs.Add(eq);
                }
            }

            return validEqs;
        }

        private IReadOnlyList<Graphing.IEquation>? TryInitializeGraph(bool keepCurrentView, Graphing.IExpression? graphingExp)
        {
            if (keepCurrentView || IsKeepCurrentView)
            {
                var renderer = _graph.GetRenderer();
                double xMin, xMax, yMin, yMax;
                renderer.GetDisplayRanges(out xMin, out xMax, out yMin, out yMax);
                var initResult = _graph.TryInitialize(graphingExp);
                if (initResult != null)
                {
                    if (IsKeepCurrentView)
                    {
                        // PrepareGraph() populates the values of the graph after TryInitialize but before rendering.
                        if (renderer.PrepareGraph() == 0)
                        {
                            // Get the initial display ranges from the graph that was just initialized to be used in ResetGrid
                            renderer.GetDisplayRanges(out _initialDisplayRangeXMin, out _initialDisplayRangeXMax, out _initialDisplayRangeYMin, out _initialDisplayRangeYMax);
                            _resetUsingInitialDisplayRange = true;
                        }
                    }

                    renderer.SetDisplayRanges(xMin, xMax, yMin, yMax);
                }

                return initResult;
            }
            else
            {
                _resetUsingInitialDisplayRange = false;
                return _graph.TryInitialize(graphingExp);
            }
        }

        private void UpdateTracingChanged()
        {
            RenderMain? renderMain = _renderMain;
            if (renderMain is null)
            {
                return;
            }

            if (renderMain.Tracing)
            {
                TracingChangedEvent?.Invoke(this, new TracingChangedEventArgs(true));
                TracingValueChangedEvent?.Invoke(this, new TracingValueChangedEventArgs(renderMain.XTraceValue, renderMain.YTraceValue));
            }
            else
            {
                TracingChangedEvent?.Invoke(this, new TracingChangedEventArgs(false));
            }
        }

        private static void OnEquationsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grapher = (Grapher)d;
            var oldValue = e.OldValue as EquationCollection;
            var newValue = e.NewValue as EquationCollection;
            if (oldValue != null)
            {
                oldValue.EquationChanged -= grapher.OnEquationChanged;
                oldValue.EquationStyleChanged -= grapher.OnEquationStyleChanged;
                oldValue.EquationLineEnabledChanged -= grapher.OnEquationLineEnabledChanged;
            }

            if (newValue != null)
            {
                newValue.EquationChanged += grapher.OnEquationChanged;
                newValue.EquationStyleChanged += grapher.OnEquationStyleChanged;
                newValue.EquationLineEnabledChanged += grapher.OnEquationLineEnabledChanged;
            }

            grapher.PlotGraph(false);
        }

        private void OnEquationChanged(object? sender, EquationChangedEventArgs eventArgs)
        {
            Equation equation = eventArgs.Equation;
            // Reset error properties
            equation.HasGraphError = false;
            equation.IsValidated = false;
            TryPlotGraph(false, true);
        }

        private void OnEquationStyleChanged(object? sender, EquationChangedEventArgs eventArgs)
        {
            Equation equation = eventArgs.Equation;
            if (_graph != null)
            {
                _graph.TryResetSelection();
                UpdateGraphOptions(_graph.GetOptions(), GetGraphableEquations());
            }

            if (_renderMain != null)
            {
                _renderMain.RunRenderPass();
            }
        }

        private void OnEquationLineEnabledChanged(object? sender, EquationChangedEventArgs eventArgs)
        {
            Equation equation = eventArgs.Equation;
            // If the equation is in an error state or is empty, it should not be graphed anyway
            if (equation.HasGraphError || string.IsNullOrEmpty(equation.Expression))
                return;
            bool keepCurrentView = true;
            // If the equation has changed, the IsLineEnabled state is reset.
            // This checks if the equation has been reset and sets keepCurrentView to false in this case.
            if (!equation.HasGraphError && !equation.IsValidated && equation.IsLineEnabled)
            {
                keepCurrentView = false;
            }

            PlotGraph(keepCurrentView);
        }

        private static void OnForceProportionalAxesPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grapher = (Grapher)d;
            var oldValue = (bool)e.OldValue;
            var newValue = (bool)e.NewValue;
            grapher._calculatedForceProportional = newValue;
            grapher.TryUpdateGraph(false);
        }

        private static void OnUseCommaDecimalSeperatorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grapher = (Grapher)d;
            var oldValue = (bool)e.OldValue;
            var newValue = (bool)e.NewValue;
            if (newValue)
            {
                grapher._solver.ParsingOptions().SetLocalizationType(Graphing.LocalizationType.DecimalCommaAndListSemicolon);
                grapher._solver.FormatOptions().SetLocalizationType(Graphing.LocalizationType.DecimalCommaAndListSemicolon);
            }
            else
            {
                grapher._solver.ParsingOptions().SetLocalizationType(Graphing.LocalizationType.DecimalPointAndListComma);
                grapher._solver.FormatOptions().SetLocalizationType(Graphing.LocalizationType.DecimalPointAndListComma);
            }
        }

        private static void OnAxesColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grapher = (Grapher)d;
            if (grapher._graph != null)
            {
                var axesColor = (Color)e.NewValue;
                grapher._graph.GetOptions().AxisColor = (axesColor);
                grapher._graph.GetOptions().FontColor = (axesColor);
            }
        }

        private static void OnGraphBackgroundPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grapher = (Grapher)d;
            if (grapher._renderMain != null)
            {
                grapher._renderMain.BackgroundColor = (Color)e.NewValue;
            }

            if (grapher._graph != null)
            {
                var color = (Color)e.NewValue;
                grapher._graph.GetOptions().BackColor = (color);
                grapher._graph.GetOptions().BoxColor = (color);
            }
        }

        private static void OnGridLinesColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grapher = (Grapher)d;
            if (grapher._renderMain != null && grapher._graph != null)
            {
                var gridLinesColor = (Color)e.NewValue;
                grapher._graph.GetOptions().GridColor = (gridLinesColor);
                grapher._renderMain.RunRenderPass();
            }
        }

        private static void OnLineWidthPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grapher = (Grapher)d;
            if (grapher._graph != null)
            {
                grapher.UpdateGraphOptions(grapher._graph.GetOptions(), grapher.GetGraphableEquations());
                if (grapher._renderMain != null)
                {
                    RenderMain.SetPointRadius((float)(grapher.LineWidth + 1));
                    grapher._renderMain.RunRenderPass();
                    // TraceLogger.Instance.LogLineWidthChanged();
                }
            }
        }

        private void OnCoreKeyDown(CoreWindow sender, KeyEventArgs e)
        {
            // We don't want to react to keyboard input unless the graph control has the focus
            var gcHasFocus = FocusManager.GetFocusedElement() as Grapher;
            if (gcHasFocus == null || gcHasFocus != this)
                return;
            switch (e.VirtualKey)
            {
                case VirtualKey.Left:
                case VirtualKey.Right:
                case VirtualKey.Down:
                case VirtualKey.Up:
                case VirtualKey.Shift:
                    HandleKey(true, e.VirtualKey);
                    break;
            }
        }

        private void OnCoreKeyUp(CoreWindow sender, KeyEventArgs e)
        {
            // We don't want to react to any keys when we are not in the graph control
            var gcHasFocus = FocusManager.GetFocusedElement() as Grapher;
            if (gcHasFocus == null || gcHasFocus != this)
                return;
            switch (e.VirtualKey)
            {
                case VirtualKey.Left:
                case VirtualKey.Right:
                case VirtualKey.Down:
                case VirtualKey.Up:
                case VirtualKey.Shift:
                    HandleKey(false, e.VirtualKey);
                    break;
            }
        }

        private void HandleKey(bool keyDown, VirtualKey key)
        {
            int pressedKeys = 0;
            if (key == VirtualKey.Left)
            {
                _keysPressed[0] = keyDown;
                if (keyDown)
                    pressedKeys++;
            }

            if (key == VirtualKey.Right)
            {
                _keysPressed[1] = keyDown;
                if (keyDown)
                    pressedKeys++;
            }

            if (key == VirtualKey.Down)
            {
                _keysPressed[2] = keyDown;
                if (keyDown)
                    pressedKeys++;
            }

            if (key == VirtualKey.Up)
            {
                _keysPressed[3] = keyDown;
                if (keyDown)
                    pressedKeys++;
            }

            if (key == VirtualKey.Shift)
            {
                _keysPressed[4] = keyDown;
            }

            if (pressedKeys > 0 && !_moving)
            {
                _moving = true;
                // Key(s) we care about, so ensure we are ticking our timer (and that we have one to tick)
                if (_tracingTrackingTimer == null)
                {
                    _tracingTrackingTimer = new DispatcherTimer();
                    _tracingTrackingTimer.Tick += HandleTracingMovementTick;
                    _tracingTrackingTimer.Interval = TimeSpan.FromMilliseconds(100);
                }

                _tracingTrackingTimer.Start();
            }
        }

        private void HandleTracingMovementTick(object sender, object e)
        {
            int delta = 5;
            int liveKeys = 0;
            if (_keysPressed[4]) // Accelerator (Shift)
            {
                delta = 1;
            }

            var curPos = ActiveTraceCursorPosition;
            if (_keysPressed[0]) // Left
            {
                liveKeys++;
                curPos.X -= delta;
                if (curPos.X < 0)
                {
                    curPos.X = 0;
                }
            }

            if (_keysPressed[1]) // Right
            {
                liveKeys++;
                curPos.X += delta;
                if (curPos.X > ActualWidth - delta)
                {
                    curPos.X = ActualWidth - delta;
                }
            }

            if (_keysPressed[3]) // Up
            {
                liveKeys++;
                curPos.Y -= delta;
                if (curPos.Y < 0)
                {
                    curPos.Y = 0;
                }
            }

            if (_keysPressed[2]) // Down
            {
                liveKeys++;
                curPos.Y += delta;
                if (curPos.Y > ActualHeight - delta)
                {
                    curPos.Y = ActualHeight - delta;
                }
            }

            if (liveKeys == 0)
            {
                _moving = false;
                // None of the keys we care about are being hit any longer so shut down our timer
                _tracingTrackingTimer?.Stop();
            }
            else
            {
                ActiveTraceCursorPosition = curPos;
                PointerValueChangedEvent?.Invoke(this, new PointerValueChangedEventArgs(curPos));
            }
        }

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            if (e is null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            if (_renderMain != null)
            {
                OnPointerMoved(e);
                e.Handled = true;
            }

            base.OnPointerEntered(e);
        }

        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            if (e is null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            if (_renderMain != null)
            {
                _renderMain.DrawNearestPoint = true;
                Point currPosition = e.GetCurrentPoint(this).Position;
                if (_renderMain.ActiveTracing)
                {
                    PointerValueChangedEvent?.Invoke(this, new PointerValueChangedEventArgs(currPosition));
                    ActiveTraceCursorPosition = currPosition;
                    if (_cachedCursor == null)
                    {
                        _cachedCursor = CoreWindow.GetForCurrentThread().PointerCursor;
                        CoreWindow.GetForCurrentThread().PointerCursor = null;
                    }
                }
                else if (_cachedCursor != null)
                {
                    _renderMain.PointerLocation = currPosition;
                    CoreWindow.GetForCurrentThread().PointerCursor = _cachedCursor;
                    _cachedCursor = null;
                    UpdateTracingChanged();
                }
                else
                {
                    _renderMain.PointerLocation = currPosition;
                    UpdateTracingChanged();
                }

                e.Handled = true;
            }

            base.OnPointerMoved(e);
        }

        protected override void OnPointerExited(PointerRoutedEventArgs e)
        {
            if (e is null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            if (_renderMain != null)
            {
                _renderMain.DrawNearestPoint = false;
                TracingChangedEvent?.Invoke(this, new TracingChangedEventArgs(false));
                e.Handled = true;
            }

            if (_cachedCursor != null)
            {
                CoreWindow.GetForCurrentThread().PointerCursor = _cachedCursor;
                _cachedCursor = null;
            }

            base.OnPointerExited(e);
        }

        protected override void OnPointerWheelChanged(PointerRoutedEventArgs e)
        {
            if (e is null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            var currentPointer = e.GetCurrentPoint(this);
            double delta = currentPointer.Properties.MouseWheelDelta;
            // The maximum delta is 120 according to Windows documentation
            // Apply a dampening effect so that small mouse movements have a smoother zoom
            const double scrollDamper = 0.15;
            double scale = 1.0 + (Math.Abs(delta) / 120.0) * scrollDamper;
            // positive delta if wheel scrolled away from the user
            if (delta >= 0)
            {
                scale = 1.0 / scale;
            }

            // For scaling, the graphing engine interprets x,y position between the range [-1, 1]
            // Translate the pointer position to the [-1, 1] bounds
            var pos = currentPointer.Position;
            var centerX = (2 * pos.X / ActualWidth - 1);
            var centerY = (1 - 2 * pos.Y / ActualHeight);
            ScaleRange(centerX, centerY, scale);
            GraphViewChangedEvent?.Invoke(this, new GraphViewChangedEventArgs(GraphViewChangedReason.Manipulation));
            e.Handled = true;
            base.OnPointerWheelChanged(e);
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            if (e is null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            // Set the pointer capture to the element being interacted with so that only it
            // will fire pointer-related events
            CapturePointer(e.Pointer);
            base.OnPointerPressed(e);
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            if (e is null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            ReleasePointerCapture(e.Pointer);
            base.OnPointerReleased(e);
        }

        protected override void OnPointerCanceled(PointerRoutedEventArgs e)
        {
            if (e is null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            ReleasePointerCapture(e.Pointer);
            base.OnPointerCanceled(e);
        }

        protected override void OnManipulationDelta(ManipulationDeltaRoutedEventArgs e)
        {
            if (e is null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            if (_renderMain != null && _graph != null)
            {
                var renderer = _graph.GetRenderer();
                if (renderer != null)
                {
                    // Only call for a render pass if we actually scaled or translated
                    bool needsRenderPass = false;
                    double width = ActualWidth;
                    double height = ActualHeight;
                    // Handle translation
                    var translation = e.Delta.Translation;
                    double translationX = translation.X;
                    double translationY = translation.Y;
                    if (translationX != 0 || translationY != 0)
                    {
                        // The graphing engine pans the graph according to a ratio for x and y
                        // A value of +1 means move a half screen in the positive direction for the given axis
                        // Convert the manipulation's translation values to ratios for the engine
                        translationX /= -width;
                        translationY /= height;
                        if (renderer.MoveRangeByRatio(translationX, translationY) != 0)
                        {
                            return;
                        }

                        needsRenderPass = true;
                    }

                    // Handle scaling
                    double scale = e.Delta.Scale;
                    if (scale != 1.0)
                    {
                        // The graphing engine interprets scale amounts as the inverse of the value retrieved
                        // from the ManipulationUpdatedEventArgs. Invert the scale amount for the engine
                        scale = 1.0 / scale;
                        // Convert from PointerPosition to graph position (-1 to 1 range)
                        var pos = e.Position;
                        double centerX = (2 * pos.X / width - 1);
                        double centerY = (1 - 2 * pos.Y / height);
                        if (renderer.ScaleRange(centerX, centerY, scale) != 0)
                        {
                            return;
                        }

                        needsRenderPass = true;
                    }

                    if (needsRenderPass)
                    {
                        _renderMain.RunRenderPass();
                        GraphViewChangedEvent?.Invoke(this, new GraphViewChangedEventArgs(GraphViewChangedReason.Manipulation));
                    }
                }
            }

            base.OnManipulationDelta(e);
        }

        public async Task<IRandomAccessStream?> GetGraphBitmapStreamAsync()
        {
            if (_renderMain != null && _graph != null)
            {
                var renderer = _graph.GetRenderer();
                if (renderer != null)
                {
                    Graphing.IBitmap bitmapOut;
                    bool hasSomeMissingDataOut;
                    int hr = renderer.GetBitmap(out bitmapOut, out hasSomeMissingDataOut);
                    if (hr == 0 && bitmapOut != null)
                    {
                        // Get the raw data
                        byte[] byteVector = bitmapOut.GetData();
                        // Create a memory stream
                        var stream = new InMemoryRandomAccessStream();
                        // Get a writer to transfer the data
                        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                        {
                            writer.WriteBytes(byteVector);
                            await writer.StoreAsync();
                            await writer.FlushAsync();
                        }

                        // Reset the stream position
                        stream.Seek(0);
                        return stream;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Grapher::GetGraphBitmapStream() unable to get graph image from renderer");
                        throw new InvalidOperationException($"Failed to get bitmap: {hr}");
                    }
                }
            }

            return null;
        }
    }
}
