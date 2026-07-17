using System.Collections.Immutable;
using Graphing;
using Graphing.Analyzer;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class GuardedExactConstantRationalAnalysisTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Fact]
    public void ProportionalSymbolicMobiusRetainsItsHoleAndIsConstantOnBothComponents()
    {
        InputExpression expression = Divide(
            Add(Multiply(Symbol("pi"), Variable()), Symbol("pi")),
            Add(Variable(), Number(1)));
        SemanticExpression semantic = new SemanticGraphBuilder(new ResourceBudget()).Build(expression);
        Assert.True(ExactCoefficientPatternExtractor.TryExtract(
            semantic,
            "x",
            AngleUnit.Radians,
            new ResourceBudget(),
            out ExactCoefficientPattern extracted));
        var mobius = Assert.IsType<ExactRationalPattern>(extracted);
        Assert.True(
            ExactRationalCoefficientTheorems.TryCompute(
                mobius,
                AnalysisFeatures.Zeros,
                new ResourceBudget(),
                out _),
            mobius.Canonical);
        AnalysisRequest request = Request(expression, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        var range = Assert.IsType<PointSet>(Proved(report.Range));
        Assert.Equal(
            "pi:1:0",
            ExactRealCanonical.Format(Assert.Single(range.Points)));
        Assert.IsType<EmptySet>(Proved(report.Zeros));

        ImmutableArray<MonotoneRegion> regions = Proved(report.Monotonicity);
        Assert.Equal(2, regions.Length);
        Assert.All(regions, static region =>
            Assert.Equal(Monotonicity.Constant, region.Direction));
        Assert.Equal(
            "interval[-inf,0,q:-1,0]",
            regions[0].Region.Canonical);
        Assert.Equal(
            "interval[q:-1,0,+inf,0]",
            regions[1].Region.Canonical);

        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Range));
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Zeros));
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Monotonicity));
        Assert.IsType<ExactCoefficientProofCertificate>(report.Range.Certificate);
        Assert.IsType<ExactCoefficientProofCertificate>(report.Zeros.Certificate);
        var certificate = Assert.IsType<ExactCoefficientProofCertificate>(
            report.Monotonicity.Certificate);
        ExactOrderWitness zeroWitness = Assert.Single(
            certificate.OrderWitnesses.Where(static witness => witness.Sign == 0));
        Assert.Equal(BigRational.Zero, zeroWitness.Lower);
        Assert.Equal(BigRational.Zero, zeroWitness.Upper);

        int witnessIndex = certificate.OrderWitnesses.IndexOf(zeroWitness);
        ExactCoefficientProofCertificate mutated = certificate with
        {
            OrderWitnesses = certificate.OrderWitnesses.SetItem(
                witnessIndex,
                zeroWitness with { Upper = BigRational.One })
        };
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<ImmutableArray<MonotoneRegion>>.Proved(regions, mutated)));
    }

    [Theory]
    [InlineData("(pi*x+pi)/(x+1)", "x ≠ −1", "y ∈ {π}")]
    [InlineData("(e*x-e)/(x-1)", "x ≠ 1", "y ∈ {e}")]
    [InlineData("(2*pi*x+4*pi)/(2*x+4)", "x ≠ −2", "y ∈ {π}")]
    public void ProportionalSymbolicMobiusPublicCorpusFormatsCertifiedClaims(
        string formula,
        string expectedDomain,
        string expectedRange)
    {
        GraphFunctionAnalysisData result = AnalyzePublic(formula);

        Assert.Equal(expectedDomain, result.Domain);
        Assert.Equal(expectedRange, result.Range);
        Assert.Empty(result.Zeros);
        Assert.Equal(2, result.MonotoneIntervals.Count);
        Assert.All(result.MonotoneIntervals.Values, static direction =>
            Assert.Equal((int)FunctionMonotonicityType.Constant, direction));
        Assert.Equal(0, result.TooComplexFeatures);
    }

    private static GraphFunctionAnalysisData AnalyzePublic(string formula)
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
            analyzer.PerformFunctionAnalysis((uint)PerformAnalysisType.All));
        return solver.Analyze(analyzer);
    }

    private static T Proved<T>(ProofOutcome<T> outcome)
    {
        Assert.True(
            outcome.State == ProofState.Proved,
            $"Expected Proved, got {outcome.State}/{outcome.UnknownReason}.");
        Assert.NotNull(outcome.Certificate);
        return outcome.Value!;
    }

    private static AnalysisRequest Request(
        InputExpression expression,
        AnalysisFeatures features) =>
        new(expression, features, AngleUnit.Radians, "x", static () => true);

    private static InputExpression Variable() => InputExpression.Variable("x", Source);

    private static InputExpression Symbol(string name) => InputExpression.Variable(name, Source);

    private static InputExpression Number(int value) =>
        InputExpression.Number(new BigRational(value), Source);

    private static InputExpression Add(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Add, left, right, Source);

    private static InputExpression Multiply(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Multiply, left, right, Source);

    private static InputExpression Divide(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Divide, left, right, Source);
}
