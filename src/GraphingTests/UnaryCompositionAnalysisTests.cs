using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class UnaryCompositionAnalysisTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Theory]
    [InlineData("sin", "asin", "interval[q:-1,1,q:1,1]", "interval[q:-1,1,q:1,1]")]
    [InlineData("cos", "acos", "interval[q:-1,1,q:1,1]", "interval[q:-1,1,q:1,1]")]
    [InlineData("tan", "atan", "reals", "reals")]
    [InlineData("exp", "ln", "interval[q:0,0,+inf,0]", "interval[q:0,0,+inf,0]")]
    [InlineData("ln", "exp", "reals", "reals")]
    public void GuardedInverseIdentitiesHaveDedicatedExactProofs(
        string outer,
        string inner,
        string expectedDomain,
        string expectedRange)
    {
        InputExpression input = Function(outer, Function(inner, Variable()));

        Assert.Equal(expectedDomain, Analyze<RealSet>(input, AnalysisFeatures.Domain).Canonical);
        Assert.Equal(expectedRange, Analyze<RealSet>(input, AnalysisFeatures.Range).Canonical);
        Assert.Empty(Analyze<ImmutableArray<FeaturePoint>>(input, AnalysisFeatures.InflectionPoints));
        Assert.Empty(Analyze<ImmutableArray<Asymptote>>(input, AnalysisFeatures.VerticalAsymptotes));
        Assert.Empty(Analyze<ImmutableArray<Asymptote>>(input, AnalysisFeatures.HorizontalAsymptotes));
        Assert.Equal(
            PeriodicityKind.NotPeriodic,
            Analyze<Periodicity>(input, AnalysisFeatures.Period).Kind);
        Assert.Single(Analyze<ImmutableArray<MonotoneRegion>>(input, AnalysisFeatures.Monotonicity));

        if (inner is "asin" or "acos")
        {
            Assert.Single(Analyze<ImmutableArray<FeaturePoint>>(input, AnalysisFeatures.Minima));
            Assert.Single(Analyze<ImmutableArray<FeaturePoint>>(input, AnalysisFeatures.Maxima));
        }
        else
        {
            Assert.Empty(Analyze<ImmutableArray<FeaturePoint>>(input, AnalysisFeatures.Minima));
            Assert.Empty(Analyze<ImmutableArray<FeaturePoint>>(input, AnalysisFeatures.Maxima));
        }
    }

    [Fact]
    public void PrincipalInverseReverseCompositionsPreserveBranchRangesAndHoles()
    {
        InputExpression x = Variable();

        InputExpression arcsineSine = Function("asin", Function("sin", x));
        Assert.Equal(
            "interval[pi:-1/2:0,1,pi:1/2:0,1]",
            Analyze<RealSet>(arcsineSine, AnalysisFeatures.Range).Canonical);
        Assert.Equal(
            "periodic-points[pi:0:0,pi:1:0,m,m:Z]",
            Analyze<RealSet>(arcsineSine, AnalysisFeatures.Zeros).Canonical);
        Assert.Equal(FunctionParity.Odd, Analyze<FunctionParity>(arcsineSine, AnalysisFeatures.Parity));
        Assert.Equal(2, Analyze<ImmutableArray<MonotoneRegion>>(
            arcsineSine,
            AnalysisFeatures.Monotonicity).Length);
        AssertPeriod(arcsineSine, "pi:2:0");

        InputExpression arccosineCosine = Function("acos", Function("cos", x));
        Assert.Equal(
            "interval[q:0,1,pi:1:0,1]",
            Analyze<RealSet>(arccosineCosine, AnalysisFeatures.Range).Canonical);
        Assert.Equal(FunctionParity.Even, Analyze<FunctionParity>(
            arccosineCosine,
            AnalysisFeatures.Parity));
        AssertPeriod(arccosineCosine, "pi:2:0");

        InputExpression arctangentTangent = Function("atan", Function("tan", x));
        Assert.Equal(
            "periodic-intervals[pi:1:0,m,m:Z,[pi:-1/2:0,0,pi:1/2:0,0]]",
            Analyze<RealSet>(arctangentTangent, AnalysisFeatures.Domain).Canonical);
        Assert.Equal(
            "interval[pi:-1/2:0,0,pi:1/2:0,0]",
            Analyze<RealSet>(arctangentTangent, AnalysisFeatures.Range).Canonical);
        Assert.Empty(Analyze<ImmutableArray<Asymptote>>(
            arctangentTangent,
            AnalysisFeatures.VerticalAsymptotes));
        Assert.Single(Analyze<ImmutableArray<MonotoneRegion>>(
            arctangentTangent,
            AnalysisFeatures.Monotonicity));
        AssertPeriod(arctangentTangent, "pi:1:0");
    }

    [Fact]
    public void ExponentialOfSineHasExactRangeCellsExtremaAndInflections()
    {
        InputExpression input = Function("exp", Function("sin", Variable()));

        Assert.Equal(
            "interval[fn:divide(q:1,named:e),1,named:e,1]",
            Analyze<RealSet>(input, AnalysisFeatures.Range).Canonical);
        Assert.True(Analyze<RealSet>(input, AnalysisFeatures.Zeros).IsEmpty);
        Assert.Equal("q:1", ExactRealCanonical.Format(
            Analyze<OptionalValue<ExactReal>>(input, AnalysisFeatures.YIntercept).Value!));
        Assert.Single(Analyze<ImmutableArray<FeaturePoint>>(input, AnalysisFeatures.Minima));
        Assert.Single(Analyze<ImmutableArray<FeaturePoint>>(input, AnalysisFeatures.Maxima));
        Assert.Equal(2, Analyze<ImmutableArray<FeaturePoint>>(
            input,
            AnalysisFeatures.InflectionPoints).Length);
        Assert.Equal(2, Analyze<ImmutableArray<MonotoneRegion>>(
            input,
            AnalysisFeatures.Monotonicity).Length);
        Assert.Equal(FunctionParity.Neither, Analyze<FunctionParity>(input, AnalysisFeatures.Parity));
        AssertPeriod(input, "pi:2:0");
    }

    [Fact]
    public void SquareRootAndLogarithmOfSineRetainExactPeriodicDomains()
    {
        InputExpression squareRoot = Function("sqrt", Function("sin", Variable()));
        Assert.Equal(
            "periodic-intervals[pi:2:0,m,m:Z,[pi:0:0,1,pi:1:0,1]]",
            Analyze<RealSet>(squareRoot, AnalysisFeatures.Domain).Canonical);
        Assert.Equal(
            "interval[q:0,1,q:1,1]",
            Analyze<RealSet>(squareRoot, AnalysisFeatures.Range).Canonical);
        Assert.Equal(2, Analyze<ImmutableArray<FeaturePoint>>(
            squareRoot,
            AnalysisFeatures.Minima).Length);
        Assert.Single(Analyze<ImmutableArray<FeaturePoint>>(
            squareRoot,
            AnalysisFeatures.Maxima));
        Assert.Empty(Analyze<ImmutableArray<FeaturePoint>>(
            squareRoot,
            AnalysisFeatures.InflectionPoints));
        Assert.Equal(2, Analyze<ImmutableArray<MonotoneRegion>>(
            squareRoot,
            AnalysisFeatures.Monotonicity).Length);

        InputExpression logarithm = Function("ln", Function("sin", Variable()));
        Assert.Equal(
            "periodic-intervals[pi:2:0,m,m:Z,[pi:0:0,0,pi:1:0,0]]",
            Analyze<RealSet>(logarithm, AnalysisFeatures.Domain).Canonical);
        Assert.Equal(
            "interval[-inf,0,q:0,1]",
            Analyze<RealSet>(logarithm, AnalysisFeatures.Range).Canonical);
        Assert.Single(Analyze<ImmutableArray<FeaturePoint>>(
            logarithm,
            AnalysisFeatures.Maxima));
        Assert.Empty(Analyze<ImmutableArray<FeaturePoint>>(
            logarithm,
            AnalysisFeatures.Minima));
        Assert.Single(Analyze<ImmutableArray<Asymptote>>(
            logarithm,
            AnalysisFeatures.VerticalAsymptotes));
        Assert.Equal(2, Analyze<ImmutableArray<MonotoneRegion>>(
            logarithm,
            AnalysisFeatures.Monotonicity).Length);
        AssertPeriod(logarithm, "pi:2:0");
    }

    [Fact]
    public void NaturalLogarithmOfAbsoluteAffineHasExactDisconnectedCells()
    {
        InputExpression input = Function("ln", Function("abs", Variable()));

        Assert.Equal(
            "difference[reals,points[q:0]]",
            Analyze<RealSet>(input, AnalysisFeatures.Domain).Canonical);
        Assert.Equal("reals", Analyze<RealSet>(input, AnalysisFeatures.Range).Canonical);
        Assert.Equal(
            "points[q:-1,q:1]",
            Analyze<RealSet>(input, AnalysisFeatures.Zeros).Canonical);
        Assert.False(Analyze<OptionalValue<ExactReal>>(
            input,
            AnalysisFeatures.YIntercept).HasValue);
        Assert.Empty(Analyze<ImmutableArray<FeaturePoint>>(
            input,
            AnalysisFeatures.Minima));
        Assert.Empty(Analyze<ImmutableArray<FeaturePoint>>(
            input,
            AnalysisFeatures.Maxima));
        Assert.Empty(Analyze<ImmutableArray<FeaturePoint>>(
            input,
            AnalysisFeatures.InflectionPoints));
        Assert.Single(Analyze<ImmutableArray<Asymptote>>(
            input,
            AnalysisFeatures.VerticalAsymptotes));
        Assert.Equal(FunctionParity.Even, Analyze<FunctionParity>(
            input,
            AnalysisFeatures.Parity));
        ImmutableArray<MonotoneRegion> monotonicity = Analyze<ImmutableArray<MonotoneRegion>>(
            input,
            AnalysisFeatures.Monotonicity);
        Assert.Equal(2, monotonicity.Length);
        Assert.Equal(Monotonicity.Decreasing, monotonicity[0].Direction);
        Assert.Equal(Monotonicity.Increasing, monotonicity[1].Direction);
        Assert.Equal(
            PeriodicityKind.NotPeriodic,
            Analyze<Periodicity>(input, AnalysisFeatures.Period).Kind);
    }

    [Fact]
    public void NaturalAndCommonLogarithmsOfAbsoluteAffineKeepTheirBasesDistinct()
    {
        InputExpression affine = Add(Multiply(Number(2), Variable()), Number(10));
        InputExpression natural = Function("ln", Function("abs", affine));
        InputExpression common = Function("log", Function("abs", affine));

        OptionalValue<ExactReal> naturalIntercept = Analyze<OptionalValue<ExactReal>>(
            natural,
            AnalysisFeatures.YIntercept);
        OptionalValue<ExactReal> commonIntercept = Analyze<OptionalValue<ExactReal>>(
            common,
            AnalysisFeatures.YIntercept);
        Assert.Equal("fn:ln(q:10)", ExactRealCanonical.Format(naturalIntercept.Value!));
        Assert.Equal("q:1", ExactRealCanonical.Format(commonIntercept.Value!));

        foreach (AnalysisFeatures feature in new[]
                 {
                     AnalysisFeatures.Domain,
                     AnalysisFeatures.Range,
                     AnalysisFeatures.Parity,
                     AnalysisFeatures.Zeros,
                     AnalysisFeatures.Minima,
                     AnalysisFeatures.Maxima,
                     AnalysisFeatures.InflectionPoints,
                     AnalysisFeatures.VerticalAsymptotes,
                     AnalysisFeatures.HorizontalAsymptotes,
                     AnalysisFeatures.ObliqueAsymptotes,
                     AnalysisFeatures.Monotonicity,
                     AnalysisFeatures.Period
                 })
        {
            Assert.Equal(
                ClaimCanonical.ForObject(Analyze<object>(natural, feature)),
                ClaimCanonical.ForObject(Analyze<object>(common, feature)));
        }

        Assert.Equal(
            "q:2",
            ExactRealCanonical.Format(Analyze<OptionalValue<ExactReal>>(
                Function(
                    "log",
                    Function("abs", Add(Variable(), Number(100)))),
                AnalysisFeatures.YIntercept).Value!));
        Assert.Equal(
            "q:-2",
            ExactRealCanonical.Format(Analyze<OptionalValue<ExactReal>>(
                Function(
                    "log",
                    Function(
                        "abs",
                        Add(Variable(), Number(new BigRational(1, 100))))),
                AnalysisFeatures.YIntercept).Value!));

        (AnalysisRequest request, SemanticExpression expression,
            ProofOutcome<OptionalValue<ExactReal>> outcome) =
            AnalyzeOutcome<OptionalValue<ExactReal>>(
                common,
                AnalysisFeatures.YIntercept);
        var certificate = Assert.IsType<UnaryCompositionProofCertificate>(outcome.Certificate);
        Assert.Equal("log", certificate.OuterFunction);
        Assert.False(UnaryCompositionCertificateChecker.Check(
            request,
            expression,
            certificate with { OuterFunction = "ln" },
            ClaimCanonical.For(outcome.Value!),
            new ResourceBudget()));
    }

    [Fact]
    public void CommonLogarithmOfSineRoutesToTheBaseAwareAffinePhaseProof()
    {
        InputExpression input = Function("log", Function("sin", Variable()));
        AssertUnsupported<RealSet>(input, AnalysisFeatures.Range);

        AnalysisRequest request = Request(input, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        Assert.Equal(ProofState.Proved, report.Range.State);
        var certificate = Assert.IsType<AffinePhaseSineCompositionProofCertificate>(
            report.Range.Certificate);
        Assert.Equal(AffinePhaseSineOuterKind.CommonLogarithm, certificate.OuterKind);
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Range));
    }

    [Fact]
    public void MonotoneOuterTrigOfSineUsesInnerSignCellsExactly()
    {
        foreach (string outer in new[] { "sin", "tan" })
        {
            InputExpression input = Function(outer, Function("sin", Variable()));
            Assert.IsType<IntervalSet>(Analyze<RealSet>(input, AnalysisFeatures.Range));
            Assert.Equal(
                "periodic-points[pi:0:0,pi:1:0,m,m:Z]",
                Analyze<RealSet>(input, AnalysisFeatures.Zeros).Canonical);
            Assert.Single(Analyze<ImmutableArray<FeaturePoint>>(input, AnalysisFeatures.Minima));
            Assert.Single(Analyze<ImmutableArray<FeaturePoint>>(input, AnalysisFeatures.Maxima));
            Assert.Equal(FunctionParity.Odd, Analyze<FunctionParity>(input, AnalysisFeatures.Parity));
            Assert.Equal(2, Analyze<ImmutableArray<MonotoneRegion>>(
                input,
                AnalysisFeatures.Monotonicity).Length);
            AssertPeriod(input, "pi:2:0");
        }

        InputExpression sine = Function("sin", Function("sin", Variable()));
        Assert.Single(Analyze<ImmutableArray<FeaturePoint>>(
            sine,
            AnalysisFeatures.InflectionPoints));

        InputExpression tangent = Function("tan", Function("sin", Variable()));
        AssertUnsupported<ImmutableArray<FeaturePoint>>(tangent, AnalysisFeatures.InflectionPoints);
    }

    [Fact]
    public void EvenCosineOfSineHasHalfPeriodAndLeavesTranscendentalInflectionsUnknown()
    {
        InputExpression input = Function("cos", Function("sin", Variable()));

        Assert.Equal(
            "interval[fn:cos(q:1),1,q:1,1]",
            Analyze<RealSet>(input, AnalysisFeatures.Range).Canonical);
        Assert.True(Analyze<RealSet>(input, AnalysisFeatures.Zeros).IsEmpty);
        Assert.Equal(FunctionParity.Even, Analyze<FunctionParity>(input, AnalysisFeatures.Parity));
        Assert.Single(Analyze<ImmutableArray<FeaturePoint>>(input, AnalysisFeatures.Minima));
        Assert.Single(Analyze<ImmutableArray<FeaturePoint>>(input, AnalysisFeatures.Maxima));
        Assert.Equal(2, Analyze<ImmutableArray<MonotoneRegion>>(
            input,
            AnalysisFeatures.Monotonicity).Length);
        AssertPeriod(input, "pi:1:0");
        AssertUnsupported<ImmutableArray<FeaturePoint>>(input, AnalysisFeatures.InflectionPoints);
    }

    [Fact]
    public void AffineScalingReflectionAndGuardedRewritesAreStructural()
    {
        InputExpression x = Variable();
        InputExpression reflected = Function(
            "exp",
            Function("sin", Add(Multiply(Number(-2), x), Number(1))));
        Assert.Equal("pi:1:0", ExactRealCanonical.Format(
            Analyze<Periodicity>(reflected, AnalysisFeatures.Period).FundamentalPeriod!));
        Assert.Equal(2, Analyze<ImmutableArray<MonotoneRegion>>(
            reflected,
            AnalysisFeatures.Monotonicity).Length);

        InputExpression retainedHole = Add(
            Function("exp", Function("sin", x)),
            Multiply(Number(0), Divide(Number(1), Subtract(x, Number(3)))));
        AssertUnsupported<RealSet>(retainedHole, AnalysisFeatures.Range);
        AssertUnsupported<Periodicity>(retainedHole, AnalysisFeatures.Period);
    }

    [Fact]
    public void DedicatedCertificateReplayRejectsKindPatternDomainRuleFeatureAndClaimMutations()
    {
        InputExpression input = Function("exp", Function("sin", Variable()));
        (AnalysisRequest request, SemanticExpression expression, ProofOutcome<RealSet> outcome) =
            AnalyzeOutcome<RealSet>(input, AnalysisFeatures.Range);
        RealSet value = outcome.Value!;
        var certificate = Assert.IsType<UnaryCompositionProofCertificate>(outcome.Certificate);

        AssertRejected(certificate with { Kind = UnaryCompositionKind.GuardedInverseIdentity });
        AssertRejected(certificate with { OuterFunction = "cos" });
        AssertRejected(certificate with { InnerFunction = "cos" });
        AssertRejected(certificate with { PatternCanonical = certificate.PatternCanonical + ":changed" });
        AssertRejected(certificate with { DefinednessCanonical = "false" });
        AssertRejected(certificate with { Rule = "untrusted" });
        AssertRejected(certificate with { ProvenFeature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { Claim = "empty" });

        void AssertRejected(UnaryCompositionProofCertificate changed) =>
            Assert.False(UnaryCompositionCertificateChecker.Check(
                request,
                expression,
                changed,
                ClaimCanonical.For(value),
                new ResourceBudget()));
    }

    [Fact]
    public void AnalysisEnginePublishesOnlyCheckedCompositionOutcomes()
    {
        InputExpression exponential = Function("exp", Function("sin", Variable()));
        AnalysisRequest exponentialRequest = Request(exponential, AnalysisFeatures.All);
        AnalysisReport exponentialReport = AnalysisEngine.Analyze(exponentialRequest);

        AssertAllFeaturesProved(exponentialReport);
        Assert.IsType<UnaryCompositionProofCertificate>(exponentialReport.Range.Certificate);
        Assert.IsType<UnaryCompositionProofCertificate>(exponentialReport.InflectionPoints.Certificate);
        Assert.True(CertificateChecker.Check(
            exponentialRequest,
            exponentialReport.Expression!,
            exponentialReport.Range));
        Assert.True(CertificateChecker.Check(
            exponentialRequest,
            exponentialReport.Expression!,
            exponentialReport.InflectionPoints));

        InputExpression guardedIdentity = Function("sin", Function("asin", Variable()));
        AnalysisRequest identityRequest = Request(guardedIdentity, AnalysisFeatures.All);
        AnalysisReport identityReport = AnalysisEngine.Analyze(identityRequest);
        AssertAllFeaturesProved(identityReport);
        Assert.IsType<UnaryCompositionProofCertificate>(identityReport.Range.Certificate);
        Assert.True(CertificateChecker.Check(
            identityRequest,
            identityReport.Expression!,
            identityReport.Range));

        foreach (InputExpression partial in new[]
                 {
                     Function("sqrt", Function("sin", Variable())),
                     Function("ln", Function("sin", Variable())),
                     Function("ln", Function("abs", Variable()))
                 })
        {
            AnalysisRequest request = Request(partial, AnalysisFeatures.All);
            AnalysisReport report = AnalysisEngine.Analyze(request);
            AssertAllFeaturesProved(report);
            Assert.IsType<UnaryCompositionProofCertificate>(report.Domain.Certificate);
            Assert.True(CertificateChecker.Check(request, report.Expression!, report.Domain));
        }

        foreach (string outer in new[] { "tan", "cos" })
        {
            InputExpression unresolved = Function(outer, Function("sin", Variable()));
            AnalysisReport report = AnalysisEngine.Analyze(Request(
                unresolved,
                AnalysisFeatures.All));
            Assert.Equal(ProofState.Unknown, report.InflectionPoints.State);
            Assert.Equal(
                UnknownReason.UnsupportedFragment,
                report.InflectionPoints.UnknownReason);
        }
    }

    [Fact]
    public void WorkIsDeterministicAndBudgetsAndCancellationAreEnforced()
    {
        InputExpression input = Function("asin", Function("sin", Variable()));
        (AnalysisRequest _, SemanticExpression _, ProofOutcome<RealSet> first) =
            AnalyzeOutcome<RealSet>(input, AnalysisFeatures.Range);
        (AnalysisRequest _, SemanticExpression _, ProofOutcome<RealSet> second) =
            AnalyzeOutcome<RealSet>(input, AnalysisFeatures.Range);
        Assert.Equal(first.Value!.Canonical, second.Value!.Canonical);

        var cancelledBudget = new ResourceBudget(static () => false);
        Assert.Throws<AnalysisCancelledException>(() =>
            new SemanticGraphBuilder(cancelledBudget).Build(input));

        ExactInteger oversized = ExactInteger.One << (AnalysisLimits.CoefficientBits + 1);
        InputExpression excessive = Function(
            "exp",
            Function("sin", Multiply(Number(new BigRational(oversized)), Variable())));
        var budget = new ResourceBudget();
        SemanticExpression semantic = new SemanticGraphBuilder(budget).Build(excessive);
        Assert.Throws<BudgetExceededException>(() =>
            UnaryCompositionAnalyzer.TryAnalyze(
                Request(excessive, AnalysisFeatures.Range),
                semantic,
                AnalysisFeatures.Range,
                budget,
                out ProofOutcome<RealSet> _));
    }

    private static void AssertPeriod(InputExpression input, string expected)
    {
        Periodicity period = Analyze<Periodicity>(input, AnalysisFeatures.Period);
        Assert.Equal(PeriodicityKind.PeriodicWithFundamentalPeriod, period.Kind);
        Assert.Equal(expected, ExactRealCanonical.Format(period.FundamentalPeriod!));
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

    private static T Analyze<T>(InputExpression input, AnalysisFeatures feature)
    {
        (_, _, ProofOutcome<T> outcome) = AnalyzeOutcome<T>(input, feature);
        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.NotNull(outcome.Value);
        return outcome.Value;
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
        Assert.True(UnaryCompositionAnalyzer.TryAnalyze(
            request,
            expression,
            feature,
            budget,
            out ProofOutcome<T> outcome),
            $"{expression.Value.Canonical}; defined={expression.DefinedWhen.Canonical}; feature={feature}");
        var certificate = Assert.IsType<UnaryCompositionProofCertificate>(outcome.Certificate);
        Assert.True(UnaryCompositionCertificateChecker.Check(
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
        Assert.False(UnaryCompositionAnalyzer.TryAnalyze(
            request,
            expression,
            feature,
            budget,
            out ProofOutcome<T> _));
    }

    private static AnalysisRequest Request(InputExpression expression, AnalysisFeatures feature) =>
        new(expression, feature, AngleUnit.Radians, "x", static () => true);

    private static InputExpression Variable() => InputExpression.Variable("x", Source);

    private static InputExpression Number(int value) => Number(new BigRational(value));

    private static InputExpression Number(BigRational value) => InputExpression.Number(value, Source);

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
