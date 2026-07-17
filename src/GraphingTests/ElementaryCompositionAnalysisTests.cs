using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class ElementaryCompositionAnalysisTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Fact]
    public void SineSquareRootProvesEndpointAndPulledBackLattices()
    {
        InputExpression input = Function("sin", Function("sqrt", Variable()));

        Assert.Equal(
            "interval[q:0,1,+inf,0]",
            Analyze<RealSet>(input, AnalysisFeatures.Domain).Canonical);
        Assert.Equal(
            "interval[q:-1,1,q:1,1]",
            Analyze<RealSet>(input, AnalysisFeatures.Range).Canonical);
        var zeros = Assert.IsType<IntegerLatticeSet>(
            Analyze<RealSet>(input, AnalysisFeatures.Zeros));
        Assert.Equal("(πn₁)^2", zeros.Expression);
        Assert.Contains("n₁ ≥ 0", zeros.Predicates);

        ImmutableArray<FeaturePoint> minima = Analyze<ImmutableArray<FeaturePoint>>(
            input,
            AnalysisFeatures.Minima);
        Assert.Equal(2, minima.Length);
        var endpoint = Assert.IsType<ConstantYFeaturePoint>(minima[0]);
        Assert.Equal("q:0", ExactRealCanonical.Format(
            Assert.IsType<SingletonReal>(endpoint.X).Value));
        Assert.Single(Analyze<ImmutableArray<FeaturePoint>>(
            input,
            AnalysisFeatures.Maxima));
        AssertUnsupported<ImmutableArray<FeaturePoint>>(
            input,
            AnalysisFeatures.InflectionPoints);
        Assert.Equal(2, Analyze<ImmutableArray<MonotoneRegion>>(
            input,
            AnalysisFeatures.Monotonicity).Length);
        Assert.Equal(
            PeriodicityKind.NotPeriodic,
            Analyze<Periodicity>(input, AnalysisFeatures.Period).Kind);
    }

    [Fact]
    public void SineLogarithmProvesExactExtremaAndInflectionPullbacks()
    {
        InputExpression input = Function("sin", Function("ln", Variable()));

        Assert.Equal(
            "interval[q:0,0,+inf,0]",
            Analyze<RealSet>(input, AnalysisFeatures.Domain).Canonical);
        Assert.Equal(
            "interval[q:-1,1,q:1,1]",
            Analyze<RealSet>(input, AnalysisFeatures.Range).Canonical);
        Assert.Equal("e^(πn₁)", Assert.IsType<IntegerLatticeSet>(
            Analyze<RealSet>(input, AnalysisFeatures.Zeros)).Expression);
        Assert.False(Analyze<OptionalValue<ExactReal>>(
            input,
            AnalysisFeatures.YIntercept).HasValue);
        Assert.Single(Analyze<ImmutableArray<FeaturePoint>>(
            input,
            AnalysisFeatures.Minima));
        Assert.Single(Analyze<ImmutableArray<FeaturePoint>>(
            input,
            AnalysisFeatures.Maxima));
        Assert.Equal(2, Analyze<ImmutableArray<FeaturePoint>>(
            input,
            AnalysisFeatures.InflectionPoints).Length);
        Assert.Equal(2, Analyze<ImmutableArray<MonotoneRegion>>(
            input,
            AnalysisFeatures.Monotonicity).Length);
        Assert.Equal(
            PeriodicityKind.NotPeriodic,
            Analyze<Periodicity>(input, AnalysisFeatures.Period).Kind);
    }

    [Fact]
    public void SineCommonLogarithmUsesBaseTenPullbacksAndCurvature()
    {
        InputExpression input = Function("sin", Function("log", Variable()));

        Assert.Equal(
            "interval[q:0,0,+inf,0]",
            Analyze<RealSet>(input, AnalysisFeatures.Domain).Canonical);
        Assert.Equal(
            "interval[q:-1,1,q:1,1]",
            Analyze<RealSet>(input, AnalysisFeatures.Range).Canonical);
        var zeros = Assert.IsType<IntegerLatticeSet>(
            Analyze<RealSet>(input, AnalysisFeatures.Zeros));
        Assert.Equal("10^(πn₁)", zeros.Expression);
        Assert.DoesNotContain("e^", zeros.Expression, StringComparison.Ordinal);
        Assert.False(Analyze<OptionalValue<ExactReal>>(
            input,
            AnalysisFeatures.YIntercept).HasValue);

        var minimum = Assert.IsType<ConstantYFeaturePoint>(Assert.Single(
            Analyze<ImmutableArray<FeaturePoint>>(input, AnalysisFeatures.Minima)));
        Assert.Equal(
            "10^(2πn₁ + 3π/2)",
            Assert.IsType<LatticeReal>(minimum.X).Expression);
        var maximum = Assert.IsType<ConstantYFeaturePoint>(Assert.Single(
            Analyze<ImmutableArray<FeaturePoint>>(input, AnalysisFeatures.Maxima)));
        Assert.Equal(
            "10^(2πn₁ + π/2)",
            Assert.IsType<LatticeReal>(maximum.X).Expression);

        ImmutableArray<FeaturePoint> inflections =
            Analyze<ImmutableArray<FeaturePoint>>(
                input,
                AnalysisFeatures.InflectionPoints);
        Assert.Equal(2, inflections.Length);
        Assert.Equal(
            "10^(2πn₁ − arctan(ln(10)))",
            Assert.IsType<LatticeReal>(
                Assert.IsType<ConstantYFeaturePoint>(inflections[0]).X).Expression);
        Assert.Equal(
            "10^(2πn₁ + π − arctan(ln(10)))",
            Assert.IsType<LatticeReal>(
                Assert.IsType<ConstantYFeaturePoint>(inflections[1]).X).Expression);

        ImmutableArray<MonotoneRegion> monotonicity =
            Analyze<ImmutableArray<MonotoneRegion>>(
                input,
                AnalysisFeatures.Monotonicity);
        Assert.Equal(2, monotonicity.Length);
        Assert.All(
            monotonicity,
            region => Assert.Contains(
                "cos(log(x))",
                Assert.IsType<ComprehensionSet>(region.Region).PredicateCanonical,
                StringComparison.Ordinal));
        Assert.Empty(Analyze<ImmutableArray<Asymptote>>(
            input,
            AnalysisFeatures.VerticalAsymptotes));
        Assert.Empty(Analyze<ImmutableArray<Asymptote>>(
            input,
            AnalysisFeatures.HorizontalAsymptotes));
        Assert.Empty(Analyze<ImmutableArray<Asymptote>>(
            input,
            AnalysisFeatures.ObliqueAsymptotes));
        Assert.Equal(
            PeriodicityKind.NotPeriodic,
            Analyze<Periodicity>(input, AnalysisFeatures.Period).Kind);
    }

    [Fact]
    public void NaturalAndCommonLogarithmCertificatesCannotBeInterchanged()
    {
        InputExpression naturalInput = Function("sin", Function("ln", Variable()));
        InputExpression commonInput = Function("sin", Function("log", Variable()));
        (_, _, ProofOutcome<RealSet> natural) = AnalyzeOutcome<RealSet>(
            naturalInput,
            AnalysisFeatures.Zeros);
        (AnalysisRequest commonRequest, SemanticExpression commonExpression,
            ProofOutcome<RealSet> common) = AnalyzeOutcome<RealSet>(
            commonInput,
            AnalysisFeatures.Zeros);

        var naturalCertificate = Assert.IsType<ElementaryCompositionProofCertificate>(
            natural.Certificate);
        var commonCertificate = Assert.IsType<ElementaryCompositionProofCertificate>(
            common.Certificate);
        Assert.Equal(ElementaryCompositionKind.SineOfLogarithm, naturalCertificate.Kind);
        Assert.Equal(
            ElementaryCompositionKind.SineOfCommonLogarithm,
            commonCertificate.Kind);
        Assert.NotEqual(natural.Value!.Canonical, common.Value!.Canonical);
        Assert.False(ElementaryCompositionCertificateChecker.Check(
            commonRequest,
            commonExpression,
            commonCertificate with
            {
                Kind = ElementaryCompositionKind.SineOfLogarithm
            },
            ClaimCanonical.For(common.Value),
            new ResourceBudget()));
    }

    [Fact]
    public void SineExponentialProvesLeftHorizontalLimitAndPositiveLattices()
    {
        InputExpression input = Function("sin", Function("exp", Variable()));

        Assert.Equal("reals", Analyze<RealSet>(input, AnalysisFeatures.Domain).Canonical);
        Assert.Equal(
            "interval[q:-1,1,q:1,1]",
            Analyze<RealSet>(input, AnalysisFeatures.Range).Canonical);
        var zeros = Assert.IsType<IntegerLatticeSet>(
            Analyze<RealSet>(input, AnalysisFeatures.Zeros));
        Assert.Equal("ln(πn₁)", zeros.Expression);
        Assert.Contains("n₁ ≥ 1", zeros.Predicates);
        Assert.True(Analyze<OptionalValue<ExactReal>>(
            input,
            AnalysisFeatures.YIntercept).HasValue);
        Assert.Single(Analyze<ImmutableArray<FeaturePoint>>(
            input,
            AnalysisFeatures.Minima));
        Assert.Single(Analyze<ImmutableArray<FeaturePoint>>(
            input,
            AnalysisFeatures.Maxima));
        AssertUnsupported<ImmutableArray<FeaturePoint>>(
            input,
            AnalysisFeatures.InflectionPoints);
        Assert.Empty(Analyze<ImmutableArray<Asymptote>>(
            input,
            AnalysisFeatures.VerticalAsymptotes));
        ImmutableArray<Asymptote> horizontal = Analyze<ImmutableArray<Asymptote>>(
            input,
            AnalysisFeatures.HorizontalAsymptotes);
        Assert.Single(horizontal);
        Assert.Equal("q:0", ExactRealCanonical.Format(
            Assert.IsType<SingletonReal>(horizontal[0].Coordinate).Value));
        Assert.Equal("q:0", ExactRealCanonical.Format(horizontal[0].Intercept!));
        Assert.Equal(2, Analyze<ImmutableArray<MonotoneRegion>>(
            input,
            AnalysisFeatures.Monotonicity).Length);
    }

    [Fact]
    public void SineTangentRetainsPolesAndTwoParameterPullbacks()
    {
        InputExpression input = Function("sin", Function("tan", Variable()));

        Assert.IsType<PeriodicIntervalSet>(Analyze<RealSet>(input, AnalysisFeatures.Domain));
        Assert.Equal(
            "interval[q:-1,1,q:1,1]",
            Analyze<RealSet>(input, AnalysisFeatures.Range).Canonical);
        var zeros = Assert.IsType<IntegerLatticeSet>(
            Analyze<RealSet>(input, AnalysisFeatures.Zeros));
        Assert.Equal("arctan(πn₁) + πn₂", zeros.Expression);
        Assert.Equal(2, zeros.Parameters.Length);
        Assert.Equal(FunctionParity.Odd, Analyze<FunctionParity>(
            input,
            AnalysisFeatures.Parity));
        Assert.Single(Analyze<ImmutableArray<FeaturePoint>>(
            input,
            AnalysisFeatures.Minima));
        Assert.Single(Analyze<ImmutableArray<FeaturePoint>>(
            input,
            AnalysisFeatures.Maxima));
        AssertUnsupported<ImmutableArray<FeaturePoint>>(
            input,
            AnalysisFeatures.InflectionPoints);
        Assert.Empty(Analyze<ImmutableArray<Asymptote>>(
            input,
            AnalysisFeatures.VerticalAsymptotes));
        Assert.Equal(2, Analyze<ImmutableArray<MonotoneRegion>>(
            input,
            AnalysisFeatures.Monotonicity).Length);
        Periodicity period = Analyze<Periodicity>(input, AnalysisFeatures.Period);
        Assert.Equal(PeriodicityKind.PeriodicWithFundamentalPeriod, period.Kind);
        Assert.Equal("pi:1:0", ExactRealCanonical.Format(period.FundamentalPeriod!));
    }

    [Fact]
    public void ExponentialPlusIdentityHasUniqueLambertZeroAndLeftObliqueTail()
    {
        InputExpression input = Add(Function("exp", Variable()), Variable());

        Assert.Equal("reals", Analyze<RealSet>(input, AnalysisFeatures.Domain).Canonical);
        Assert.Equal("reals", Analyze<RealSet>(input, AnalysisFeatures.Range).Canonical);
        PointSet zeros = Assert.IsType<PointSet>(
            Analyze<RealSet>(input, AnalysisFeatures.Zeros));
        Assert.Equal("fn:negate(fn:lambertw(q:1))", ExactRealCanonical.Format(
            Assert.Single(zeros.Points)));
        Assert.Equal("q:1", ExactRealCanonical.Format(
            Analyze<OptionalValue<ExactReal>>(input, AnalysisFeatures.YIntercept).Value!));
        Assert.Empty(Analyze<ImmutableArray<FeaturePoint>>(
            input,
            AnalysisFeatures.Minima));
        Assert.Empty(Analyze<ImmutableArray<FeaturePoint>>(
            input,
            AnalysisFeatures.Maxima));
        Assert.Empty(Analyze<ImmutableArray<FeaturePoint>>(
            input,
            AnalysisFeatures.InflectionPoints));
        ImmutableArray<Asymptote> oblique = Analyze<ImmutableArray<Asymptote>>(
            input,
            AnalysisFeatures.ObliqueAsymptotes);
        Assert.Single(oblique);
        Assert.Equal("q:1", ExactRealCanonical.Format(oblique[0].Slope!));
        Assert.Single(Analyze<ImmutableArray<MonotoneRegion>>(
            input,
            AnalysisFeatures.Monotonicity));
        Assert.Equal(
            PeriodicityKind.NotPeriodic,
            Analyze<Periodicity>(input, AnalysisFeatures.Period).Kind);
    }

    [Fact]
    public void CertificateReplayRejectsKindPatternDomainUnitRuleFeatureAndClaimMutations()
    {
        InputExpression input = Function("sin", Function("ln", Variable()));
        (AnalysisRequest request, SemanticExpression expression, ProofOutcome<RealSet> outcome) =
            AnalyzeOutcome<RealSet>(input, AnalysisFeatures.Range);
        RealSet value = outcome.Value!;
        var certificate = Assert.IsType<ElementaryCompositionProofCertificate>(outcome.Certificate);

        AssertRejected(certificate with { Kind = ElementaryCompositionKind.SineOfExponential });
        AssertRejected(certificate with { Kind = ElementaryCompositionKind.SineOfCommonLogarithm });
        AssertRejected(certificate with { PatternCanonical = certificate.PatternCanonical + ":changed" });
        AssertRejected(certificate with { DefinednessCanonical = "true" });
        AssertRejected(certificate with { AngleUnit = AngleUnit.Degrees });
        AssertRejected(certificate with { Rule = "untrusted" });
        AssertRejected(certificate with { ProvenFeature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { Claim = "empty" });

        void AssertRejected(ElementaryCompositionProofCertificate changed) =>
            Assert.False(ElementaryCompositionCertificateChecker.Check(
                request,
                expression,
                changed,
                ClaimCanonical.For(value),
                new ResourceBudget()));
    }

    [Fact]
    public void ProductionEngineChecksEveryPublishedElementaryCompositionClaim()
    {
        foreach (InputExpression complete in new[]
                 {
                     Function("sin", Function("ln", Variable())),
                     Function("sin", Function("log", Variable())),
                     Add(Function("exp", Variable()), Variable())
                 })
        {
            AnalysisRequest request = Request(complete, AnalysisFeatures.All);
            AnalysisReport report = AnalysisEngine.Analyze(request);
            AssertAllFeaturesProved(report);
            Assert.IsType<ElementaryCompositionProofCertificate>(report.Range.Certificate);
            Assert.True(CertificateChecker.Check(request, report.Expression!, report.Range));
        }

        foreach (InputExpression partial in new[]
                 {
                     Function("sin", Function("sqrt", Variable())),
                     Function("sin", Function("exp", Variable())),
                     Function("sin", Function("tan", Variable()))
                 })
        {
            AnalysisRequest request = Request(partial, AnalysisFeatures.All);
            AnalysisReport report = AnalysisEngine.Analyze(request);
            AssertAllFeaturesExceptInflectionProved(report);
            Assert.Equal(ProofState.Unknown, report.InflectionPoints.State);
            Assert.Equal(
                UnknownReason.UnsupportedFragment,
                report.InflectionPoints.UnknownReason);
            Assert.IsType<ElementaryCompositionProofCertificate>(report.Range.Certificate);
            Assert.True(CertificateChecker.Check(request, report.Expression!, report.Domain));
            Assert.True(CertificateChecker.Check(request, report.Expression!, report.Range));
        }
    }

    [Fact]
    public void ExtraHolesAngleUnitsAndCancellationAreNeverGuessed()
    {
        InputExpression x = Variable();
        InputExpression retainedHole = Add(
            Function("sin", Function("exp", x)),
            Multiply(Number(0), Divide(Number(1), Subtract(x, Number(3)))));
        AssertUnsupported<RealSet>(retainedHole, AnalysisFeatures.Range);

        AnalysisRequest degrees = new(
            Function("sin", Function("exp", x)),
            AnalysisFeatures.Range,
            AngleUnit.Degrees,
            "x",
            static () => true);
        var degreeBudget = new ResourceBudget();
        SemanticExpression degreeExpression = new SemanticGraphBuilder(degreeBudget).Build(
            degrees.Expression);
        Assert.False(ElementaryCompositionAnalyzer.TryAnalyze(
            degrees,
            degreeExpression,
            AnalysisFeatures.Range,
            degreeBudget,
            out ProofOutcome<RealSet> _));

        var cancelled = new ResourceBudget(static () => false);
        Assert.Throws<AnalysisCancelledException>(() =>
            ElementaryCompositionAnalyzer.TryAnalyze(
                Request(Function("sin", Function("tan", x)), AnalysisFeatures.Range),
                new SemanticGraphBuilder(new ResourceBudget()).Build(
                    Function("sin", Function("tan", x))),
                AnalysisFeatures.Range,
                cancelled,
                out ProofOutcome<RealSet> _));

        InputExpression budgetInput = Function("sin", Function("ln", x));
        AnalysisRequest budgetRequest = Request(budgetInput, AnalysisFeatures.Range);
        SemanticExpression budgetExpression = new SemanticGraphBuilder(new ResourceBudget()).Build(
            budgetInput);
        var exhausted = new ResourceBudget();
        exhausted.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            ElementaryCompositionAnalyzer.TryAnalyze(
                budgetRequest,
                budgetExpression,
                AnalysisFeatures.Range,
                exhausted,
                out ProofOutcome<RealSet> _));
    }

    private static T Analyze<T>(InputExpression input, AnalysisFeatures feature)
    {
        (_, _, ProofOutcome<T> outcome) = AnalyzeOutcome<T>(input, feature);
        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.NotNull(outcome.Value);
        return outcome.Value;
    }

    private static void AssertAllFeaturesProved(AnalysisReport report)
    {
        AssertAllFeaturesExceptInflectionProved(report);
        Assert.Equal(ProofState.Proved, report.InflectionPoints.State);
    }

    private static void AssertAllFeaturesExceptInflectionProved(AnalysisReport report)
    {
        Assert.Equal(ProofState.Proved, report.Domain.State);
        Assert.Equal(ProofState.Proved, report.Range.State);
        Assert.Equal(ProofState.Proved, report.Parity.State);
        Assert.Equal(ProofState.Proved, report.Zeros.State);
        Assert.Equal(ProofState.Proved, report.YIntercept.State);
        Assert.Equal(ProofState.Proved, report.Minima.State);
        Assert.Equal(ProofState.Proved, report.Maxima.State);
        Assert.Equal(ProofState.Proved, report.VerticalAsymptotes.State);
        Assert.Equal(ProofState.Proved, report.HorizontalAsymptotes.State);
        Assert.Equal(ProofState.Proved, report.ObliqueAsymptotes.State);
        Assert.Equal(ProofState.Proved, report.Monotonicity.State);
        Assert.Equal(ProofState.Proved, report.Period.State);
    }

    private static (
        AnalysisRequest Request,
        SemanticExpression Expression,
        ProofOutcome<T> Outcome) AnalyzeOutcome<T>(
        InputExpression input,
        AnalysisFeatures feature)
    {
        AnalysisRequest request = Request(input, feature);
        var budget = new ResourceBudget();
        SemanticExpression expression = new SemanticGraphBuilder(budget).Build(input);
        Assert.True(ElementaryCompositionAnalyzer.TryAnalyze(
            request,
            expression,
            feature,
            budget,
            out ProofOutcome<T> outcome),
            $"{expression.Value.Canonical}; defined={expression.DefinedWhen.Canonical}; feature={feature}");
        var certificate = Assert.IsType<ElementaryCompositionProofCertificate>(outcome.Certificate);
        Assert.True(ElementaryCompositionCertificateChecker.Check(
            request,
            expression,
            certificate,
            ClaimCanonical.For(outcome.Value!),
            new ResourceBudget()));
        return (request, expression, outcome);
    }

    private static void AssertUnsupported<T>(InputExpression input, AnalysisFeatures feature)
    {
        AnalysisRequest request = Request(input, feature);
        var budget = new ResourceBudget();
        SemanticExpression expression = new SemanticGraphBuilder(budget).Build(input);
        Assert.False(ElementaryCompositionAnalyzer.TryAnalyze(
            request,
            expression,
            feature,
            budget,
            out ProofOutcome<T> _));
    }

    private static AnalysisRequest Request(InputExpression expression, AnalysisFeatures feature) =>
        new(expression, feature, AngleUnit.Radians, "x", static () => true);

    private static InputExpression Variable() => InputExpression.Variable("x", Source);

    private static InputExpression Number(int value) => InputExpression.Number(
        new BigRational(value),
        Source);

    private static InputExpression Add(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Add, left, right, Source);

    private static InputExpression Subtract(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Subtract, left, right, Source);

    private static InputExpression Multiply(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Multiply, left, right, Source);

    private static InputExpression Divide(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Divide, left, right, Source);

    private static InputExpression Function(string name, params InputExpression[] arguments) =>
        InputExpression.Function(name, arguments.ToImmutableArray(), Source);
}
