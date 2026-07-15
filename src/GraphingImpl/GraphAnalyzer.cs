using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Globalization;
using Graphing;
using Graphing.Analyzer;

namespace GraphingImpl;

internal sealed class ManagedGraphAnalyzer : IGraphAnalyzer
{
    private const int Samples = 4096;
    private readonly Lock _lock = new();
    private readonly ManagedGraph _graph;
    private readonly EvaluationOptions _evaluationOptions;
    private GraphSnapshot _snapshot = GraphSnapshot.Empty;
    private GraphFunctionAnalysisData _data = GraphFunctionAnalysisData.Empty;

    public ManagedGraphAnalyzer(ManagedGraph graph, EvaluationOptions evaluationOptions)
    {
        _graph = graph;
        _evaluationOptions = evaluationOptions;
    }

    public GraphFunctionAnalysisData Data
    {
        get
        {
            lock (_lock)
            {
                return _data;
            }
        }
    }

    public bool CanFunctionAnalysisBePerformed(out bool variableIsNotX)
    {
        GraphSnapshot snapshot = Snapshot();
        variableIsNotX = snapshot.Definitions.Length == 1 &&
                         snapshot.Definitions[0].Kind == GraphEquationKind.InverseX;
        return snapshot.Definitions.Length == 1 &&
               snapshot.Definitions[0].Kind == GraphEquationKind.ExplicitY;
    }

    public GraphStatus PerformFunctionAnalysis(uint analysisType)
    {
        if ((analysisType & ~(uint)PerformAnalysisType.All) != 0)
        {
            return GraphStatus.InvalidArgument;
        }

        GraphSnapshot snapshot = Snapshot();
        if (snapshot.Definitions.Length != 1 || snapshot.Definitions[0].Kind != GraphEquationKind.ExplicitY)
        {
            return GraphStatus.False;
        }

        CompiledGraphEquation definition = snapshot.Definitions[0];
        var requested = (PerformAnalysisType)analysisType;
        AnalysisResult result = Analyze(definition, snapshot, requested);
        lock (_lock)
        {
            if (_snapshot.Revision != snapshot.Revision)
            {
                return GraphStatus.Cancelled;
            }

            _data = result.ToContract();
        }

        return GraphStatus.Ok;
    }

    public GraphStatus GetAnalysisTypeCaption(AnalysisType type, out string caption)
    {
        caption = type switch
        {
            AnalysisType.Domain => "Domain",
            AnalysisType.Range => "Range",
            AnalysisType.Parity => "Parity",
            AnalysisType.Zeros => "Zeros",
            AnalysisType.YIntercept => "Y-intercept",
            AnalysisType.Minima => "Minima",
            AnalysisType.Maxima => "Maxima",
            AnalysisType.InflectionPoints => "Inflection points",
            AnalysisType.VerticalAsymptotes => "Vertical asymptotes",
            AnalysisType.HorizontalAsymptotes => "Horizontal asymptotes",
            AnalysisType.ObliqueAsymptotes => "Oblique asymptotes",
            AnalysisType.Monotonicity => "Monotonicity",
            AnalysisType.Period => "Period",
            _ => string.Empty
        };
        return caption.Length == 0 ? GraphStatus.InvalidArgument : GraphStatus.Ok;
    }

    public GraphStatus GetMessage(GraphAnalyzerMessage message, out string text)
    {
        text = message switch
        {
            GraphAnalyzerMessage.None => "No data",
            GraphAnalyzerMessage.NoZeros => "No zeros",
            GraphAnalyzerMessage.NoYIntercept => "No y-intercept",
            GraphAnalyzerMessage.NoMinima => "No minima",
            GraphAnalyzerMessage.NoMaxima => "No maxima",
            GraphAnalyzerMessage.NoInflectionPoints => "No inflection points",
            GraphAnalyzerMessage.NoVerticalAsymptotes => "No vertical asymptotes",
            GraphAnalyzerMessage.NoHorizontalAsymptotes => "No horizontal asymptotes",
            GraphAnalyzerMessage.NoObliqueAsymptotes => "No oblique asymptotes",
            GraphAnalyzerMessage.NotAbleToCalculate => "Not able to calculate",
            GraphAnalyzerMessage.NotAbleToMarkAllGraphFeatures => "Not able to mark all graph features",
            GraphAnalyzerMessage.TheseFeaturesAreTooComplexToCalculate => "These features are too complex to calculate",
            GraphAnalyzerMessage.ThisFeatureIsTooComplexToCalculate => "This feature is too complex to calculate",
            _ => string.Empty
        };
        return text.Length == 0 ? GraphStatus.InvalidArgument : GraphStatus.Ok;
    }

    internal void UpdateSnapshot(GraphSnapshot snapshot)
    {
        lock (_lock)
        {
            _snapshot = snapshot;
            _data = GraphFunctionAnalysisData.Empty;
        }
    }

    private AnalysisResult Analyze(
        CompiledGraphEquation definition,
        GraphSnapshot snapshot,
        PerformAnalysisType requested)
    {
        if (TryAnalyzeCanonicalTrigonometricFunction(definition.Syntax, requested, out AnalysisResult symbolic))
        {
            return symbolic;
        }

        var result = new AnalysisResult();
        SymbolicFacts facts = SymbolicFacts.For(definition.Syntax, _evaluationOptions.GetTrigUnitMode());
        if (requested.HasFlag(PerformAnalysisType.Domain))
        {
            result.Domain = facts.Domain ?? "−∞ < x < ∞";
        }

        if (requested.HasFlag(PerformAnalysisType.Range))
        {
            result.Range = facts.Range ?? NumericRange(definition, snapshot);
            if (result.Range.Length == 0)
            {
                result.TooComplex |= FeatureBit(AnalysisType.Range);
            }
        }

        if (requested.HasFlag(PerformAnalysisType.Parity))
        {
            result.Parity = facts.Parity ?? NumericParity(definition, snapshot);
        }

        if (requested.HasFlag(PerformAnalysisType.InterceptionPointsWithXAndYAxis))
        {
            result.Zeros = FindZeros(definition, snapshot);
            EvaluationValue intercept = Evaluate(definition, snapshot, 0);
            result.YIntercept = intercept.IsFinite ? Point(0, intercept.Value) : string.Empty;
        }

        if (requested.HasFlag(PerformAnalysisType.CriticalPoints))
        {
            FindCriticalPoints(definition, snapshot, result);
        }

        if (requested.HasFlag(PerformAnalysisType.Asymptotes))
        {
            FindAsymptotes(definition, snapshot, facts, result);
        }

        if (requested.HasFlag(PerformAnalysisType.Monotonicity))
        {
            FindMonotonicity(definition, snapshot, result);
        }

        if (requested.HasFlag(PerformAnalysisType.Period))
        {
            result.PeriodicityDirection = facts.Period is null
                ? (int)FunctionPeriodicityType.Unknown
                : (int)FunctionPeriodicityType.Periodic;
            result.Period = facts.Period ?? string.Empty;
            if (facts.Period is null)
            {
                result.TooComplex |= FeatureBit(AnalysisType.Period);
            }
        }

        return result;
    }

    private string NumericRange(CompiledGraphEquation definition, GraphSnapshot snapshot)
    {
        double minimum = double.PositiveInfinity;
        double maximum = double.NegativeInfinity;
        int finite = 0;
        for (int index = 0; index <= Samples; index++)
        {
            double x = -100 + (200d * index / Samples);
            EvaluationValue value = Evaluate(definition, snapshot, x);
            if (!value.IsFinite)
            {
                continue;
            }

            minimum = Math.Min(minimum, value.Value);
            maximum = Math.Max(maximum, value.Value);
            finite++;
        }

        return finite == 0 ? string.Empty : $"{Format(minimum)} ≤ y ≤ {Format(maximum)}";
    }

    private int NumericParity(CompiledGraphEquation definition, GraphSnapshot snapshot)
    {
        bool even = true;
        bool odd = true;
        for (int index = 1; index <= 64; index++)
        {
            double x = 10d * index / 64;
            EvaluationValue positive = Evaluate(definition, snapshot, x);
            EvaluationValue negative = Evaluate(definition, snapshot, -x);
            if (!positive.IsFinite || !negative.IsFinite)
            {
                continue;
            }

            double tolerance = 1e-9 * Math.Max(1, Math.Max(Math.Abs(positive.Value), Math.Abs(negative.Value)));
            even &= Math.Abs(positive.Value - negative.Value) <= tolerance;
            odd &= Math.Abs(positive.Value + negative.Value) <= tolerance;
        }

        return even
            ? (int)FunctionParityType.Even
            : odd
                ? (int)FunctionParityType.Odd
                : (int)FunctionParityType.None;
    }

    private string FindZeros(CompiledGraphEquation definition, GraphSnapshot snapshot)
    {
        var zeros = new List<double>();
        double previousX = -100;
        EvaluationValue previous = Evaluate(definition, snapshot, previousX);
        for (int index = 1; index <= Samples; index++)
        {
            double x = -100 + (200d * index / Samples);
            EvaluationValue current = Evaluate(definition, snapshot, x);
            if (current.IsFinite && Math.Abs(current.Value) <= 1e-10)
            {
                AddUnique(zeros, x);
            }
            else if (previous.IsFinite && current.IsFinite && Math.Sign(previous.Value) != Math.Sign(current.Value))
            {
                AddUnique(zeros, BisectZero(definition, snapshot, previousX, x));
            }

            previousX = x;
            previous = current;
        }

        return string.Join(", ", zeros.Select(value => Point(value, 0)));
    }

    private void FindCriticalPoints(
        CompiledGraphEquation definition,
        GraphSnapshot snapshot,
        AnalysisResult result)
    {
        int xIndex = definition.XIndex;
        if (xIndex < 0)
        {
            return;
        }

        var firstRoots = FindDerivativeRoots(definition, snapshot, secondDerivative: false);
        foreach (double x in firstRoots)
        {
            DerivativeJet jet = EvaluateJet(definition, snapshot, x);
            if (!jet.IsFinite)
            {
                continue;
            }

            if (jet.Second > 1e-7)
            {
                result.Minima.Add(Point(x, jet.Value));
            }
            else if (jet.Second < -1e-7)
            {
                result.Maxima.Add(Point(x, jet.Value));
            }
        }

        foreach (double x in FindDerivativeRoots(definition, snapshot, secondDerivative: true))
        {
            EvaluationValue value = Evaluate(definition, snapshot, x);
            if (value.IsFinite)
            {
                result.Inflections.Add(Point(x, value.Value));
            }
        }
    }

    private void FindAsymptotes(
        CompiledGraphEquation definition,
        GraphSnapshot snapshot,
        SymbolicFacts facts,
        AnalysisResult result)
    {
        if (facts.VerticalAsymptote is not null)
        {
            result.VerticalAsymptotes.Add(facts.VerticalAsymptote);
        }

        EvaluationValue positive = Evaluate(definition, snapshot, 1e6);
        EvaluationValue negative = Evaluate(definition, snapshot, -1e6);
        if (positive.IsFinite && negative.IsFinite)
        {
            double tolerance = 1e-6 * Math.Max(1, Math.Max(Math.Abs(positive.Value), Math.Abs(negative.Value)));
            if (Math.Abs(positive.Value - negative.Value) <= tolerance && Math.Abs(positive.Value) < 1e6)
            {
                result.HorizontalAsymptotes.Add($"y = {Format((positive.Value + negative.Value) * 0.5)}");
            }
        }
    }

    private void FindMonotonicity(
        CompiledGraphEquation definition,
        GraphSnapshot snapshot,
        AnalysisResult result)
    {
        List<double> critical = FindDerivativeRoots(definition, snapshot, secondDerivative: false);
        var boundaries = new List<double> { -100 };
        boundaries.AddRange(critical);
        boundaries.Add(100);
        for (int index = 1; index < boundaries.Count; index++)
        {
            double left = boundaries[index - 1];
            double right = boundaries[index];
            DerivativeJet middle = EvaluateJet(definition, snapshot, left + ((right - left) * 0.5));
            if (!middle.IsFinite)
            {
                continue;
            }

            int direction = middle.First > 1e-8
                ? (int)FunctionMonotonicityType.Ascending
                : middle.First < -1e-8
                    ? (int)FunctionMonotonicityType.Descending
                    : (int)FunctionMonotonicityType.Constant;
            result.MonotoneIntervals[$"({FormatBoundary(left)}, {FormatBoundary(right)})"] = direction;
        }
    }

    private List<double> FindDerivativeRoots(
        CompiledGraphEquation definition,
        GraphSnapshot snapshot,
        bool secondDerivative)
    {
        var roots = new List<double>();
        double previousX = -100;
        DerivativeJet previousJet = EvaluateJet(definition, snapshot, previousX);
        double previous = secondDerivative ? previousJet.Second : previousJet.First;
        for (int index = 1; index <= 2048; index++)
        {
            double x = -100 + (200d * index / 2048);
            DerivativeJet jet = EvaluateJet(definition, snapshot, x);
            double current = secondDerivative ? jet.Second : jet.First;
            if (jet.IsFinite && double.IsFinite(previous) && Math.Sign(current) != Math.Sign(previous))
            {
                double left = previousX;
                double right = x;
                for (int iteration = 0; iteration < 48; iteration++)
                {
                    double middle = left + ((right - left) * 0.5);
                    DerivativeJet middleJet = EvaluateJet(definition, snapshot, middle);
                    double middleValue = secondDerivative ? middleJet.Second : middleJet.First;
                    if (!middleJet.IsFinite)
                    {
                        break;
                    }

                    if (Math.Sign(previous) == Math.Sign(middleValue))
                    {
                        left = middle;
                        previous = middleValue;
                    }
                    else
                    {
                        right = middle;
                    }
                }

                AddUnique(roots, left + ((right - left) * 0.5));
            }

            previousX = x;
            previous = current;
        }

        return roots;
    }

    private double BisectZero(
        CompiledGraphEquation definition,
        GraphSnapshot snapshot,
        double left,
        double right)
    {
        EvaluationValue leftValue = Evaluate(definition, snapshot, left);
        for (int iteration = 0; iteration < 64; iteration++)
        {
            double middle = left + ((right - left) * 0.5);
            EvaluationValue middleValue = Evaluate(definition, snapshot, middle);
            if (!middleValue.IsFinite || Math.Abs(middleValue.Value) < 1e-13)
            {
                return middle;
            }

            if (Math.Sign(leftValue.Value) == Math.Sign(middleValue.Value))
            {
                left = middle;
                leftValue = middleValue;
            }
            else
            {
                right = middle;
            }
        }

        return left + ((right - left) * 0.5);
    }

    private EvaluationValue Evaluate(CompiledGraphEquation definition, GraphSnapshot snapshot, double x)
    {
        double[] values = snapshot.Values.ToArray();
        if (definition.XIndex >= 0)
        {
            values[definition.XIndex] = x;
        }

        return definition.Program.Evaluate(values, _evaluationOptions.GetTrigUnitMode());
    }

    private DerivativeJet EvaluateJet(CompiledGraphEquation definition, GraphSnapshot snapshot, double x)
    {
        if (definition.XIndex < 0)
        {
            return DerivativeJet.Invalid(EvaluationState.Undefined);
        }

        double[] values = snapshot.Values.ToArray();
        values[definition.XIndex] = x;
        return definition.Program.EvaluateJet(values, definition.XIndex, _evaluationOptions.GetTrigUnitMode());
    }

    private GraphSnapshot Snapshot()
    {
        lock (_lock)
        {
            return _snapshot;
        }
    }

    private static void AddUnique(List<double> values, double candidate)
    {
        if (values.All(value => Math.Abs(value - candidate) > 1e-6 * Math.Max(1, Math.Abs(candidate))))
        {
            values.Add(candidate);
        }
    }

    private static string Point(double x, double y) => $"({Format(x)}, {Format(y)})";

    private static string Format(double value) =>
        Math.Abs(value) < 5e-13 ? "0" : value.ToString("G10", CultureInfo.InvariantCulture);

    private static string FormatBoundary(double value) => value switch
    {
        <= -100 => "−∞",
        >= 100 => "∞",
        _ => Format(value)
    };

    private static int FeatureBit(AnalysisType type) => 1 << (int)type;

    private bool TryAnalyzeCanonicalTrigonometricFunction(
        AstNode node,
        PerformAnalysisType requested,
        out AnalysisResult result)
    {
        result = null!;
        if (node.Kind != AstKind.Function || node.Children.Length != 1 ||
            node.Children[0].Kind != AstKind.Variable ||
            !node.Children[0].Name.Equals("x", StringComparison.OrdinalIgnoreCase) ||
            node.Name is not ("sin" or "cos" or "tan"))
        {
            return false;
        }

        TrigSymbolSet symbols = TrigSymbolSet.For(_evaluationOptions.GetTrigUnitMode());
        var analysis = new AnalysisResult();
        PopulateTrigonometricIdentity(node.Name, symbols, requested, analysis);
        PopulateTrigonometricCriticalPoints(node.Name, symbols, requested, analysis);
        PopulateTrigonometricAsymptotes(node.Name, symbols, requested, analysis);
        PopulateTrigonometricMonotonicity(node.Name, symbols, requested, analysis);
        PopulateTrigonometricPeriod(node.Name, symbols, requested, analysis);
        result = analysis;
        return true;
    }

    private static void PopulateTrigonometricIdentity(
        string function,
        TrigSymbolSet symbols,
        PerformAnalysisType requested,
        AnalysisResult analysis)
    {
        if (requested.HasFlag(PerformAnalysisType.Domain))
        {
            analysis.Domain = function == "tan"
                ? $"x ≠ {symbols.HalfTurn}n₁ + {symbols.QuarterTurn}, n₁ ∈ ℤ"
                : "−∞ < x < ∞";
        }

        if (requested.HasFlag(PerformAnalysisType.Range))
        {
            analysis.Range = function == "tan" ? "−∞ < y < ∞" : "y ∈ [−1, 1]";
        }

        if (requested.HasFlag(PerformAnalysisType.Parity))
        {
            analysis.Parity = function == "cos"
                ? (int)FunctionParityType.Even
                : (int)FunctionParityType.Odd;
        }

        if (requested.HasFlag(PerformAnalysisType.InterceptionPointsWithXAndYAxis))
        {
            analysis.Zeros = function switch
            {
                "cos" => $"x = {symbols.HalfTurn}n₁ + {symbols.QuarterTurn}, n₁ ∈ ℤ",
                _ => $"x = {symbols.HalfTurn}n₁, n₁ ∈ ℤ"
            };
            analysis.YIntercept = function == "cos" ? "y = 1" : "y = 0";
        }
    }

    private static void PopulateTrigonometricCriticalPoints(
        string function,
        TrigSymbolSet symbols,
        PerformAnalysisType requested,
        AnalysisResult analysis)
    {
        if (!requested.HasFlag(PerformAnalysisType.CriticalPoints))
        {
            return;
        }

        switch (function)
        {
            case "sin":
                analysis.Minima.Add($"({symbols.FullTurn}n₁ + {symbols.ThreeQuarterTurn}, −1), n₁ ∈ ℤ");
                analysis.Maxima.Add($"({symbols.FullTurn}n₁ + {symbols.QuarterTurn}, 1), n₁ ∈ ℤ");
                analysis.Inflections.Add($"({symbols.HalfTurn}n₁, 0), n₁ ∈ ℤ");
                break;
            case "cos":
                analysis.Minima.Add($"({symbols.FullTurn}n₁ + {symbols.HalfTurn}, −1), n₁ ∈ ℤ");
                analysis.Maxima.Add($"({symbols.FullTurn}n₁, 1), n₁ ∈ ℤ");
                analysis.Inflections.Add($"({symbols.HalfTurn}n₁ + {symbols.QuarterTurn}, 0), n₁ ∈ ℤ");
                break;
            case "tan":
                analysis.Inflections.Add($"({symbols.HalfTurn}n₁, 0), n₁ ∈ ℤ");
                break;
        }
    }

    private static void PopulateTrigonometricAsymptotes(
        string function,
        TrigSymbolSet symbols,
        PerformAnalysisType requested,
        AnalysisResult analysis)
    {
        if (requested.HasFlag(PerformAnalysisType.Asymptotes) && function == "tan")
        {
            analysis.VerticalAsymptotes.Add(
                $"x = {symbols.HalfTurn}n₁ + {symbols.QuarterTurn}, n₁ ∈ ℤ");
        }
    }

    private static void PopulateTrigonometricMonotonicity(
        string function,
        TrigSymbolSet symbols,
        PerformAnalysisType requested,
        AnalysisResult analysis)
    {
        if (!requested.HasFlag(PerformAnalysisType.Monotonicity))
        {
            return;
        }

        switch (function)
        {
            case "sin":
                analysis.MonotoneIntervals[
                    $"({symbols.FullTurn}n₁ + {symbols.QuarterTurn}, {symbols.FullTurn}n₁ + {symbols.ThreeQuarterTurn}), n₁ ∈ ℤ"] =
                    (int)FunctionMonotonicityType.Descending;
                analysis.MonotoneIntervals[
                    $"({symbols.FullTurn}n₁ + {symbols.ThreeQuarterTurn}, {symbols.FullTurn}n₁ + {symbols.FiveQuarterTurn}), n₁ ∈ ℤ"] =
                    (int)FunctionMonotonicityType.Ascending;
                break;
            case "cos":
                analysis.MonotoneIntervals[
                    $"({symbols.FullTurn}n₁, {symbols.FullTurn}n₁ + {symbols.HalfTurn}), n₁ ∈ ℤ"] =
                    (int)FunctionMonotonicityType.Descending;
                analysis.MonotoneIntervals[
                    $"({symbols.FullTurn}n₁ + {symbols.HalfTurn}, {symbols.FullTurn}n₁ + {symbols.FullTurn}), n₁ ∈ ℤ"] =
                    (int)FunctionMonotonicityType.Ascending;
                break;
            case "tan":
                analysis.MonotoneIntervals[
                    $"({symbols.HalfTurn}n₁ − {symbols.QuarterTurn}, {symbols.HalfTurn}n₁ + {symbols.QuarterTurn}), n₁ ∈ ℤ"] =
                    (int)FunctionMonotonicityType.Ascending;
                break;
        }
    }

    private static void PopulateTrigonometricPeriod(
        string function,
        TrigSymbolSet symbols,
        PerformAnalysisType requested,
        AnalysisResult analysis)
    {
        if (!requested.HasFlag(PerformAnalysisType.Period))
        {
            return;
        }

        analysis.PeriodicityDirection = (int)FunctionPeriodicityType.Periodic;
        analysis.Period = function == "tan" ? symbols.HalfTurn : symbols.FullTurn;
    }

    private sealed record TrigSymbolSet(
        string QuarterTurn,
        string HalfTurn,
        string ThreeQuarterTurn,
        string FullTurn,
        string FiveQuarterTurn)
    {
        public static TrigSymbolSet For(EvalTrigUnitMode trigMode) => trigMode switch
        {
            EvalTrigUnitMode.Degrees => new("90°", "180°", "270°", "360°", "450°"),
            EvalTrigUnitMode.Grads => new("100", "200", "300", "400", "500"),
            _ => new("π/2", "π", "3π/2", "2π", "5π/2")
        };
    }

    private sealed class AnalysisResult
    {
        public string Domain { get; set; } = string.Empty;
        public string Range { get; set; } = string.Empty;
        public int Parity { get; set; } = (int)FunctionParityType.Unknown;
        public int PeriodicityDirection { get; set; } = (int)FunctionPeriodicityType.Unknown;
        public string Period { get; set; } = string.Empty;
        public string Zeros { get; set; } = string.Empty;
        public string YIntercept { get; set; } = string.Empty;
        public List<string> Minima { get; } = [];
        public List<string> Maxima { get; } = [];
        public List<string> Inflections { get; } = [];
        public List<string> VerticalAsymptotes { get; } = [];
        public List<string> HorizontalAsymptotes { get; } = [];
        public List<string> ObliqueAsymptotes { get; } = [];
        public Dictionary<string, int> MonotoneIntervals { get; } = new(StringComparer.Ordinal);
        public int TooComplex { get; set; }

        public GraphFunctionAnalysisData ToContract() => new(
            Domain,
            Range,
            Parity,
            PeriodicityDirection,
            Period,
            Zeros,
            YIntercept,
            Minima.ToImmutableArray(),
            Maxima.ToImmutableArray(),
            Inflections.ToImmutableArray(),
            VerticalAsymptotes.ToImmutableArray(),
            HorizontalAsymptotes.ToImmutableArray(),
            ObliqueAsymptotes.ToImmutableArray(),
            new ReadOnlyDictionary<string, int>(
                new Dictionary<string, int>(MonotoneIntervals, StringComparer.Ordinal)),
            TooComplex);
    }

    private sealed record SymbolicFacts(
        string? Domain,
        string? Range,
        int? Parity,
        string? Period,
        string? VerticalAsymptote)
    {
        public static SymbolicFacts For(AstNode node, EvalTrigUnitMode trigMode)
        {
            double fullPeriod = trigMode switch
            {
                EvalTrigUnitMode.Degrees => 360,
                EvalTrigUnitMode.Grads => 400,
                _ => Math.Tau
            };

            if (node.Kind == AstKind.Variable && node.Name.Equals("x", StringComparison.OrdinalIgnoreCase))
            {
                return new("−∞ < x < ∞", "−∞ < y < ∞", (int)FunctionParityType.Odd, null, null);
            }

            if (node.Kind == AstKind.Number)
            {
                string value = Format(node.Number);
                return new("−∞ < x < ∞", $"y = {value}", (int)FunctionParityType.Even, null, null);
            }

            if (node.Kind == AstKind.Function && node.Children.Length == 1 && IsX(node.Children[0]))
            {
                return node.Name switch
                {
                    "sin" => new("−∞ < x < ∞", "−1 ≤ y ≤ 1", (int)FunctionParityType.Odd, Format(fullPeriod), null),
                    "cos" => new("−∞ < x < ∞", "−1 ≤ y ≤ 1", (int)FunctionParityType.Even, Format(fullPeriod), null),
                    "tan" => new("x ≠ π/2 + kπ", "−∞ < y < ∞", (int)FunctionParityType.Odd, Format(fullPeriod / 2), null),
                    "abs" => new("−∞ < x < ∞", "0 ≤ y < ∞", (int)FunctionParityType.Even, null, null),
                    "sqrt" => new("x ≥ 0", "y ≥ 0", (int)FunctionParityType.None, null, null),
                    "ln" or "log" or "log10" => new("x > 0", "−∞ < y < ∞", (int)FunctionParityType.None, null, "x = 0"),
                    _ => new(null, null, null, null, null)
                };
            }

            if (node.Kind == AstKind.Power && IsX(node.Children[0]) &&
                node.Children[1].Kind == AstKind.Number &&
                node.Children[1].Number == Math.Truncate(node.Children[1].Number))
            {
                long power = (long)node.Children[1].Number;
                return (power & 1L) == 0
                    ? new("−∞ < x < ∞", "0 ≤ y < ∞", (int)FunctionParityType.Even, null, null)
                    : new("−∞ < x < ∞", "−∞ < y < ∞", (int)FunctionParityType.Odd, null, null);
            }

            if (node.Kind == AstKind.Divide && node.Children[0].Kind == AstKind.Number && IsX(node.Children[1]))
            {
                return new("x ≠ 0", "y ≠ 0", (int)FunctionParityType.Odd, null, "x = 0");
            }

            return new(null, null, null, null, null);
        }

        private static bool IsX(AstNode node) =>
            node.Kind == AstKind.Variable && node.Name.Equals("x", StringComparison.OrdinalIgnoreCase);
    }
}
