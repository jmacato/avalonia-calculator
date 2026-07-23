using Graphing;
using Graphing.Analyzer;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class NegativeIntegralPowerAnalysisTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Fact]
    public void NegativeLiteralExponentFoldsBeforePowerDomainConstruction()
    {
        InputExpression expression = Power(-1);
        SemanticExpression semantic = new SemanticGraphBuilder(new ResourceBudget()).Build(expression);

        Assert.Equal(ValueKind.Power, semantic.Value.Kind);
        ValueTerm exponent = semantic.Value.Operands[1];
        Assert.Equal(ValueKind.Constant, exponent.Kind);
        Assert.Equal(BigRational.MinusOne, exponent.Constant);
        Assert.Contains(
            semantic.SourceOperands[1].RewriteHistory,
            static rewrite => rewrite.Rule == "exact-unary-constant-fold" &&
                              rewrite.Guard == Formula.True);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-2)]
    public void ProductionEnginePublishesOnlyCheckedRationalClaims(int exponent)
    {
        var request = new AnalysisRequest(
            Power(exponent),
            AnalysisFeatures.All,
            AngleUnit.Radians,
            "x",
            static () => true);

        AnalysisReport report = AnalysisEngine.Analyze(request);
        Assert.NotNull(report.Expression);
        AssertChecked(report.Domain);
        AssertChecked(report.Range);
        AssertChecked(report.Parity);
        AssertChecked(report.Zeros);
        AssertChecked(report.YIntercept);
        AssertChecked(report.Minima);
        AssertChecked(report.Maxima);
        AssertChecked(report.InflectionPoints);
        AssertChecked(report.VerticalAsymptotes);
        AssertChecked(report.HorizontalAsymptotes);
        AssertChecked(report.ObliqueAsymptotes);
        AssertChecked(report.Monotonicity);
        AssertChecked(report.Period);
        return;

        void AssertChecked<T>(ProofOutcome<T> outcome)
        {
            Assert.Equal(ProofState.Proved, outcome.State);
            Assert.NotNull(outcome.Certificate);
            Assert.True(CertificateChecker.Check(request, report.Expression!, outcome));
        }
    }

    [Theory]
    [InlineData(-1, "y ∈ ℝ ∖ {0}", FunctionParityType.Odd)]
    [InlineData(-2, "y ∈ (0, ∞)", FunctionParityType.Even)]
    public void PublicContractMatchesStableWindowsNegativePowerRows(
        int exponent,
        string expectedRange,
        FunctionParityType expectedParity)
    {
        GraphFunctionAnalysisData result = AnalyzePublic($"x^({exponent})");

        Assert.Equal("x ≠ 0", result.Domain);
        Assert.Equal(expectedRange, result.Range);
        Assert.Empty(result.Zeros);
        Assert.Empty(result.YIntercept);
        Assert.Empty(result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        Assert.Equal("x = 0", Assert.Single(result.VerticalAsymptotes));
        Assert.Equal("y = 0", Assert.Single(result.HorizontalAsymptotes));
        Assert.Empty(result.ObliqueAsymptotes);
        Assert.Equal((int)expectedParity, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.NotPeriodic, result.PeriodicityDirection);
        Assert.Empty(result.PeriodicityExpression);
        Assert.Equal(2, result.MonotoneIntervals.Count);
        Assert.Equal(
            (int)FunctionMonotonicityType.Descending,
            result.MonotoneIntervals["(0, ∞)"]);
        Assert.Equal(
            exponent == -1
                ? (int)FunctionMonotonicityType.Descending
                : (int)FunctionMonotonicityType.Ascending,
            result.MonotoneIntervals["(−∞, 0)"]);
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
        Assert.Equal(GraphStatus.Ok, analyzer.PerformFunctionAnalysis((uint)PerformAnalysisType.All));
        return solver.Analyze(analyzer);
    }

    private static InputExpression Power(int exponent)
    {
        return InputExpression.Binary(
            InputExpressionKind.Power,
            InputExpression.Variable("x", Source),
            InputExpression.Unary(
                InputExpressionKind.Negate,
                InputExpression.Number(new BigRational(-exponent), Source),
                Source),
            Source);
    }
}
