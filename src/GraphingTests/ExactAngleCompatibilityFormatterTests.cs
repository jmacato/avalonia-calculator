using Graphing;
using Graphing.Analyzer;

namespace GraphingTests;

public sealed class ExactAngleCompatibilityFormatterTests
{
    public static TheoryData<string, string> PrincipalAngles => new()
    {
        { "asin(x-1)", "y = −π/2" },
        { "asin(x-1/2)", "y = −π/6" },
        { "asin(x)", "y = 0" },
        { "asin(x+1/2)", "y = π/6" },
        { "asin(x+1)", "y = π/2" },
        { "acos(x-1)", "y = π" },
        { "acos(x-1/2)", "y = 2π/3" },
        { "acos(x)", "y = π/2" },
        { "acos(x+1/2)", "y = π/3" },
        { "acos(x+1)", "y = 0" },
        { "atan(x-1)", "y = −π/4" },
        { "atan(x)", "y = 0" },
        { "atan(x+1)", "y = π/4" }
    };

    [Theory]
    [MemberData(nameof(PrincipalAngles))]
    public void PrincipalInverseAnglesUseExactPiMultiples(
        string formula,
        string expectedYIntercept)
    {
        GraphFunctionAnalysisData result = Analyze(
            formula,
            PerformAnalysisType.InterceptionPointsWithXAndYAxis);

        Assert.Equal(expectedYIntercept, result.YIntercept);
        Assert.Equal(0, result.TooComplexFeatures);
    }

    [Fact]
    public void AffineSineZerosReduceNestedInverseAndAffineWrappersExactly()
    {
        GraphFunctionAnalysisData result = Analyze(
            "2*sin(2*x)+1",
            PerformAnalysisType.InterceptionPointsWithXAndYAxis);

        Assert.Equal(
            "x ∈ {πn₁ + 11π/12, πn₁ + 7π/12}, n₁ ∈ ℤ",
            result.Zeros);
    }

    [Theory]
    [InlineData("sin(x)+cos(x)", "x = πn₁ + 3π/4, n₁ ∈ ℤ")]
    [InlineData("sin(x)-cos(x)", "x = πn₁ + π/4, n₁ ∈ ℤ")]
    [InlineData("sin(x)*cos(x)", "x = π/2n₁, n₁ ∈ ℤ")]
    [InlineData("sin(x)^2", "x = πn₁, n₁ ∈ ℤ")]
    [InlineData("cos(x)^2", "x = πn₁ + π/2, n₁ ∈ ℤ")]
    public void EquivalentPeriodicZeroBranchesCompressToOneExactLattice(
        string formula,
        string expectedZeros)
    {
        GraphFunctionAnalysisData result = Analyze(
            formula,
            PerformAnalysisType.InterceptionPointsWithXAndYAxis);

        Assert.Equal(expectedZeros, result.Zeros);
    }

    [Fact]
    public void HalfAngleSqrtTwoExtremaUseExactPrincipalAngles()
    {
        GraphFunctionAnalysisData sum = Analyze(
            "sin(x)+cos(x)",
            PerformAnalysisType.CriticalPoints);
        GraphFunctionAnalysisData difference = Analyze(
            "sin(x)-cos(x)",
            PerformAnalysisType.CriticalPoints);

        Assert.Equal(
            "(2πn₁ + π/4, sqrt(2)), n₁ ∈ ℤ",
            Assert.Single(sum.Maxima));
        Assert.Equal(
            "(2πn₁ + 5π/4, −sqrt(2)), n₁ ∈ ℤ",
            Assert.Single(sum.Minima));
        Assert.Equal(
            "(2πn₁ + 3π/4, sqrt(2)), n₁ ∈ ℤ",
            Assert.Single(difference.Maxima));
        Assert.Equal(
            "(2πn₁ + 7π/4, −sqrt(2)), n₁ ∈ ℤ",
            Assert.Single(difference.Minima));
    }

    [Fact]
    public void UnsupportedInverseAnglesRemainExactSymbolicFunctions()
    {
        GraphFunctionAnalysisData result = Analyze(
            "atan(x-2)",
            PerformAnalysisType.InterceptionPointsWithXAndYAxis);

        Assert.Equal("y = arctan(−2)", result.YIntercept);
    }

    private static GraphFunctionAnalysisData Analyze(
        string formula,
        PerformAnalysisType requested)
    {
        IMathSolver solver = MathSolver.CreateMathSolver();
        IExpression expression = solver.ParseInput(formula, out int errorCode, out int errorType)
            ?? throw new InvalidOperationException($"Parse failed: {errorCode}/{errorType}");
        IGraph graph = solver.CreateGrapher();
        Assert.NotNull(graph.TryInitialize(expression));
        IGraphAnalyzer analyzer = graph.GetAnalyzer();
        Assert.True(analyzer.CanFunctionAnalysisBePerformed(out bool variableIsNotX));
        Assert.False(variableIsNotX);
        Assert.Equal(GraphStatus.Ok, analyzer.PerformFunctionAnalysis((uint)requested));
        return solver.Analyze(analyzer);
    }
}
