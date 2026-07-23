using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class AffineSquareLogAnalysisTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Fact]
    public void BaseTenLogarithmOfSquareCertifiesEveryExactFeature()
    {
        InputExpression input = LogSquare(Variable());
        AnalysisRequest request = Request(input, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        AssertAllFeaturesProved(report);
        Assert.Equal(
            "union[interval[-inf,0,q:0,0],interval[q:0,0,+inf,0]]",
            report.Domain.Value!.Canonical);
        Assert.IsType<AllRealSet>(report.Range.Value);
        Assert.Equal(FunctionParity.Even, report.Parity.Value);
        Assert.Equal("points[q:-1,q:1]", report.Zeros.Value!.Canonical);
        Assert.False(report.YIntercept.Value!.HasValue);
        Assert.Empty(report.Minima.Value!);
        Assert.Empty(report.Maxima.Value!);
        Assert.Empty(report.InflectionPoints.Value!);

        Asymptote vertical = Assert.Single(report.VerticalAsymptotes.Value!);
        Assert.Equal(AsymptoteOrientation.Vertical, vertical.Orientation);
        Assert.Equal(
            "q:0",
            ExactRealCanonical.Format(Assert.IsType<SingletonReal>(vertical.Coordinate).Value));
        Assert.Empty(report.HorizontalAsymptotes.Value!);
        Assert.Empty(report.ObliqueAsymptotes.Value!);

        ImmutableArray<MonotoneRegion> monotonicity = report.Monotonicity.Value!;
        Assert.Equal(2, monotonicity.Length);
        Assert.Equal("interval[-inf,0,q:0,0]", monotonicity[0].Region.Canonical);
        Assert.Equal(Monotonicity.Decreasing, monotonicity[0].Direction);
        Assert.Equal("interval[q:0,0,+inf,0]", monotonicity[1].Region.Canonical);
        Assert.Equal(Monotonicity.Increasing, monotonicity[1].Direction);
        Assert.Equal(PeriodicityKind.NotPeriodic, report.Period.Value!.Kind);
        Assert.Null(report.Period.Value.FundamentalPeriod);

        AssertAllCertificatesReplay(request, report);
    }

    [Theory]
    [MemberData(nameof(AffineCases))]
    public void RationalAffineSquaresRetainTheirHoleAndExactPullbacks(
        object inputValue,
        string expectedDomain,
        string expectedZeros,
        int expectedParity,
        string expectedYIntercept)
    {
        var input = Assert.IsType<InputExpression>(inputValue);
        AnalysisRequest request = Request(input, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        AssertAllFeaturesProved(report);
        Assert.Equal(expectedDomain, report.Domain.Value!.Canonical);
        Assert.Equal(expectedZeros, report.Zeros.Value!.Canonical);
        Assert.Equal((FunctionParity)expectedParity, report.Parity.Value);
        OptionalValue<ExactReal> yIntercept = report.YIntercept.Value!;
        Assert.True(yIntercept.HasValue);
        Assert.Equal(
            expectedYIntercept,
            ExactRealCanonical.Format(yIntercept.Value!));
        AssertAllCertificatesReplay(request, report);
    }

    [Fact]
    public void ExtraDefinednessHolesAreNeverErasedByTheAffineTheorem()
    {
        InputExpression x = Variable();
        InputExpression retainedHole = Add(
            x,
            Multiply(
                Number(0),
                Divide(Number(1), Subtract(x, Number(3)))));
        InputExpression input = LogSquare(retainedHole);
        AnalysisRequest request = Request(input, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        Assert.Equal(ProofState.Proved, report.Domain.State);
        Assert.Contains("q:0", report.Domain.Value!.Canonical, StringComparison.Ordinal);
        Assert.Contains("q:3", report.Domain.Value.Canonical, StringComparison.Ordinal);
        AssertUnsupported(report.Range);
        AssertUnsupported(report.Parity);
        AssertUnsupported(report.Zeros);
        Assert.Equal(ProofState.Proved, report.YIntercept.State);
        Assert.False(report.YIntercept.Value!.HasValue);
        Assert.IsType<ExactOriginProofCertificate>(report.YIntercept.Certificate);
        Assert.True(CertificateChecker.Check(
            request,
            report.Expression!,
            report.YIntercept));
        AssertUnsupported(report.Minima);
        AssertUnsupported(report.Maxima);
        AssertUnsupported(report.InflectionPoints);
        AssertUnsupported(report.VerticalAsymptotes);
        AssertUnsupported(report.HorizontalAsymptotes);
        AssertUnsupported(report.ObliqueAsymptotes);
        AssertUnsupported(report.Monotonicity);
        AssertUnsupported(report.Period);

        SemanticExpression semantic = report.Expression!;
        Assert.False(AffineSquareLogAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Range,
            new ResourceBudget(),
            out ProofOutcome<RealSet> _));
    }

    [Fact]
    public void IndependentlyProvedRedundantGuardsPreserveCertifiedOutputs()
    {
        InputExpression x = Variable();
        InputExpression denominator = Add(Power(x, Number(2)), Number(1));
        InputExpression guardedAffine = Add(
            x,
            Multiply(Number(0), Divide(Number(1), denominator)));
        InputExpression input = LogSquare(guardedAffine);
        AnalysisRequest request = Request(input, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        Assert.NotEqual(Formula.True.Canonical, report.Expression!.DefinedWhen.Canonical);
        AssertAllFeaturesProved(report);
        Assert.Equal(
            "union[interval[-inf,0,q:0,0],interval[q:0,0,+inf,0]]",
            report.Domain.Value!.Canonical);
        AssertAllCertificatesReplay(request, report);
    }

    [Fact]
    public void NonSquareNaturalLogarithmAndConstantBasesStayOutsideTheTheorem()
    {
        InputExpression[] unsupported =
        [
            Function("log", Power(Variable(), Number(4))),
            Function("ln", Power(Variable(), Number(2))),
            Function("log", Power(Number(2), Number(2))),
            Function("log", Power(Add(Power(Variable(), Number(2)), Number(1)), Number(2)))
        ];

        foreach (InputExpression input in unsupported)
        {
            AnalysisRequest request = Request(input, AnalysisFeatures.Range);
            SemanticExpression semantic = new SemanticGraphBuilder(new ResourceBudget()).Build(input);
            Assert.False(AffineSquareLogAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.Range,
                new ResourceBudget(),
                out ProofOutcome<RealSet> _));
        }
    }

    [Fact]
    public void DedicatedCertificateReplayRejectsEveryMaterialMutation()
    {
        InputExpression input = LogSquare(Add(Multiply(Number(2), Variable()), Number(-4)));
        AnalysisRequest request = Request(input, AnalysisFeatures.Range);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        RealSet range = report.Range.Value!;
        var certificate = Assert.IsType<AffineSquareLogProofCertificate>(report.Range.Certificate);

        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Range));
        AssertRejected(certificate with { Slope = new BigRational(3) });
        AssertRejected(certificate with { Intercept = new BigRational(-3) });
        AssertRejected(certificate with { PatternCanonical = certificate.PatternCanonical + ":changed" });
        AssertRejected(certificate with { DefinednessCanonical = "true" });
        AssertRejected(certificate with { Rule = "untrusted" });
        AssertRejected(certificate with { Subject = certificate.Subject + ":changed" });
        AssertRejected(certificate with { ProvenFeature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { Feature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { Claim = "empty" });

        AnalysisRequest unrequested = Request(input, AnalysisFeatures.Zeros);
        Assert.False(AffineSquareLogCertificateChecker.Check(
            unrequested,
            report.Expression!,
            certificate,
            ClaimCanonical.For(range),
            new ResourceBudget()));

        AnalysisRequest wrongVariable = new(
            input,
            AnalysisFeatures.Range,
            AngleUnit.Radians,
            "t",
            static () => true);
        Assert.False(AffineSquareLogCertificateChecker.Check(
            wrongVariable,
            report.Expression!,
            certificate,
            ClaimCanonical.For(range),
            new ResourceBudget()));

        void AssertRejected(AffineSquareLogProofCertificate changed) =>
            Assert.False(AffineSquareLogCertificateChecker.Check(
                request,
                report.Expression!,
                changed,
                ClaimCanonical.For(range),
                new ResourceBudget()));
    }

    [Fact]
    public void IndependentReplayRejectsHiddenHolesAndMutatedSemanticContext()
    {
        InputExpression cleanInput = LogSquare(Variable());
        AnalysisRequest cleanRequest = Request(cleanInput, AnalysisFeatures.Range);
        AnalysisReport cleanReport = AnalysisEngine.Analyze(cleanRequest);
        RealSet range = cleanReport.Range.Value!;
        var certificate = Assert.IsType<AffineSquareLogProofCertificate>(
            cleanReport.Range.Certificate);

        InputExpression x = Variable();
        InputExpression affineWithHiddenHole = Add(
            x,
            Multiply(
                Number(0),
                Divide(Number(1), Subtract(x, Number(3)))));
        InputExpression holedInput = LogSquare(affineWithHiddenHole);
        AnalysisRequest holedRequest = Request(holedInput, AnalysisFeatures.Range);
        SemanticExpression holedSemantic = new SemanticGraphBuilder(new ResourceBudget())
            .Build(holedInput);
        Assert.Equal(cleanReport.Expression!.Value.Canonical, holedSemantic.Value.Canonical);
        Assert.NotEqual(
            cleanReport.Expression.DefinedWhen.Canonical,
            holedSemantic.DefinedWhen.Canonical);

        AffineSquareLogProofCertificate forgedForHole = certificate with
        {
            DefinednessCanonical = holedSemantic.DefinedWhen.Canonical
        };
        Assert.False(AffineSquareLogCertificateChecker.Check(
            holedRequest,
            holedSemantic,
            forgedForHole,
            ClaimCanonical.For(range),
            new ResourceBudget()));
        Assert.False(CertificateChecker.Check(
            holedRequest,
            holedSemantic,
            ProofOutcome<RealSet>.Proved(range, forgedForHole)));

        SemanticExpression missingSourceContext = cleanReport.Expression with
        {
            SourceOperands = []
        };
        Assert.False(AffineSquareLogCertificateChecker.Check(
            cleanRequest,
            missingSourceContext,
            certificate,
            ClaimCanonical.For(range),
            new ResourceBudget()));
    }

    [Fact]
    public void CoefficientBudgetAndRevisionCancellationFailClosedDeterministically()
    {
        ExactInteger huge = ExactInteger.One << (AnalysisLimits.CoefficientBits + 1);
        InputExpression oversized = LogSquare(Multiply(
            Number(new BigRational(huge)),
            Variable()));
        AnalysisReport first = AnalysisEngine.Analyze(Request(
            oversized,
            AnalysisFeatures.Range));
        AnalysisReport second = AnalysisEngine.Analyze(Request(
            oversized,
            AnalysisFeatures.Range));

        Assert.Equal(ProofState.Unknown, first.Range.State);
        Assert.Equal(UnknownReason.BudgetExceeded, first.Range.UnknownReason);
        Assert.Equal(first.Range.UnknownReason, second.Range.UnknownReason);
        Assert.Equal(first.ChargedWorkUnits, second.ChargedWorkUnits);

        InputExpression valid = LogSquare(Variable());
        AnalysisRequest request = Request(valid, AnalysisFeatures.Range);
        SemanticExpression semantic = new SemanticGraphBuilder(new ResourceBudget()).Build(valid);
        var cancelled = new ResourceBudget(static () => false);
        Assert.Throws<AnalysisCancelledException>(() =>
            AffineSquareLogAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.Range,
                cancelled,
                out ProofOutcome<RealSet> _));

        var exhausted = new ResourceBudget();
        exhausted.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            AffineSquareLogAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.Range,
                exhausted,
                out ProofOutcome<RealSet> _));

        AnalysisReport validReport = AnalysisEngine.Analyze(request);
        var certificate = Assert.IsType<AffineSquareLogProofCertificate>(
            validReport.Range.Certificate);
        string claim = ClaimCanonical.For(validReport.Range.Value!);
        var cancelledReplay = new ResourceBudget(static () => false);
        Assert.Throws<AnalysisCancelledException>(() =>
            AffineSquareLogCertificateChecker.Check(
                request,
                semantic,
                certificate,
                claim,
                cancelledReplay));

        var exhaustedReplay = new ResourceBudget();
        exhaustedReplay.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            AffineSquareLogCertificateChecker.Check(
                request,
                semantic,
                certificate,
                claim,
                exhaustedReplay));
    }

    public static IEnumerable<object[]> AffineCases()
    {
        yield return
        [
            LogSquare(Add(Multiply(Number(2), Variable()), Number(-4))),
            "union[interval[-inf,0,q:2,0],interval[q:2,0,+inf,0]]",
            "points[q:3/2,q:5/2]",
            (int)FunctionParity.Neither,
            "fn:log(q:16)"
        ];
        yield return
        [
            LogSquare(Add(Multiply(Number(-3), Variable()), Number(1))),
            "union[interval[-inf,0,q:1/3,0],interval[q:1/3,0,+inf,0]]",
            "points[q:0,q:2/3]",
            (int)FunctionParity.Neither,
            "q:0"
        ];
        yield return
        [
            LogSquare(Add(Multiply(Number(10), Variable()), Number(10))),
            "union[interval[-inf,0,q:-1,0],interval[q:-1,0,+inf,0]]",
            "points[q:-11/10,q:-9/10]",
            (int)FunctionParity.Neither,
            "q:2"
        ];
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
        AssertReplay(report.Domain, false);
        AssertReplay(report.Range, true);
        AssertReplay(report.Parity, true);
        AssertReplay(report.Zeros, true);
        AssertReplay(report.YIntercept, true);
        AssertReplay(report.Minima, true);
        AssertReplay(report.Maxima, true);
        AssertReplay(report.InflectionPoints, true);
        AssertReplay(report.VerticalAsymptotes, true);
        AssertReplay(report.HorizontalAsymptotes, true);
        AssertReplay(report.ObliqueAsymptotes, true);
        AssertReplay(report.Monotonicity, true);
        AssertReplay(report.Period, true);
        return;

        void AssertReplay<T>(ProofOutcome<T> outcome, bool expectsDedicatedCertificate)
        {
            if (expectsDedicatedCertificate)
            {
                Assert.IsType<AffineSquareLogProofCertificate>(outcome.Certificate);
            }

            Assert.True(CertificateChecker.Check(request, report.Expression!, outcome));
        }
    }

    private static void AssertUnsupported<T>(ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Unknown, outcome.State);
        Assert.Equal(UnknownReason.UnsupportedFragment, outcome.UnknownReason);
        Assert.Null(outcome.Certificate);
    }

    private static AnalysisRequest Request(
        InputExpression expression,
        AnalysisFeatures features)
    {
        return new AnalysisRequest(expression, features, AngleUnit.Radians, "x", static () => true);
    }

    private static InputExpression LogSquare(InputExpression affine)
    {
        return Function("log", Power(affine, Number(2)));
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

    private static InputExpression Power(InputExpression basis, InputExpression exponent)
    {
        return InputExpression.Binary(InputExpressionKind.Power, basis, exponent, Source);
    }

    private static InputExpression Function(string name, params InputExpression[] arguments)
    {
        return InputExpression.Function(name, arguments.ToImmutableArray(), Source);
    }
}
