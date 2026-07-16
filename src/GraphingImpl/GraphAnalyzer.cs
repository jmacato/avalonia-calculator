using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Graphing;
using Graphing.Analyzer;
using Graphing.Symbolics;

namespace GraphingImpl;

internal sealed class ManagedGraphAnalyzer : IGraphAnalyzer
{
    private readonly Lock _lock = new();
    private readonly EvaluationOptions _evaluationOptions;
    private GraphSnapshot _snapshot = GraphSnapshot.Empty;
    private GraphFunctionAnalysisData _data = GraphFunctionAnalysisData.Empty;

    public ManagedGraphAnalyzer(ManagedGraph graph, EvaluationOptions evaluationOptions)
    {
        _ = graph;
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
                         !snapshot.Definitions[0].IsInequality &&
                         snapshot.Definitions[0].Kind == GraphEquationKind.InverseX;
        return snapshot.Definitions.Length == 1 &&
               !snapshot.Definitions[0].IsInequality &&
               snapshot.Definitions[0].Kind == GraphEquationKind.ExplicitY;
    }

    public GraphStatus PerformFunctionAnalysis(uint analysisType)
    {
        if ((analysisType & ~(uint)PerformAnalysisType.All) != 0)
        {
            return GraphStatus.InvalidArgument;
        }

        GraphSnapshot snapshot = Snapshot();
        if (snapshot.Definitions.Length != 1 ||
            snapshot.Definitions[0].IsInequality ||
            snapshot.Definitions[0].Kind != GraphEquationKind.ExplicitY)
        {
            return GraphStatus.False;
        }

        var requested = (PerformAnalysisType)analysisType;
        InputExpression expression = SymbolicsAdapter.Translate(snapshot.Definitions[0].BoundarySyntax);
        var request = new AnalysisRequest(
            expression,
            SymbolicsAdapter.Translate(requested),
            SymbolicsAdapter.Translate(_evaluationOptions.GetTrigUnitMode()),
            "x",
            () => Snapshot().Revision == snapshot.Revision);
        AnalysisReport report;
        try
        {
            report = AnalysisEngine.Analyze(request);
        }
        catch (AnalysisCancelledException)
        {
            return GraphStatus.Cancelled;
        }

        GraphFunctionAnalysisData result = ToContract(report, requested);
        lock (_lock)
        {
            if (_snapshot.Revision != snapshot.Revision)
            {
                return GraphStatus.Cancelled;
            }

            _data = result;
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

    private static GraphFunctionAnalysisData ToContract(
        AnalysisReport report,
        PerformAnalysisType requested)
    {
        string domain = Proved(report.Domain, out RealSet domainSet)
            ? SymbolicsCompatibilityFormatter.Set(domainSet, "x")
            : string.Empty;
        string range = Proved(report.Range, out RealSet rangeSet)
            ? SymbolicsCompatibilityFormatter.Set(rangeSet, "y", range: true)
            : string.Empty;
        int parity = Proved(report.Parity, out FunctionParity parityValue)
            ? parityValue switch
            {
                FunctionParity.Odd => (int)FunctionParityType.Odd,
                FunctionParity.Even or FunctionParity.Both => (int)FunctionParityType.Even,
                _ => (int)FunctionParityType.None
            }
            : (int)FunctionParityType.Unknown;
        string zeros = Proved(report.Zeros, out RealSet zeroSet) && !zeroSet.IsEmpty
            ? SymbolicsCompatibilityFormatter.Set(zeroSet, "x")
            : string.Empty;
        string yIntercept = Proved(report.YIntercept, out OptionalValue<ExactReal> intercept) &&
                            intercept.HasValue
            ? "y = " + SymbolicsCompatibilityFormatter.Real(intercept.Value!)
            : string.Empty;
        ImmutableArray<string> minima = FormatPoints(report.Minima);
        ImmutableArray<string> maxima = FormatPoints(report.Maxima);
        ImmutableArray<string> inflections = FormatPoints(report.InflectionPoints);
        ImmutableArray<string> vertical = FormatAsymptotes(report.VerticalAsymptotes);
        ImmutableArray<string> horizontal = FormatAsymptotes(report.HorizontalAsymptotes);
        ImmutableArray<string> oblique = FormatAsymptotes(report.ObliqueAsymptotes);
        var monotone = new Dictionary<string, int>(StringComparer.Ordinal);
        if (Proved(report.Monotonicity, out ImmutableArray<MonotoneRegion> regions))
        {
            foreach (MonotoneRegion region in regions)
            {
                monotone[SymbolicsCompatibilityFormatter.MonotoneRegion(region)] =
                    region.Direction switch
                    {
                        Monotonicity.Increasing => (int)FunctionMonotonicityType.Ascending,
                        Monotonicity.Decreasing => (int)FunctionMonotonicityType.Descending,
                        _ => (int)FunctionMonotonicityType.Constant
                    };
            }
        }

        int periodicityDirection = (int)FunctionPeriodicityType.Unknown;
        string period = string.Empty;
        if (Proved(report.Period, out Periodicity periodicity))
        {
            periodicityDirection = periodicity.Kind == PeriodicityKind.NotPeriodic
                ? (int)FunctionPeriodicityType.NotPeriodic
                : (int)FunctionPeriodicityType.Periodic;
            if (periodicity.FundamentalPeriod is not null)
            {
                period = SymbolicsCompatibilityFormatter.Real(periodicity.FundamentalPeriod);
            }
        }

        int tooComplex = TooComplex(report, requested);
        return new GraphFunctionAnalysisData(
            domain,
            range,
            parity,
            periodicityDirection,
            period,
            zeros,
            yIntercept,
            minima,
            maxima,
            inflections,
            vertical,
            horizontal,
            oblique,
            new ReadOnlyDictionary<string, int>(monotone),
            tooComplex);
    }

    private static ImmutableArray<string> FormatPoints(
        ProofOutcome<ImmutableArray<FeaturePoint>> outcome) =>
        Proved(outcome, out ImmutableArray<FeaturePoint> points)
            ? points.Select(SymbolicsCompatibilityFormatter.FeaturePoint).ToImmutableArray()
            : [];

    private static ImmutableArray<string> FormatAsymptotes(
        ProofOutcome<ImmutableArray<Asymptote>> outcome) =>
        Proved(outcome, out ImmutableArray<Asymptote> asymptotes)
            ? asymptotes.Select(SymbolicsCompatibilityFormatter.Asymptote).ToImmutableArray()
            : [];

    private static int TooComplex(AnalysisReport report, PerformAnalysisType requested)
    {
        int result = 0;
        AddUnknown(requested.HasFlag(PerformAnalysisType.Domain), report.Domain, AnalysisType.Domain, ref result);
        AddUnknown(requested.HasFlag(PerformAnalysisType.Range), report.Range, AnalysisType.Range, ref result);
        AddUnknown(requested.HasFlag(PerformAnalysisType.Parity), report.Parity, AnalysisType.Parity, ref result);
        bool interceptions = requested.HasFlag(PerformAnalysisType.InterceptionPointsWithXAndYAxis);
        AddUnknown(interceptions, report.Zeros, AnalysisType.Zeros, ref result);
        AddUnknown(interceptions, report.YIntercept, AnalysisType.YIntercept, ref result);
        bool critical = requested.HasFlag(PerformAnalysisType.CriticalPoints);
        AddUnknown(critical, report.Minima, AnalysisType.Minima, ref result);
        AddUnknown(critical, report.Maxima, AnalysisType.Maxima, ref result);
        AddUnknown(critical, report.InflectionPoints, AnalysisType.InflectionPoints, ref result);
        bool asymptotes = requested.HasFlag(PerformAnalysisType.Asymptotes);
        AddUnknown(asymptotes, report.VerticalAsymptotes, AnalysisType.VerticalAsymptotes, ref result);
        AddUnknown(asymptotes, report.HorizontalAsymptotes, AnalysisType.HorizontalAsymptotes, ref result);
        AddUnknown(asymptotes, report.ObliqueAsymptotes, AnalysisType.ObliqueAsymptotes, ref result);
        AddUnknown(
            requested.HasFlag(PerformAnalysisType.Monotonicity),
            report.Monotonicity,
            AnalysisType.Monotonicity,
            ref result);
        AddUnknown(requested.HasFlag(PerformAnalysisType.Period), report.Period, AnalysisType.Period, ref result);
        return result;
    }

    private static void AddUnknown<T>(
        bool requested,
        ProofOutcome<T> outcome,
        AnalysisType type,
        ref int bits)
    {
        if (requested && outcome.State == ProofState.Unknown)
        {
            bits |= 1 << (int)type;
        }
    }

    private static bool Proved<T>(ProofOutcome<T> outcome, out T value)
    {
        if (outcome.State == ProofState.Proved && outcome.Value is not null)
        {
            value = outcome.Value;
            return true;
        }

        value = default!;
        return false;
    }

    private GraphSnapshot Snapshot()
    {
        lock (_lock)
        {
            return _snapshot;
        }
    }
}
