using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class SingleHoleSineAnalysisTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Fact]
    public void SineTimesSelfDivisionRetainsTheOriginHoleInEveryFeature()
    {
        InputExpression x = Variable();
        InputExpression input = Multiply(Sin(x), Divide(x, x));
        AnalysisRequest request = Request(input, AnalysisFeatures.All);
        SemanticExpression semantic = Build(input);

        Assert.Equal(
            "difference[reals,points[q:0]]",
            Prove<RealSet>(AnalysisFeatures.Domain).Canonical);
        Assert.Equal(
            "interval[q:-1,1,q:1,1]",
            Prove<RealSet>(AnalysisFeatures.Range).Canonical);
        Assert.Equal(FunctionParity.Odd, Prove<FunctionParity>(AnalysisFeatures.Parity));
        var zeros = Assert.IsType<PeriodicPointSet>(Prove<RealSet>(AnalysisFeatures.Zeros));
        Assert.Equal("q:0", ExactRealCanonical.Format(zeros.Offset));
        Assert.Equal("pi:1:0", ExactRealCanonical.Format(zeros.Period));
        Assert.Equal(Comparison.NotEqual, zeros.Constraint.Comparison);
        Assert.Equal(0, zeros.Constraint.Bound.Value);
        Assert.False(Prove<OptionalValue<ExactReal>>(AnalysisFeatures.YIntercept).HasValue);

        AssertPoint(
            Assert.Single(Prove<ImmutableArray<FeaturePoint>>(AnalysisFeatures.Minima)),
            "pi:3/2:0",
            "pi:2:0",
            "q:-1");
        AssertPoint(
            Assert.Single(Prove<ImmutableArray<FeaturePoint>>(AnalysisFeatures.Maxima)),
            "pi:1/2:0",
            "pi:2:0",
            "q:1");
        var inflection = Assert.IsType<ConstantYFeaturePoint>(
            Assert.Single(Prove<ImmutableArray<FeaturePoint>>(AnalysisFeatures.InflectionPoints)));
        var inflectionXs = Assert.IsType<PeriodicReal>(inflection.X);
        Assert.Equal("q:0", ExactRealCanonical.Format(inflectionXs.Offset));
        Assert.Equal("pi:1:0", ExactRealCanonical.Format(inflectionXs.Period));
        Assert.Equal(Comparison.NotEqual, inflectionXs.Constraint.Comparison);
        Assert.Equal("q:0", ExactRealCanonical.Format(inflection.Y));

        Assert.Empty(Prove<ImmutableArray<Asymptote>>(AnalysisFeatures.VerticalAsymptotes));
        Assert.Empty(Prove<ImmutableArray<Asymptote>>(AnalysisFeatures.HorizontalAsymptotes));
        Assert.Empty(Prove<ImmutableArray<Asymptote>>(AnalysisFeatures.ObliqueAsymptotes));
        ImmutableArray<MonotoneRegion> monotonicity =
            Prove<ImmutableArray<MonotoneRegion>>(AnalysisFeatures.Monotonicity);
        Assert.Equal(4, monotonicity.Length);
        Assert.Equal(3, monotonicity.Count(static region =>
            region.Direction == Monotonicity.Increasing));
        Assert.Single(monotonicity.Where(static region =>
            region.Direction == Monotonicity.Decreasing));
        Periodicity period = Prove<Periodicity>(AnalysisFeatures.Period);
        Assert.Equal(PeriodicityKind.NotPeriodic, period.Kind);
        Assert.Null(period.FundamentalPeriod);
        return;

        T Prove<T>(AnalysisFeatures feature)
        {
            Assert.True(SingleHoleSineAnalyzer.TryAnalyze(
                request,
                semantic,
                feature,
                new ResourceBudget(),
                out ProofOutcome<T> outcome));
            var certificate = Assert.IsType<SingleHoleSineProofCertificate>(outcome.Certificate);
            Assert.True(SingleHoleSineCertificateChecker.Check(
                request,
                semantic,
                certificate,
                ClaimCanonical.ForObject(outcome.Value!),
                new ResourceBudget()));
            return outcome.Value!;
        }
    }

    [Fact]
    public void ProductionEnginePublishesOnlyCentrallyAcceptedSingleHoleSineProofs()
    {
        InputExpression x = Variable();
        InputExpression input = Multiply(Sin(x), Divide(x, x));
        AnalysisRequest request = Request(input, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        SemanticExpression semantic = Assert.IsType<SemanticExpression>(report.Expression);

        Assert.Equal(ProofState.Proved, report.Domain.State);
        Assert.IsType<DomainProofCertificate>(report.Domain.Certificate);
        Assert.True(CertificateChecker.Check(request, semantic, report.Domain));

        AssertSingleHoleSine(report.Range);
        AssertSingleHoleSine(report.Parity);
        AssertSingleHoleSine(report.Zeros);
        AssertSingleHoleSine(report.YIntercept);
        AssertSingleHoleSine(report.Minima);
        AssertSingleHoleSine(report.Maxima);
        AssertSingleHoleSine(report.InflectionPoints);
        AssertSingleHoleSine(report.VerticalAsymptotes);
        AssertSingleHoleSine(report.HorizontalAsymptotes);
        AssertSingleHoleSine(report.ObliqueAsymptotes);
        AssertSingleHoleSine(report.Monotonicity);
        AssertSingleHoleSine(report.Period);
        return;

        void AssertSingleHoleSine<T>(ProofOutcome<T> outcome)
        {
            Assert.Equal(ProofState.Proved, outcome.State);
            Assert.IsType<SingleHoleSineProofCertificate>(outcome.Certificate);
            Assert.True(CertificateChecker.Check(request, semantic, outcome));
        }
    }

    [Theory]
    [MemberData(nameof(AliasCases))]
    public void OperandOrderFrequencyAndReflectionRemainExact(
        object inputValue,
        string expectedRange,
        int expectedAroundZeroDirection,
        string expectedZeroStep)
    {
        var input = Assert.IsType<InputExpression>(inputValue);
        AnalysisRequest request = Request(input, AnalysisFeatures.All);
        SemanticExpression semantic = Build(input);

        Assert.True(SingleHoleSineAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Range,
            new ResourceBudget(),
            out ProofOutcome<RealSet> range));
        Assert.Equal(expectedRange, range.Value!.Canonical);
        Assert.True(SingleHoleSineAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Zeros,
            new ResourceBudget(),
            out ProofOutcome<RealSet> zeros));
        Assert.Equal(
            expectedZeroStep,
            ExactRealCanonical.Format(Assert.IsType<PeriodicPointSet>(zeros.Value).Period));
        Assert.True(SingleHoleSineCertificateChecker.Check(
            request,
            semantic,
            Assert.IsType<SingleHoleSineProofCertificate>(zeros.Certificate),
            ClaimCanonical.ForObject(zeros.Value!),
            new ResourceBudget()));
        Assert.True(SingleHoleSineAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Monotonicity,
            new ResourceBudget(),
            out ProofOutcome<ImmutableArray<MonotoneRegion>> monotonicity));
        Assert.Equal(
            (Monotonicity)expectedAroundZeroDirection,
            monotonicity.Value![0].Direction);
    }

    [Fact]
    public void OtherHolesPhasesAndNonidentitiesFailClosed()
    {
        InputExpression x = Variable();
        InputExpression xMinusOne = Subtract(x, Number(1));
        InputExpression[] unsupported =
        [
            Multiply(Sin(x), Divide(xMinusOne, xMinusOne)),
            Multiply(Sin(Add(x, Number(1))), Divide(x, x)),
            Multiply(Add(Sin(x), Number(1)), Divide(x, x)),
            Multiply(Sin(x), Divide(Power(x, 2), Power(x, 2))),
            Add(Multiply(Sin(x), Divide(x, x)), Multiply(Number(0), Divide(Number(1), xMinusOne)))
        ];

        foreach (InputExpression input in unsupported)
        {
            AnalysisRequest request = Request(input, AnalysisFeatures.Range);
            Assert.False(SingleHoleSineAnalyzer.TryAnalyze(
                request,
                Build(input),
                AnalysisFeatures.Range,
                new ResourceBudget(),
                out ProofOutcome<RealSet> _));
        }
    }

    [Theory]
    [InlineData((int)AngleUnit.Degrees, "q:180", "q:360")]
    [InlineData((int)AngleUnit.Grads, "q:200", "q:400")]
    public void NonRadianAngleUnitsRetainTheSameHoleAndUseExactUnitSteps(
        int angleUnitValue,
        string expectedZeroStep,
        string expectedExtremaStep)
    {
        var angleUnit = (AngleUnit)angleUnitValue;
        InputExpression x = Variable();
        InputExpression input = Multiply(Sin(x), Divide(x, x));
        AnalysisRequest request = Request(input, AnalysisFeatures.All, angleUnit);
        SemanticExpression semantic = Build(input);

        Assert.True(SingleHoleSineAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Zeros,
            new ResourceBudget(),
            out ProofOutcome<RealSet> zeros));
        Assert.Equal(
            expectedZeroStep,
            ExactRealCanonical.Format(Assert.IsType<PeriodicPointSet>(zeros.Value).Period));

        Assert.True(SingleHoleSineAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Maxima,
            new ResourceBudget(),
            out ProofOutcome<ImmutableArray<FeaturePoint>> maxima));
        var maximum = Assert.IsType<ConstantYFeaturePoint>(Assert.Single(maxima.Value!));
        Assert.Equal(
            expectedExtremaStep,
            ExactRealCanonical.Format(Assert.IsType<PeriodicReal>(maximum.X).Period));
        Assert.True(SingleHoleSineCertificateChecker.Check(
            request,
            semantic,
            Assert.IsType<SingleHoleSineProofCertificate>(maxima.Certificate),
            ClaimCanonical.ForObject(maxima.Value!),
            new ResourceBudget()));
        Assert.Equal(
            "difference[reals,points[q:0]]",
            SingleHoleSineContext.TryCreate(
                semantic,
                request.Variable,
                request.AngleUnit,
                new ResourceBudget(),
                out SingleHoleSineContext context)
                ? context.Domain.Canonical
                : string.Empty);
    }

    [Fact]
    public void CertificateReplayRejectsEveryMaterialMutation()
    {
        InputExpression x = Variable();
        InputExpression input = Multiply(Sin(x), Divide(x, x));
        AnalysisRequest request = Request(input, AnalysisFeatures.Range);
        SemanticExpression semantic = Build(input);
        Assert.True(SingleHoleSineAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Range,
            new ResourceBudget(),
            out ProofOutcome<RealSet> outcome));
        var certificate = Assert.IsType<SingleHoleSineProofCertificate>(outcome.Certificate);
        string claim = ClaimCanonical.ForObject(outcome.Value!);

        Assert.True(CertificateChecker.Check(request, semantic, outcome));
        AssertRejected(certificate with { Feature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { SineCanonical = certificate.SineCanonical + ":changed" });
        AssertRejected(certificate with { GuardCanonical = certificate.GuardCanonical + ":changed" });
        AssertRejected(certificate with { PatternCanonical = certificate.PatternCanonical + ":changed" });
        AssertRejected(certificate with { DefinednessCanonical = "true" });
        AssertRejected(certificate with { DomainCanonical = AllRealSet.Instance.Canonical });
        AssertRejected(certificate with { Rule = "untrusted" });
        AssertRejected(certificate with { Subject = certificate.Subject + ":changed" });
        AssertRejected(certificate with { SubjectCanonical = certificate.Subject + ":changed" });
        AssertRejected(certificate with { ProvenFeature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { Claim = "empty" });
        AssertRejected(certificate with { ClaimCanonical = "empty" });
        Assert.False(SingleHoleSineCertificateChecker.Check(
            request with { Features = AnalysisFeatures.Domain },
            semantic,
            certificate,
            claim,
            new ResourceBudget()));
        Assert.False(SingleHoleSineCertificateChecker.Check(
            request with { Variable = "t" },
            semantic,
            certificate,
            claim,
            new ResourceBudget()));
        return;

        void AssertRejected(SingleHoleSineProofCertificate changed)
        {
            Assert.False(SingleHoleSineCertificateChecker.Check(
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
    public void CertificateReplayRejectsForgedSourceRewritesAndRegularity()
    {
        InputExpression x = Variable();
        InputExpression input = Multiply(Sin(x), Divide(x, x));
        AnalysisRequest request = Request(input, AnalysisFeatures.Range);
        SemanticExpression semantic = Build(input);
        Assert.True(SingleHoleSineAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Range,
            new ResourceBudget(),
            out ProofOutcome<RealSet> outcome));
        var certificate = Assert.IsType<SingleHoleSineProofCertificate>(outcome.Certificate);
        string claim = ClaimCanonical.ForObject(outcome.Value!);

        AssertRejected(semantic with { RewriteHistory = [] });
        AssertRejected(semantic with { ContinuousWhen = Formula.False });
        AssertRejected(semantic with
        {
            RewriteHistory =
            [semantic.RewriteHistory[0] with { Guard = Formula.False }]
        });

        SemanticExpression first = semantic.SourceOperands[0];
        SemanticExpression second = semantic.SourceOperands[1];
        AssertRejected(semantic with { SourceOperands = [second, first] });
        SemanticExpression guard = first.Value is
        {
            Kind: ValueKind.Constant,
            Constant.IsOne: true
        }
            ? first
            : second;
        SemanticExpression changedGuard = guard with
        {
            ContinuousWhen = Formula.False
        };
        AssertRejected(ReferenceEquals(guard, first)
            ? semantic with { SourceOperands = [changedGuard, second] }
            : semantic with { SourceOperands = [first, changedGuard] });
        return;

        void AssertRejected(SemanticExpression changed) =>
            Assert.False(SingleHoleSineCertificateChecker.Check(
                request,
                changed,
                certificate,
                claim,
                new ResourceBudget()));
    }

    [Fact]
    public void RevisionCancellationAndWorkBudgetFailClosed()
    {
        InputExpression x = Variable();
        InputExpression input = Multiply(Sin(x), Divide(x, x));
        AnalysisRequest request = Request(input, AnalysisFeatures.Range);
        SemanticExpression semantic = Build(input);

        var cancelled = new ResourceBudget(static () => false);
        Assert.Throws<AnalysisCancelledException>(() =>
            SingleHoleSineAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.Range,
                cancelled,
                out ProofOutcome<RealSet> _));

        var exhausted = new ResourceBudget();
        exhausted.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            SingleHoleSineAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.Range,
                exhausted,
                out ProofOutcome<RealSet> _));

        Assert.True(SingleHoleSineAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Range,
            new ResourceBudget(),
            out ProofOutcome<RealSet> outcome));
        var certificate = Assert.IsType<SingleHoleSineProofCertificate>(outcome.Certificate);
        string claim = ClaimCanonical.ForObject(outcome.Value!);
        Assert.Throws<AnalysisCancelledException>(() =>
            SingleHoleSineCertificateChecker.Check(
                request,
                semantic,
                certificate,
                claim,
                new ResourceBudget(static () => false)));
        var exhaustedChecker = new ResourceBudget();
        exhaustedChecker.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            SingleHoleSineCertificateChecker.Check(
                request,
                semantic,
                certificate,
                claim,
                exhaustedChecker));
    }

    public static IEnumerable<object[]> AliasCases()
    {
        InputExpression x = Variable();
        InputExpression guard = Divide(x, x);
        yield return
        [
            Multiply(guard, Sin(x)),
            "interval[q:-1,1,q:1,1]",
            (int)Monotonicity.Increasing,
            "pi:1:0"
        ];
        yield return
        [
            Multiply(Multiply(Number(2), Sin(Multiply(Number(2), x))), guard),
            "interval[q:-2,1,q:2,1]",
            (int)Monotonicity.Increasing,
            "pi:1/2:0"
        ];
        yield return
        [
            Multiply(Negate(Sin(Multiply(Number(2), x))), guard),
            "interval[q:-1,1,q:1,1]",
            (int)Monotonicity.Decreasing,
            "pi:1/2:0"
        ];
        yield return
        [
            Multiply(Sin(Multiply(Number(-2), x)), guard),
            "interval[q:-1,1,q:1,1]",
            (int)Monotonicity.Decreasing,
            "pi:1/2:0"
        ];
        yield return
        [
            Multiply(Multiply(Symbol("pi"), Sin(x)), guard),
            "interval[pi:-1:0,1,pi:1:0,1]",
            (int)Monotonicity.Increasing,
            "pi:1:0"
        ];
    }

    private static void AssertPoint(
        FeaturePoint point,
        string expectedOffset,
        string expectedPeriod,
        string expectedY)
    {
        var constant = Assert.IsType<ConstantYFeaturePoint>(point);
        var periodic = Assert.IsType<PeriodicReal>(constant.X);
        Assert.Equal(expectedOffset, ExactRealCanonical.Format(periodic.Offset));
        Assert.Equal(expectedPeriod, ExactRealCanonical.Format(periodic.Period));
        Assert.Equal(expectedY, ExactRealCanonical.Format(constant.Y));
    }

    private static AnalysisRequest Request(
        InputExpression expression,
        AnalysisFeatures features,
        AngleUnit angleUnit = AngleUnit.Radians) =>
        new(expression, features, angleUnit, "x", static () => true);

    private static SemanticExpression Build(InputExpression input) =>
        new SemanticGraphBuilder(new ResourceBudget()).Build(input);

    private static InputExpression Variable() => InputExpression.Variable("x", Source);

    private static InputExpression Symbol(string name) => InputExpression.Variable(name, Source);

    private static InputExpression Number(int value) =>
        InputExpression.Number(new BigRational(value), Source);

    private static InputExpression Negate(InputExpression value) =>
        InputExpression.Unary(InputExpressionKind.Negate, value, Source);

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

    private static InputExpression Sin(InputExpression argument) =>
        InputExpression.Function("sin", [argument], Source);
}
