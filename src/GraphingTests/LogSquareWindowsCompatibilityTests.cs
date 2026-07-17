using Graphing;
using Graphing.Analyzer;

namespace GraphingTests;

public sealed class LogSquareWindowsCompatibilityTests
{
    [Fact]
    public void BaseTenLogarithmOfSquareMatchesTheStableWindowsRow()
    {
        GraphFunctionAnalysisData result = AnalyzePublic("log(x^2)");

        Assert.Equal("x ≠ 0", result.Domain);
        Assert.Equal("y ∈ ℝ", result.Range);
        Assert.Equal("x = −1 ∨ x = 1", result.Zeros);
        Assert.Empty(result.YIntercept);
        Assert.Empty(result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        Assert.Equal("x = 0", Assert.Single(result.VerticalAsymptotes));
        Assert.Empty(result.HorizontalAsymptotes);
        Assert.Empty(result.ObliqueAsymptotes);
        Assert.Equal((int)FunctionParityType.Even, result.Parity);
        Assert.Equal(
            (int)FunctionPeriodicityType.NotPeriodic,
            result.PeriodicityDirection);
        Assert.Empty(result.PeriodicityExpression);
        Assert.Equal(
            new Dictionary<string, int>
            {
                ["(−∞, 0)"] = (int)FunctionMonotonicityType.Descending,
                ["(0, ∞)"] = (int)FunctionMonotonicityType.Ascending
            },
            result.MonotoneIntervals);
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
}
