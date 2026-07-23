using Graphing;
using Graphing.Analyzer;

namespace GraphingTests;

public sealed class ExactCoefficientCompatibilityFormatterTests
{
    public static TheoryData<string, string, string, string> GoldenFields => new()
    {
        { "linear-pi-slope", "pi*x", "Oblique", "y = πx" },
        { "linear-e-slope", "e*x", "Oblique", "y = ex" },
        { "linear-sqrt-two-slope", "sqrt(2)*x", "Oblique", "y = sqrt(2)x" },
        { "linear-pi-intercept", "x+pi", "Zeros", "x = −π" },
        { "linear-symbolic-affine", "pi*x+e", "Zeros", "x = −e/π" },
        {
            "quadratic-pi-roots",
            "x^2-pi",
            "Zeros",
            "x = −sqrt(π) ∨ x = sqrt(π)"
        },
        { "rational-symbolic-pole", "1/(x-pi)", "Range", "y ∈ ℝ ∖ {0}" },
        { "rational-symbolic-pole", "1/(x-pi)", "YIntercept", "y = −1/π" },
        {
            "rational-symbolic-numerator",
            "pi/(x-1)+e",
            "Zeros",
            "x = (e − π)/e"
        },
        {
            "rational-symbolic-numerator",
            "pi/(x-1)+e",
            "YIntercept",
            "y = e − π"
        },
        { "sine-phase-pi", "sin(x+pi)", "Zeros", "x = πn₁, n₁ ∈ ℤ" },
        {
            "sine-phase-pi",
            "sin(x+pi)",
            "Maxima",
            "(2πn₁ + 3π/2, 1), n₁ ∈ ℤ"
        },
        {
            "sine-phase-pi",
            "sin(x+pi)",
            "Monotonicity",
            "Ascending: (2πn₁ + π/2, 2πn₁ + 3π/2), n₁ ∈ ℤ; " +
            "Descending: (2πn₁ + 3π/2, 2πn₁ + 5π/2), n₁ ∈ ℤ"
        },
        {
            "sine-phase-half-pi",
            "sin(x+pi/2)",
            "Zeros",
            "x = πn₁ + π/2, n₁ ∈ ℤ"
        },
        {
            "sine-phase-half-pi",
            "sin(x+pi/2)",
            "InflectionPoints",
            "(πn₁ + π/2, 0), n₁ ∈ ℤ"
        },
        {
            "cosine-phase-half-pi",
            "cos(x-pi/2)",
            "Zeros",
            "x = πn₁, n₁ ∈ ℤ"
        },
        {
            "cosine-phase-half-pi",
            "cos(x-pi/2)",
            "InflectionPoints",
            "(πn₁, 0), n₁ ∈ ℤ"
        },
        {
            "tangent-phase-half-pi",
            "tan(x+pi/2)",
            "Zeros",
            "x = πn₁ + π/2, n₁ ∈ ℤ"
        },
        {
            "tangent-phase-half-pi",
            "tan(x+pi/2)",
            "VerticalAsymptotes",
            string.Empty
        },
        {
            "sine-symbolic-offset",
            "sin(x)+pi",
            "Range",
            "y ∈ [π − 1, π + 1]"
        },
        {
            "cosine-symbolic-offset",
            "e+cos(x)",
            "YIntercept",
            "y = e + 1"
        },
        {
            "sine-symbolic-frequency",
            "sin(pi*x)",
            "Zeros",
            "x = n₁, n₁ ∈ ℤ"
        },
        {
            "sine-symbolic-frequency",
            "sin(pi*x)",
            "Minima",
            "(2n₁ − 1/2, −1), n₁ ∈ ℤ"
        },
        {
            "sine-symbolic-frequency",
            "sin(pi*x)",
            "InflectionPoints",
            "(n₁, 0), n₁ ∈ ℤ"
        },
        {
            "cosine-symbolic-frequency",
            "cos(e*x)",
            "Zeros",
            "x = πn₁/e + π/2e, n₁ ∈ ℤ"
        },
        {
            "cosine-symbolic-frequency",
            "cos(e*x)",
            "Maxima",
            "(2πn₁/e, 1), n₁ ∈ ℤ"
        },
        {
            "cosine-symbolic-frequency",
            "cos(e*x)",
            "Monotonicity",
            "Ascending: (2πn₁/e + π/e, 2πn₁/e + 2π/e), n₁ ∈ ℤ; " +
            "Descending: (2πn₁/e, 2πn₁/e + π/e), n₁ ∈ ℤ"
        },
        {
            "zero-power-symbolic-affine",
            "0^(pi*x-1)",
            "Domain",
            "x > 1/π"
        },
        {
            "zero-power-symbolic-affine",
            "0^(pi*x-1)",
            "Zeros",
            "x > 1/π"
        },
        {
            "zero-power-symbolic-affine",
            "0^(pi*x-1)",
            "Monotonicity",
            "Constant: (1/π, ∞)"
        }
    };

    [Theory]
    [MemberData(nameof(GoldenFields))]
    public void OrderedExactCoefficientFamiliesUseCanonicalPublicFormatting(
        string id,
        string formula,
        string field,
        string expected)
    {
        GraphFunctionAnalysisData result = Analyze(formula);

        string actual = Field(result, field);
        Assert.True(
            string.Equals(expected, actual, StringComparison.Ordinal),
            $"{id}/{field}: expected '{expected}', actual '{actual}'.");
        int expectedTooComplex = id == "tangent-phase-half-pi"
            ? FeatureBit(AnalysisType.InflectionPoints) |
              FeatureBit(AnalysisType.VerticalAsymptotes)
            : 0;
        Assert.Equal(expectedTooComplex, result.TooComplexFeatures);
    }

    private static string Field(GraphFunctionAnalysisData result, string field)
    {
        return field switch
        {
            "Domain" => result.Domain,
            "Range" => result.Range,
            "Zeros" => result.Zeros,
            "YIntercept" => result.YIntercept,
            "Minima" => string.Join("; ", result.Minima),
            "Maxima" => string.Join("; ", result.Maxima),
            "InflectionPoints" => string.Join("; ", result.InflectionPoints),
            "VerticalAsymptotes" => string.Join("; ", result.VerticalAsymptotes),
            "HorizontalAsymptotes" => string.Join("; ", result.HorizontalAsymptotes),
            "Oblique" => string.Join("; ", result.ObliqueAsymptotes),
            "Period" => result.PeriodicityExpression,
            "Monotonicity" => string.Join(
                "; ",
                result.MonotoneIntervals.Select(pair =>
                        $"{(FunctionMonotonicityType)pair.Value}: {pair.Key}")
                    .Order(StringComparer.Ordinal)),
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null)
        };
    }

    private static int FeatureBit(AnalysisType type)
    {
        return type switch
        {
            AnalysisType.InflectionPoints => 256,
            AnalysisType.VerticalAsymptotes => 512,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    private static GraphFunctionAnalysisData Analyze(string formula)
    {
        IMathSolver solver = MathSolver.CreateMathSolver();
        solver.EvalOptions().SetTrigUnitMode(EvalTrigUnitMode.Radians);
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
