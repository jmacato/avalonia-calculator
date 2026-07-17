using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class UnaryCompositionCertificateReplayTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Theory]
    [InlineData((int)AngleUnit.Radians)]
    [InlineData((int)AngleUnit.Degrees)]
    [InlineData((int)AngleUnit.Grads)]
    public void IndependentReplayCoversEveryPublishedFeatureAndCompositionKind(
        int unitValue)
    {
        AngleUnit angleUnit = (AngleUnit)unitValue;
        InputExpression x = Variable();

        foreach (InputExpression identity in new[]
                 {
                     Function("sin", Function("asin", Add(Multiply(Number(2), x), Number(1)))),
                     Function("cos", Function("acos", Add(Multiply(Number(2), x), Number(1)))),
                     Function("tan", Function("atan", Add(Multiply(Number(-2), x), Number(1)))),
                     Function("exp", Function("ln", Add(Multiply(Number(2), x), Number(1)))),
                     Function("ln", Function("exp", Add(Multiply(Number(-2), x), Number(1))))
                 })
        {
            AssertEveryPublishedFeature(identity, angleUnit, supportsInflections: true);
        }

        foreach ((InputExpression Composition, bool SupportsInflections) item in new[]
                 {
                     (Function("asin", Function("sin", Add(Multiply(Number(2), x), Number(1)))), true),
                     (Function("acos", Function("cos", Add(Multiply(Number(2), x), Number(1)))), true),
                     (Function("atan", Function("tan", Add(Multiply(Number(2), x), Number(1)))), true),
                     (Function("exp", Function("sin", Add(Multiply(Number(-2), x), Number(1)))), true),
                     (Function("sqrt", Function("sin", Multiply(Number(2), x))), true),
                     (Function("ln", Function("sin", Multiply(Number(2), x))), true),
                     (Function("sin", Function("sin", Add(Multiply(Number(-2), x), Number(1)))), true),
                     (Function("tan", Function("sin", Add(Multiply(Number(-2), x), Number(1)))), false),
                     (Function("cos", Function("sin", Add(Multiply(Number(2), x), Number(1)))), false)
                 })
        {
            AssertEveryPublishedFeature(
                item.Composition,
                angleUnit,
                item.SupportsInflections);
        }

        foreach (InputExpression logarithm in new[]
                 {
                     Function("ln", Function("abs", Add(Multiply(Number(-2), x), Number(10)))),
                     Function("log", Function("abs", Add(Multiply(Number(2), x), Number(100))))
                 })
        {
            AssertEveryPublishedFeature(logarithm, angleUnit, supportsInflections: true);
        }
    }

    [Theory]
    [InlineData((int)AngleUnit.Degrees, -50, 40, 90)]
    [InlineData((int)AngleUnit.Grads, -55, 45, 100)]
    public void PrincipalTangentReplayUsesExactRequestUnitBranchBoundaries(
        int unitValue,
        int expectedLower,
        int expectedUpper,
        int expectedPeriod)
    {
        AngleUnit angleUnit = (AngleUnit)unitValue;
        InputExpression input = Function(
            "atan",
            Function(
                "tan",
                Add(Multiply(Number(2), Variable()), Number(10))));
        (AnalysisRequest request, SemanticExpression semantic) = Build(
            input,
            AnalysisFeatures.All,
            angleUnit);

        ProofOutcome<RealSet> domain = Analyze<RealSet>(
            request,
            semantic,
            AnalysisFeatures.Domain);
        var intervals = Assert.IsType<PeriodicIntervalSet>(domain.Value);
        PeriodicInterval cell = Assert.Single(intervals.Intervals);
        Assert.Equal($"q:{expectedLower}", ExactRealCanonical.Format(cell.LowerOffset));
        Assert.Equal($"q:{expectedUpper}", ExactRealCanonical.Format(cell.UpperOffset));
        Assert.Equal($"q:{expectedPeriod}", ExactRealCanonical.Format(intervals.Period));

        ProofOutcome<RealSet> range = Analyze<RealSet>(
            request,
            semantic,
            AnalysisFeatures.Range);
        int halfTurn = angleUnit == AngleUnit.Degrees ? 90 : 100;
        Assert.Equal(
            $"interval[q:{-halfTurn},0,q:{halfTurn},0]",
            range.Value!.Canonical);

        ProofOutcome<Periodicity> period = Analyze<Periodicity>(
            request,
            semantic,
            AnalysisFeatures.Period);
        Assert.Equal(
            $"q:{expectedPeriod}",
            ExactRealCanonical.Format(period.Value!.FundamentalPeriod!));
    }

    [Theory]
    [InlineData((int)AngleUnit.Degrees, 180)]
    [InlineData((int)AngleUnit.Grads, 200)]
    public void ReplayRejectsLegacyRawRadianNestedTrigClaims(
        int unitValue,
        int halfTurn)
    {
        AngleUnit angleUnit = (AngleUnit)unitValue;
        InputExpression input = Function(
            "sin",
            Function("sin", Add(Variable(), Number(1))));
        (AnalysisRequest request, SemanticExpression semantic) = Build(
            input,
            AnalysisFeatures.YIntercept,
            angleUnit);
        ProofOutcome<OptionalValue<ExactReal>> outcome = Analyze<OptionalValue<ExactReal>>(
            request,
            semantic,
            AnalysisFeatures.YIntercept);
        var certificate = Assert.IsType<UnaryCompositionProofCertificate>(
            outcome.Certificate);

        ExactReal legacyInner = new FunctionReal(
            "sin",
            [new RationalReal(BigRational.One)]);
        var legacyValue = OptionalValue<ExactReal>.Some(
            new FunctionReal("sin", [legacyInner]));
        string wrongClaim = ClaimCanonical.For(legacyValue);
        UnaryCompositionProofCertificate forged = certificate with
        {
            Claim = wrongClaim
        };

        Assert.Contains(
            $"pi:1/{halfTurn}:0",
            ExactRealCanonical.Format(outcome.Value!.Value!),
            StringComparison.Ordinal);
        Assert.False(UnaryCompositionCertificateChecker.Check(
            request,
            semantic,
            forged,
            wrongClaim,
            new ResourceBudget()));
        Assert.False(CertificateChecker.Check(
            request,
            semantic,
            ProofOutcome<OptionalValue<ExactReal>>.Proved(legacyValue, forged)));
    }

    [Theory]
    [InlineData((int)AngleUnit.Degrees, 90)]
    [InlineData((int)AngleUnit.Grads, 100)]
    public void ReplayRejectsLegacyRadianPrincipalBranchRanges(
        int unitValue,
        int expectedHalfTurn)
    {
        AngleUnit angleUnit = (AngleUnit)unitValue;
        InputExpression input = Function("asin", Function("sin", Variable()));
        (AnalysisRequest request, SemanticExpression semantic) = Build(
            input,
            AnalysisFeatures.Range,
            angleUnit);
        ProofOutcome<RealSet> outcome = Analyze<RealSet>(
            request,
            semantic,
            AnalysisFeatures.Range);
        var certificate = Assert.IsType<UnaryCompositionProofCertificate>(
            outcome.Certificate);

        RealSet legacyRadians = new IntervalSet(
            RealBound.Finite(new AffinePiReal(new BigRational(-1, 2), BigRational.Zero)),
            true,
            RealBound.Finite(new AffinePiReal(new BigRational(1, 2), BigRational.Zero)),
            true);
        string wrongClaim = ClaimCanonical.For(legacyRadians);

        Assert.Equal(
            $"interval[q:{-expectedHalfTurn},1,q:{expectedHalfTurn},1]",
            outcome.Value!.Canonical);
        Assert.False(UnaryCompositionCertificateChecker.Check(
            request,
            semantic,
            certificate with { Claim = wrongClaim },
            wrongClaim,
            new ResourceBudget()));
    }

    [Fact]
    public void ReplayRejectsCertificateAndGuardedSemanticChainMutations()
    {
        InputExpression input = Function(
            "sin",
            Function(
                "asin",
                Add(Multiply(Number(2), Variable()), Number(1))));
        (AnalysisRequest request, SemanticExpression semantic) = Build(
            input,
            AnalysisFeatures.Range,
            AngleUnit.Degrees);
        ProofOutcome<RealSet> outcome = Analyze<RealSet>(
            request,
            semantic,
            AnalysisFeatures.Range);
        var certificate = Assert.IsType<UnaryCompositionProofCertificate>(
            outcome.Certificate);
        string claim = ClaimCanonical.For(outcome.Value!);

        AssertRejected(certificate with { Kind = UnaryCompositionKind.PrimitiveOfAffineTrigonometric });
        AssertRejected(certificate with { OuterFunction = "cos" });
        AssertRejected(certificate with { InnerFunction = "acos" });
        AssertRejected(certificate with { PatternCanonical = certificate.PatternCanonical + ":forged" });
        AssertRejected(certificate with { DefinednessCanonical = Formula.True.Canonical });
        AssertRejected(certificate with { Rule = "untrusted" });
        AssertRejected(certificate with { Subject = certificate.Subject + ":forged" });
        AssertRejected(certificate with { ProvenFeature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { Claim = "empty" });

        Assert.False(Check(semantic with { RewriteHistory = [] }, certificate));
        Assert.False(Check(semantic with { SourceOperands = [] }, certificate));
        Assert.False(Check(
            semantic with { ContinuousWhen = Formula.False },
            certificate));

        SemanticExpression inner = Assert.Single(semantic.SourceOperands);
        SemanticExpression forgedInner = inner with
        {
            DefinedWhen = Formula.True,
            ContinuousWhen = Formula.True,
            DifferentiableWhen = Formula.True
        };
        Assert.False(Check(
            semantic with { SourceOperands = [forgedInner] },
            certificate));
        return;

        void AssertRejected(UnaryCompositionProofCertificate changed) =>
            Assert.False(Check(semantic, changed));

        bool Check(
            SemanticExpression candidate,
            UnaryCompositionProofCertificate candidateCertificate) =>
            UnaryCompositionCertificateChecker.Check(
                request,
                candidate,
                candidateCertificate,
                claim,
                new ResourceBudget());
    }

    [Fact]
    public void IndependentReplayHonorsCancellationAndWorkBudget()
    {
        InputExpression input = Function(
            "exp",
            Function("sin", Add(Multiply(Number(2), Variable()), Number(1))));
        (AnalysisRequest request, SemanticExpression semantic) = Build(
            input,
            AnalysisFeatures.InflectionPoints,
            AngleUnit.Grads);
        ProofOutcome<ImmutableArray<FeaturePoint>> outcome =
            Analyze<ImmutableArray<FeaturePoint>>(
                request,
                semantic,
                AnalysisFeatures.InflectionPoints);
        var certificate = Assert.IsType<UnaryCompositionProofCertificate>(
            outcome.Certificate);
        string claim = ClaimCanonical.For(outcome.Value!);

        Assert.Throws<AnalysisCancelledException>(() =>
            UnaryCompositionCertificateChecker.Check(
                request,
                semantic,
                certificate,
                claim,
                new ResourceBudget(static () => false)));

        var exhausted = new ResourceBudget();
        exhausted.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            UnaryCompositionCertificateChecker.Check(
                request,
                semantic,
                certificate,
                claim,
                exhausted));
    }

    private static void AssertEveryPublishedFeature(
        InputExpression input,
        AngleUnit angleUnit,
        bool supportsInflections)
    {
        (AnalysisRequest request, SemanticExpression semantic) = Build(
            input,
            AnalysisFeatures.All,
            angleUnit);

        AssertReplay<RealSet>(request, semantic, AnalysisFeatures.Domain);
        AssertReplay<RealSet>(request, semantic, AnalysisFeatures.Range);
        AssertReplay<FunctionParity>(request, semantic, AnalysisFeatures.Parity);
        AssertReplay<RealSet>(request, semantic, AnalysisFeatures.Zeros);
        AssertReplay<OptionalValue<ExactReal>>(
            request,
            semantic,
            AnalysisFeatures.YIntercept);
        AssertReplay<ImmutableArray<FeaturePoint>>(
            request,
            semantic,
            AnalysisFeatures.Minima);
        AssertReplay<ImmutableArray<FeaturePoint>>(
            request,
            semantic,
            AnalysisFeatures.Maxima);
        if (supportsInflections)
        {
            AssertReplay<ImmutableArray<FeaturePoint>>(
                request,
                semantic,
                AnalysisFeatures.InflectionPoints);
        }
        else
        {
            Assert.False(UnaryCompositionAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.InflectionPoints,
                new ResourceBudget(),
                out ProofOutcome<ImmutableArray<FeaturePoint>> _));
        }

        AssertReplay<ImmutableArray<Asymptote>>(
            request,
            semantic,
            AnalysisFeatures.VerticalAsymptotes);
        AssertReplay<ImmutableArray<Asymptote>>(
            request,
            semantic,
            AnalysisFeatures.HorizontalAsymptotes);
        AssertReplay<ImmutableArray<Asymptote>>(
            request,
            semantic,
            AnalysisFeatures.ObliqueAsymptotes);
        AssertReplay<ImmutableArray<MonotoneRegion>>(
            request,
            semantic,
            AnalysisFeatures.Monotonicity);
        AssertReplay<Periodicity>(request, semantic, AnalysisFeatures.Period);
    }

    private static void AssertReplay<T>(
        AnalysisRequest request,
        SemanticExpression semantic,
        AnalysisFeatures feature)
    {
        ProofOutcome<T> outcome = Analyze<T>(request, semantic, feature);
        Assert.IsType<UnaryCompositionProofCertificate>(outcome.Certificate);
        Assert.True(CertificateChecker.Check(request, semantic, outcome));
    }

    private static ProofOutcome<T> Analyze<T>(
        AnalysisRequest request,
        SemanticExpression semantic,
        AnalysisFeatures feature)
    {
        Assert.True(
            UnaryCompositionAnalyzer.TryAnalyze(
                request,
                semantic,
                feature,
                new ResourceBudget(),
                out ProofOutcome<T> outcome),
            $"{semantic.Value.Canonical}; unit={request.AngleUnit}; feature={feature}");
        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.NotNull(outcome.Value);
        return outcome;
    }

    private static (AnalysisRequest Request, SemanticExpression Semantic) Build(
        InputExpression input,
        AnalysisFeatures features,
        AngleUnit angleUnit)
    {
        AnalysisRequest request = new(
            input,
            features,
            angleUnit,
            "x",
            static () => true);
        SemanticExpression semantic = new SemanticGraphBuilder(
            new ResourceBudget()).Build(input);
        return (request, semantic);
    }

    private static InputExpression Variable() =>
        InputExpression.Variable("x", Source);

    private static InputExpression Number(int value) =>
        InputExpression.Number(new BigRational(value), Source);

    private static InputExpression Add(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Add, left, right, Source);

    private static InputExpression Multiply(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Multiply, left, right, Source);

    private static InputExpression Function(
        string name,
        params InputExpression[] arguments) =>
        InputExpression.Function(name, arguments.ToImmutableArray(), Source);
}
