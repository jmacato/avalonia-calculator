using Graphing;
using Graphing.Analyzer;
using GraphControl;

namespace GraphingTests;

public sealed class FunctionAnalysisFormatterGoldenTests
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
            new Dictionary<string, int>
            {
                ["(2πn₁ + π/2, 2πn₁ + 3π/2), n₁ ∈ ℤ"] =
                    (int)FunctionMonotonicityType.Descending,
                ["(2πn₁ + 3π/2, 2πn₁ + 5π/2), n₁ ∈ ℤ"] =
                    (int)FunctionMonotonicityType.Ascending
            },
            result.MonotoneIntervals);
    }

    [Theory]
    [InlineData("sin(x)tan(x)log(x)")]
    [InlineData("log(x)*sin(x)*tan(x)")]
    public void CompositeDomainIsDerivedAndNormalizedAsPeriodicIntervals(string formula)
    {
        GraphFunctionAnalysisData result = Analyze(formula, PerformAnalysisType.Domain);

        Assert.Equal(
            "x ∈ (πn₁ + π/2, πn₁ + 3π/2), n₁ ∈ ℤ, n₁ > −1 ∨ x ∈ (0, π/2)",
            result.Domain);
    }

    [Fact]
    public void CompositeZerosAreRestrictedToTheExactPeriodicDomain()
    {
        GraphFunctionAnalysisData result = Analyze(
            "sin(x)tan(x)log(x)",
            PerformAnalysisType.InterceptionPointsWithXAndYAxis);

        Assert.Equal("x = πn₁, n₁ ∈ ℤ, n₁ > 0 ∨ x = 1", result.Zeros);
        Assert.Empty(result.YIntercept);
        Assert.Equal(0, result.TooComplexFeatures);
    }

    [Fact]
    public void UnsupportedTranscendentalCalculusIsExplicitlyUnknownNotSampled()
    {
        GraphFunctionAnalysisData result = Analyze(
            "sin(x)tan(x)log(x)",
            PerformAnalysisType.Range |
            PerformAnalysisType.CriticalPoints |
            PerformAnalysisType.Monotonicity);

        Assert.Empty(result.Range);
        Assert.Empty(result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        Assert.Empty(result.MonotoneIntervals);
        int expected = FeatureBit(AnalysisType.Range) |
                       FeatureBit(AnalysisType.Minima) |
                       FeatureBit(AnalysisType.Maxima) |
                       FeatureBit(AnalysisType.InflectionPoints) |
                       FeatureBit(AnalysisType.Monotonicity);
        Assert.Equal(expected, result.TooComplexFeatures);
    }

    [Fact]
    public void AffineLogDomainIsSolvedSymbolically()
    {
        GraphFunctionAnalysisData result = Analyze("log(2x-4)", PerformAnalysisType.Domain);

        Assert.Equal("x > 2", result.Domain);
    }

    [Fact]
    public void UpperBoundedPeriodicDomainKeepsItsInfiniteIntervalFamily()
    {
        GraphFunctionAnalysisData result = Analyze("log(-x)*tan(x)", PerformAnalysisType.Domain);

        Assert.Equal(
            "x ∈ (πn₁ + π/2, πn₁ + 3π/2), n₁ ∈ ℤ, n₁ < −1 ∨ x ∈ (−π/2, 0)",
            result.Domain);
    }

    [Fact]
    public void PeriodicExclusionsOutsideABoundedDomainAreDiscarded()
    {
        GraphFunctionAnalysisData result = Analyze(
            "sqrt(x)*sqrt(0.1-x)*tan(x)",
            PerformAnalysisType.Domain);

        Assert.Equal("0 ≤ x ≤ 1/10", result.Domain);
    }

    [Fact]
    public void PolynomialRangeParityAndNonperiodicityAreSymbolic()
    {
        GraphFunctionAnalysisData result = Analyze(
            "x^2+1",
            PerformAnalysisType.Range | PerformAnalysisType.Parity | PerformAnalysisType.Period);

        Assert.Equal("y ∈ [1, ∞)", result.Range);
        Assert.Equal((int)FunctionParityType.Even, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.NotPeriodic, result.PeriodicityDirection);
        Assert.Equal(0, result.TooComplexFeatures);
    }

    [Fact]
    public void AffineTrigonometricRangeParityAndPeriodAreSymbolic()
    {
        GraphFunctionAnalysisData result = Analyze(
            "sin(2x)+1",
            PerformAnalysisType.Range | PerformAnalysisType.Parity | PerformAnalysisType.Period);

        Assert.Equal("y ∈ [0, 2]", result.Range);
        Assert.Equal((int)FunctionParityType.None, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.Periodic, result.PeriodicityDirection);
        Assert.Equal("π", result.PeriodicityExpression);
        Assert.Equal(0, result.TooComplexFeatures);
    }

    [Fact]
    public void PolynomialZerosInterceptAndCriticalPointsAreSymbolic()
    {
        GraphFunctionAnalysisData result = Analyze(
            "x^2-1",
            PerformAnalysisType.InterceptionPointsWithXAndYAxis |
            PerformAnalysisType.CriticalPoints |
            PerformAnalysisType.Monotonicity);

        Assert.Equal("x = −1 ∨ x = 1", result.Zeros);
        Assert.Equal("y = −1", result.YIntercept);
        Assert.Equal("(0, −1)", Assert.Single(result.Minima));
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        Assert.Equal(
            new Dictionary<string, int>
            {
                ["(−∞, 0)"] = (int)FunctionMonotonicityType.Descending,
                ["(0, ∞)"] = (int)FunctionMonotonicityType.Ascending
            },
            result.MonotoneIntervals);
        Assert.Equal(0, result.TooComplexFeatures);
    }

    [Fact]
    public void AffineTrigonometricCriticalPointFamiliesAreSymbolic()
    {
        GraphFunctionAnalysisData result = Analyze(
            "sin(2x)+1",
            PerformAnalysisType.CriticalPoints | PerformAnalysisType.Monotonicity);

        Assert.Equal("(πn₁ + 3π/4, 0), n₁ ∈ ℤ", Assert.Single(result.Minima));
        Assert.Equal("(πn₁ + π/4, 2), n₁ ∈ ℤ", Assert.Single(result.Maxima));
        Assert.Equal("(π/2n₁, 1), n₁ ∈ ℤ", Assert.Single(result.InflectionPoints));
        Assert.Equal(2, result.MonotoneIntervals.Count);
        Assert.Contains((int)FunctionMonotonicityType.Ascending, result.MonotoneIntervals.Values);
        Assert.Contains((int)FunctionMonotonicityType.Descending, result.MonotoneIntervals.Values);
        Assert.All(
            result.MonotoneIntervals.Keys,
            interval => Assert.Contains("n₁ ∈ ℤ", interval, StringComparison.Ordinal));
        Assert.Equal(0, result.TooComplexFeatures);
    }

    [Fact]
    public void AffineRationalFunctionIsAnalyzedExactly()
    {
        GraphFunctionAnalysisData result = Analyze("2/(x-3)+4");

        Assert.Equal("x ≠ 3", result.Domain);
        Assert.Equal("y ∈ ℝ ∖ {4}", result.Range);
        Assert.Equal((int)FunctionParityType.None, result.Parity);
        Assert.Equal("x = 5/2", result.Zeros);
        Assert.Equal("y = 10/3", result.YIntercept);
        Assert.Empty(result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        Assert.Equal("x = 3", Assert.Single(result.VerticalAsymptotes));
        Assert.Equal("y = 4", Assert.Single(result.HorizontalAsymptotes));
        Assert.Empty(result.ObliqueAsymptotes);
        Assert.Equal(
            new Dictionary<string, int>
            {
                ["(−∞, 3)"] = (int)FunctionMonotonicityType.Descending,
                ["(3, ∞)"] = (int)FunctionMonotonicityType.Descending
            },
            result.MonotoneIntervals);
        Assert.Equal((int)FunctionPeriodicityType.NotPeriodic, result.PeriodicityDirection);
        Assert.Equal(0, result.TooComplexFeatures);
    }

    [Fact]
    public void WindowsCapturedTailAsymptotesKeepExactProofValues()
    {
        GraphFunctionAnalysisData constant = Analyze("3");
        Assert.Equal("y ∈ {3}", constant.Range);
        Assert.Equal("y = 3", Assert.Single(constant.HorizontalAsymptotes));
        Assert.Equal(
            (int)FunctionPeriodicityType.Periodic,
            constant.PeriodicityDirection);
        Assert.Empty(constant.PeriodicityExpression);

        GraphFunctionAnalysisData line = Analyze("x");
        Assert.Equal("y = x", Assert.Single(line.ObliqueAsymptotes));

        GraphFunctionAnalysisData absolute = Analyze("abs(2x-4)");
        Assert.Equal("y ∈ [0, ∞)", absolute.Range);
        Assert.Contains("y = 2x − 4", absolute.ObliqueAsymptotes);
        Assert.Contains("y = −2x + 4", absolute.ObliqueAsymptotes);
        Assert.Equal(0, constant.TooComplexFeatures);
        Assert.Equal(0, line.TooComplexFeatures);
        Assert.Equal(0, absolute.TooComplexFeatures);
    }

    [Fact]
    public void WindowsCapturedElementaryPrimitiveFormattingIsDeterministic()
    {
        GraphFunctionAnalysisData exponential = Analyze("exp(x)");
        Assert.Equal("y ∈ (0, ∞)", exponential.Range);
        Assert.Equal((int)FunctionParityType.None, exponential.Parity);
        Assert.Empty(exponential.Zeros);
        Assert.Equal("y = 1", exponential.YIntercept);
        Assert.Equal("y = 0", Assert.Single(exponential.HorizontalAsymptotes));
        Assert.Equal(
            (int)FunctionMonotonicityType.Ascending,
            Assert.Single(exponential.MonotoneIntervals).Value);

        GraphFunctionAnalysisData logarithm = Analyze("log(2x-4)");
        Assert.Equal("x > 2", logarithm.Domain);
        Assert.Equal("y ∈ ℝ", logarithm.Range);
        Assert.Equal("x = 5/2", logarithm.Zeros);
        Assert.Equal("x = 2", Assert.Single(logarithm.VerticalAsymptotes));
        Assert.Equal(
            new Dictionary<string, int>
            {
                ["(2, ∞)"] = (int)FunctionMonotonicityType.Ascending
            },
            logarithm.MonotoneIntervals);

        GraphFunctionAnalysisData hyperbolicSine = Analyze("sinh(x)");
        Assert.Equal("y ∈ ℝ", hyperbolicSine.Range);
        Assert.Equal((int)FunctionParityType.Odd, hyperbolicSine.Parity);
        Assert.Equal("(0, 0)", Assert.Single(hyperbolicSine.InflectionPoints));

        GraphFunctionAnalysisData hyperbolicCosine = Analyze("cosh(x)");
        Assert.Equal("y ∈ [1, ∞)", hyperbolicCosine.Range);
        Assert.Equal((int)FunctionParityType.Even, hyperbolicCosine.Parity);
        Assert.Equal("(0, 1)", Assert.Single(hyperbolicCosine.Minima));

        GraphFunctionAnalysisData oddRoot = Analyze("root(x,3)");
        Assert.Equal("y ∈ ℝ", oddRoot.Range);
        Assert.Equal((int)FunctionParityType.Odd, oddRoot.Parity);
        Assert.Equal("(0, 0)", Assert.Single(oddRoot.InflectionPoints));

        Assert.Equal(0, exponential.TooComplexFeatures);
        Assert.Equal(0, logarithm.TooComplexFeatures);
        Assert.Equal(0, hyperbolicSine.TooComplexFeatures);
        Assert.Equal(0, hyperbolicCosine.TooComplexFeatures);
        Assert.Equal(0, oddRoot.TooComplexFeatures);
    }

    [Fact]
    public void NegatedAndShiftedTrigonometricPrimitivesStayCertified()
    {
        GraphFunctionAnalysisData reflectedSine = Analyze("-sin(x)");
        Assert.Equal("y ∈ [−1, 1]", reflectedSine.Range);
        Assert.Equal((int)FunctionParityType.Odd, reflectedSine.Parity);
        Assert.Equal("2π", reflectedSine.PeriodicityExpression);
        Assert.Equal(0, reflectedSine.TooComplexFeatures);

        GraphFunctionAnalysisData shiftedTangent = Analyze("tan(2x-1)");
        Assert.Equal((int)FunctionParityType.None, shiftedTangent.Parity);
        Assert.Equal("y = tan(−1)", shiftedTangent.YIntercept);
        Assert.Equal(
            "(π/2n₁ + π/4 + 1/2, π/2n₁ + 3π/4 + 1/2), n₁ ∈ ℤ",
            Assert.Single(shiftedTangent.MonotoneIntervals).Key);
        Assert.Equal(0, shiftedTangent.TooComplexFeatures);
    }

    [Fact]
    public void ConstantTrigonometricIdentityUsesPointSetRangeFormatting()
    {
        GraphFunctionAnalysisData result = Analyze("sin(x)^2+cos(x)^2");

        Assert.Equal("y ∈ {1}", result.Range);
        Assert.Equal("y = 1", Assert.Single(result.HorizontalAsymptotes));
        Assert.Equal(
            (int)FunctionMonotonicityType.Constant,
            Assert.Single(result.MonotoneIntervals).Value);
        Assert.Equal((int)FunctionPeriodicityType.Periodic, result.PeriodicityDirection);
        Assert.Empty(result.PeriodicityExpression);
        Assert.Equal(0, result.TooComplexFeatures);
    }

    [Theory]
    [InlineData("asin(x)")]
    [InlineData("arcsin(x)")]
    public void InverseTrigonometricEndpointsAndInflectionAreExact(string formula)
    {
        GraphFunctionAnalysisData result = Analyze(formula);

        Assert.Equal("−1 ≤ x ≤ 1", result.Domain);
        Assert.Equal("y ∈ [−π/2, π/2]", result.Range);
        Assert.Equal((int)FunctionParityType.Odd, result.Parity);
        Assert.Equal("x = 0", result.Zeros);
        Assert.Equal("y = 0", result.YIntercept);
        Assert.Equal("(−1, −π/2)", Assert.Single(result.Minima));
        Assert.Equal("(1, π/2)", Assert.Single(result.Maxima));
        Assert.Equal("(0, 0)", Assert.Single(result.InflectionPoints));
        Assert.Empty(result.VerticalAsymptotes);
        Assert.Empty(result.HorizontalAsymptotes);
        Assert.Empty(result.ObliqueAsymptotes);
        Assert.Equal(
            (int)FunctionMonotonicityType.Ascending,
            Assert.Single(result.MonotoneIntervals).Value);
        Assert.Equal((int)FunctionPeriodicityType.NotPeriodic, result.PeriodicityDirection);
        Assert.Equal(0, result.TooComplexFeatures);
    }

    [Theory]
    [InlineData("asin(x)", "arcsin(x)")]
    [InlineData("acos(x)", "arccos(x)")]
    [InlineData("atan(x)", "arctan(x)")]
    [InlineData("sign(x)", "sgn(x)")]
    [InlineData("ceil(x)", "ceiling(x)")]
    public void FunctionAliasesShareCanonicalSymbolicAnalysis(string canonical, string alias)
    {
        AssertAnalysisEqual(Analyze(canonical), Analyze(alias));
    }

    [Fact]
    public void LatexArcFunctionNameUsesCanonicalSymbolicAnalysis()
    {
        GraphFunctionAnalysisData expected = Analyze("asin(x)");
        GraphFunctionAnalysisData actual = Analyze(
            @"\arcsin(x)",
            PerformAnalysisType.All,
            FormatType.Latex);

        AssertAnalysisEqual(expected, actual);
    }

    [Fact]
    public void PartialPowerUsesTheWindowsCalculatorCompatibilityContract()
    {
        GraphFunctionAnalysisData result = Analyze("sin(x)^tan(x)");

        Assert.Equal(
            "x ∈ (2πn₁, 2πn₁ + π/2) ∪ " +
            "(2πn₁ + π/2, 2πn₁ + π), n₁ ∈ ℤ",
            result.Domain);
        Assert.Empty(result.Zeros);
        Assert.Empty(result.YIntercept);
        Assert.Empty(result.HorizontalAsymptotes);
        int expected = FeatureBit(AnalysisType.Range) |
                       FeatureBit(AnalysisType.Parity) |
                       FeatureBit(AnalysisType.Period) |
                       FeatureBit(AnalysisType.Minima) |
                       FeatureBit(AnalysisType.Maxima) |
                       FeatureBit(AnalysisType.InflectionPoints) |
                       FeatureBit(AnalysisType.VerticalAsymptotes) |
                       FeatureBit(AnalysisType.ObliqueAsymptotes) |
                       FeatureBit(AnalysisType.Monotonicity);
        Assert.Equal(expected, result.TooComplexFeatures);
    }

    private static GraphFunctionAnalysisData Analyze(
        string formula,
        PerformAnalysisType requested = PerformAnalysisType.All,
        FormatType format = FormatType.Linear)
    {
        IMathSolver solver = MathSolver.CreateMathSolver();
        solver.ParsingOptions().SetFormatType(format);
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

    private static void AssertAnalysisEqual(
        GraphFunctionAnalysisData expected,
        GraphFunctionAnalysisData actual)
    {
        Assert.Equal(expected.Domain, actual.Domain);
        Assert.Equal(expected.Range, actual.Range);
        Assert.Equal(expected.Parity, actual.Parity);
        Assert.Equal(expected.PeriodicityDirection, actual.PeriodicityDirection);
        Assert.Equal(expected.PeriodicityExpression, actual.PeriodicityExpression);
        Assert.Equal(expected.Zeros, actual.Zeros);
        Assert.Equal(expected.YIntercept, actual.YIntercept);
        Assert.Equal(expected.Minima, actual.Minima);
        Assert.Equal(expected.Maxima, actual.Maxima);
        Assert.Equal(expected.InflectionPoints, actual.InflectionPoints);
        Assert.Equal(expected.VerticalAsymptotes, actual.VerticalAsymptotes);
        Assert.Equal(expected.HorizontalAsymptotes, actual.HorizontalAsymptotes);
        Assert.Equal(expected.ObliqueAsymptotes, actual.ObliqueAsymptotes);
        Assert.Equal(expected.MonotoneIntervals.Count, actual.MonotoneIntervals.Count);
        foreach ((string interval, int direction) in expected.MonotoneIntervals)
        {
            Assert.True(actual.MonotoneIntervals.TryGetValue(interval, out int actualDirection));
            Assert.Equal(direction, actualDirection);
        }

        Assert.Equal(expected.TooComplexFeatures, actual.TooComplexFeatures);
    }

    private static int FeatureBit(AnalysisType type) => 1 << (int)type;
}
