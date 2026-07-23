using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class ZeroBaseTangentPowerAnalysisTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Fact]
    public void ZeroToTangentIsExactlyZeroOnPositiveTangentBranches()
    {
        InputExpression input = Power(Number(0), Tan(Variable()));
        AnalysisRequest request = Request(input, AnalysisFeatures.All);
        SemanticExpression semantic = Build(input);

        var domain = Assert.IsType<PeriodicIntervalSet>(
            Prove<RealSet>(AnalysisFeatures.Domain));
        Assert.Equal("pi:1:0", ExactRealCanonical.Format(domain.Period));
        PeriodicInterval interval = Assert.Single(domain.Intervals);
        Assert.Equal("q:0", ExactRealCanonical.Format(interval.LowerOffset));
        Assert.Equal("pi:1/2:0", ExactRealCanonical.Format(interval.UpperOffset));
        Assert.False(interval.IncludesLower);
        Assert.False(interval.IncludesUpper);

        Assert.Equal("points[q:0]", Prove<RealSet>(AnalysisFeatures.Range).Canonical);
        Assert.Equal(domain.Canonical, Prove<RealSet>(AnalysisFeatures.Zeros).Canonical);
        Assert.Equal(FunctionParity.Neither, Prove<FunctionParity>(AnalysisFeatures.Parity));
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
        Assert.Equal(domain.Canonical, monotone.Region.Canonical);
        Assert.Equal(Monotonicity.Constant, monotone.Direction);
        Periodicity period = Prove<Periodicity>(AnalysisFeatures.Period);
        Assert.Equal(PeriodicityKind.PeriodicWithFundamentalPeriod, period.Kind);
        Assert.Equal("pi:1:0", ExactRealCanonical.Format(period.FundamentalPeriod!));
        return;

        T Prove<T>(AnalysisFeatures feature)
        {
            Assert.True(ZeroBaseTangentPowerAnalyzer.TryAnalyze(
                request,
                semantic,
                feature,
                new ResourceBudget(),
                out ProofOutcome<T> outcome));
            var certificate = Assert.IsType<ZeroBaseTangentPowerProofCertificate>(outcome.Certificate);
            Assert.True(ZeroBaseTangentPowerCertificateChecker.Check(
                request,
                semantic,
                certificate,
                ClaimCanonical.ForObject(outcome.Value!),
                new ResourceBudget()));
            return outcome.Value!;
        }
    }

    [Fact]
    public void ProductionEnginePublishesOnlyCentrallyAcceptedTangentPowerProofs()
    {
        InputExpression input = Power(Number(0), Tan(Variable()));
        AnalysisRequest request = Request(input, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        SemanticExpression semantic = Assert.IsType<SemanticExpression>(report.Expression);

        AssertTangentPower(report.Domain);
        AssertTangentPower(report.Range);
        AssertTangentPower(report.Parity);
        AssertTangentPower(report.Zeros);
        AssertTangentPower(report.YIntercept);
        AssertTangentPower(report.Minima);
        AssertTangentPower(report.Maxima);
        AssertTangentPower(report.InflectionPoints);
        AssertTangentPower(report.VerticalAsymptotes);
        AssertTangentPower(report.HorizontalAsymptotes);
        AssertTangentPower(report.ObliqueAsymptotes);
        AssertTangentPower(report.Monotonicity);
        AssertTangentPower(report.Period);
        return;

        void AssertTangentPower<T>(ProofOutcome<T> outcome)
        {
            Assert.Equal(ProofState.Proved, outcome.State);
            Assert.IsType<ZeroBaseTangentPowerProofCertificate>(outcome.Certificate);
            Assert.True(CertificateChecker.Check(request, semantic, outcome));
        }
    }

    [Theory]
    [MemberData(nameof(CenteredTangentCases))]
    public void CenteredScaledTangentBranchesRespectSignAndFrequency(
        object inputValue,
        string expectedPeriod,
        string expectedLower,
        string expectedUpper)
    {
        var input = Assert.IsType<InputExpression>(inputValue);
        AnalysisRequest request = Request(input, AnalysisFeatures.Domain);
        SemanticExpression semantic = Build(input);
        Assert.True(ZeroBaseTangentPowerAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Domain,
            new ResourceBudget(),
            out ProofOutcome<RealSet> outcome));
        var domain = Assert.IsType<PeriodicIntervalSet>(outcome.Value);
        Assert.Equal(expectedPeriod, ExactRealCanonical.Format(domain.Period));
        PeriodicInterval interval = Assert.Single(domain.Intervals);
        Assert.Equal(expectedLower, ExactRealCanonical.Format(interval.LowerOffset));
        Assert.Equal(expectedUpper, ExactRealCanonical.Format(interval.UpperOffset));
        Assert.True(ZeroBaseTangentPowerCertificateChecker.Check(
            request,
            semantic,
            Assert.IsType<ZeroBaseTangentPowerProofCertificate>(outcome.Certificate),
            ClaimCanonical.ForObject(outcome.Value!),
            new ResourceBudget()));
    }

    [Theory]
    [InlineData((int)AngleUnit.Degrees, "q:180")]
    [InlineData((int)AngleUnit.Grads, "q:200")]
    public void CertificateReplayUsesTheRequestedAngleUnitExactly(
        int angleUnitValue,
        string expectedPeriod)
    {
        InputExpression input = Power(Number(0), Tan(Variable()));
        AnalysisRequest request = Request(
            input,
            AnalysisFeatures.All,
            (AngleUnit)angleUnitValue);
        SemanticExpression semantic = Build(input);

        Assert.True(ZeroBaseTangentPowerAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Period,
            new ResourceBudget(),
            out ProofOutcome<Periodicity> outcome));
        Assert.Equal(
            expectedPeriod,
            ExactRealCanonical.Format(outcome.Value!.FundamentalPeriod!));
        Assert.True(ZeroBaseTangentPowerCertificateChecker.Check(
            request,
            semantic,
            Assert.IsType<ZeroBaseTangentPowerProofCertificate>(outcome.Certificate),
            ClaimCanonical.ForObject(outcome.Value!),
            new ResourceBudget()));
    }

    [Fact]
    public void ShiftedNonTangentAndRetainedHoleExponentsFailClosed()
    {
        InputExpression x = Variable();
        InputExpression hole = Divide(Number(1), Subtract(x, Number(1)));
        InputExpression[] unsupported =
        [
            Power(Number(0), Add(Tan(x), Number(1))),
            Power(Number(0), Tan(Add(x, Number(1)))),
            Power(Number(0), Function("sin", x)),
            Power(Number(0), Add(Tan(x), Multiply(Number(0), hole))),
            Power(Multiply(Number(0), hole), Tan(x))
        ];

        foreach (InputExpression input in unsupported)
        {
            AnalysisRequest request = Request(input, AnalysisFeatures.Range);
            Assert.False(ZeroBaseTangentPowerAnalyzer.TryAnalyze(
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
        InputExpression input = Power(Number(0), Tan(Variable()));
        AnalysisRequest request = Request(input, AnalysisFeatures.Range);
        SemanticExpression semantic = Build(input);
        Assert.True(ZeroBaseTangentPowerAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Range,
            new ResourceBudget(),
            out ProofOutcome<RealSet> outcome));
        var certificate = Assert.IsType<ZeroBaseTangentPowerProofCertificate>(outcome.Certificate);
        string claim = ClaimCanonical.ForObject(outcome.Value!);

        Assert.True(CertificateChecker.Check(request, semantic, outcome));
        AssertRejected(certificate with { Feature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { ExponentCanonical = certificate.ExponentCanonical + ":changed" });
        AssertRejected(certificate with { PatternCanonical = certificate.PatternCanonical + ":changed" });
        AssertRejected(certificate with { DefinednessCanonical = "true" });
        AssertRejected(certificate with { DomainCanonical = AllRealSet.Instance.Canonical });
        AssertRejected(certificate with { Rule = "untrusted" });
        AssertRejected(certificate with { Subject = certificate.Subject + ":changed" });
        AssertRejected(certificate with { SubjectCanonical = certificate.Subject + ":changed" });
        AssertRejected(certificate with { ProvenFeature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { Claim = "empty" });
        AssertRejected(certificate with { ClaimCanonical = "empty" });
        Assert.False(ZeroBaseTangentPowerCertificateChecker.Check(
            request with { Features = AnalysisFeatures.Domain },
            semantic,
            certificate,
            claim,
            new ResourceBudget()));
        Assert.False(ZeroBaseTangentPowerCertificateChecker.Check(
            request with { Variable = "t" },
            semantic,
            certificate,
            claim,
            new ResourceBudget()));
        return;

        void AssertRejected(ZeroBaseTangentPowerProofCertificate changed)
        {
            Assert.False(ZeroBaseTangentPowerCertificateChecker.Check(
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
    public void CertificateReplayRejectsForgedPowerAndOperandConditions()
    {
        InputExpression input = Power(Number(0), Tan(Variable()));
        AnalysisRequest request = Request(input, AnalysisFeatures.Range);
        SemanticExpression semantic = Build(input);
        Assert.True(ZeroBaseTangentPowerAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Range,
            new ResourceBudget(),
            out ProofOutcome<RealSet> outcome));
        var certificate = Assert.IsType<ZeroBaseTangentPowerProofCertificate>(outcome.Certificate);
        string claim = ClaimCanonical.ForObject(outcome.Value!);

        AssertRejected(semantic with { ContinuousWhen = Formula.False }, certificate);
        AssertRejected(semantic with
        {
            RewriteHistory =
            [
                new RewriteStep(
                    "forged",
                    semantic.Value.Canonical,
                    semantic.Value.Canonical,
                    Formula.True)
            ]
        }, certificate);

        SemanticExpression cleanBasis = semantic.SourceOperands[0];
        SemanticExpression exponent = semantic.SourceOperands[1];
        SemanticExpression holedBasis = cleanBasis with
        {
            DefinedWhen = Formula.False
        };
        Formula holedDefined = Formula.And(
            holedBasis.DefinedWhen,
            exponent.DefinedWhen,
            Formula.Compare(
                exponent.Value,
                Comparison.Greater,
                holedBasis.Value));
        SemanticExpression holed = semantic with
        {
            SourceOperands = [holedBasis, exponent],
            DefinedWhen = holedDefined
        };
        AssertRejected(
            holed,
            certificate with { DefinednessCanonical = holedDefined.Canonical });

        Formula duplicatePole = new JunctionFormula(
            true,
            [exponent.DefinedWhen, exponent.DefinedWhen]);
        SemanticExpression duplicatedExponent = exponent with
        {
            DefinedWhen = duplicatePole,
            ContinuousWhen = duplicatePole,
            DifferentiableWhen = duplicatePole
        };
        Formula duplicateDefined = Formula.And(
            cleanBasis.DefinedWhen,
            duplicatedExponent.DefinedWhen,
            Formula.Compare(
                duplicatedExponent.Value,
                Comparison.Greater,
                cleanBasis.Value));
        SemanticExpression duplicated = semantic with
        {
            SourceOperands = [cleanBasis, duplicatedExponent],
            DefinedWhen = duplicateDefined
        };
        AssertRejected(
            duplicated,
            certificate with { DefinednessCanonical = duplicateDefined.Canonical });
        return;

        void AssertRejected(
            SemanticExpression changed,
            ZeroBaseTangentPowerProofCertificate changedCertificate) =>
            Assert.False(ZeroBaseTangentPowerCertificateChecker.Check(
                request,
                changed,
                changedCertificate,
                claim,
                new ResourceBudget()));
    }

    [Fact]
    public void RevisionCancellationAndWorkBudgetFailClosed()
    {
        InputExpression input = Power(Number(0), Tan(Variable()));
        AnalysisRequest request = Request(input, AnalysisFeatures.Range);
        SemanticExpression semantic = Build(input);

        var cancelled = new ResourceBudget(static () => false);
        Assert.Throws<AnalysisCancelledException>(() =>
            ZeroBaseTangentPowerAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.Range,
                cancelled,
                out ProofOutcome<RealSet> _));

        var exhausted = new ResourceBudget();
        exhausted.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            ZeroBaseTangentPowerAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.Range,
                exhausted,
                out ProofOutcome<RealSet> _));

        Assert.True(ZeroBaseTangentPowerAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Range,
            new ResourceBudget(),
            out ProofOutcome<RealSet> outcome));
        var certificate = Assert.IsType<ZeroBaseTangentPowerProofCertificate>(outcome.Certificate);
        string claim = ClaimCanonical.ForObject(outcome.Value!);
        Assert.Throws<AnalysisCancelledException>(() =>
            ZeroBaseTangentPowerCertificateChecker.Check(
                request,
                semantic,
                certificate,
                claim,
                new ResourceBudget(static () => false)));
        var exhaustedChecker = new ResourceBudget();
        exhaustedChecker.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            ZeroBaseTangentPowerCertificateChecker.Check(
                request,
                semantic,
                certificate,
                claim,
                exhaustedChecker));
    }

    public static IEnumerable<object[]> CenteredTangentCases()
    {
        yield return
        [
            Power(Number(0), Tan(Multiply(Number(2), Variable()))),
            "pi:1/2:0",
            "q:0",
            "pi:1/4:0"
        ];
        yield return
        [
            Power(Number(0), Negate(Tan(Multiply(Number(2), Variable())))),
            "pi:1/2:0",
            "pi:-1/4:0",
            "q:0"
        ];
        yield return
        [
            Power(Number(0), Multiply(Number(-3), Tan(Variable()))),
            "pi:1:0",
            "pi:-1/2:0",
            "q:0"
        ];
        yield return
        [
            Power(Number(0), Tan(Multiply(Number(-2), Variable()))),
            "pi:1/2:0",
            "pi:-1/4:0",
            "q:0"
        ];
    }

    private static AnalysisRequest Request(
        InputExpression expression,
        AnalysisFeatures features,
        AngleUnit angleUnit = AngleUnit.Radians)
    {
        return new AnalysisRequest(expression, features, angleUnit, "x", static () => true);
    }

    private static SemanticExpression Build(InputExpression input)
    {
        return new SemanticGraphBuilder(new ResourceBudget()).Build(input);
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

    private static InputExpression Power(InputExpression basis, InputExpression exponent)
    {
        return InputExpression.Binary(InputExpressionKind.Power, basis, exponent, Source);
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
