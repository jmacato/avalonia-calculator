using Graphing;
using Graphing.Analyzer;

namespace GraphingTests;

public sealed class AlgebraicCompatibilityFormatterTests
{
    [Theory]
    [InlineData("x^2-2", "x = −sqrt(2) ∨ x = sqrt(2)")]
    [InlineData("x^2-2x-2", "x = 1 − sqrt(3) ∨ x = sqrt(3) + 1")]
    [InlineData("x^2-72", "x = −6sqrt(2) ∨ x = 6sqrt(2)")]
    [InlineData("9x^2-2", "x = −sqrt(2)/3 ∨ x = sqrt(2)/3")]
    public void QuadraticRootsUseReducedExactRadicals(
        string expression,
        string expectedZeros)
    {
        GraphFunctionAnalysisData result = Analyze(expression);

        Assert.Equal(expectedZeros, result.Zeros);
    }

    [Fact]
    public void EvenBiquadraticRootsSelectSignedBranchesFromCertifiedIntervals()
    {
        GraphFunctionAnalysisData result = Analyze("x^4-6x^2+1");

        Assert.Equal(
            "x = −sqrt(2) − 1 ∨ x = 1 − sqrt(2) ∨ " +
            "x = sqrt(2) − 1 ∨ x = sqrt(2) + 1",
            result.Zeros);
    }

    [Fact]
    public void QuadraticCriticalPointsAndPolynomialImagesShareOneRadicalField()
    {
        GraphFunctionAnalysisData result = Analyze("x^3-x-1");

        Assert.Equal(
            "(sqrt(3)/3, −2sqrt(3)/9 − 1)",
            Assert.Single(result.Minima));
        Assert.Equal(
            "(−sqrt(3)/3, 2sqrt(3)/9 − 1)",
            Assert.Single(result.Maxima));
        Assert.Contains("(−sqrt(3)/3, sqrt(3)/3)", result.MonotoneIntervals.Keys);
    }

    [Fact]
    public void EvenPolynomialImagesReduceToRationalOrQuadraticFieldValues()
    {
        GraphFunctionAnalysisData quartic = Analyze("x^4-5x^2+4");

        Assert.Equal(
            ["(−sqrt(10)/2, −9/4)", "(sqrt(10)/2, −9/4)"],
            quartic.Minima);
        Assert.Equal(
            ["(−sqrt(30)/6, 19/36)", "(sqrt(30)/6, 19/36)"],
            quartic.InflectionPoints);

        GraphFunctionAnalysisData rational = Analyze("(x^2-1)/(x^2+1)");
        Assert.Equal(
            ["(−sqrt(3)/3, −1/2)", "(sqrt(3)/3, −1/2)"],
            rational.InflectionPoints);
    }

    [Fact]
    public void OddRationalImagesRetainTheCertifiedRadicalSign()
    {
        GraphFunctionAnalysisData result = Analyze("x/(x^2+1)");

        Assert.Equal(
            [
                "(−sqrt(3), −sqrt(3)/4)",
                "(0, 0)",
                "(sqrt(3), sqrt(3)/4)"
            ],
            result.InflectionPoints);
    }

    [Fact]
    public void GenuineBiquadraticImagesUseExactNestedRadicals()
    {
        GraphFunctionAnalysisData result = Analyze("x^5-5x^3+4x");

        Assert.Equal(
            [
                "(−sqrt(150 − 10sqrt(145))/10, −sqrt(4750 − 290sqrt(145))/25)",
                "(sqrt(150 + 10sqrt(145))/10, −sqrt(4750 + 290sqrt(145))/25)"
            ],
            result.Minima);
        Assert.Equal(
            [
                "(−sqrt(150 + 10sqrt(145))/10, sqrt(4750 + 290sqrt(145))/25)",
                "(sqrt(150 − 10sqrt(145))/10, sqrt(4750 − 290sqrt(145))/25)"
            ],
            result.Maxima);
    }

    [Fact]
    public void ShiftedBiquadraticImagesCombineRationalAndNestedRadicalParts()
    {
        GraphFunctionAnalysisData result = Analyze("x^5-5x^3+4x+1");

        Assert.Equal(
            "(−sqrt(150 − 10sqrt(145))/10, " +
            "1 − sqrt(4750 − 290sqrt(145))/25)",
            result.Minima[0]);
        Assert.Equal(
            "(−sqrt(150 + 10sqrt(145))/10, " +
            "1 + sqrt(4750 + 290sqrt(145))/25)",
            result.Maxima[0]);
    }

    [Fact]
    public void CardanoUsesExactConjugateRealCubeRoots()
    {
        GraphFunctionAnalysisData result = Analyze("x^3-x-1");

        Assert.Equal(
            "x = root((sqrt(69) + 9)/18, 3) + " +
            "root((9 − sqrt(69))/18, 3)",
            result.Zeros);
    }

    [Theory]
    [InlineData("x^3-2", "x = root(2, 3)")]
    [InlineData("x^3+2", "x = −root(2, 3)")]
    [InlineData("(x-1)^3-2", "x = root(2, 3) + 1")]
    [InlineData("(x+1)^3+2", "x = −root(2, 3) − 1")]
    [InlineData("2x^3-32", "x = 2root(2, 3)")]
    public void CardanoHandlesPositiveNegativeShiftedAndScaledCubics(
        string expression,
        string expectedZeros)
    {
        GraphFunctionAnalysisData result = Analyze(expression);

        Assert.Equal(expectedZeros, result.Zeros);
    }

    [Fact]
    public void CasusIrreducibilisKeepsCertifiedRootDescriptors()
    {
        GraphFunctionAnalysisData result = Analyze("x^3-3x+1");

        Assert.Equal(
            "x = root(x^3 − 3x + 1, 1) ∨ " +
            "x = root(x^3 − 3x + 1, 2) ∨ " +
            "x = root(x^3 − 3x + 1, 3)",
            result.Zeros);
    }

    private static GraphFunctionAnalysisData Analyze(string formula)
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
