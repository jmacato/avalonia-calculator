using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Graphing;
using Graphing.Analyzer;
using Graphing.Symbolics;

namespace GraphingImpl;

internal sealed class ManagedGraphAnalyzer : ICancellableGraphAnalyzer
{
    private readonly EvaluationOptions _evaluationOptions;
    private GraphSnapshot _snapshot = GraphSnapshot.Empty;
    private ManagedGraphAnalyzerAnalysisResult _result = new(0, GraphFunctionAnalysisData.Empty);
    public ManagedGraphAnalyzer(ManagedGraph graph, EvaluationOptions evaluationOptions)
    {
        _ = graph;
        _evaluationOptions = evaluationOptions;
    }

    public GraphFunctionAnalysisData Data
    {
        get
        {
            GraphSnapshot snapshot = Volatile.Read(ref _snapshot);
            ManagedGraphAnalyzerAnalysisResult result = Volatile.Read(ref _result);
            return result.Revision == snapshot.Revision ? result.Data : GraphFunctionAnalysisData.Empty;
        }
    }

    public bool CanFunctionAnalysisBePerformed(out bool variableIsNotX)
    {
        GraphSnapshot snapshot = Snapshot();
        variableIsNotX = snapshot.Definitions.Length == 1 && !snapshot.Definitions[0].IsInequality && snapshot.Definitions[0].Kind == GraphEquationKind.InverseX;
        return snapshot.Definitions.Length == 1 && !snapshot.Definitions[0].IsInequality && snapshot.Definitions[0].Kind == GraphEquationKind.ExplicitY && WindowsFunctionAnalysisEligibility.IsSupported(snapshot.Definitions[0].BoundarySyntax, snapshot.Definitions[0].Source);
    }

    public GraphStatus PerformFunctionAnalysis(uint analysisType) => PerformFunctionAnalysis(analysisType, CancellationToken.None);
    public GraphStatus PerformFunctionAnalysis(uint analysisType, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return GraphStatus.Cancelled;
        }

        if ((analysisType & ~(uint)PerformAnalysisType.All) != 0)
        {
            return GraphStatus.InvalidArgument;
        }

        GraphSnapshot snapshot = Snapshot();
        if (snapshot.Definitions.Length != 1 || snapshot.Definitions[0].IsInequality || snapshot.Definitions[0].Kind != GraphEquationKind.ExplicitY || !WindowsFunctionAnalysisEligibility.IsSupported(snapshot.Definitions[0].BoundarySyntax, snapshot.Definitions[0].Source))
        {
            return GraphStatus.False;
        }

        var requested = (PerformAnalysisType)analysisType;
        InputExpression expression = SymbolicsAdapter.Translate(snapshot.Definitions[0].BoundarySyntax);
        var request = new AnalysisRequest(expression, SymbolicsAdapter.Translate(requested), SymbolicsAdapter.Translate(_evaluationOptions.GetTrigUnitMode()), "x", () => !cancellationToken.IsCancellationRequested && Snapshot().Revision == snapshot.Revision);
        AnalysisReport report;
        try
        {
            report = AnalysisEngine.Analyze(request);
        }
        catch (OperationCanceledException)
        {
            return GraphStatus.Cancelled;
        }

        GraphFunctionAnalysisData result = ToContract(report, requested, request.AngleUnit);
        if (cancellationToken.IsCancellationRequested || Volatile.Read(ref _snapshot).Revision != snapshot.Revision)
        {
            return GraphStatus.Cancelled;
        }

        Volatile.Write(ref _result, new ManagedGraphAnalyzerAnalysisResult(snapshot.Revision, result));
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
        Volatile.Write(ref _snapshot, snapshot);
        Volatile.Write(ref _result, new ManagedGraphAnalyzerAnalysisResult(snapshot.Revision, GraphFunctionAnalysisData.Empty));
    }

    private static GraphFunctionAnalysisData ToContract(AnalysisReport report, PerformAnalysisType requested, AngleUnit angleUnit)
    {
        WindowsFunctionAnalysisOverrides compatibility = WindowsFunctionAnalysisCompatibility.For(report, requested, angleUnit);
        string domain = FormatDomain(report, compatibility);
        string range = FormatRange(report, compatibility);
        int parity = !compatibility.Suppresses(CompatibilityFeatureFlag.Parity) && Proved(report.Parity, out FunctionParity parityValue) ? parityValue switch
        {
            FunctionParity.Odd => (int)FunctionParityType.Odd,
            FunctionParity.Even or FunctionParity.Both => (int)FunctionParityType.Even,
            _ => (int)FunctionParityType.None
        } : (int)FunctionParityType.Unknown;
        string zeros = FormatZeros(report, compatibility);
        string yIntercept = FormatYIntercept(report, compatibility);
        ImmutableArray<string> minima = compatibility.Suppresses(CompatibilityFeatureFlag.Minima) ? [] : FormatExtrema(report.Minima, CompatibilityFeatureFlag.Minima, compatibility);
        ImmutableArray<string> maxima = compatibility.Suppresses(CompatibilityFeatureFlag.Maxima) ? [] : FormatExtrema(report.Maxima, CompatibilityFeatureFlag.Maxima, compatibility);
        ImmutableArray<string> inflections = compatibility.Suppresses(CompatibilityFeatureFlag.InflectionPoints) ? [] : FormatPoints(report.InflectionPoints);
        ImmutableArray<string> vertical = FormatVerticalAsymptotes(report, compatibility);
        ImmutableArray<string> horizontal = compatibility.Suppresses(CompatibilityFeatureFlag.HorizontalAsymptotes) ? [] : FormatAsymptotes(report.HorizontalAsymptotes);
        ImmutableArray<string> oblique = compatibility.Suppresses(CompatibilityFeatureFlag.ObliqueAsymptotes) ? [] : FormatAsymptotes(report.ObliqueAsymptotes);
        var monotone = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!compatibility.Suppresses(CompatibilityFeatureFlag.Monotonicity) && Proved(report.Monotonicity, out ImmutableArray<MonotoneRegion> regions))
        {
            if (compatibility.TryGetProjection(CompatibilityFeatureFlag.Monotonicity, out CompatibilityValueProjectionKind tangentProjection) && tangentProjection == CompatibilityValueProjectionKind.CapturedAffineTangentPresentation)
            {
                foreach (MonotoneRegion region in regions)
                {
                    string displayed = CapturedAffineTangentCompatibilityFormatter.TryMonotoneRegion(region, out string projected) ? projected : SymbolicsCompatibilityFormatter.MonotoneRegion(region);
                    monotone[displayed] = region.Direction switch
                    {
                        Monotonicity.Increasing => (int)FunctionMonotonicityType.Ascending,
                        Monotonicity.Decreasing => (int)FunctionMonotonicityType.Descending,
                        _ => (int)FunctionMonotonicityType.Constant
                    };
                }
            }
            else if (compatibility.TryGetProjection(CompatibilityFeatureFlag.Monotonicity, out CompatibilityValueProjectionKind projection) && projection == CompatibilityValueProjectionKind.ConstantPunctureHalfLines && SymbolicsCompatibilityFormatter.TryConstantPunctureHalfLines(regions, out ImmutableArray<string> projectedRegions))
            {
                foreach (string projectedRegion in projectedRegions)
                {
                    monotone[projectedRegion] = (int)FunctionMonotonicityType.Constant;
                }
            }
            else
            {
                foreach (MonotoneRegion region in regions)
                {
                    monotone[SymbolicsCompatibilityFormatter.MonotoneRegion(region)] = region.Direction switch
                    {
                        Monotonicity.Increasing => (int)FunctionMonotonicityType.Ascending,
                        Monotonicity.Decreasing => (int)FunctionMonotonicityType.Descending,
                        _ => (int)FunctionMonotonicityType.Constant
                    };
                }
            }
        }

        int periodicityDirection = (int)FunctionPeriodicityType.Unknown;
        string period = string.Empty;
        if (!compatibility.Suppresses(CompatibilityFeatureFlag.Period) && Proved(report.Period, out Periodicity periodicity))
        {
            periodicityDirection = periodicity.Kind == PeriodicityKind.NotPeriodic ? (int)FunctionPeriodicityType.NotPeriodic : (int)FunctionPeriodicityType.Periodic;
            if (periodicity.FundamentalPeriod is not null)
            {
                ExactReal displayPeriod = periodicity.FundamentalPeriod;
                if (compatibility.TryGetProjection(CompatibilityFeatureFlag.Period, out CompatibilityValueProjectionKind projection) && projection == CompatibilityValueProjectionKind.DoubleFundamentalPeriod)
                {
                    displayPeriod = ExactRealArithmetic.Scale(displayPeriod, new BigRational(2));
                }

                period = SymbolicsCompatibilityFormatter.Real(displayPeriod);
            }
        }

        int tooComplex = TooComplex(report, requested, compatibility);
        var readOnlyMonotone = new ReadOnlyDictionary<string, int>(monotone);
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
            readOnlyMonotone,
            tooComplex)
        {
            Documents = FunctionAnalysisMathDocumentFactory.Create(
                domain,
                range,
                period,
                zeros,
                yIntercept,
                minima,
                maxima,
                inflections,
                vertical,
                horizontal,
                oblique,
                readOnlyMonotone)
        };
    }

    private static string FormatDomain(AnalysisReport report, WindowsFunctionAnalysisOverrides compatibility)
    {
        if (compatibility.Suppresses(CompatibilityFeatureFlag.Domain) || !Proved(report.Domain, out RealSet domainSet))
        {
            return string.Empty;
        }

        if (compatibility.TryGetProjection(CompatibilityFeatureFlag.Domain, out CompatibilityValueProjectionKind projection) && projection == CompatibilityValueProjectionKind.CapturedAffineTangentPresentation && CapturedAffineTangentCompatibilityFormatter.TryDomain(domainSet, "x", out string projected))
        {
            return projected;
        }

        return SymbolicsCompatibilityFormatter.Set(domainSet, "x");
    }

    private static string FormatZeros(AnalysisReport report, WindowsFunctionAnalysisOverrides compatibility)
    {
        if (compatibility.Suppresses(CompatibilityFeatureFlag.Zeros) || !Proved(report.Zeros, out RealSet zeroSet) || zeroSet.IsEmpty)
        {
            return string.Empty;
        }

        if (compatibility.TryGetProjection(CompatibilityFeatureFlag.Zeros, out CompatibilityValueProjectionKind projection))
        {
            if (projection == CompatibilityValueProjectionKind.EmptyZeroSetPresentation)
            {
                return string.Empty;
            }

            if (projection == CompatibilityValueProjectionKind.SymmetricNonnegativePeriodicPoints && SymbolicsCompatibilityFormatter.TrySymmetricNonnegativePeriodicPoints(zeroSet, "x", out string projected))
            {
                return projected;
            }

            if (projection == CompatibilityValueProjectionKind.CapturedAffineTangentPresentation && CapturedAffineTangentCompatibilityFormatter.TryZeros(zeroSet, "x", out string tangentProjected))
            {
                return tangentProjected;
            }
        }

        return SymbolicsCompatibilityFormatter.Set(zeroSet, "x");
    }

    private static string FormatRange(AnalysisReport report, WindowsFunctionAnalysisOverrides compatibility)
    {
        if (compatibility.Suppresses(CompatibilityFeatureFlag.Range) || !Proved(report.Range, out RealSet rangeSet))
        {
            return string.Empty;
        }

        if (compatibility.TryGetProjection(CompatibilityFeatureFlag.Range, out CompatibilityValueProjectionKind projection) && projection == CompatibilityValueProjectionKind.OpenFiniteIntervalEndpoints)
        {
            if (rangeSet is not IntervalSet { Lower.Kind: BoundKind.Finite, Upper.Kind: BoundKind.Finite } interval)
            {
                return string.Empty;
            }

            rangeSet = interval with
            {
                IncludesLower = false,
                IncludesUpper = false
            };
        }

        return SymbolicsCompatibilityFormatter.Set(rangeSet, "y", range: true);
    }

    private static string FormatYIntercept(AnalysisReport report, WindowsFunctionAnalysisOverrides compatibility)
    {
        if (compatibility.Suppresses(CompatibilityFeatureFlag.YIntercept) || !Proved(report.YIntercept, out OptionalValue<ExactReal> intercept) || !intercept.HasValue)
        {
            return string.Empty;
        }

        if (compatibility.TryGetProjection(CompatibilityFeatureFlag.YIntercept, out CompatibilityValueProjectionKind projection) && projection == CompatibilityValueProjectionKind.HyperbolicTangentAtZero)
        {
            return "y = tanh(0)";
        }

        return "y = " + SymbolicsCompatibilityFormatter.Real(intercept.Value!);
    }

    private static ImmutableArray<string> FormatPoints(ProofOutcome<ImmutableArray<FeaturePoint>> outcome) => Proved(outcome, out ImmutableArray<FeaturePoint> points) ? points.Select(SymbolicsCompatibilityFormatter.FeaturePoint).ToImmutableArray() : [];
    private static ImmutableArray<string> FormatExtrema(ProofOutcome<ImmutableArray<FeaturePoint>> outcome, CompatibilityFeatureFlag feature, WindowsFunctionAnalysisOverrides compatibility)
    {
        if (!Proved(outcome, out ImmutableArray<FeaturePoint> points))
        {
            return [];
        }

        if (compatibility.TryGetProjection(feature, out CompatibilityValueProjectionKind projection) && feature == CompatibilityFeatureFlag.Minima && projection == CompatibilityValueProjectionKind.ShiftedDoubleSineEndpointMinima && SymbolicsCompatibilityFormatter.TryShiftedDoubleSineEndpointMinima(points, out ImmutableArray<string> projected))
        {
            return projected;
        }

        if (projection == CompatibilityValueProjectionKind.SinePhaseExtremaPresentation && SymbolicsCompatibilityFormatter.TrySinePhaseExtrema(points, out ImmutableArray<string> phaseProjected))
        {
            return phaseProjected;
        }

        return points.Select(SymbolicsCompatibilityFormatter.FeaturePoint).ToImmutableArray();
    }

    private static ImmutableArray<string> FormatAsymptotes(ProofOutcome<ImmutableArray<Asymptote>> outcome) => Proved(outcome, out ImmutableArray<Asymptote> asymptotes) ? asymptotes.Select(SymbolicsCompatibilityFormatter.Asymptote).ToImmutableArray() : [];
    private static ImmutableArray<string> FormatVerticalAsymptotes(AnalysisReport report, WindowsFunctionAnalysisOverrides compatibility)
    {
        if (compatibility.Suppresses(CompatibilityFeatureFlag.VerticalAsymptotes) || !Proved(report.VerticalAsymptotes, out ImmutableArray<Asymptote> asymptotes))
        {
            return [];
        }

        if (compatibility.TryGetProjection(CompatibilityFeatureFlag.VerticalAsymptotes, out CompatibilityValueProjectionKind projection) && projection == CompatibilityValueProjectionKind.CapturedAffineTangentPresentation && CapturedAffineTangentCompatibilityFormatter.TryVerticalAsymptotes(asymptotes, out ImmutableArray<string> projected))
        {
            return projected;
        }

        return asymptotes.Select(SymbolicsCompatibilityFormatter.Asymptote).ToImmutableArray();
    }

    private static int TooComplex(AnalysisReport report, PerformAnalysisType requested, WindowsFunctionAnalysisOverrides compatibility)
    {
        CompatibilityFeatureFlag result = compatibility.AdditionalTooComplex;
        AddUnknown(requested.HasFlag(PerformAnalysisType.Domain), report.Domain, CompatibilityFeatureFlag.Domain, ref result);
        AddUnknown(requested.HasFlag(PerformAnalysisType.Range), report.Range, CompatibilityFeatureFlag.Range, ref result);
        AddUnknown(requested.HasFlag(PerformAnalysisType.Parity), report.Parity, CompatibilityFeatureFlag.Parity, ref result);
        bool interceptions = requested.HasFlag(PerformAnalysisType.InterceptionPointsWithXAndYAxis);
        AddUnknown(interceptions, report.Zeros, CompatibilityFeatureFlag.Zeros, ref result);
        AddUnknown(interceptions, report.YIntercept, CompatibilityFeatureFlag.YIntercept, ref result);
        bool critical = requested.HasFlag(PerformAnalysisType.CriticalPoints);
        AddUnknown(critical, report.Minima, CompatibilityFeatureFlag.Minima, ref result);
        AddUnknown(critical, report.Maxima, CompatibilityFeatureFlag.Maxima, ref result);
        AddUnknown(critical, report.InflectionPoints, CompatibilityFeatureFlag.InflectionPoints, ref result);
        bool asymptotes = requested.HasFlag(PerformAnalysisType.Asymptotes);
        AddUnknown(asymptotes, report.VerticalAsymptotes, CompatibilityFeatureFlag.VerticalAsymptotes, ref result);
        AddUnknown(asymptotes, report.HorizontalAsymptotes, CompatibilityFeatureFlag.HorizontalAsymptotes, ref result);
        AddUnknown(asymptotes, report.ObliqueAsymptotes, CompatibilityFeatureFlag.ObliqueAsymptotes, ref result);
        AddUnknown(requested.HasFlag(PerformAnalysisType.Monotonicity), report.Monotonicity, CompatibilityFeatureFlag.Monotonicity, ref result);
        AddUnknown(requested.HasFlag(PerformAnalysisType.Period), report.Period, CompatibilityFeatureFlag.Period, ref result);
        return (int)result;
    }

    private static void AddUnknown<T>(bool requested, ProofOutcome<T> outcome, CompatibilityFeatureFlag flag, ref CompatibilityFeatureFlag bits)
    {
        if (requested && outcome.State == ProofState.Unknown)
        {
            bits |= flag;
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

    private GraphSnapshot Snapshot() => Volatile.Read(ref _snapshot);
}
