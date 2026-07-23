using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class ZeroBaseAffinePowerAnalysisTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Fact]
    public void ZeroToVariableIsExactlyZeroOnItsStrictPositiveDomain()
    {
        InputExpression input = Power(Number(0), Variable());
        AnalysisRequest request = Request(input, AnalysisFeatures.All);
        SemanticExpression semantic = Build(input);

        Assert.Equal("interval[q:0,0,+inf,0]", Prove<RealSet>(AnalysisFeatures.Domain).Canonical);
        Assert.Equal("points[q:0]", Prove<RealSet>(AnalysisFeatures.Range).Canonical);
        Assert.Equal(FunctionParity.Neither, Prove<FunctionParity>(AnalysisFeatures.Parity));
        Assert.Equal("interval[q:0,0,+inf,0]", Prove<RealSet>(AnalysisFeatures.Zeros).Canonical);
        Assert.False(Prove<OptionalValue<ExactReal>>(AnalysisFeatures.YIntercept).HasValue);
        Assert.Empty(Prove<ImmutableArray<FeaturePoint>>(AnalysisFeatures.Minima));
        Assert.Empty(Prove<ImmutableArray<FeaturePoint>>(AnalysisFeatures.Maxima));
        Assert.Empty(Prove<ImmutableArray<FeaturePoint>>(AnalysisFeatures.InflectionPoints));
        Assert.Empty(Prove<ImmutableArray<Asymptote>>(AnalysisFeatures.VerticalAsymptotes));
        Assert.Empty(Prove<ImmutableArray<Asymptote>>(AnalysisFeatures.ObliqueAsymptotes));
        Asymptote horizontal = Assert.Single(
            Prove<ImmutableArray<Asymptote>>(AnalysisFeatures.HorizontalAsymptotes));
        Assert.Equal("q:0", ExactRealCanonical.Format(
            Assert.IsType<SingletonReal>(horizontal.Coordinate).Value));
        MonotoneRegion monotone = Assert.Single(
            Prove<ImmutableArray<MonotoneRegion>>(AnalysisFeatures.Monotonicity));
        Assert.Equal("interval[q:0,0,+inf,0]", monotone.Region.Canonical);
        Assert.Equal(Monotonicity.Constant, monotone.Direction);
        Assert.Equal(
            PeriodicityKind.NotPeriodic,
            Prove<Periodicity>(AnalysisFeatures.Period).Kind);
        return;

        T Prove<T>(AnalysisFeatures feature)
        {
            Assert.True(ZeroBaseAffinePowerAnalyzer.TryAnalyze(
                request,
                semantic,
                feature,
                new ResourceBudget(),
                out ProofOutcome<T> outcome));
            var certificate = Assert.IsType<ZeroBaseAffinePowerProofCertificate>(outcome.Certificate);
            Assert.True(ZeroBaseAffinePowerCertificateChecker.Check(
                request,
                semantic,
                certificate,
                ClaimCanonical.ForObject(outcome.Value!),
                new ResourceBudget()));
            return outcome.Value!;
        }
    }

    [Theory]
    [MemberData(nameof(AffineExponents))]
    public void AffineExponentOrientationAndInterceptAreExact(
        object expressionValue,
        string expectedDomain,
        bool hasYIntercept)
    {
        var input = Assert.IsType<InputExpression>(expressionValue);
        AnalysisRequest request = Request(input, AnalysisFeatures.All);
        SemanticExpression semantic = Build(input);

        Assert.True(ZeroBaseAffinePowerAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Domain,
            new ResourceBudget(),
            out ProofOutcome<RealSet> domain));
        Assert.Equal(expectedDomain, domain.Value!.Canonical);
        Assert.True(ZeroBaseAffinePowerAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.YIntercept,
            new ResourceBudget(),
            out ProofOutcome<OptionalValue<ExactReal>> intercept));
        Assert.Equal(hasYIntercept, intercept.Value!.HasValue);
        if (hasYIntercept)
        {
            Assert.Equal("q:0", ExactRealCanonical.Format(intercept.Value.Value!));
        }
    }

    [Fact]
    public void ProductionEnginePublishesOnlyCentrallyCheckedZeroBaseClaims()
    {
        InputExpression input = Power(Number(0), Add(
            Multiply(Number(2), Variable()),
            Number(-4)));
        AnalysisRequest request = Request(input, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        Assert.Equal(ProofState.Proved, report.Domain.State);
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Domain));
        AssertZeroBase(report.Range);
        AssertZeroBase(report.Parity);
        AssertZeroBase(report.Zeros);
        AssertZeroBase(report.YIntercept);
        AssertZeroBase(report.Minima);
        AssertZeroBase(report.Maxima);
        AssertZeroBase(report.InflectionPoints);
        AssertZeroBase(report.VerticalAsymptotes);
        AssertZeroBase(report.HorizontalAsymptotes);
        AssertZeroBase(report.ObliqueAsymptotes);
        AssertZeroBase(report.Monotonicity);
        AssertZeroBase(report.Period);
        return;

        void AssertZeroBase<T>(ProofOutcome<T> outcome)
        {
            Assert.Equal(ProofState.Proved, outcome.State);
            Assert.IsType<ZeroBaseAffinePowerProofCertificate>(outcome.Certificate);
            Assert.True(CertificateChecker.Check(request, report.Expression!, outcome));
        }
    }

    [Fact]
    public void SymbolicAffineExponentPublishesEveryClaimAfterIndependentReplay()
    {
        InputExpression input = Power(
            Number(0),
            Subtract(Multiply(Symbol("pi"), Variable()), Number(1)));
        AnalysisRequest request = Request(input, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        SemanticExpression semantic = Assert.IsType<SemanticExpression>(report.Expression);
        const string expectedDomain =
            "interval[fn:divide(q:1,pi:1:0),0,+inf,0]";

        Assert.Equal(expectedDomain, Proved(report.Domain).Canonical);
        Assert.Equal("points[q:0]", Proved(report.Range).Canonical);
        Assert.Equal(FunctionParity.Neither, Proved(report.Parity));
        Assert.Equal(expectedDomain, Proved(report.Zeros).Canonical);
        Assert.False(Proved(report.YIntercept).HasValue);
        Assert.Empty(Proved(report.Minima));
        Assert.Empty(Proved(report.Maxima));
        Assert.Empty(Proved(report.InflectionPoints));
        Assert.Empty(Proved(report.VerticalAsymptotes));
        Assert.Empty(Proved(report.ObliqueAsymptotes));

        Asymptote horizontal = Assert.Single(Proved(report.HorizontalAsymptotes));
        Assert.Equal(
            "q:0",
            ExactRealCanonical.Format(
                Assert.IsType<SingletonReal>(horizontal.Coordinate).Value));

        MonotoneRegion monotone = Assert.Single(Proved(report.Monotonicity));
        Assert.Equal(expectedDomain, monotone.Region.Canonical);
        Assert.Equal(Monotonicity.Constant, monotone.Direction);
        Assert.Equal(PeriodicityKind.NotPeriodic, Proved(report.Period).Kind);
        return;

        T Proved<T>(ProofOutcome<T> outcome)
        {
            Assert.Equal(ProofState.Proved, outcome.State);
            Assert.IsType<ZeroBaseAffinePowerProofCertificate>(outcome.Certificate);
            Assert.True(CertificateChecker.Check(request, semantic, outcome));
            return outcome.Value!;
        }
    }

    [Fact]
    public void ExactOrderOfPiMinusEulerDeterminesTheHalfLineOrientation()
    {
        InputExpression slope = Subtract(Symbol("pi"), Symbol("e"));
        InputExpression input = Power(
            Number(0),
            Subtract(Multiply(slope, Variable()), Number(1)));
        AnalysisRequest request = Request(input, AnalysisFeatures.Domain);
        SemanticExpression semantic = Build(input);

        Assert.True(ZeroBaseAffinePowerAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Domain,
            new ResourceBudget(),
            out ProofOutcome<RealSet> outcome));
        var certificate = Assert.IsType<ZeroBaseAffinePowerProofCertificate>(
            outcome.Certificate);
        Assert.EndsWith(":sign=1", certificate.SlopeCanonical, StringComparison.Ordinal);
        var domain = Assert.IsType<IntervalSet>(outcome.Value);
        Assert.Equal(BoundKind.Finite, domain.Lower.Kind);
        Assert.Equal(BoundKind.PositiveInfinity, domain.Upper.Kind);
        Assert.False(domain.IncludesLower);
        Assert.True(ZeroBaseAffinePowerCertificateChecker.Check(
            request,
            semantic,
            certificate,
            ClaimCanonical.ForObject(outcome.Value!),
            new ResourceBudget()));
    }

    [Fact]
    public void RetainedBaseOrExponentHolesAreNeverErased()
    {
        InputExpression x = Variable();
        InputExpression hole = Divide(Number(1), Subtract(x, Number(1)));
        InputExpression guardedZero = Multiply(Number(0), hole);
        InputExpression guardedExponent = Add(x, Multiply(Number(0), hole));
        InputExpression guardedSymbolicExponent = Add(
            Multiply(Symbol("pi"), x),
            Multiply(Number(0), hole));
        InputExpression[] inputs =
        [
            Power(guardedZero, x),
            Power(Number(0), guardedExponent),
            Power(Number(0), guardedSymbolicExponent),
            Power(Number(0), Number(2)),
            Power(Number(1), x),
            Power(Number(0), Power(x, Number(2)))
        ];

        foreach (InputExpression input in inputs)
        {
            AnalysisRequest request = Request(input, AnalysisFeatures.Range);
            Assert.False(ZeroBaseAffinePowerAnalyzer.TryAnalyze(
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
        InputExpression input = Power(
            Number(0),
            Subtract(Multiply(Symbol("pi"), Variable()), Number(1)));
        AnalysisRequest request = Request(input, AnalysisFeatures.Range);
        SemanticExpression semantic = Build(input);
        Assert.True(ZeroBaseAffinePowerAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Range,
            new ResourceBudget(),
            out ProofOutcome<RealSet> outcome));
        var certificate = Assert.IsType<ZeroBaseAffinePowerProofCertificate>(outcome.Certificate);
        string claim = ClaimCanonical.ForObject(outcome.Value!);

        Assert.True(CertificateChecker.Check(request, semantic, outcome));

        AssertRejected(certificate with { ExponentCanonical = certificate.ExponentCanonical + ":changed" });
        AssertRejected(certificate with { SlopeCanonical = "q:3:sign=1" });
        AssertRejected(certificate with { InterceptCanonical = "q:-3:sign=-1" });
        AssertRejected(certificate with { DefinednessCanonical = "true" });
        AssertRejected(certificate with { DomainCanonical = AllRealSet.Instance.Canonical });
        AssertRejected(certificate with { Rule = "untrusted" });
        AssertRejected(certificate with { Subject = certificate.Subject + ":changed" });
        AssertRejected(certificate with { ProvenFeature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { Claim = "empty" });
        Assert.False(CertificateChecker.Check(
            request,
            semantic,
            ProofOutcome<RealSet>.Proved(
                outcome.Value!,
                certificate with { Rule = "untrusted" })));
        return;

        void AssertRejected(ZeroBaseAffinePowerProofCertificate changed) =>
            Assert.False(ZeroBaseAffinePowerCertificateChecker.Check(
                request,
                semantic,
                changed,
                claim,
                new ResourceBudget()));
    }

    [Fact]
    public void RevisionCancellationAndExhaustedWorkBudgetFailClosed()
    {
        InputExpression[] inputs =
        [
            Power(Number(0), Variable()),
            Power(
                Number(0),
                Subtract(Multiply(Symbol("pi"), Variable()), Number(1)))
        ];

        foreach (InputExpression input in inputs)
        {
            AnalysisRequest request = Request(input, AnalysisFeatures.Range);
            SemanticExpression semantic = Build(input);
            Assert.True(ZeroBaseAffinePowerAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.Range,
                new ResourceBudget(),
                out ProofOutcome<RealSet> outcome));
            var certificate = Assert.IsType<ZeroBaseAffinePowerProofCertificate>(
                outcome.Certificate);
            string claim = ClaimCanonical.ForObject(outcome.Value!);

            var cancelledAnalysis = new ResourceBudget(static () => false);
            Assert.Throws<AnalysisCancelledException>(() =>
                ZeroBaseAffinePowerAnalyzer.TryAnalyze(
                    request,
                    semantic,
                    AnalysisFeatures.Range,
                    cancelledAnalysis,
                    out ProofOutcome<RealSet> _));

            var cancelledReplay = new ResourceBudget(static () => false);
            Assert.Throws<AnalysisCancelledException>(() =>
                ZeroBaseAffinePowerCertificateChecker.Check(
                    request,
                    semantic,
                    certificate,
                    claim,
                    cancelledReplay));

            var exhaustedAnalysis = new ResourceBudget();
            exhaustedAnalysis.Charge(AnalysisLimits.WorkUnits);
            Assert.Throws<BudgetExceededException>(() =>
                ZeroBaseAffinePowerAnalyzer.TryAnalyze(
                    request,
                    semantic,
                    AnalysisFeatures.Range,
                    exhaustedAnalysis,
                    out ProofOutcome<RealSet> _));

            var exhaustedReplay = new ResourceBudget();
            exhaustedReplay.Charge(AnalysisLimits.WorkUnits);
            Assert.Throws<BudgetExceededException>(() =>
                ZeroBaseAffinePowerCertificateChecker.Check(
                    request,
                    semantic,
                    certificate,
                    claim,
                    exhaustedReplay));
        }
    }

    public static IEnumerable<object[]> AffineExponents()
    {
        yield return
        [
            Power(Number(0), Add(Multiply(Number(2), Variable()), Number(-4))),
            "interval[q:2,0,+inf,0]",
            false
        ];
        yield return
        [
            Power(Number(0), Add(Multiply(Number(-3), Variable()), Number(6))),
            "interval[-inf,0,q:2,0]",
            true
        ];
        yield return
        [
            Power(Number(0), Add(Multiply(Number(-1), Variable()), Number(-2))),
            "interval[-inf,0,q:-2,0]",
            false
        ];
        yield return
        [
            Power(
                Number(0),
                Subtract(Multiply(Symbol("pi"), Variable()), Number(1))),
            "interval[fn:divide(q:1,pi:1:0),0,+inf,0]",
            false
        ];
        yield return
        [
            Power(
                Number(0),
                Add(Multiply(Number(-1), Multiply(Symbol("pi"), Variable())), Number(1))),
            "interval[-inf,0,fn:divide(q:1,pi:1:0),0]",
            true
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

    private static InputExpression Variable()
    {
        return InputExpression.Variable("x", Source);
    }

    private static InputExpression Symbol(string name)
    {
        return InputExpression.Variable(name, Source);
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

    private static InputExpression Power(InputExpression basis, InputExpression exponent)
    {
        return InputExpression.Binary(InputExpressionKind.Power, basis, exponent, Source);
    }
}
