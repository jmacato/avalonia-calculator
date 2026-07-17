using Graphing;
using Graphing.Analyzer;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class QuadraticHarmonicRangeAnalysisTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Theory]
    [MemberData(nameof(ExactRangeCases))]
    public void QuadraticsInOneHarmonicHaveExactClosedRanges(
        object inputValue,
        string expectedCanonical)
    {
        var input = Assert.IsType<InputExpression>(inputValue);
        AnalysisRequest request = Request(input);
        SemanticExpression semantic = Build(input);

        Assert.True(QuadraticHarmonicRangeAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Range,
            new ResourceBudget(),
            out ProofOutcome<RealSet> outcome));
        Assert.Equal(expectedCanonical, outcome.Value!.Canonical);
        var certificate = Assert.IsType<QuadraticHarmonicRangeProofCertificate>(
            outcome.Certificate);
        Assert.True(QuadraticHarmonicRangeCertificateChecker.Check(
            request,
            semantic,
            certificate,
            ClaimCanonical.For(outcome.Value),
            new ResourceBudget()));
    }

    [Fact]
    public void PartialDomainsAndUnrelatedFourierShapesFailClosed()
    {
        InputExpression x = Variable();
        InputExpression[] unsupported =
        [
            Multiply(
                Add(Sin(x), Cos(Multiply(Number(2), x))),
                Divide(x, x)),
            Add(Sin(x), Sin(Multiply(Number(2), x))),
            Add(Sin(x), Cos(Multiply(Number(3), x))),
            Add(Sin(Add(x, Number(1))), Cos(Multiply(Number(2), x))),
            Add(Sin(x), x)
        ];

        foreach (InputExpression input in unsupported)
        {
            Assert.False(QuadraticHarmonicRangeAnalyzer.TryAnalyze(
                Request(input),
                Build(input),
                AnalysisFeatures.Range,
                new ResourceBudget(),
                out ProofOutcome<RealSet> _));
        }
    }

    [Fact]
    public void OnlyTheRangeFeatureIsClaimed()
    {
        InputExpression input = Target();
        Assert.False(QuadraticHarmonicRangeAnalyzer.TryAnalyze(
            Request(input),
            Build(input),
            AnalysisFeatures.Zeros,
            new ResourceBudget(),
            out ProofOutcome<RealSet> _));
    }

    [Fact]
    public void ProductionEngineReplaysAndPublishesTheWindowsRange()
    {
        InputExpression input = Target();
        AnalysisRequest request = Request(input);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        Assert.Equal(ProofState.Proved, report.Range.State);
        Assert.Equal("interval[q:-2,1,q:9/8,1]", report.Range.Value!.Canonical);
        Assert.IsType<QuadraticHarmonicRangeProofCertificate>(report.Range.Certificate);
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Range));

        IMathSolver solver = MathSolver.CreateMathSolver();
        IExpression expression = solver.ParseInput(
            "sin(2*x)+cos(4*x)",
            out int errorCode,
            out int errorType) ??
            throw new InvalidOperationException($"Parse failed: {errorCode}/{errorType}");
        IGraph graph = solver.CreateGrapher();
        Assert.NotNull(graph.TryInitialize(expression));
        IGraphAnalyzer analyzer = graph.GetAnalyzer();
        Assert.True(analyzer.CanFunctionAnalysisBePerformed(out bool variableIsNotX));
        Assert.False(variableIsNotX);
        Assert.Equal(
            GraphStatus.Ok,
            analyzer.PerformFunctionAnalysis((uint)PerformAnalysisType.Range));

        GraphFunctionAnalysisData result = solver.Analyze(analyzer);
        Assert.Equal("y ∈ [−2, 9/8]", result.Range);
        Assert.Equal(0, result.TooComplexFeatures);
    }

    [Fact]
    public void CertificateReplayRejectsEveryMaterialMutation()
    {
        InputExpression input = Target();
        AnalysisRequest request = Request(input);
        SemanticExpression semantic = Build(input);
        Assert.True(QuadraticHarmonicRangeAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Range,
            new ResourceBudget(),
            out ProofOutcome<RealSet> outcome));
        var certificate = Assert.IsType<QuadraticHarmonicRangeProofCertificate>(
            outcome.Certificate);
        string claim = ClaimCanonical.For(outcome.Value!);

        AssertRejected(certificate with
        {
            Basis = certificate.Basis == QuadraticHarmonicBasis.Sine
                ? QuadraticHarmonicBasis.Cosine
                : QuadraticHarmonicBasis.Sine
        });
        AssertRejected(certificate with { Frequency = certificate.Frequency + 1 });
        AssertRejected(certificate with
        {
            QuadraticCoefficient = certificate.QuadraticCoefficient + BigRational.One
        });
        AssertRejected(certificate with
        {
            LinearCoefficient = certificate.LinearCoefficient + BigRational.One
        });
        AssertRejected(certificate with { Constant = certificate.Constant + BigRational.One });
        AssertRejected(certificate with { FourierCanonical = "forged" });
        AssertRejected(certificate with { DefinednessCanonical = "false" });
        AssertRejected(certificate with { Rule = "untrusted" });
        AssertRejected(certificate with { Subject = certificate.Subject + ":changed" });
        AssertRejected(certificate with { ProvenFeature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { Claim = "empty" });
        SemanticExpression forgedPartialDomain = semantic with
        {
            DefinedWhen = Formula.False
        };
        Assert.False(QuadraticHarmonicRangeCertificateChecker.Check(
            request,
            forgedPartialDomain,
            certificate with { DefinednessCanonical = Formula.False.Canonical },
            claim,
            new ResourceBudget()));
        return;

        void AssertRejected(QuadraticHarmonicRangeProofCertificate changed) =>
            Assert.False(QuadraticHarmonicRangeCertificateChecker.Check(
                request,
                semantic,
                changed,
                claim,
                new ResourceBudget()));
    }

    [Fact]
    public void RevisionCancellationAndWorkBudgetFailClosed()
    {
        InputExpression input = Target();
        AnalysisRequest request = Request(input);
        SemanticExpression semantic = Build(input);

        var cancelled = new ResourceBudget(static () => false);
        Assert.Throws<AnalysisCancelledException>(() =>
            QuadraticHarmonicRangeAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.Range,
                cancelled,
                out ProofOutcome<RealSet> _));

        var exhausted = new ResourceBudget();
        exhausted.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            QuadraticHarmonicRangeAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.Range,
                exhausted,
                out ProofOutcome<RealSet> _));

        Assert.True(QuadraticHarmonicRangeAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Range,
            new ResourceBudget(),
            out ProofOutcome<RealSet> outcome));
        var certificate = Assert.IsType<QuadraticHarmonicRangeProofCertificate>(
            outcome.Certificate);
        string claim = ClaimCanonical.For(outcome.Value!);
        Assert.Throws<AnalysisCancelledException>(() =>
            QuadraticHarmonicRangeCertificateChecker.Check(
                request,
                semantic,
                certificate,
                claim,
                new ResourceBudget(static () => false)));
        var exhaustedReplay = new ResourceBudget();
        exhaustedReplay.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            QuadraticHarmonicRangeCertificateChecker.Check(
                request,
                semantic,
                certificate,
                claim,
                exhaustedReplay));
    }

    public static IEnumerable<object[]> ExactRangeCases()
    {
        InputExpression x = Variable();
        yield return [Target(), "interval[q:-2,1,q:9/8,1]"];
        yield return
        [
            Add(Power(Sin(x), 2), Sin(x)),
            "interval[q:-1/4,1,q:2,1]"
        ];
        yield return
        [
            Add(
                Subtract(Multiply(Number(2), Power(Cos(x), 2)), Multiply(Number(3), Cos(x))),
                Number(4)),
            "interval[q:23/8,1,q:9,1]"
        ];
        yield return
        [
            Add(Power(Sin(x), 2), Multiply(Number(4), Sin(x))),
            "interval[q:-3,1,q:5,1]"
        ];
    }

    private static InputExpression Target()
    {
        InputExpression x = Variable();
        return Add(Sin(Multiply(Number(2), x)), Cos(Multiply(Number(4), x)));
    }

    private static AnalysisRequest Request(InputExpression input) =>
        new(input, AnalysisFeatures.Range, AngleUnit.Radians, "x", static () => true);

    private static SemanticExpression Build(InputExpression input) =>
        new SemanticGraphBuilder(new ResourceBudget()).Build(input);

    private static InputExpression Variable() => InputExpression.Variable("x", Source);

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

    private static InputExpression Sin(InputExpression argument) =>
        InputExpression.Function("sin", [argument], Source);

    private static InputExpression Cos(InputExpression argument) =>
        InputExpression.Function("cos", [argument], Source);
}
