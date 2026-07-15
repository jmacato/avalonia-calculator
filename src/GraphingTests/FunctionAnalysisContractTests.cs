using Graphing;
using Graphing.Analyzer;
using GraphControl;

namespace GraphingTests;

public sealed class FunctionAnalysisContractTests
{
    [Fact(Timeout = 2_000)]
    public void InequalityReturnsNativeUnsupportedStateWithoutRunningFunctionAnalysis()
    {
        var grapher = new Grapher();
        var equation = new Equation { Expression = "y<sqrt(x)" };
        grapher.Equations.Add(equation);

        KeyGraphFeaturesInfo? result = grapher.AnalyzeEquation(equation);

        Assert.NotNull(result);
        Assert.Equal(AnalysisErrorType.AnalysisNotSupported, result.AnalysisError);
    }

    [Fact]
    public void SineAnalysisUsesSymbolicFamiliesInsteadOfFiniteSamplePoints()
    {
        GraphFunctionAnalysisData result = Analyze("sin(x)");

        Assert.Equal("y ∈ [−1, 1]", result.Range);
        Assert.Equal("x = πn₁, n₁ ∈ ℤ", result.Zeros);
        Assert.Equal("y = 0", result.YIntercept);
        Assert.Equal("(2πn₁ + 3π/2, −1), n₁ ∈ ℤ", Assert.Single(result.Minima));
        Assert.Equal("(2πn₁ + π/2, 1), n₁ ∈ ℤ", Assert.Single(result.Maxima));
        Assert.Equal("(πn₁, 0), n₁ ∈ ℤ", Assert.Single(result.InflectionPoints));
        Assert.Equal("2π", result.PeriodicityExpression);
        Assert.Equal(
            new[] { (int)FunctionMonotonicityType.Descending, (int)FunctionMonotonicityType.Ascending },
            result.MonotoneIntervals.Values);
        Assert.All(
            result.MonotoneIntervals.Keys,
            interval => Assert.Contains("n₁ ∈ ℤ", interval, StringComparison.Ordinal));
    }

    private static GraphFunctionAnalysisData Analyze(string formula)
    {
        IMathSolver solver = MathSolver.CreateMathSolver();
        solver.ParsingOptions().SetFormatType(FormatType.Linear);
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
}
