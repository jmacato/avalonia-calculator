using System.Collections.Immutable;
using Graphing;
using Graphing.Analyzer;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class AffineMinMaxWindowsCompatibilityMatrixTests
{
    private static readonly SourceRange Source = new(0, 1);
    [Fact]
    public void GeneratedEightyFourRowMatrixMatchesSettledWindowsBoundary()
    {
        int rows = 0;
        int nonparallelRows = 0;
        int equalSlopeRows = 0;
        foreach (AffineMinMaxWindowsCompatibilityMatrixTestsLinePair pair in LinePairs())
        {
            foreach (string function in new[]
            {
                "min",
                "max"
            }

            )
            {
                for (int order = 0; order < 2; order++)
                {
                    AffineMinMaxWindowsCompatibilityMatrixTestsLineSpec first = order == 0 ? pair.Left : pair.Right;
                    AffineMinMaxWindowsCompatibilityMatrixTestsLineSpec second = order == 0 ? pair.Right : pair.Left;
                    string formula = EnvelopeFormula(function, first, second);
                    GraphFunctionAnalysisData actual = AnalyzePublic(formula);
                    AnalysisRequest request = Request(Envelope(function, first, second));
                    AnalysisReport report = AnalysisEngine.Analyze(request);
                    AssertEveryAffineMinMaxCertificateReplays(request, report, function, first, second);
                    if (first.Slope != second.Slope)
                    {
                        AssertNonparallelWindowsContract(actual, function, first, second);
                        nonparallelRows++;
                    }
                    else
                    {
                        AffineMinMaxWindowsCompatibilityMatrixTestsLineSpec selected = SelectParallelLine(function, first, second);
                        GraphFunctionAnalysisData expected = AnalyzePublic(LineFormula(selected));
                        AssertPublicContractsEqual(expected, actual);
                        equalSlopeRows++;
                    }

                    rows++;
                }
            }
        }

        Assert.Equal(84, rows);
        Assert.Equal(56, nonparallelRows);
        Assert.Equal(28, equalSlopeRows);
    }

    [Fact]
    public void NonparallelCompatibilityIsExactAndRequestedMaskAware()
    {
        const string strictKink = "max(-x,x-1/2)";
        GraphFunctionAnalysisData domain = AnalyzePublic(strictKink, PerformAnalysisType.Domain);
        Assert.Equal("x ∈ ℝ", domain.Domain);
        Assert.Equal(0, domain.TooComplexFeatures);
        GraphFunctionAnalysisData range = AnalyzePublic(strictKink, PerformAnalysisType.Range);
        Assert.Empty(range.Range);
        Assert.Equal(Bits(AnalysisType.Range), range.TooComplexFeatures);
        GraphFunctionAnalysisData intercepts = AnalyzePublic(strictKink, PerformAnalysisType.InterceptionPointsWithXAndYAxis);
        Assert.Empty(intercepts.Zeros);
        Assert.Equal("y = 0", intercepts.YIntercept);
        Assert.Equal(Bits(AnalysisType.Zeros), intercepts.TooComplexFeatures);
        GraphFunctionAnalysisData critical = AnalyzePublic(strictKink, PerformAnalysisType.CriticalPoints);
        Assert.Empty(critical.Minima);
        Assert.Empty(critical.Maxima);
        Assert.Empty(critical.InflectionPoints);
        Assert.Equal(Bits(AnalysisType.Minima, AnalysisType.Maxima, AnalysisType.InflectionPoints), critical.TooComplexFeatures);
        GraphFunctionAnalysisData asymptotes = AnalyzePublic("max(-x-1,1)", PerformAnalysisType.Asymptotes);
        AssertAllAsymptotesEmpty(asymptotes);
        Assert.Equal(Bits(AnalysisType.HorizontalAsymptotes, AnalysisType.ObliqueAsymptotes), asymptotes.TooComplexFeatures);
        GraphFunctionAnalysisData parity = AnalyzePublic(strictKink, PerformAnalysisType.Parity);
        Assert.Equal((int)FunctionParityType.Unknown, parity.Parity);
        Assert.Equal(Bits(AnalysisType.Parity), parity.TooComplexFeatures);
        GraphFunctionAnalysisData monotonicity = AnalyzePublic(strictKink, PerformAnalysisType.Monotonicity);
        Assert.Empty(monotonicity.MonotoneIntervals);
        Assert.Equal(Bits(AnalysisType.Monotonicity), monotonicity.TooComplexFeatures);
        GraphFunctionAnalysisData period = AnalyzePublic(strictKink, PerformAnalysisType.Period);
        Assert.Equal((int)FunctionPeriodicityType.NotPeriodic, period.PeriodicityDirection);
        Assert.Empty(period.PeriodicityExpression);
        Assert.Equal(0, period.TooComplexFeatures);
        GraphFunctionAnalysisData all = AnalyzePublic(strictKink);
        Assert.Equal(NonparallelTooComplexBits(), all.TooComplexFeatures);
    }

    [Fact]
    public void AffineCertificateBoundaryDoesNotLeakToNearMissExpressions()
    {
        InputExpression x = Variable();
        InputExpression hiddenHole = Function("min", Add(x, Multiply(Number(0), Divide(Number(1), Subtract(x, Number(3))))), Number(2));
        InputExpression nonlinear = Function("max", Power(Variable(), Number(2)), Number(1));
        InputExpression symbolicCoefficient = Function("min", Multiply(Named("pi"), Variable()), Number(1));
        InputExpression nestedEnvelope = Function("max", Function("min", Variable(), Number(1)), Number(2));
        foreach (InputExpression expression in new[]
        {
            hiddenHole,
            nonlinear,
            symbolicCoefficient,
            nestedEnvelope
        }

        )
        {
            AnalysisReport report = AnalysisEngine.Analyze(Request(expression));
            AssertNoAffineMinMaxCertificate(report);
        }
    }

    private static void AssertNonparallelWindowsContract(GraphFunctionAnalysisData actual, string function, AffineMinMaxWindowsCompatibilityMatrixTestsLineSpec first, AffineMinMaxWindowsCompatibilityMatrixTestsLineSpec second)
    {
        Assert.Equal("x ∈ ℝ", actual.Domain);
        Assert.Empty(actual.Range);
        Assert.Empty(actual.Zeros);
        Assert.Equal($"y = {DisplayRational(EnvelopeIntercept(function, first, second))}", actual.YIntercept);
        Assert.Empty(actual.Minima);
        Assert.Empty(actual.Maxima);
        Assert.Empty(actual.InflectionPoints);
        AssertAllAsymptotesEmpty(actual);
        Assert.Equal((int)FunctionParityType.Unknown, actual.Parity);
        Assert.Equal((int)FunctionPeriodicityType.NotPeriodic, actual.PeriodicityDirection);
        Assert.Empty(actual.PeriodicityExpression);
        Assert.Empty(actual.MonotoneIntervals);
        Assert.Equal(NonparallelTooComplexBits(), actual.TooComplexFeatures);
    }

    private static void AssertEveryAffineMinMaxCertificateReplays(AnalysisRequest request, AnalysisReport report, string function, AffineMinMaxWindowsCompatibilityMatrixTestsLineSpec first, AffineMinMaxWindowsCompatibilityMatrixTestsLineSpec second)
    {
        Assert.NotNull(report.Expression);
        var domainCertificate = Assert.IsType<AffineMinMaxProofCertificate>(report.Domain.Certificate);
        Assert.Equal(function, domainCertificate.Function);
        Assert.Equal(first.Slope, domainCertificate.FirstSlope);
        Assert.Equal(first.Intercept, domainCertificate.FirstIntercept);
        Assert.Equal(second.Slope, domainCertificate.SecondSlope);
        Assert.Equal(second.Intercept, domainCertificate.SecondIntercept);
        AssertAffineMinMaxProof(request, report, report.Domain);
        AssertAffineMinMaxProof(request, report, report.Range);
        AssertAffineMinMaxProof(request, report, report.Parity);
        AssertAffineMinMaxProof(request, report, report.Zeros);
        AssertAffineMinMaxProof(request, report, report.YIntercept);
        AssertAffineMinMaxProof(request, report, report.Minima);
        AssertAffineMinMaxProof(request, report, report.Maxima);
        AssertAffineMinMaxProof(request, report, report.InflectionPoints);
        AssertAffineMinMaxProof(request, report, report.VerticalAsymptotes);
        AssertAffineMinMaxProof(request, report, report.HorizontalAsymptotes);
        AssertAffineMinMaxProof(request, report, report.ObliqueAsymptotes);
        AssertAffineMinMaxProof(request, report, report.Monotonicity);
        AssertAffineMinMaxProof(request, report, report.Period);
    }

    private static void AssertAffineMinMaxProof<T>(AnalysisRequest request, AnalysisReport report, ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.IsType<AffineMinMaxProofCertificate>(outcome.Certificate);
        Assert.True(CertificateChecker.Check(request, report.Expression!, outcome));
    }

    private static void AssertNoAffineMinMaxCertificate(AnalysisReport report)
    {
        Assert.False(report.Domain.Certificate is AffineMinMaxProofCertificate);
        Assert.False(report.Range.Certificate is AffineMinMaxProofCertificate);
        Assert.False(report.Parity.Certificate is AffineMinMaxProofCertificate);
        Assert.False(report.Zeros.Certificate is AffineMinMaxProofCertificate);
        Assert.False(report.YIntercept.Certificate is AffineMinMaxProofCertificate);
        Assert.False(report.Minima.Certificate is AffineMinMaxProofCertificate);
        Assert.False(report.Maxima.Certificate is AffineMinMaxProofCertificate);
        Assert.False(report.InflectionPoints.Certificate is AffineMinMaxProofCertificate);
        Assert.False(report.VerticalAsymptotes.Certificate is AffineMinMaxProofCertificate);
        Assert.False(report.HorizontalAsymptotes.Certificate is AffineMinMaxProofCertificate);
        Assert.False(report.ObliqueAsymptotes.Certificate is AffineMinMaxProofCertificate);
        Assert.False(report.Monotonicity.Certificate is AffineMinMaxProofCertificate);
        Assert.False(report.Period.Certificate is AffineMinMaxProofCertificate);
    }

    private static void AssertPublicContractsEqual(GraphFunctionAnalysisData expected, GraphFunctionAnalysisData actual)
    {
        Assert.Equal(expected.Domain, actual.Domain);
        Assert.Equal(expected.Range, actual.Range);
        Assert.Equal(expected.Parity, actual.Parity);
        Assert.Equal(expected.PeriodicityDirection, actual.PeriodicityDirection);
        Assert.Equal(expected.PeriodicityExpression, actual.PeriodicityExpression);
        Assert.Equal(expected.Zeros, actual.Zeros);
        Assert.Equal(expected.YIntercept, actual.YIntercept);
        Assert.Equal(expected.Minima.ToArray(), actual.Minima.ToArray());
        Assert.Equal(expected.Maxima.ToArray(), actual.Maxima.ToArray());
        Assert.Equal(expected.InflectionPoints.ToArray(), actual.InflectionPoints.ToArray());
        Assert.Equal(expected.VerticalAsymptotes.ToArray(), actual.VerticalAsymptotes.ToArray());
        Assert.Equal(expected.HorizontalAsymptotes.ToArray(), actual.HorizontalAsymptotes.ToArray());
        Assert.Equal(expected.ObliqueAsymptotes.ToArray(), actual.ObliqueAsymptotes.ToArray());
        Assert.Equal(expected.MonotoneIntervals.OrderBy(static pair => pair.Key).ToArray(), actual.MonotoneIntervals.OrderBy(static pair => pair.Key).ToArray());
        Assert.Equal(expected.TooComplexFeatures, actual.TooComplexFeatures);
    }

    private static void AssertAllAsymptotesEmpty(GraphFunctionAnalysisData result)
    {
        Assert.Empty(result.VerticalAsymptotes);
        Assert.Empty(result.HorizontalAsymptotes);
        Assert.Empty(result.ObliqueAsymptotes);
    }

    private static AffineMinMaxWindowsCompatibilityMatrixTestsLineSpec SelectParallelLine(string function, AffineMinMaxWindowsCompatibilityMatrixTestsLineSpec first, AffineMinMaxWindowsCompatibilityMatrixTestsLineSpec second)
    {
        if (first.Intercept == second.Intercept)
        {
            return first;
        }

        bool selectFirst = function == "min" ? first.Intercept < second.Intercept : first.Intercept > second.Intercept;
        return selectFirst ? first : second;
    }

    private static BigRational EnvelopeIntercept(string function, AffineMinMaxWindowsCompatibilityMatrixTestsLineSpec first, AffineMinMaxWindowsCompatibilityMatrixTestsLineSpec second) => function == "min" ? RationalMin(first.Intercept, second.Intercept) : RationalMax(first.Intercept, second.Intercept);
    private static BigRational RationalMin(BigRational left, BigRational right) => left <= right ? left : right;
    private static BigRational RationalMax(BigRational left, BigRational right) => left >= right ? left : right;
    private static int NonparallelTooComplexBits() => Bits(AnalysisType.Range, AnalysisType.Parity, AnalysisType.Zeros, AnalysisType.Minima, AnalysisType.Maxima, AnalysisType.InflectionPoints, AnalysisType.HorizontalAsymptotes, AnalysisType.ObliqueAsymptotes, AnalysisType.Monotonicity);
    private static IEnumerable<AffineMinMaxWindowsCompatibilityMatrixTestsLinePair> LinePairs()
    {
        yield return Pair(Spec(-2, 0), Spec(-1, -1, 2));
        yield return Pair(Spec(-2, 1, 2), Spec(0, 0));
        yield return Pair(Spec(-2, -1, 2), Spec(1, 1, 2));
        yield return Pair(Spec(-2, 1), Spec(2, -1));
        yield return Pair(Spec(-1, -1), Spec(0, 1));
        yield return Pair(Spec(-1, 0), Spec(1, -1, 2));
        yield return Pair(Spec(-1, 1, 2), Spec(2, 0));
        yield return Pair(Spec(0, -1, 2), Spec(1, 1, 2));
        yield return Pair(Spec(0, 1), Spec(2, -1));
        yield return Pair(Spec(1, -1), Spec(2, 1));
        yield return Pair(Spec(-2, -1, 2), Spec(-2, 1, 2));
        yield return Pair(Spec(-1, -1, 2), Spec(-1, 1, 2));
        yield return Pair(Spec(0, -1, 2), Spec(0, 1, 2));
        yield return Pair(Spec(1, -1, 2), Spec(1, 1, 2));
        yield return Pair(Spec(2, -1, 2), Spec(2, 1, 2));
        yield return Pair(Spec(0, 1, 2), Spec(0, 1, 2));
        yield return Pair(Spec(2, -1, 2), Spec(2, -1, 2));
        yield return Pair(Spec(1, 0), Spec(-1, 0));
        yield return Pair(Spec(2, 0), Spec(-2, 0));
        yield return Pair(Spec(1, 1, 2), Spec(-1, 1, 2));
        yield return Pair(Spec(1, -1, 2), Spec(-1, 1, 2));
    }

    private static AffineMinMaxWindowsCompatibilityMatrixTestsLinePair Pair(AffineMinMaxWindowsCompatibilityMatrixTestsLineSpec first, AffineMinMaxWindowsCompatibilityMatrixTestsLineSpec second) => new(first, second);
    private static AffineMinMaxWindowsCompatibilityMatrixTestsLineSpec Spec(int slope, int intercept) => new(slope, intercept);
    private static AffineMinMaxWindowsCompatibilityMatrixTestsLineSpec Spec(int slope, int interceptNumerator, int interceptDenominator) => new(slope, new BigRational(interceptNumerator, interceptDenominator));
    private static GraphFunctionAnalysisData AnalyzePublic(string formula, PerformAnalysisType requested = PerformAnalysisType.All)
    {
        IMathSolver solver = MathSolver.CreateMathSolver();
        IExpression expression = solver.ParseInput(formula, out int errorCode, out int errorType) ?? throw new InvalidOperationException($"Parse failed for {formula}: {errorCode}/{errorType}");
        IGraph graph = solver.CreateGrapher();
        Assert.NotNull(graph.TryInitialize(expression));
        IGraphAnalyzer analyzer = graph.GetAnalyzer();
        Assert.True(analyzer.CanFunctionAnalysisBePerformed(out bool variableIsNotX));
        Assert.False(variableIsNotX);
        Assert.Equal(GraphStatus.Ok, analyzer.PerformFunctionAnalysis((uint)requested));
        return solver.Analyze(analyzer);
    }

    private static AnalysisRequest Request(InputExpression expression) => new(expression, AnalysisFeatures.All, AngleUnit.Radians, "x", static () => true);
    private static InputExpression Envelope(string function, AffineMinMaxWindowsCompatibilityMatrixTestsLineSpec first, AffineMinMaxWindowsCompatibilityMatrixTestsLineSpec second) => Function(function, Line(first), Line(second));
    private static InputExpression Line(AffineMinMaxWindowsCompatibilityMatrixTestsLineSpec line) => Add(Multiply(Number(line.Slope), Variable()), Number(line.Intercept));
    private static string EnvelopeFormula(string function, AffineMinMaxWindowsCompatibilityMatrixTestsLineSpec first, AffineMinMaxWindowsCompatibilityMatrixTestsLineSpec second) => $"{function}({LineFormula(first)},{LineFormula(second)})";
    private static string LineFormula(AffineMinMaxWindowsCompatibilityMatrixTestsLineSpec line)
    {
        if (line.Slope.IsZero)
        {
            return line.Intercept.ToString();
        }

        string variableTerm = line.Slope == BigRational.One ? "x" : line.Slope == BigRational.MinusOne ? "-x" : $"{line.Slope}*x";
        if (line.Intercept.IsZero)
        {
            return variableTerm;
        }

        string operation = line.Intercept.Sign > 0 ? "+" : string.Empty;
        return $"{variableTerm}{operation}{line.Intercept}";
    }

    private static string DisplayRational(BigRational value) => value.ToString().Replace('-', '−');
    private static int Bits(params AnalysisType[] features) => features.Aggregate(0, static (bits, feature) => bits | FeatureBit(feature));
    private static int FeatureBit(AnalysisType type) => type switch
    {
        AnalysisType.Domain => 1,
        AnalysisType.Range => 2,
        AnalysisType.Parity => 4,
        AnalysisType.Period => 8,
        AnalysisType.Zeros => 16,
        AnalysisType.YIntercept => 32,
        AnalysisType.Minima => 64,
        AnalysisType.Maxima => 128,
        AnalysisType.InflectionPoints => 256,
        AnalysisType.VerticalAsymptotes => 512,
        AnalysisType.HorizontalAsymptotes => 1024,
        AnalysisType.ObliqueAsymptotes => 2048,
        AnalysisType.Monotonicity => 4096,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
    private static InputExpression Variable() => InputExpression.Variable("x", Source);
    private static InputExpression Named(string name) => InputExpression.Variable(name, Source);
    private static InputExpression Number(int value) => Number(new BigRational(value));
    private static InputExpression Number(BigRational value) => InputExpression.Number(value, Source);
    private static InputExpression Add(InputExpression left, InputExpression right) => InputExpression.Binary(InputExpressionKind.Add, left, right, Source);
    private static InputExpression Subtract(InputExpression left, InputExpression right) => InputExpression.Binary(InputExpressionKind.Subtract, left, right, Source);
    private static InputExpression Multiply(InputExpression left, InputExpression right) => InputExpression.Binary(InputExpressionKind.Multiply, left, right, Source);
    private static InputExpression Divide(InputExpression left, InputExpression right) => InputExpression.Binary(InputExpressionKind.Divide, left, right, Source);
    private static InputExpression Power(InputExpression basis, InputExpression exponent) => InputExpression.Binary(InputExpressionKind.Power, basis, exponent, Source);
    private static InputExpression Function(string name, params InputExpression[] arguments) => InputExpression.Function(name, arguments.ToImmutableArray(), Source);
}
