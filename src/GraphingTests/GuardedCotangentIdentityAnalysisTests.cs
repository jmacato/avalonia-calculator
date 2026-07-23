using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class GuardedCotangentIdentityAnalysisTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Fact]
    public void PythagoreanIdentityOverTangentRetainsEveryOriginalHole()
    {
        InputExpression x = Variable();
        InputExpression input = Divide(Pythagorean(x), Tan(x));
        AnalysisRequest request = Request(input, AnalysisFeatures.All);
        SemanticExpression semantic = Build(input);

        var domain = Assert.IsType<PeriodicIntervalSet>(Prove<RealSet>(AnalysisFeatures.Domain));
        Assert.Equal("pi:1/2:0", ExactRealCanonical.Format(domain.Period));
        PeriodicInterval cell = Assert.Single(domain.Intervals);
        Assert.Equal("q:0", ExactRealCanonical.Format(cell.LowerOffset));
        Assert.Equal("pi:1/2:0", ExactRealCanonical.Format(cell.UpperOffset));
        Assert.False(cell.IncludesLower);
        Assert.False(cell.IncludesUpper);

        Assert.Equal(
            "union[interval[-inf,0,q:0,0],interval[q:0,0,+inf,0]]",
            Prove<RealSet>(AnalysisFeatures.Range).Canonical);
        Assert.Equal(FunctionParity.Odd, Prove<FunctionParity>(AnalysisFeatures.Parity));
        Assert.IsType<EmptySet>(Prove<RealSet>(AnalysisFeatures.Zeros));
        Assert.False(Prove<OptionalValue<ExactReal>>(AnalysisFeatures.YIntercept).HasValue);
        Assert.Empty(Prove<ImmutableArray<FeaturePoint>>(AnalysisFeatures.Minima));
        Assert.Empty(Prove<ImmutableArray<FeaturePoint>>(AnalysisFeatures.Maxima));
        Assert.Empty(Prove<ImmutableArray<FeaturePoint>>(AnalysisFeatures.InflectionPoints));
        Asymptote vertical = Assert.Single(
            Prove<ImmutableArray<Asymptote>>(AnalysisFeatures.VerticalAsymptotes));
        var poles = Assert.IsType<PeriodicReal>(vertical.Coordinate);
        Assert.Equal("q:0", ExactRealCanonical.Format(poles.Offset));
        Assert.Equal("pi:1:0", ExactRealCanonical.Format(poles.Period));
        Assert.Empty(Prove<ImmutableArray<Asymptote>>(AnalysisFeatures.HorizontalAsymptotes));
        Assert.Empty(Prove<ImmutableArray<Asymptote>>(AnalysisFeatures.ObliqueAsymptotes));
        MonotoneRegion monotone = Assert.Single(
            Prove<ImmutableArray<MonotoneRegion>>(AnalysisFeatures.Monotonicity));
        Assert.Equal(domain.Canonical, monotone.Region.Canonical);
        Assert.Equal(Monotonicity.Decreasing, monotone.Direction);
        Periodicity period = Prove<Periodicity>(AnalysisFeatures.Period);
        Assert.Equal(PeriodicityKind.PeriodicWithFundamentalPeriod, period.Kind);
        Assert.Equal("pi:1:0", ExactRealCanonical.Format(period.FundamentalPeriod!));
        return;

        T Prove<T>(AnalysisFeatures feature)
        {
            Assert.True(GuardedCotangentIdentityAnalyzer.TryAnalyze(
                request,
                semantic,
                feature,
                new ResourceBudget(),
                out ProofOutcome<T> outcome));
            var certificate = Assert.IsType<GuardedCotangentIdentityProofCertificate>(outcome.Certificate);
            Assert.True(GuardedCotangentIdentityCertificateChecker.Check(
                request,
                semantic,
                certificate,
                ClaimCanonical.ForObject(outcome.Value!),
                new ResourceBudget()));
            return outcome.Value!;
        }
    }

    [Fact]
    public void ProductionEnginePublishesOnlyCentrallyAcceptedGuardedCotangentProofs()
    {
        InputExpression x = Variable();
        InputExpression input = Divide(Pythagorean(x), Tan(x));
        AnalysisRequest request = Request(input, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        SemanticExpression semantic = Assert.IsType<SemanticExpression>(report.Expression);

        AssertGuardedCotangent(report.Domain);
        AssertGuardedCotangent(report.Range);
        AssertGuardedCotangent(report.Parity);
        AssertGuardedCotangent(report.Zeros);
        AssertGuardedCotangent(report.YIntercept);
        AssertGuardedCotangent(report.Minima);
        AssertGuardedCotangent(report.Maxima);
        AssertGuardedCotangent(report.InflectionPoints);
        AssertGuardedCotangent(report.VerticalAsymptotes);
        AssertGuardedCotangent(report.HorizontalAsymptotes);
        AssertGuardedCotangent(report.ObliqueAsymptotes);
        AssertGuardedCotangent(report.Monotonicity);
        AssertGuardedCotangent(report.Period);
        return;

        void AssertGuardedCotangent<T>(ProofOutcome<T> outcome)
        {
            Assert.Equal(ProofState.Proved, outcome.State);
            Assert.IsType<GuardedCotangentIdentityProofCertificate>(outcome.Certificate);
            Assert.True(CertificateChecker.Check(request, semantic, outcome));
        }
    }

    [Theory]
    [MemberData(nameof(ScaledTangentCases))]
    public void TangentScaleAndFrequencyOrientExactCells(
        object inputValue,
        string expectedDomainPeriod,
        string expectedFunctionPeriod,
        int expectedDirection)
    {
        var input = Assert.IsType<InputExpression>(inputValue);
        AnalysisRequest request = Request(input, AnalysisFeatures.All);
        SemanticExpression semantic = Build(input);

        Assert.True(GuardedCotangentIdentityAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Domain,
            new ResourceBudget(),
            out ProofOutcome<RealSet> domainOutcome));
        Assert.Equal(
            expectedDomainPeriod,
            ExactRealCanonical.Format(Assert.IsType<PeriodicIntervalSet>(domainOutcome.Value).Period));
        Assert.True(GuardedCotangentIdentityAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Period,
            new ResourceBudget(),
            out ProofOutcome<Periodicity> periodOutcome));
        Assert.Equal(
            expectedFunctionPeriod,
            ExactRealCanonical.Format(periodOutcome.Value!.FundamentalPeriod!));
        Assert.True(GuardedCotangentIdentityAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Monotonicity,
            new ResourceBudget(),
            out ProofOutcome<ImmutableArray<MonotoneRegion>> monotonicity));
        Assert.Equal(
            (Monotonicity)expectedDirection,
            Assert.Single(monotonicity.Value!).Direction);
    }

    [Fact]
    public void EquivalentIdentityOrderIsAcceptedButUnsafeVariantsFailClosed()
    {
        InputExpression x = Variable();
        InputExpression reordered = Divide(
            Add(Power(Cos(x), 2), Power(Sin(x), 2)),
            Tan(x));
        AnalysisRequest accepted = Request(reordered, AnalysisFeatures.Range);
        Assert.True(GuardedCotangentIdentityAnalyzer.TryAnalyze(
            accepted,
            Build(reordered),
            AnalysisFeatures.Range,
            new ResourceBudget(),
            out ProofOutcome<RealSet> _));

        InputExpression hole = Divide(Number(1), Subtract(x, Number(1)));
        InputExpression[] unsupported =
        [
            Divide(Add(Power(Sin(x), 2), Power(Cos(x), 3)), Tan(x)),
            Divide(Pythagorean(x), Tan(Add(x, Number(1)))),
            Divide(Pythagorean(x), Sin(x)),
            Divide(Add(Pythagorean(x), Multiply(Number(0), hole)), Tan(x)),
            Divide(Pythagorean(x), Add(Tan(x), Multiply(Number(0), hole)))
        ];
        foreach (InputExpression input in unsupported)
        {
            AnalysisRequest request = Request(input, AnalysisFeatures.Range);
            Assert.False(GuardedCotangentIdentityAnalyzer.TryAnalyze(
                request,
                Build(input),
                AnalysisFeatures.Range,
                new ResourceBudget(),
                out ProofOutcome<RealSet> _));
        }
    }

    [Fact]
    public void CertificateReplayRejectsEveryMaterialMutation()
    {
        InputExpression x = Variable();
        InputExpression input = Divide(Pythagorean(x), Tan(x));
        AnalysisRequest request = Request(input, AnalysisFeatures.Range);
        SemanticExpression semantic = Build(input);
        Assert.True(GuardedCotangentIdentityAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Range,
            new ResourceBudget(),
            out ProofOutcome<RealSet> outcome));
        var certificate = Assert.IsType<GuardedCotangentIdentityProofCertificate>(outcome.Certificate);
        string claim = ClaimCanonical.ForObject(outcome.Value!);

        Assert.True(CertificateChecker.Check(request, semantic, outcome));
        AssertRejected(certificate with { NumeratorCanonical = certificate.NumeratorCanonical + ":changed" });
        AssertRejected(certificate with { DenominatorCanonical = certificate.DenominatorCanonical + ":changed" });
        AssertRejected(certificate with { PatternCanonical = certificate.PatternCanonical + ":changed" });
        AssertRejected(certificate with { DefinednessCanonical = "true" });
        AssertRejected(certificate with { DomainCanonical = AllRealSet.Instance.Canonical });
        AssertRejected(certificate with { Rule = "untrusted" });
        AssertRejected(certificate with { Subject = certificate.Subject + ":changed" });
        AssertRejected(certificate with { ProvenFeature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { Claim = "empty" });
        return;

        void AssertRejected(GuardedCotangentIdentityProofCertificate changed)
        {
            Assert.False(GuardedCotangentIdentityCertificateChecker.Check(
                request,
                semantic,
                changed,
                claim,
                new ResourceBudget()));
            Assert.False(CertificateChecker.Check(
                request,
                semantic,
                ProofOutcome<RealSet>.Proved(outcome.Value!, changed)));
        }
    }

    [Fact]
    public void RevisionCancellationAndWorkBudgetFailClosed()
    {
        InputExpression x = Variable();
        InputExpression input = Divide(Pythagorean(x), Tan(x));
        AnalysisRequest request = Request(input, AnalysisFeatures.Range);
        SemanticExpression semantic = Build(input);

        var cancelled = new ResourceBudget(static () => false);
        Assert.Throws<AnalysisCancelledException>(() =>
            GuardedCotangentIdentityAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.Range,
                cancelled,
                out ProofOutcome<RealSet> _));

        var exhausted = new ResourceBudget();
        exhausted.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            GuardedCotangentIdentityAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.Range,
                exhausted,
                out ProofOutcome<RealSet> _));
    }

    public static IEnumerable<object[]> ScaledTangentCases()
    {
        InputExpression x = Variable();
        yield return
        [
            Divide(Pythagorean(x), Tan(Multiply(Number(2), x))),
            "pi:1/4:0",
            "pi:1/2:0",
            (int)Monotonicity.Decreasing
        ];
        yield return
        [
            Divide(Pythagorean(x), Negate(Tan(x))),
            "pi:1/2:0",
            "pi:1:0",
            (int)Monotonicity.Increasing
        ];
        yield return
        [
            Divide(Pythagorean(x), Multiply(Number(-3), Tan(Multiply(Number(2), x)))),
            "pi:1/4:0",
            "pi:1/2:0",
            (int)Monotonicity.Increasing
        ];
    }

    private static AnalysisRequest Request(
        InputExpression expression,
        AnalysisFeatures features)
    {
        return new AnalysisRequest(expression, features, AngleUnit.Radians, "x", static () => true);
    }

    private static SemanticExpression Build(InputExpression input)
    {
        return new SemanticGraphBuilder(new ResourceBudget()).Build(input);
    }

    private static InputExpression Pythagorean(InputExpression argument)
    {
        return Add(Power(Sin(argument), 2), Power(Cos(argument), 2));
    }

    private static InputExpression Variable()
    {
        return InputExpression.Variable("x", Source);
    }

    private static InputExpression Number(int value)
    {
        return InputExpression.Number(new BigRational(value), Source);
    }

    private static InputExpression Negate(InputExpression value)
    {
        return InputExpression.Unary(InputExpressionKind.Negate, value, Source);
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

    private static InputExpression Sin(InputExpression argument)
    {
        return Function("sin", argument);
    }

    private static InputExpression Cos(InputExpression argument)
    {
        return Function("cos", argument);
    }

    private static InputExpression Tan(InputExpression argument)
    {
        return Function("tan", argument);
    }

    private static InputExpression Function(string name, params InputExpression[] arguments)
    {
        return InputExpression.Function(name, arguments.ToImmutableArray(), Source);
    }
}
