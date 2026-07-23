using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class AffinePrimitiveAnalysisTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Fact]
    public void RationalAffinePrimitiveCorpusCertifiesEverySupportedFeature()
    {
        InputExpression[] corpus =
        [
            Multiply(Number(-2), Function("asin", Affine(2, -1))),
            Multiply(Number(2), Function("acos", Affine(-3, 2))),
            Multiply(Number(-3), Function("atan", Affine(-2, 1))),
            Subtract(Multiply(Number(2), Function("exp", Affine(-3, 1))), Number(4)),
            Add(Multiply(Number(-2), Function("sinh", Affine(-3, 1))), Number(4)),
            Add(Multiply(Number(-2), Function("cosh", Affine(3, -1))), Number(4)),
            Add(Multiply(Number(-3), Function("tanh", Affine(2, -1))), Number(1)),
            Subtract(Multiply(Number(2), Function("log", Affine(-3, 2))), Number(1)),
            Add(Multiply(Number(-2), Function("ln", Affine(3, 2))), Number(1)),
            Add(
                Multiply(
                    Number(-3),
                    Function("root", Affine(-2, 1), Number(5))),
                Number(6))
        ];

        foreach (InputExpression expression in corpus)
        {
            AnalysisRequest request = Request(expression, AnalysisFeatures.All);
            AnalysisReport report = AnalysisEngine.Analyze(request);

            AssertAllFeaturesProved(report);
            AssertAllCertificatesReplay(request, report);
        }
    }

    [Fact]
    public void ReflectedInverseTrigTransformsDomainsRangesExtremaAndOrientation()
    {
        InputExpression reflectedAsin = Multiply(
            Number(-2),
            Function("asin", Affine(2, -1)));
        AnalysisRequest asinRequest = Request(reflectedAsin, AnalysisFeatures.All);
        AnalysisReport asin = AnalysisEngine.Analyze(asinRequest);

        AssertAllFeaturesProved(asin);
        Assert.Equal("interval[q:0,1,q:1,1]", Proved(asin.Domain).Canonical);
        Assert.Equal("interval[pi:-1:0,1,pi:1:0,1]", Proved(asin.Range).Canonical);
        Assert.Equal("points[q:1/2]", Proved(asin.Zeros).Canonical);
        Assert.Equal("pi:1:0", ExactRealCanonical.Format(Proved(asin.YIntercept).Value!));
        AssertFeaturePoint(Proved(asin.Minima), "q:1", "pi:-1:0");
        AssertFeaturePoint(Proved(asin.Maxima), "q:0", "pi:1:0");
        AssertFeaturePoint(Proved(asin.InflectionPoints), "q:1/2", "q:0");
        MonotoneRegion asinRegion = Assert.Single(Proved(asin.Monotonicity));
        Assert.Equal(Proved(asin.Domain).Canonical, asinRegion.Region.Canonical);
        Assert.Equal(Monotonicity.Decreasing, asinRegion.Direction);
        Assert.Equal(FunctionParity.Neither, Proved(asin.Parity));
        AssertAllCertificatesReplay(asinRequest, asin);

        InputExpression reflectedAcos = Multiply(
            Number(2),
            Function("acos", Affine(-3, 2)));
        AnalysisRequest acosRequest = Request(reflectedAcos, AnalysisFeatures.All);
        AnalysisReport acos = AnalysisEngine.Analyze(acosRequest);

        AssertAllFeaturesProved(acos);
        Assert.Equal("interval[q:1/3,1,q:1,1]", Proved(acos.Domain).Canonical);
        Assert.Equal("interval[q:0,1,pi:2:0,1]", Proved(acos.Range).Canonical);
        Assert.Equal("points[q:1/3]", Proved(acos.Zeros).Canonical);
        AssertFeaturePoint(Proved(acos.Minima), "q:1/3", "q:0");
        AssertFeaturePoint(Proved(acos.Maxima), "q:1", "pi:2:0");
        AssertFeaturePoint(Proved(acos.InflectionPoints), "q:2/3", "pi:1:0");
        Assert.Equal(
            Monotonicity.Increasing,
            Assert.Single(Proved(acos.Monotonicity)).Direction);
        AssertAllCertificatesReplay(acosRequest, acos);

        InputExpression reflectedAtan = Multiply(
            Number(-3),
            Function("atan", Affine(-2, 1)));
        AnalysisReport atan = AnalysisEngine.Analyze(Request(
            reflectedAtan,
            AnalysisFeatures.Range |
            AnalysisFeatures.Zeros |
            AnalysisFeatures.InflectionPoints |
            AnalysisFeatures.HorizontalAsymptotes |
            AnalysisFeatures.Monotonicity));

        Assert.Equal("interval[pi:-3/2:0,0,pi:3/2:0,0]", Proved(atan.Range).Canonical);
        Assert.Equal("points[q:1/2]", Proved(atan.Zeros).Canonical);
        AssertFeaturePoint(Proved(atan.InflectionPoints), "q:1/2", "q:0");
        Assert.Equal(2, Proved(atan.HorizontalAsymptotes).Length);
        Assert.Equal(
            Monotonicity.Increasing,
            Assert.Single(Proved(atan.Monotonicity)).Direction);
    }

    [Fact]
    public void AffineElementaryPrimitivesTransformExactCriticalData()
    {
        InputExpression exponential = Subtract(
            Multiply(Number(2), Function("exp", Affine(-3, 1))),
            Number(4));
        AnalysisReport exp = AnalysisEngine.Analyze(Request(exponential, AnalysisFeatures.All));
        Assert.Equal("interval[q:-4,0,+inf,0]", Proved(exp.Range).Canonical);
        Assert.Equal(
            "fn:add(fn:scale(named:e,q:2),q:-4)",
            ExactRealCanonical.Format(Proved(exp.YIntercept).Value!));
        Assert.Empty(Proved(exp.Minima));
        Assert.Empty(Proved(exp.Maxima));
        Assert.Empty(Proved(exp.InflectionPoints));
        AssertAsymptote(Proved(exp.HorizontalAsymptotes), "q:-4");
        Assert.Equal(
            Monotonicity.Decreasing,
            Assert.Single(Proved(exp.Monotonicity)).Direction);

        InputExpression hyperbolicCosine = Add(
            Multiply(Number(-2), Function("cosh", Affine(3, -1))),
            Number(4));
        AnalysisReport cosh = AnalysisEngine.Analyze(Request(
            hyperbolicCosine,
            AnalysisFeatures.All));
        Assert.Equal("interval[-inf,0,q:2,1]", Proved(cosh.Range).Canonical);
        Assert.Empty(Proved(cosh.Minima));
        AssertFeaturePoint(Proved(cosh.Maxima), "q:1/3", "q:2");
        Assert.Equal(2, Assert.IsType<PointSet>(Proved(cosh.Zeros)).Points.Length);
        ImmutableArray<MonotoneRegion> coshRegions = Proved(cosh.Monotonicity);
        Assert.Equal(2, coshRegions.Length);
        Assert.Equal(Monotonicity.Increasing, coshRegions[0].Direction);
        Assert.Equal(Monotonicity.Decreasing, coshRegions[1].Direction);

        InputExpression hyperbolicTangent = Add(
            Multiply(Number(-3), Function("tanh", Affine(2, -1))),
            Number(1));
        AnalysisReport tanh = AnalysisEngine.Analyze(Request(
            hyperbolicTangent,
            AnalysisFeatures.All));
        Assert.Equal("interval[q:-2,0,q:4,0]", Proved(tanh.Range).Canonical);
        AssertFeaturePoint(Proved(tanh.InflectionPoints), "q:1/2", "q:1");
        Assert.Equal(2, Proved(tanh.HorizontalAsymptotes).Length);
        Assert.Equal(
            Monotonicity.Decreasing,
            Assert.Single(Proved(tanh.Monotonicity)).Direction);

        InputExpression logarithm = Subtract(
            Multiply(Number(2), Function("ln", Affine(-3, 2))),
            Number(1));
        AnalysisReport log = AnalysisEngine.Analyze(Request(logarithm, AnalysisFeatures.All));
        Assert.Equal("interval[-inf,0,q:2/3,0]", Proved(log.Domain).Canonical);
        Assert.IsType<AllRealSet>(Proved(log.Range));
        AssertAsymptote(Proved(log.VerticalAsymptotes), "q:2/3");
        Assert.Empty(Proved(log.HorizontalAsymptotes));
        Assert.Equal(
            Monotonicity.Decreasing,
            Assert.Single(Proved(log.Monotonicity)).Direction);

        InputExpression oddRoot = Add(
            Multiply(
                Number(-3),
                Function("root", Affine(-2, 1), Number(5))),
            Number(6));
        AnalysisReport root = AnalysisEngine.Analyze(Request(oddRoot, AnalysisFeatures.All));
        Assert.IsType<AllRealSet>(Proved(root.Range));
        Assert.Equal("points[q:-31/2]", Proved(root.Zeros).Canonical);
        Assert.Equal("q:3", ExactRealCanonical.Format(Proved(root.YIntercept).Value!));
        AssertFeaturePoint(Proved(root.InflectionPoints), "q:1/2", "q:6");
        Assert.Equal(
            Monotonicity.Increasing,
            Assert.Single(Proved(root.Monotonicity)).Direction);
    }

    [Fact]
    public void AliasesAndAlgebraicOrderHaveIdenticalCertifiedClaims()
    {
        foreach ((string canonical, string alias) in new[]
                 {
                     ("asin", "arcsin"),
                     ("acos", "arccos"),
                     ("atan", "arctan")
                 })
        {
            InputExpression argument = Affine(-2, 1);
            AnalysisReport expected = AnalysisEngine.Analyze(Request(
                Function(canonical, argument),
                AnalysisFeatures.All));
            AnalysisReport actual = AnalysisEngine.Analyze(Request(
                Function(alias, argument),
                AnalysisFeatures.All));

            AssertAllFeaturesProved(expected);
            AssertAllFeaturesProved(actual);
            AssertSameClaims(expected, actual);
        }

        InputExpression primitive = Function("asin", Affine(2, -1));
        InputExpression[] equivalent =
        [
            Add(Multiply(Number(2), primitive), Number(3)),
            Add(Number(3), Multiply(primitive, Number(2))),
            Multiply(Number(2), Add(primitive, Number(new BigRational(3, 2)))),
            Add(Add(primitive, primitive), Number(3)),
            Divide(Add(Multiply(Number(4), primitive), Number(6)), Number(2))
        ];
        AnalysisFeatures supported = AnalysisFeatures.All & ~AnalysisFeatures.Zeros;
        AnalysisReport first = AnalysisEngine.Analyze(Request(equivalent[0], supported));
        for (int i = 1; i < equivalent.Length; i++)
        {
            AnalysisReport current = AnalysisEngine.Analyze(Request(equivalent[i], supported));
            AssertSameClaims(first, current);
        }
    }

    [Fact]
    public void RetainedDomainHolesPreventWholeDomainPrimitiveTheorems()
    {
        InputExpression primitive = Subtract(
            Multiply(Number(2), Function("exp", Affine(2, 1))),
            Number(3));
        InputExpression retainedHole = Add(
            primitive,
            Multiply(
                Number(0),
                Divide(Number(1), Subtract(Variable(), Number(3)))));
        AnalysisReport report = AnalysisEngine.Analyze(Request(
            retainedHole,
            AnalysisFeatures.All));

        Assert.Equal(ProofState.Proved, report.Domain.State);
        Assert.NotEqual(AllRealSet.Instance.Canonical, Proved(report.Domain).Canonical);
        AssertUnknownUnsupported(report.Range);
        AssertUnknownUnsupported(report.Parity);
        AssertUnknownUnsupported(report.Zeros);
        OptionalValue<ExactReal> intercept = Proved(report.YIntercept);
        Assert.True(intercept.HasValue);
        Assert.Equal(
            "fn:add(fn:scale(named:e,q:2),q:-3)",
            ExactRealCanonical.Format(intercept.Value!));
        Assert.IsType<ExactOriginProofCertificate>(report.YIntercept.Certificate);
        AssertUnknownUnsupported(report.Minima);
        AssertUnknownUnsupported(report.Maxima);
        AssertUnknownUnsupported(report.InflectionPoints);
        AssertUnknownUnsupported(report.VerticalAsymptotes);
        AssertUnknownUnsupported(report.HorizontalAsymptotes);
        AssertUnknownUnsupported(report.ObliqueAsymptotes);
        AssertUnknownUnsupported(report.Monotonicity);
        AssertUnknownUnsupported(report.Period);
    }

    [Fact]
    public void UnsupportedInverseRangeComparisonRemainsUnknownNotEmpty()
    {
        InputExpression expression = Add(
            Function("asin", Affine(2, -1)),
            Number(new BigRational(1, 3)));
        AnalysisRequest request = Request(expression, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        Assert.Equal(ProofState.Unknown, report.Zeros.State);
        Assert.Equal(UnknownReason.UnsupportedFragment, report.Zeros.UnknownReason);
        Assert.Null(report.Zeros.Certificate);
        Assert.Equal(ProofState.Proved, report.Domain.State);
        Assert.Equal(ProofState.Proved, report.Range.State);
        Assert.Equal(ProofState.Proved, report.YIntercept.State);
        Assert.Equal(ProofState.Proved, report.Minima.State);
        Assert.Equal(ProofState.Proved, report.Maxima.State);
        Assert.Equal(ProofState.Proved, report.InflectionPoints.State);
        Assert.Equal(ProofState.Proved, report.Monotonicity.State);
        Assert.Equal(ProofState.Proved, report.Period.State);
        AssertAllCertificatesReplay(request, report);
    }

    [Fact]
    public void AffinePrimitiveCertificatesRejectMutationAndRespectBudgets()
    {
        InputExpression expression = Subtract(
            Multiply(Number(2), Function("exp", Affine(-3, 1))),
            Number(4));
        AnalysisRequest request = Request(expression, AnalysisFeatures.Range);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        RealSet range = Proved(report.Range);
        var certificate = Assert.IsType<TheoremProofCertificate>(report.Range.Certificate);
        Assert.Equal(TheoremRule.ElementaryPrimitive, certificate.Theorem);

        TheoremProofCertificate changedPattern = certificate with
        {
            Parameters = certificate.Parameters.SetItem(
                0,
                certificate.Parameters[0] + ":mutated")
        };
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<RealSet>.Proved(range, changedPattern)));
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<RealSet>.Proved(EmptySet.Instance, certificate)));

        AnalysisReport degree = AnalysisEngine.Analyze(Request(
            Function("root", Affine(2, 1), Number(257)),
            AnalysisFeatures.Range));
        Assert.Equal(ProofState.Unknown, degree.Range.State);
        Assert.Equal(UnknownReason.BudgetExceeded, degree.Range.UnknownReason);

        ExactInteger factor = ExactInteger.One << (AnalysisLimits.CoefficientBits / 2 + 1);
        InputExpression oversizedScale = Multiply(
            Number(new BigRational(factor)),
            Multiply(
                Number(new BigRational(factor)),
                Function("exp", Affine(2, 1))));
        AnalysisReport coefficient = AnalysisEngine.Analyze(Request(
            oversizedScale,
            AnalysisFeatures.Range));
        Assert.Equal(ProofState.Unknown, coefficient.Range.State);
        Assert.Equal(UnknownReason.BudgetExceeded, coefficient.Range.UnknownReason);

        AnalysisRequest cancelled = new(
            expression,
            AnalysisFeatures.All,
            AngleUnit.Radians,
            "x",
            static () => false);
        Assert.Throws<AnalysisCancelledException>(() => AnalysisEngine.Analyze(cancelled));
    }

    [Fact]
    public void SharedDagOuterAffineCollectionIsLinearInUniqueNodes()
    {
        ValueTerm variable = new(
            1,
            ValueKind.Variable,
            default,
            "x",
            [],
            "v:x");
        ValueTerm shared = new(
            2,
            ValueKind.Function,
            default,
            "exp",
            [variable],
            "f:exp(v:x)");
        for (int i = 0; i < 512; i++)
        {
            shared = new ValueTerm(
                shared.Id + 1,
                ValueKind.Add,
                default,
                string.Empty,
                [shared, shared],
                $"double:{i}");
        }

        var semantic = new SemanticExpression(
            shared,
            Formula.True,
            Formula.True,
            Formula.True,
            [],
            [],
            []);
        AnalysisRequest request = Request(
            Function("exp", Variable()),
            AnalysisFeatures.Range);
        var budget = new ResourceBudget();

        Assert.True(TrigonometricAndLatticeAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Range,
            budget,
            out ProofOutcome<RealSet> outcome));
        Assert.Equal("interval[q:0,0,+inf,0]", Proved(outcome).Canonical);
        Assert.InRange(budget.WorkUsed, 1, 20_000);
    }

    private static void AssertAllFeaturesProved(AnalysisReport report)
    {
        Assert.Equal(ProofState.Proved, report.Domain.State);
        Assert.Equal(ProofState.Proved, report.Range.State);
        Assert.Equal(ProofState.Proved, report.Parity.State);
        Assert.Equal(ProofState.Proved, report.Zeros.State);
        Assert.Equal(ProofState.Proved, report.YIntercept.State);
        Assert.Equal(ProofState.Proved, report.Minima.State);
        Assert.Equal(ProofState.Proved, report.Maxima.State);
        Assert.Equal(ProofState.Proved, report.InflectionPoints.State);
        Assert.Equal(ProofState.Proved, report.VerticalAsymptotes.State);
        Assert.Equal(ProofState.Proved, report.HorizontalAsymptotes.State);
        Assert.Equal(ProofState.Proved, report.ObliqueAsymptotes.State);
        Assert.Equal(ProofState.Proved, report.Monotonicity.State);
        Assert.Equal(ProofState.Proved, report.Period.State);
    }

    private static void AssertAllCertificatesReplay(
        AnalysisRequest request,
        AnalysisReport report)
    {
        AssertReplay(request, report, report.Domain);
        AssertReplay(request, report, report.Range);
        AssertReplay(request, report, report.Parity);
        AssertReplay(request, report, report.Zeros);
        AssertReplay(request, report, report.YIntercept);
        AssertReplay(request, report, report.Minima);
        AssertReplay(request, report, report.Maxima);
        AssertReplay(request, report, report.InflectionPoints);
        AssertReplay(request, report, report.VerticalAsymptotes);
        AssertReplay(request, report, report.HorizontalAsymptotes);
        AssertReplay(request, report, report.ObliqueAsymptotes);
        AssertReplay(request, report, report.Monotonicity);
        AssertReplay(request, report, report.Period);
    }

    private static void AssertReplay<T>(
        AnalysisRequest request,
        AnalysisReport report,
        ProofOutcome<T> outcome)
    {
        if (outcome.State == ProofState.Unknown)
        {
            Assert.Null(outcome.Certificate);
            return;
        }

        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.NotNull(outcome.Certificate);
        Assert.True(CertificateChecker.Check(request, report.Expression!, outcome));
    }

    private static void AssertSameClaims(AnalysisReport expected, AnalysisReport actual)
    {
        AssertSameClaim(expected.Domain, actual.Domain);
        AssertSameClaim(expected.Range, actual.Range);
        AssertSameClaim(expected.Parity, actual.Parity);
        AssertSameClaim(expected.Zeros, actual.Zeros);
        AssertSameClaim(expected.YIntercept, actual.YIntercept);
        AssertSameClaim(expected.Minima, actual.Minima);
        AssertSameClaim(expected.Maxima, actual.Maxima);
        AssertSameClaim(expected.InflectionPoints, actual.InflectionPoints);
        AssertSameClaim(expected.VerticalAsymptotes, actual.VerticalAsymptotes);
        AssertSameClaim(expected.HorizontalAsymptotes, actual.HorizontalAsymptotes);
        AssertSameClaim(expected.ObliqueAsymptotes, actual.ObliqueAsymptotes);
        AssertSameClaim(expected.Monotonicity, actual.Monotonicity);
        AssertSameClaim(expected.Period, actual.Period);
    }

    private static void AssertSameClaim<T>(
        ProofOutcome<T> expected,
        ProofOutcome<T> actual)
    {
        Assert.Equal(expected.State, actual.State);
        Assert.Equal(expected.UnknownReason, actual.UnknownReason);
        if (expected.State == ProofState.Proved)
        {
            Assert.Equal(
                ClaimCanonical.For(expected.Value!),
                ClaimCanonical.For(actual.Value!));
        }
    }

    private static void AssertFeaturePoint(
        ImmutableArray<FeaturePoint> points,
        string expectedX,
        string expectedY)
    {
        var point = Assert.IsType<ConstantYFeaturePoint>(Assert.Single(points));
        var x = Assert.IsType<SingletonReal>(point.X);
        Assert.Equal(expectedX, ExactRealCanonical.Format(x.Value));
        Assert.Equal(expectedY, ExactRealCanonical.Format(point.Y));
    }

    private static void AssertAsymptote(
        ImmutableArray<Asymptote> asymptotes,
        string expectedCoordinate)
    {
        Asymptote asymptote = Assert.Single(asymptotes);
        var coordinate = Assert.IsType<SingletonReal>(asymptote.Coordinate);
        Assert.Equal(expectedCoordinate, ExactRealCanonical.Format(coordinate.Value));
    }

    private static void AssertUnknownUnsupported<T>(ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Unknown, outcome.State);
        Assert.Equal(UnknownReason.UnsupportedFragment, outcome.UnknownReason);
        Assert.Null(outcome.Certificate);
    }

    private static T Proved<T>(ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.NotNull(outcome.Value);
        return outcome.Value!;
    }

    private static AnalysisRequest Request(
        InputExpression expression,
        AnalysisFeatures features)
    {
        return new AnalysisRequest(expression, features, AngleUnit.Radians, "x", static () => true);
    }

    private static InputExpression Affine(int slope, int intercept)
    {
        return Add(Multiply(Number(slope), Variable()), Number(intercept));
    }

    private static InputExpression Variable()
    {
        return InputExpression.Variable("x", Source);
    }

    private static InputExpression Number(int value)
    {
        return Number(new BigRational(value));
    }

    private static InputExpression Number(BigRational value)
    {
        return InputExpression.Number(value, Source);
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

    private static InputExpression Function(
        string name,
        params InputExpression[] arguments)
    {
        return InputExpression.Function(name, arguments.ToImmutableArray(), Source);
    }
}
