using Graphing;
using Graphing.Analyzer;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class SingleHarmonicRangeAnalysisTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Theory]
    [MemberData(nameof(ExactRangeCases))]
    public void SingleFourierHarmonicsHaveExactClosedRanges(
        object inputValue,
        string expectedCanonical)
    {
        var input = Assert.IsType<InputExpression>(inputValue);
        AnalysisRequest request = Request(input);
        SemanticExpression semantic = Build(input);

        Assert.True(SingleHarmonicRangeAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Range,
            new ResourceBudget(),
            out ProofOutcome<RealSet> outcome));
        Assert.Equal(expectedCanonical, outcome.Value!.Canonical);
        var certificate = Assert.IsType<SingleHarmonicRangeProofCertificate>(outcome.Certificate);
        Assert.True(SingleHarmonicRangeCertificateChecker.Check(
            request,
            semantic,
            certificate,
            ClaimCanonical.For(outcome.Value),
            new ResourceBudget()));
    }

    [Fact]
    public void MultipleHarmonicsPartialDomainsAndNontrigonometricValuesFailClosed()
    {
        InputExpression x = Variable();
        InputExpression[] unsupported =
        [
            Add(Sin(x), Sin(Multiply(Number(2), x))),
            Multiply(Sin(x), Divide(x, x)),
            Add(Sin(x), x),
            Power(Sin(x), 0),
            Number(1)
        ];

        foreach (InputExpression input in unsupported)
        {
            Assert.False(SingleHarmonicRangeAnalyzer.TryAnalyze(
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
        InputExpression input = Add(Sin(Variable()), Cos(Variable()));
        Assert.False(SingleHarmonicRangeAnalyzer.TryAnalyze(
            Request(input),
            Build(input),
            AnalysisFeatures.Zeros,
            new ResourceBudget(),
            out ProofOutcome<RealSet> _));
    }

    [Theory]
    [InlineData("sin(x)+cos(x)", "y ∈ [−sqrt(2), sqrt(2)]")]
    [InlineData("sin(x)*cos(x)", "y ∈ [−1/2, 1/2]")]
    [InlineData("cos(x)^2-sin(x)^2", "y ∈ [−1, 1]")]
    public void ProductionEngineAndPublicFormatterPublishTheCheckedRange(
        string formula,
        string expected)
    {
        IMathSolver solver = MathSolver.CreateMathSolver();
        IExpression expression = solver.ParseInput(formula, out int errorCode, out int errorType)
            ?? throw new InvalidOperationException($"Parse failed: {errorCode}/{errorType}");
        IGraph graph = solver.CreateGrapher();
        Assert.NotNull(graph.TryInitialize(expression));
        IGraphAnalyzer analyzer = graph.GetAnalyzer();
        Assert.True(analyzer.CanFunctionAnalysisBePerformed(out bool variableIsNotX));
        Assert.False(variableIsNotX);
        Assert.Equal(
            GraphStatus.Ok,
            analyzer.PerformFunctionAnalysis((uint)PerformAnalysisType.Range));

        GraphFunctionAnalysisData result = solver.Analyze(analyzer);
        Assert.Equal(expected, result.Range);
        Assert.Equal(0, result.TooComplexFeatures);
    }

    [Fact]
    public void ProductionCertificateIsReplayedBeforePublication()
    {
        InputExpression input = Add(Sin(Variable()), Cos(Variable()));
        AnalysisRequest request = Request(input);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        Assert.Equal(ProofState.Proved, report.Range.State);
        Assert.IsType<SingleHarmonicRangeProofCertificate>(report.Range.Certificate);
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Range));
    }

    [Fact]
    public void CertificateReplayRejectsEveryMaterialMutation()
    {
        InputExpression input = Add(Sin(Variable()), Cos(Variable()));
        AnalysisRequest request = Request(input);
        SemanticExpression semantic = Build(input);
        Assert.True(SingleHarmonicRangeAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Range,
            new ResourceBudget(),
            out ProofOutcome<RealSet> outcome));
        var certificate = Assert.IsType<SingleHarmonicRangeProofCertificate>(outcome.Certificate);
        string claim = ClaimCanonical.For(outcome.Value!);

        AssertRejected(certificate with { Frequency = certificate.Frequency + 1 });
        AssertRejected(certificate with { Constant = certificate.Constant + BigRational.One });
        AssertRejected(certificate with
        {
            CosineCoefficient = certificate.CosineCoefficient + BigRational.One
        });
        AssertRejected(certificate with
        {
            SineCoefficient = certificate.SineCoefficient + BigRational.One
        });
        AssertRejected(certificate with
        {
            RadiusSquared = certificate.RadiusSquared + BigRational.One
        });
        AssertRejected(certificate with { FourierCanonical = "forged" });
        AssertRejected(certificate with { DefinednessCanonical = "false" });
        AssertRejected(certificate with { Rule = "untrusted" });
        AssertRejected(certificate with { Subject = certificate.Subject + ":changed" });
        AssertRejected(certificate with { ProvenFeature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { Claim = "empty" });
        return;

        void AssertRejected(SingleHarmonicRangeProofCertificate changed) =>
            Assert.False(SingleHarmonicRangeCertificateChecker.Check(
                request,
                semantic,
                changed,
                claim,
                new ResourceBudget()));
    }

    [Fact]
    public void RevisionCancellationAndWorkBudgetFailClosed()
    {
        InputExpression input = Add(Sin(Variable()), Cos(Variable()));
        AnalysisRequest request = Request(input);
        SemanticExpression semantic = Build(input);

        var cancelled = new ResourceBudget(static () => false);
        Assert.Throws<AnalysisCancelledException>(() =>
            SingleHarmonicRangeAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.Range,
                cancelled,
                out ProofOutcome<RealSet> _));

        var exhausted = new ResourceBudget();
        exhausted.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            SingleHarmonicRangeAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.Range,
                exhausted,
                out ProofOutcome<RealSet> _));
    }

    public static IEnumerable<object[]> ExactRangeCases()
    {
        InputExpression x = Variable();
        yield return
        [
            Add(Sin(x), Cos(x)),
            "interval[fn:negate(fn:sqrt(q:2)),1,fn:sqrt(q:2),1]"
        ];
        yield return
        [
            Subtract(Sin(x), Cos(x)),
            "interval[fn:negate(fn:sqrt(q:2)),1,fn:sqrt(q:2),1]"
        ];
        yield return
        [
            Multiply(Sin(x), Cos(x)),
            "interval[q:-1/2,1,q:1/2,1]"
        ];
        yield return
        [
            Subtract(Power(Cos(x), 2), Power(Sin(x), 2)),
            "interval[q:-1,1,q:1,1]"
        ];
        yield return
        [
            Add(
                Add(
                    Multiply(Number(3), Sin(Multiply(Number(2), x))),
                    Multiply(Number(-4), Cos(Multiply(Number(2), x)))),
                Number(7)),
            "interval[q:2,1,q:12,1]"
        ];
        yield return
        [
            Add(Power(Sin(x), 2), Number(2)),
            "interval[q:2,1,q:3,1]"
        ];
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
