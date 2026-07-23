using System.Collections.Immutable;
using Graphing;
using Graphing.Analyzer;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class SemialgebraicUnaryAnalysisTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Fact]
    public void AbsoluteValueAndPrincipalSquareRootCorpusHasExactCoreClaims()
    {
        InputExpression x = Variable();
        InputExpression xSquared = Power(x, 2);
        InputExpression shifted = Subtract(x, Number(1));
        (string Name, InputExpression Expression, string Domain, string Range, string Zeros,
            bool HasYIntercept, string? YIntercept, FunctionParity Parity)[] cases =
        [
            ("abs(x)", Abs(x), "reals", "interval[q:0,1,+inf,0]", "points[q:0]",
                true, "q:0", FunctionParity.Even),
            ("abs(-x)", Abs(Negate(x)), "reals", "interval[q:0,1,+inf,0]", "points[q:0]",
                true, "q:0", FunctionParity.Even),
            ("abs(2x-4)", Abs(Subtract(Multiply(Number(2), x), Number(4))),
                "reals", "interval[q:0,1,+inf,0]", "points[q:2]",
                true, "q:4", FunctionParity.Neither),
            ("abs(x^2-1)", Abs(Subtract(xSquared, Number(1))),
                "reals", "interval[q:0,1,+inf,0]", "points[q:-1,q:1]",
                true, "q:1", FunctionParity.Even),
            ("abs(abs(x))", Abs(Abs(x)), "reals", "interval[q:0,1,+inf,0]", "points[q:0]",
                true, "q:0", FunctionParity.Even),
            ("sqrt(x)", Sqrt(x), "interval[q:0,1,+inf,0]", "interval[q:0,1,+inf,0]",
                "points[q:0]", true, "q:0", FunctionParity.Neither),
            ("sqrt(x-1)", Sqrt(shifted), "interval[q:1,1,+inf,0]", "interval[q:0,1,+inf,0]",
                "points[q:1]", false, null, FunctionParity.Neither),
            ("sqrt(1-x)", Sqrt(Subtract(Number(1), x)), "interval[-inf,0,q:1,1]",
                "interval[q:0,1,+inf,0]", "points[q:1]", true, "q:1", FunctionParity.Neither),
            ("sqrt(1-x^2)", Sqrt(Subtract(Number(1), xSquared)), "interval[q:-1,1,q:1,1]",
                "interval[q:0,1,q:1,1]", "points[q:-1,q:1]", true, "q:1", FunctionParity.Even),
            ("sqrt(x^2-1)", Sqrt(Subtract(xSquared, Number(1))),
                "union[interval[-inf,0,q:-1,1],interval[q:1,1,+inf,0]]",
                "interval[q:0,1,+inf,0]", "points[q:-1,q:1]", false, null, FunctionParity.Even),
            ("sqrt((x-1)^2)", Sqrt(Power(shifted, 2)), "reals", "interval[q:0,1,+inf,0]",
                "points[q:1]", true, "q:1", FunctionParity.Neither),
            ("sqrt(-x^2)", Sqrt(Negate(xSquared)), "points[q:0]", "points[q:0]", "points[q:0]",
                true, "q:0", FunctionParity.Both),
            ("sqrt(-(x^2-1)^2)", Sqrt(Negate(Power(Subtract(xSquared, Number(1)), 2))),
                "union[points[q:-1],points[q:1]]", "points[q:0]", "points[q:-1,q:1]",
                false, null, FunctionParity.Both),
            ("sqrt((x-1)/(x+1))", Sqrt(Divide(shifted, Add(x, Number(1)))),
                "union[interval[-inf,0,q:-1,0],interval[q:1,1,+inf,0]]",
                "union[interval[q:0,1,q:1,0],interval[q:1,0,+inf,0]]", "points[q:1]",
                false, null, FunctionParity.Neither)
        ];

        foreach (var item in cases)
        {
            AnalysisRequest request = Request(item.Expression, AnalysisFeatures.All);
            AnalysisReport report = AnalysisEngine.Analyze(request);

            Assert.Equal(item.Domain, Proved(report.Domain, item.Name).Canonical);
            Assert.Equal(item.Range, Proved(report.Range, item.Name).Canonical);
            Assert.Equal(item.Zeros, Proved(report.Zeros, item.Name).Canonical);
            OptionalValue<ExactReal> intercept = Proved(report.YIntercept, item.Name);
            Assert.Equal(item.HasYIntercept, intercept.HasValue);
            if (item.HasYIntercept)
            {
                Assert.Equal(item.YIntercept, ExactRealCanonical.Format(intercept.Value!));
            }

            Assert.Equal(item.Parity, Proved(report.Parity, item.Name));
            Assert.Equal(PeriodicityKind.NotPeriodic, Proved(report.Period, item.Name).Kind);
            AssertDedicatedUnaryCertificates(report);
            AssertAllCertificatesReplay(request, report, item.Name);
        }
    }

    [Fact]
    public void ExactSignCellsDeriveEndpointsCuspsAndDisconnectedMonotonicity()
    {
        InputExpression x = Variable();
        AnalysisReport absolute = AnalysisEngine.Analyze(Request(
            Abs(Subtract(Power(x, 2), Number(1))),
            AnalysisFeatures.All));

        AssertPoints(Proved(absolute.Minima), ("q:-1", "q:0"), ("q:1", "q:0"));
        AssertPoints(Proved(absolute.Maxima), ("q:0", "q:1"));
        AssertPoints(Proved(absolute.InflectionPoints), ("q:-1", "q:0"), ("q:1", "q:0"));
        AssertRegions(
            Proved(absolute.Monotonicity),
            ("interval[-inf,0,q:-1,0]", Monotonicity.Decreasing),
            ("interval[q:-1,0,q:0,0]", Monotonicity.Increasing),
            ("interval[q:0,0,q:1,0]", Monotonicity.Decreasing),
            ("interval[q:1,0,+inf,0]", Monotonicity.Increasing));
        Assert.Empty(Proved(absolute.VerticalAsymptotes));
        Assert.Empty(Proved(absolute.HorizontalAsymptotes));
        Assert.Empty(Proved(absolute.ObliqueAsymptotes));

        AnalysisReport semicircle = AnalysisEngine.Analyze(Request(
            Sqrt(Subtract(Number(1), Power(x, 2))),
            AnalysisFeatures.All));
        AssertPoints(Proved(semicircle.Minima), ("q:-1", "q:0"), ("q:1", "q:0"));
        AssertPoints(Proved(semicircle.Maxima), ("q:0", "q:1"));
        Assert.Empty(Proved(semicircle.InflectionPoints));
        AssertRegions(
            Proved(semicircle.Monotonicity),
            ("interval[q:-1,0,q:0,0]", Monotonicity.Increasing),
            ("interval[q:0,0,q:1,0]", Monotonicity.Decreasing));

        AnalysisReport outsideHyperbola = AnalysisEngine.Analyze(Request(
            Sqrt(Subtract(Power(x, 2), Number(1))),
            AnalysisFeatures.All));
        AssertPoints(Proved(outsideHyperbola.Minima), ("q:-1", "q:0"), ("q:1", "q:0"));
        Assert.Empty(Proved(outsideHyperbola.Maxima));
        Assert.Empty(Proved(outsideHyperbola.InflectionPoints));
        AssertRegions(
            Proved(outsideHyperbola.Monotonicity),
            ("interval[-inf,0,q:-1,0]", Monotonicity.Decreasing),
            ("interval[q:1,0,+inf,0]", Monotonicity.Increasing));
        Assert.Equal(2, Proved(outsideHyperbola.ObliqueAsymptotes).Length);
    }

    [Fact]
    public void IsolatedDomainPointsAreNeitherExtremaNorMonotoneIntervals()
    {
        InputExpression x = Variable();
        foreach (InputExpression expression in new[]
                 {
                     Sqrt(Negate(Power(x, 2))),
                     Sqrt(Negate(Power(Subtract(Power(x, 2), Number(1)), 2)))
                 })
        {
            AnalysisReport report = AnalysisEngine.Analyze(Request(expression, AnalysisFeatures.All));
            Assert.Empty(Proved(report.Minima));
            Assert.Empty(Proved(report.Maxima));
            Assert.Empty(Proved(report.InflectionPoints));
            Assert.Empty(Proved(report.Monotonicity));
            Assert.Empty(Proved(report.VerticalAsymptotes));
            Assert.Empty(Proved(report.HorizontalAsymptotes));
            Assert.Empty(Proved(report.ObliqueAsymptotes));
        }
    }

    [Fact]
    public void ReducedInnerFunctionNeverErasesOriginalDomainHoles()
    {
        InputExpression x = Variable();
        InputExpression expression = Abs(Divide(
            Subtract(Power(x, 2), Number(1)),
            Subtract(x, Number(1))));
        AnalysisRequest request = Request(expression, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        Assert.Equal(
            "union[interval[-inf,0,q:1,0],interval[q:1,0,+inf,0]]",
            Proved(report.Domain).Canonical);
        Assert.Equal("interval[q:0,1,+inf,0]", Proved(report.Range).Canonical);
        Assert.Equal("points[q:-1]", Proved(report.Zeros).Canonical);
        AssertPoints(Proved(report.Minima), ("q:-1", "q:0"));
        AssertRegions(
            Proved(report.Monotonicity),
            ("interval[-inf,0,q:-1,0]", Monotonicity.Decreasing),
            ("interval[q:-1,0,q:1,0]", Monotonicity.Increasing),
            ("interval[q:1,0,+inf,0]", Monotonicity.Increasing));
        AssertAllCertificatesReplay(request, report, "retained hole");
    }

    [Fact]
    public void NestedAbsoluteAndSquareRootSquareNormalizationsAreMetamorphic()
    {
        InputExpression x = Variable();
        AnalysisReport absolute = AnalysisEngine.Analyze(Request(Abs(x), AnalysisFeatures.All));
        AnalysisReport reflected = AnalysisEngine.Analyze(Request(Abs(Negate(x)), AnalysisFeatures.All));
        AnalysisReport nested = AnalysisEngine.Analyze(Request(Abs(Abs(x)), AnalysisFeatures.All));
        AssertSameClaims(absolute, reflected);
        AssertSameClaims(absolute, nested);

        InputExpression shifted = Subtract(x, Number(1));
        AnalysisReport direct = AnalysisEngine.Analyze(Request(Abs(shifted), AnalysisFeatures.All));
        AnalysisReport rewritten = AnalysisEngine.Analyze(Request(Sqrt(Power(shifted, 2)), AnalysisFeatures.All));
        AssertSameClaims(direct, rewritten);
    }

    [Fact]
    public void DedicatedCertificatesReplayAndRejectChartFiberAndFormulaMutations()
    {
        InputExpression x = Variable();
        InputExpression expression = Abs(Subtract(Power(x, 2), Number(1)));
        AnalysisRequest request = Request(expression, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        RealSet range = Proved(report.Range);
        var certificate = Assert.IsType<SemialgebraicUnaryProofCertificate>(report.Range.Certificate);
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Range));

        UnarySignChartCertificate chart = certificate.Chart;
        ImmutableArray<int> firstGap = chart.GapSigns[0];
        SemialgebraicUnaryProofCertificate changedSign = certificate with
        {
            Chart = chart with
            {
                GapSigns = chart.GapSigns.SetItem(
                    0,
                    firstGap.SetItem(0, firstGap[0] == 0 ? 1 : -firstGap[0]))
            }
        };
        AssertRejected(changedSign);

        SemialgebraicUnaryProofCertificate changedDomain = certificate with
        {
            Chart = chart with
            {
                PointDomain = chart.PointDomain.SetItem(0, !chart.PointDomain[0])
            }
        };
        AssertRejected(changedDomain);

        UnaryRangeFiberWitness firstFiber = certificate.RangeFibers[0];
        SemialgebraicUnaryProofCertificate changedFiber = certificate with
        {
            RangeFibers = certificate.RangeFibers.SetItem(
                0,
                firstFiber with { HasPreimage = !firstFiber.HasPreimage })
        };
        AssertRejected(changedFiber);

        SemialgebraicUnaryProofCertificate changedFormula = certificate with
        {
            DomainFormula = new PolynomialBoolean(false)
        };
        AssertRejected(changedFormula);

        SemialgebraicUnaryProofCertificate changedRule = certificate with { Rule = "untrusted" };
        AssertRejected(changedRule);
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression! with { SourceOperands = [] },
            ProofOutcome<RealSet>.Proved(range, certificate)));

        void AssertRejected(SemialgebraicUnaryProofCertificate changed) =>
            Assert.False(CertificateChecker.Check(
                request,
                report.Expression!,
                ProofOutcome<RealSet>.Proved(range, changed)));
    }

    [Fact]
    public void ParityAuxiliaryCertificateMutationIsRejected()
    {
        InputExpression x = Variable();
        AnalysisRequest request = Request(Abs(x), AnalysisFeatures.Parity);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        FunctionParity parity = Proved(report.Parity);
        var certificate = Assert.IsType<SemialgebraicUnaryProofCertificate>(report.Parity.Certificate);
        Assert.Equal(2, certificate.AuxiliaryCells.Length);

        CellDecompositionCertificate first = certificate.AuxiliaryCells[0];
        CellWitness cell = first.Cells[0];
        SemialgebraicUnaryProofCertificate mutated = certificate with
        {
            AuxiliaryCells = certificate.AuxiliaryCells.SetItem(
                0,
                first with
                {
                    Cells = first.Cells.SetItem(0, cell with { Included = !cell.Included })
                })
        };
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<FunctionParity>.Proved(parity, mutated)));
    }

    [Fact]
    public void LimitsAndCancellationFailClosedDeterministically()
    {
        InputExpression oversized = Abs(Power(Variable(), 257));
        AnalysisReport first = AnalysisEngine.Analyze(Request(oversized, AnalysisFeatures.All));
        AnalysisReport second = AnalysisEngine.Analyze(Request(oversized, AnalysisFeatures.All));
        Assert.Equal(ProofState.Unknown, first.Domain.State);
        Assert.Equal(UnknownReason.BudgetExceeded, first.Domain.UnknownReason);
        Assert.Equal(first.Domain.UnknownReason, second.Domain.UnknownReason);
        Assert.Equal(first.ChargedWorkUnits, second.ChargedWorkUnits);

        ExactInteger huge = ExactInteger.One << (AnalysisLimits.CoefficientBits + 1);
        InputExpression excessiveCoefficient = Abs(Multiply(
            InputExpression.Number(new BigRational(huge), Source),
            Variable()));
        AnalysisReport coefficient = AnalysisEngine.Analyze(Request(
            excessiveCoefficient,
            AnalysisFeatures.All));
        Assert.Equal(ProofState.Unknown, coefficient.Domain.State);
        Assert.Equal(UnknownReason.BudgetExceeded, coefficient.Domain.UnknownReason);

        AnalysisRequest cancelled = new(
            Sqrt(Subtract(Power(Variable(), 2), Number(1))),
            AnalysisFeatures.All,
            AngleUnit.Radians,
            "x",
            static () => false);
        Assert.Throws<AnalysisCancelledException>(() => AnalysisEngine.Analyze(cancelled));

        AnalysisRequest valid = Request(Abs(Variable()), AnalysisFeatures.Range);
        AnalysisReport validReport = AnalysisEngine.Analyze(valid);
        var exhausted = new ResourceBudget();
        exhausted.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() => CertificateChecker.Check(
            valid,
            validReport.Expression!,
            validReport.Range,
            exhausted));
    }

    [Fact]
    public void PublicCompatibilityAdapterFormatsCertifiedUnarySetsWithoutTooComplexBits()
    {
        GraphFunctionAnalysisData absolute = AnalyzePublic("abs(x^2-1)");
        Assert.Equal("x ∈ ℝ", absolute.Domain);
        Assert.Equal("y ∈ [0, ∞)", absolute.Range);
        Assert.Equal("x = −1 ∨ x = 1", absolute.Zeros);
        Assert.Equal(0, absolute.TooComplexFeatures);

        GraphFunctionAnalysisData semicircle = AnalyzePublic("sqrt(1-x^2)");
        Assert.Equal("−1 ≤ x ≤ 1", semicircle.Domain);
        Assert.Equal("y ∈ [0, 1]", semicircle.Range);
        Assert.Equal(0, semicircle.TooComplexFeatures);

        GraphFunctionAnalysisData rational = AnalyzePublic("sqrt((x-1)/(x+1))");
        Assert.Equal("x ∈ (−∞, −1) ∨ x ∈ [1, ∞)", rational.Domain);
        Assert.Equal("y ∈ [0, 1) ∪ (1, ∞)", rational.Range);
        Assert.Equal("x = −1", Assert.Single(rational.VerticalAsymptotes));
        Assert.Equal("y = 1", Assert.Single(rational.HorizontalAsymptotes));
        Assert.Equal(0, rational.TooComplexFeatures);
    }

    private static void AssertAllCertificatesReplay(
        AnalysisRequest request,
        AnalysisReport report,
        string label)
    {
        AssertReplay(request, report, report.Domain, label);
        AssertReplay(request, report, report.Range, label);
        AssertReplay(request, report, report.Parity, label);
        AssertReplay(request, report, report.Zeros, label);
        AssertReplay(request, report, report.YIntercept, label);
        AssertReplay(request, report, report.Minima, label);
        AssertReplay(request, report, report.Maxima, label);
        AssertReplay(request, report, report.InflectionPoints, label);
        AssertReplay(request, report, report.VerticalAsymptotes, label);
        AssertReplay(request, report, report.HorizontalAsymptotes, label);
        AssertReplay(request, report, report.ObliqueAsymptotes, label);
        AssertReplay(request, report, report.Monotonicity, label);
        AssertReplay(request, report, report.Period, label);
    }

    private static void AssertDedicatedUnaryCertificates(AnalysisReport report)
    {
        Assert.IsType<SemialgebraicUnaryProofCertificate>(report.Range.Certificate);
        Assert.IsType<SemialgebraicUnaryProofCertificate>(report.Parity.Certificate);
        Assert.IsType<SemialgebraicUnaryProofCertificate>(report.Zeros.Certificate);
        Assert.IsType<SemialgebraicUnaryProofCertificate>(report.YIntercept.Certificate);
        Assert.IsType<SemialgebraicUnaryProofCertificate>(report.Minima.Certificate);
        Assert.IsType<SemialgebraicUnaryProofCertificate>(report.Maxima.Certificate);
        Assert.IsType<SemialgebraicUnaryProofCertificate>(report.InflectionPoints.Certificate);
        Assert.IsType<SemialgebraicUnaryProofCertificate>(report.VerticalAsymptotes.Certificate);
        Assert.IsType<SemialgebraicUnaryProofCertificate>(report.HorizontalAsymptotes.Certificate);
        Assert.IsType<SemialgebraicUnaryProofCertificate>(report.ObliqueAsymptotes.Certificate);
        Assert.IsType<SemialgebraicUnaryProofCertificate>(report.Monotonicity.Certificate);
        Assert.IsType<SemialgebraicUnaryProofCertificate>(report.Period.Certificate);
    }

    private static void AssertReplay<T>(
        AnalysisRequest request,
        AnalysisReport report,
        ProofOutcome<T> outcome,
        string label)
    {
        Assert.True(
            outcome.State != ProofState.Proved ||
            CertificateChecker.Check(request, report.Expression!, outcome),
            label);
        if (outcome.State == ProofState.Unknown)
        {
            Assert.Null(outcome.Certificate);
        }
    }

    private static void AssertSameClaims(AnalysisReport expected, AnalysisReport actual)
    {
        AssertSame(expected.Domain, actual.Domain);
        AssertSame(expected.Range, actual.Range);
        AssertSame(expected.Parity, actual.Parity);
        AssertSame(expected.Zeros, actual.Zeros);
        AssertSame(expected.YIntercept, actual.YIntercept);
        AssertSame(expected.Minima, actual.Minima);
        AssertSame(expected.Maxima, actual.Maxima);
        AssertSame(expected.InflectionPoints, actual.InflectionPoints);
        AssertSame(expected.VerticalAsymptotes, actual.VerticalAsymptotes);
        AssertSame(expected.HorizontalAsymptotes, actual.HorizontalAsymptotes);
        AssertSame(expected.ObliqueAsymptotes, actual.ObliqueAsymptotes);
        AssertSame(expected.Monotonicity, actual.Monotonicity);
        AssertSame(expected.Period, actual.Period);
    }

    private static void AssertSame<T>(ProofOutcome<T> expected, ProofOutcome<T> actual)
    {
        Assert.Equal(expected.State, actual.State);
        Assert.Equal(expected.UnknownReason, actual.UnknownReason);
        if (expected.State == ProofState.Proved)
        {
            Assert.Equal(ClaimCanonical.For(expected.Value!), ClaimCanonical.For(actual.Value!));
        }
    }

    private static void AssertPoints(
        ImmutableArray<FeaturePoint> actual,
        params (string X, string Y)[] expected)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int index = 0; index < expected.Length; index++)
        {
            var point = Assert.IsType<ConstantYFeaturePoint>(actual[index]);
            var x = Assert.IsType<SingletonReal>(point.X);
            Assert.Equal(expected[index].X, ExactRealCanonical.Format(x.Value));
            Assert.Equal(expected[index].Y, ExactRealCanonical.Format(point.Y));
        }
    }

    private static void AssertRegions(
        ImmutableArray<MonotoneRegion> actual,
        params (string Region, Monotonicity Direction)[] expected)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index].Region, actual[index].Region.Canonical);
            Assert.Equal(expected[index].Direction, actual[index].Direction);
        }
    }

    private static T Proved<T>(ProofOutcome<T> outcome, string? label = null)
    {
        Assert.True(outcome.State == ProofState.Proved, $"{label}: {outcome.UnknownReason}");
        Assert.NotNull(outcome.Certificate);
        Assert.NotNull(outcome.Value);
        return outcome.Value;
    }

    private static GraphFunctionAnalysisData AnalyzePublic(string formula)
    {
        IMathSolver solver = MathSolver.CreateMathSolver();
        IExpression expression = solver.ParseInput(formula, out int errorCode, out int errorType)
            ?? throw new InvalidOperationException($"Parse failed: {errorCode}/{errorType}");
        IGraph graph = solver.CreateGrapher();
        Assert.NotNull(graph.TryInitialize(expression));
        IGraphAnalyzer analyzer = graph.GetAnalyzer();
        Assert.True(analyzer.CanFunctionAnalysisBePerformed(out bool variableIsNotX));
        Assert.False(variableIsNotX);
        Assert.Equal(GraphStatus.Ok, analyzer.PerformFunctionAnalysis((uint)PerformAnalysisType.All));
        return solver.Analyze(analyzer);
    }

    private static AnalysisRequest Request(InputExpression expression, AnalysisFeatures features)
    {
        return new AnalysisRequest(expression, features, AngleUnit.Radians, "x", static () => true);
    }

    private static InputExpression Variable()
    {
        return InputExpression.Variable("x", Source);
    }

    private static InputExpression Number(int value)
    {
        return InputExpression.Number(new BigRational(value), Source);
    }

    private static InputExpression Add(InputExpression left, InputExpression right)
    {
        return InputExpression.Binary(InputExpressionKind.Add, left, right, Source);
    }

    private static InputExpression Subtract(InputExpression left, InputExpression right)
    {
        return InputExpression.Binary(InputExpressionKind.Subtract, left, right, Source);
    }

    private static InputExpression Multiply(InputExpression left, InputExpression right)
    {
        return InputExpression.Binary(InputExpressionKind.Multiply, left, right, Source);
    }

    private static InputExpression Divide(InputExpression left, InputExpression right)
    {
        return InputExpression.Binary(InputExpressionKind.Divide, left, right, Source);
    }

    private static InputExpression Power(InputExpression basis, int exponent)
    {
        return InputExpression.Binary(InputExpressionKind.Power, basis, Number(exponent), Source);
    }

    private static InputExpression Negate(InputExpression value)
    {
        return InputExpression.Unary(InputExpressionKind.Negate, value, Source);
    }

    private static InputExpression Abs(InputExpression value)
    {
        return Function("abs", value);
    }

    private static InputExpression Sqrt(InputExpression value)
    {
        return Function("sqrt", value);
    }

    private static InputExpression Function(string name, params InputExpression[] arguments)
    {
        return InputExpression.Function(name, arguments.ToImmutableArray(), Source);
    }
}
