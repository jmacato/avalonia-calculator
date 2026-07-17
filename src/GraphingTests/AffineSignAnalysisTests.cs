using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class AffineSignAnalysisTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Fact]
    public void SignOfIdentityHasExactDiscontinuousSemanticsAndReplayableCertificates()
    {
        (AnalysisRequest request, SemanticExpression semantic) = Build(Sign(Variable()));
        AffineSignContext context = Context(request, semantic);

        Assert.Equal("q:1:sign=1", context.Slope.Canonical);
        Assert.Equal("q:0:sign=0", context.Intercept.Canonical);
        Assert.Equal("affine-sign[q:1:sign=1,q:0:sign=0]", context.NormalizedArgumentCanonical);
        AssertEveryFeatureReplays(request, semantic);

        Assert.IsType<AllRealSet>(Proved<RealSet>(request, semantic, AnalysisFeatures.Domain));
        Assert.Equal(
            "points[q:-1,q:0,q:1]",
            Proved<RealSet>(request, semantic, AnalysisFeatures.Range).Canonical);
        Assert.Equal(
            "points[q:0]",
            Proved<RealSet>(request, semantic, AnalysisFeatures.Zeros).Canonical);
        OptionalValue<ExactReal> intercept = Proved<OptionalValue<ExactReal>>(
            request,
            semantic,
            AnalysisFeatures.YIntercept);
        Assert.True(intercept.HasValue);
        Assert.Equal("q:0", ExactRealCanonical.Format(intercept.Value!));
        Assert.Equal(FunctionParity.Odd, Proved<FunctionParity>(
            request,
            semantic,
            AnalysisFeatures.Parity));

        Assert.Empty(Proved<ImmutableArray<FeaturePoint>>(
            request,
            semantic,
            AnalysisFeatures.Minima));
        Assert.Empty(Proved<ImmutableArray<FeaturePoint>>(
            request,
            semantic,
            AnalysisFeatures.Maxima));
        Assert.Empty(Proved<ImmutableArray<FeaturePoint>>(
            request,
            semantic,
            AnalysisFeatures.InflectionPoints));
        Assert.Empty(Proved<ImmutableArray<Asymptote>>(
            request,
            semantic,
            AnalysisFeatures.VerticalAsymptotes));
        Assert.Empty(Proved<ImmutableArray<Asymptote>>(
            request,
            semantic,
            AnalysisFeatures.ObliqueAsymptotes));

        ImmutableArray<Asymptote> horizontal = Proved<ImmutableArray<Asymptote>>(
            request,
            semantic,
            AnalysisFeatures.HorizontalAsymptotes);
        Assert.Equal(["q:1", "q:-1"], horizontal.Select(Coordinate));

        MonotoneRegion monotone = Assert.Single(Proved<ImmutableArray<MonotoneRegion>>(
            request,
            semantic,
            AnalysisFeatures.Monotonicity));
        Assert.IsType<AllRealSet>(monotone.Region);
        Assert.Equal(Monotonicity.Increasing, monotone.Direction);
        Assert.Equal(
            PeriodicityKind.NotPeriodic,
            Proved<Periodicity>(request, semantic, AnalysisFeatures.Period).Kind);
    }

    [Theory]
    [InlineData(2, -4, "q:2", 1, -1)]
    [InlineData(-3, 6, "q:2", -1, 1)]
    public void ShiftedAndReflectedAffineSignsHaveExactRootOrientationAndOriginValue(
        int slope,
        int intercept,
        string expectedRoot,
        int expectedDirection,
        int expectedAtOrigin)
    {
        InputExpression affine = Add(
            Multiply(Number(slope), Variable()),
            Number(intercept));
        (AnalysisRequest request, SemanticExpression semantic) = Build(Sign(affine));

        Assert.Equal(
            $"points[{expectedRoot}]",
            Proved<RealSet>(request, semantic, AnalysisFeatures.Zeros).Canonical);
        Assert.Equal(
            expectedAtOrigin,
            Rational(Proved<OptionalValue<ExactReal>>(
                request,
                semantic,
                AnalysisFeatures.YIntercept).Value!));
        Assert.Equal(FunctionParity.Neither, Proved<FunctionParity>(
            request,
            semantic,
            AnalysisFeatures.Parity));
        Assert.Equal(
            expectedDirection > 0 ? Monotonicity.Increasing : Monotonicity.Decreasing,
            Assert.Single(Proved<ImmutableArray<MonotoneRegion>>(
                request,
                semantic,
                AnalysisFeatures.Monotonicity)).Direction);
        Assert.Equal(
            expectedDirection > 0 ? ["q:1", "q:-1"] : ["q:-1", "q:1"],
            Proved<ImmutableArray<Asymptote>>(
                    request,
                    semantic,
                    AnalysisFeatures.HorizontalAsymptotes)
                .Select(Coordinate));
        AssertEveryFeatureReplays(request, semantic);
    }

    [Theory]
    [InlineData(4, 1, 1, false)]
    [InlineData(-4, -1, 1, false)]
    [InlineData(0, 0, 2, true)]
    public void ConstantAffineSignsUseConstantFunctionSemantics(
        int argument,
        int expectedValue,
        int expectedParity,
        bool zerosAreAllReals)
    {
        (AnalysisRequest request, SemanticExpression semantic) = Build(Sign(Number(argument)));

        Assert.Equal(
            $"points[q:{expectedValue}]",
            Proved<RealSet>(request, semantic, AnalysisFeatures.Range).Canonical);
        RealSet zeros = Proved<RealSet>(request, semantic, AnalysisFeatures.Zeros);
        Assert.Equal(zerosAreAllReals, zeros is AllRealSet);
        if (!zerosAreAllReals)
        {
            Assert.IsType<EmptySet>(zeros);
        }

        Assert.Equal((FunctionParity)expectedParity, Proved<FunctionParity>(
            request,
            semantic,
            AnalysisFeatures.Parity));
        Assert.Equal(
            Monotonicity.Constant,
            Assert.Single(Proved<ImmutableArray<MonotoneRegion>>(
                request,
                semantic,
                AnalysisFeatures.Monotonicity)).Direction);
        Periodicity period = Proved<Periodicity>(
            request,
            semantic,
            AnalysisFeatures.Period);
        Assert.Equal(PeriodicityKind.PeriodicWithoutFundamentalPeriod, period.Kind);
        Assert.Null(period.FundamentalPeriod);
        Assert.Equal(
            $"q:{expectedValue}",
            Coordinate(Assert.Single(Proved<ImmutableArray<Asymptote>>(
                request,
                semantic,
                AnalysisFeatures.HorizontalAsymptotes))));
        AssertEveryFeatureReplays(request, semantic);
    }

    [Fact]
    public void ExactSymbolicAffineCoefficientsRemainExact()
    {
        InputExpression affine = Subtract(
            Multiply(Named("pi"), Variable()),
            Named("e"));
        (AnalysisRequest request, SemanticExpression semantic) = Build(Sign(affine));
        AffineSignContext context = Context(request, semantic);

        Assert.Equal("pi:1:0:sign=1", context.Slope.Canonical);
        Assert.Equal("fn:negate(named:e):sign=-1", context.Intercept.Canonical);
        var rootBudget = new ResourceBudget();
        ExactScalar expectedRoot = context.Intercept
            .Negate()
            .Multiply(context.Slope.Reciprocal(rootBudget), rootBudget);
        PointSet zeros = Assert.IsType<PointSet>(Proved<RealSet>(
            request,
            semantic,
            AnalysisFeatures.Zeros));
        Assert.Equal(
            ExactRealCanonical.Format(expectedRoot.Value),
            ExactRealCanonical.Format(Assert.Single(zeros.Points)));
        Assert.Equal(FunctionParity.Neither, Proved<FunctionParity>(
            request,
            semantic,
            AnalysisFeatures.Parity));
        AssertEveryFeatureReplays(request, semantic);
    }

    [Fact]
    public void ProductionEnginePublishesOnlyCentrallyCheckedAffineSignClaims()
    {
        InputExpression input = Sign(Add(
            Multiply(Number(2), Variable()),
            Number(-4)));
        var request = new AnalysisRequest(
            input,
            AnalysisFeatures.All,
            AngleUnit.Radians,
            "x",
            static () => true);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        Assert.Equal(ProofState.Proved, report.Domain.State);
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Domain));
        AssertAffineSign(report.Range);
        AssertAffineSign(report.Parity);
        AssertAffineSign(report.Zeros);
        AssertAffineSign(report.YIntercept);
        AssertAffineSign(report.Minima);
        AssertAffineSign(report.Maxima);
        AssertAffineSign(report.InflectionPoints);
        AssertAffineSign(report.VerticalAsymptotes);
        AssertAffineSign(report.HorizontalAsymptotes);
        AssertAffineSign(report.ObliqueAsymptotes);
        AssertAffineSign(report.Monotonicity);
        AssertAffineSign(report.Period);
        return;

        void AssertAffineSign<T>(ProofOutcome<T> outcome)
        {
            Assert.Equal(ProofState.Proved, outcome.State);
            Assert.IsType<AffineSignProofCertificate>(outcome.Certificate);
            Assert.True(CertificateChecker.Check(request, report.Expression!, outcome));
        }
    }

    [Theory]
    [MemberData(nameof(UnsupportedExpressions))]
    public void NonAffineOrPartialArgumentsAreRejected(object expressionValue)
    {
        var expression = Assert.IsType<InputExpression>(expressionValue);
        (AnalysisRequest request, SemanticExpression semantic) = Build(expression);

        Assert.False(AffineSignAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Range,
            new ResourceBudget(),
            out ProofOutcome<RealSet> _));
    }

    [Fact]
    public void CertificateRejectsEveryPremiseAndClaimMutation()
    {
        (AnalysisRequest request, SemanticExpression semantic) = Build(Sign(
            Add(Multiply(Number(2), Variable()), Number(-4))));
        ProofOutcome<RealSet> range = Analyze<RealSet>(
            request,
            semantic,
            AnalysisFeatures.Range);
        var certificate = Assert.IsType<AffineSignProofCertificate>(range.Certificate);
        string claim = ClaimCanonical.ForObject(range.Value!);

        Assert.True(Check(request, semantic, range));
        Assert.True(CertificateChecker.Check(request, semantic, range));
        AssertRejected(certificate with { Feature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { Subject = "mutated" });
        AssertRejected(certificate with { SubjectCanonical = "mutated" });
        AssertRejected(certificate with { ArgumentCanonical = "mutated" });
        AssertRejected(certificate with { NormalizedArgumentCanonical = "mutated" });
        AssertRejected(certificate with { SlopeCanonical = "q:-2:sign=-1" });
        AssertRejected(certificate with { InterceptCanonical = "q:4:sign=1" });
        AssertRejected(certificate with { DefinednessCanonical = "false" });
        AssertRejected(certificate with { Rule = "untrusted" });
        AssertRejected(certificate with { ProvenFeature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { Claim = EmptySet.Instance.Canonical });
        AssertRejected(certificate with { ClaimCanonical = EmptySet.Instance.Canonical });
        Assert.False(CertificateChecker.Check(
            request,
            semantic,
            ProofOutcome<RealSet>.Proved(
                range.Value!,
                certificate with { Rule = "untrusted" })));
        Assert.False(AffineSignCertificateChecker.Check(
            request,
            semantic,
            certificate,
            EmptySet.Instance.Canonical,
            new ResourceBudget()));
        return;

        void AssertRejected(AffineSignProofCertificate changed) =>
            Assert.False(AffineSignCertificateChecker.Check(
                request,
                semantic,
                changed,
                claim,
                new ResourceBudget()));
    }

    [Fact]
    public void AnalyzerAndCheckerEnforceCancellationAndWorkBudget()
    {
        (AnalysisRequest request, SemanticExpression semantic) = Build(Sign(Variable()));
        ProofOutcome<RealSet> range = Analyze<RealSet>(
            request,
            semantic,
            AnalysisFeatures.Range);
        var certificate = Assert.IsType<AffineSignProofCertificate>(range.Certificate);

        var cancelled = new ResourceBudget(static () => false);
        Assert.Throws<AnalysisCancelledException>(() =>
            AffineSignAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.Range,
                cancelled,
                out ProofOutcome<RealSet> _));
        Assert.Throws<AnalysisCancelledException>(() =>
            AffineSignCertificateChecker.Check(
                request,
                semantic,
                certificate,
                ClaimCanonical.ForObject(range.Value!),
                new ResourceBudget(static () => false)));

        var exhaustedAnalyzer = new ResourceBudget();
        exhaustedAnalyzer.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            AffineSignAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.Range,
                exhaustedAnalyzer,
                out ProofOutcome<RealSet> _));
        var exhaustedChecker = new ResourceBudget();
        exhaustedChecker.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            AffineSignCertificateChecker.Check(
                request,
                semantic,
                certificate,
                ClaimCanonical.ForObject(range.Value!),
                exhaustedChecker));
    }

    public static IEnumerable<object[]> UnsupportedExpressions()
    {
        InputExpression x = Variable();
        yield return [Sign(Power(x, 2))];
        yield return [Sign(Function("sin", x))];
        yield return [Sign(Divide(x, x))];
        // Public `sgn` input is canonicalized to `sign` by LinearParser. A raw
        // noncanonical semantic node is not trusted as an alias by the proof
        // kernel; source-spelling eligibility stays in the public adapter.
        yield return [Function("sgn", x)];
    }

    private static void AssertEveryFeatureReplays(
        AnalysisRequest request,
        SemanticExpression semantic)
    {
        Replay<RealSet>(AnalysisFeatures.Domain);
        Replay<RealSet>(AnalysisFeatures.Range);
        Replay<FunctionParity>(AnalysisFeatures.Parity);
        Replay<RealSet>(AnalysisFeatures.Zeros);
        Replay<OptionalValue<ExactReal>>(AnalysisFeatures.YIntercept);
        Replay<ImmutableArray<FeaturePoint>>(AnalysisFeatures.Minima);
        Replay<ImmutableArray<FeaturePoint>>(AnalysisFeatures.Maxima);
        Replay<ImmutableArray<FeaturePoint>>(AnalysisFeatures.InflectionPoints);
        Replay<ImmutableArray<Asymptote>>(AnalysisFeatures.VerticalAsymptotes);
        Replay<ImmutableArray<Asymptote>>(AnalysisFeatures.HorizontalAsymptotes);
        Replay<ImmutableArray<Asymptote>>(AnalysisFeatures.ObliqueAsymptotes);
        Replay<ImmutableArray<MonotoneRegion>>(AnalysisFeatures.Monotonicity);
        Replay<Periodicity>(AnalysisFeatures.Period);
        return;

        void Replay<T>(AnalysisFeatures feature)
        {
            ProofOutcome<T> outcome = Analyze<T>(request, semantic, feature);
            Assert.IsType<AffineSignProofCertificate>(outcome.Certificate);
            Assert.True(Check(request, semantic, outcome));
        }
    }

    private static AffineSignContext Context(
        AnalysisRequest request,
        SemanticExpression semantic)
    {
        Assert.True(AffineSignContext.TryCreate(
            semantic,
            request.Variable,
            request.AngleUnit,
            new ResourceBudget(),
            out AffineSignContext context));
        return context;
    }

    private static T Proved<T>(
        AnalysisRequest request,
        SemanticExpression semantic,
        AnalysisFeatures feature) => Analyze<T>(request, semantic, feature).Value!;

    private static ProofOutcome<T> Analyze<T>(
        AnalysisRequest request,
        SemanticExpression semantic,
        AnalysisFeatures feature)
    {
        Assert.True(AffineSignAnalyzer.TryAnalyze(
            request,
            semantic,
            feature,
            new ResourceBudget(),
            out ProofOutcome<T> outcome));
        Assert.Equal(ProofState.Proved, outcome.State);
        return outcome;
    }

    private static bool Check<T>(
        AnalysisRequest request,
        SemanticExpression semantic,
        ProofOutcome<T> outcome)
    {
        var certificate = Assert.IsType<AffineSignProofCertificate>(outcome.Certificate);
        return AffineSignCertificateChecker.Check(
            request,
            semantic,
            certificate,
            ClaimCanonical.ForObject(outcome.Value!),
            new ResourceBudget());
    }

    private static (AnalysisRequest Request, SemanticExpression Semantic) Build(
        InputExpression expression)
    {
        var request = new AnalysisRequest(
            expression,
            AnalysisFeatures.All,
            AngleUnit.Radians,
            "x",
            static () => true);
        return (request, new SemanticGraphBuilder(new ResourceBudget()).Build(expression));
    }

    private static string Coordinate(Asymptote asymptote) =>
        ExactRealCanonical.Format(Assert.IsType<SingletonReal>(asymptote.Coordinate).Value);

    private static int Rational(ExactReal value) =>
        (int)Assert.IsType<RationalReal>(value).Value.Numerator;

    private static InputExpression Variable() => InputExpression.Variable("x", Source);

    private static InputExpression Named(string name) => InputExpression.Variable(name, Source);

    private static InputExpression Number(int value) =>
        InputExpression.Number(new BigRational(value), Source);

    private static InputExpression Add(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Add, left, right, Source);

    private static InputExpression Subtract(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Subtract, left, right, Source);

    private static InputExpression Multiply(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Multiply, left, right, Source);

    private static InputExpression Divide(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Divide, left, right, Source);

    private static InputExpression Power(InputExpression basis, int exponent) =>
        InputExpression.Binary(InputExpressionKind.Power, basis, Number(exponent), Source);

    private static InputExpression Sign(InputExpression argument) => Function("sign", argument);

    private static InputExpression Function(string name, params InputExpression[] arguments) =>
        InputExpression.Function(name, arguments.ToImmutableArray(), Source);
}
