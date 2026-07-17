using System.Collections.Immutable;
using Graphing;
using Graphing.Analyzer;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class ExactAmplitudeLinearDriftTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Fact]
    public void PiAmplitudeMatchesLiveWindowsAnalysisExactly()
    {
        GraphFunctionAnalysisData result = AnalyzePublic("x+pi*sin(x)");

        Assert.Equal("x ∈ ℝ", result.Domain);
        Assert.Equal("y ∈ ℝ", result.Range);
        Assert.Empty(result.Zeros);
        Assert.Equal("y = 0", result.YIntercept);
        Assert.Equal(
            "(2πn₁ + arccos(1/π) + π, " +
            "2πn₁ − sqrt(π^2 − 1) + arccos(1/π) + π), n₁ ∈ ℤ",
            Assert.Single(result.Minima));
        Assert.Equal(
            "(2πn₁ − arccos(1/π) + π, " +
            "2πn₁ + sqrt(π^2 − 1) − arccos(1/π) + π), n₁ ∈ ℤ",
            Assert.Single(result.Maxima));
        Assert.Equal("(πn₁, πn₁), n₁ ∈ ℤ", Assert.Single(result.InflectionPoints));
        Assert.Empty(result.VerticalAsymptotes);
        Assert.Empty(result.HorizontalAsymptotes);
        Assert.Empty(result.ObliqueAsymptotes);
        Assert.Equal((int)FunctionParityType.Odd, result.Parity);
        Assert.Empty(result.MonotoneIntervals);
        Assert.Equal((int)FunctionPeriodicityType.Unknown, result.PeriodicityDirection);
        Assert.Empty(result.PeriodicityExpression);
        Assert.Equal(
            FeatureBit(AnalysisType.Zeros) |
            FeatureBit(AnalysisType.Period) |
            FeatureBit(AnalysisType.Monotonicity),
            result.TooComplexFeatures);
    }

    [Fact]
    public void ExactScalarOrderUsesCertifiedRationalEnclosures()
    {
        ExactScalar pi = Scalar(Named("pi"));
        AssertComparison(pi, 3, 1);
        AssertComparison(pi, 4, -1);
        AssertComparison(pi, new BigRational(333, 106), 1);
        AssertComparison(pi, new BigRational(355, 113), -1);

        ExactScalar e = Scalar(Named("e"));
        AssertComparison(e, 2, 1);
        AssertComparison(e, 3, -1);
        AssertComparison(e, new BigRational(19, 7), 1);
        AssertComparison(e, new BigRational(87, 32), -1);

        ExactScalar squareRootTwo = Scalar(Function("sqrt", Number(2)));
        AssertComparison(squareRootTwo, 1, 1);
        AssertComparison(squareRootTwo, 2, -1);
        AssertComparison(squareRootTwo, new BigRational(7, 5), 1);
        AssertComparison(squareRootTwo, new BigRational(10, 7), -1);

        ExactScalar product = Scalar(Multiply(Named("pi"), Function("sqrt", Number(2))));
        AssertComparison(product, 4, 1);
        AssertComparison(product, 5, -1);

        ExactScalar quotient = Scalar(Divide(Named("pi"), Named("e")));
        AssertComparison(quotient, 1, 1);
        AssertComparison(quotient, 2, -1);

        ExactScalar unsupported = Scalar(Function("exp", Number(1)));
        Assert.False(unsupported.TryCompareAbsoluteTo(
            BigRational.One,
            new ResourceBudget(),
            out _));
    }

    [Fact]
    public void ExactProductsAndQuotientsSelectOnlyProvedDerivativeRegimes()
    {
        InputExpression x = Variable();
        InputExpression sine = Function("sin", x);
        InputExpression[] oscillatory =
        [
            Add(x, Multiply(Named("e"), sine)),
            Add(x, Multiply(Add(Named("pi"), Named("e")), sine)),
            Add(x, Multiply(Divide(Named("pi"), Named("e")), sine)),
            Add(
                Multiply(Number(4), x),
                Multiply(Multiply(Named("pi"), Function("sqrt", Number(2))), sine))
        ];

        foreach (InputExpression expression in oscillatory)
        {
            AnalysisRequest request = Request(
                expression,
                AnalysisFeatures.Range |
                AnalysisFeatures.Minima |
                AnalysisFeatures.Maxima |
                AnalysisFeatures.InflectionPoints |
                AnalysisFeatures.Monotonicity);
            AnalysisReport report = AnalysisEngine.Analyze(request);

            Assert.Equal(ProofState.Proved, report.Range.State);
            Assert.Single(Proved(report.Minima));
            Assert.Single(Proved(report.Maxima));
            Assert.Equal(ProofState.Proved, report.InflectionPoints.State);
            Assert.Equal(ProofState.Unknown, report.Monotonicity.State);
            AssertReplay(request, report.Expression!, report.Minima);
            AssertReplay(request, report.Expression!, report.Maxima);
        }

        InputExpression[] dominated =
        [
            Add(Multiply(Number(2), x), Multiply(Function("sqrt", Number(2)), sine)),
            Add(Multiply(Number(4), x), Multiply(Named("pi"), sine)),
            Add(x, Multiply(Divide(Named("pi"), Number(4)), sine)),
            Add(x, Multiply(Subtract(Named("pi"), Named("e")), sine)),
            Add(
                Multiply(Number(5), x),
                Multiply(Multiply(Named("pi"), Function("sqrt", Number(2))), sine))
        ];

        foreach (InputExpression expression in dominated)
        {
            AnalysisRequest request = Request(
                expression,
                AnalysisFeatures.Zeros |
                AnalysisFeatures.Minima |
                AnalysisFeatures.Maxima |
                AnalysisFeatures.Monotonicity);
            AnalysisReport report = AnalysisEngine.Analyze(request);

            Assert.Equal(ProofState.Proved, report.Zeros.State);
            Assert.Empty(Proved(report.Minima));
            Assert.Empty(Proved(report.Maxima));
            Assert.Equal(
                Monotonicity.Increasing,
                Assert.Single(Proved(report.Monotonicity)).Direction);
            AssertReplay(request, report.Expression!, report.Monotonicity);
        }
    }

    [Fact]
    public void AmplitudeCommutationAndNegativeFrequencyAreMetamorphic()
    {
        AnalysisReport left = AnalyzeInternal(
            Add(Variable(), Multiply(Named("pi"), Function("sin", Variable()))));
        AnalysisReport right = AnalyzeInternal(
            Add(Variable(), Multiply(Function("sin", Variable()), Named("pi"))));
        AssertSameClaims(left, right);

        InputExpression sharedSine = Function("sin", Variable());
        AnalysisReport combined = AnalyzeInternal(Add(
            Add(Variable(), Multiply(Named("pi"), sharedSine)),
            Multiply(sharedSine, Named("pi"))));
        AnalysisReport scaled = AnalyzeInternal(Add(
            Variable(),
            Multiply(
                Multiply(Number(2), Named("pi")),
                Function("sin", Variable()))));
        AssertSameClaims(combined, scaled);

        AnalysisReport cancelled = AnalyzeInternal(Add(
            Add(
                Add(Variable(), Multiply(Named("pi"), sharedSine)),
                Negate(Multiply(Named("pi"), sharedSine))),
            sharedSine));
        AnalysisReport rationalBoundary = AnalyzeInternal(Add(
            Variable(),
            Function("sin", Variable())));
        AssertSameClaims(cancelled, rationalBoundary);

        AnalysisReport negativeAmplitude = AnalyzeInternal(
            Subtract(Variable(), Multiply(Named("pi"), Function("sin", Variable()))));
        AnalysisReport negativeFrequency = AnalyzeInternal(
            Add(
                Variable(),
                Multiply(
                    Named("pi"),
                    Function("sin", Negate(Variable())))));
        AssertSameClaims(negativeAmplitude, negativeFrequency);

        Assert.Single(Proved(negativeAmplitude.Minima));
        Assert.Single(Proved(negativeAmplitude.Maxima));
        Assert.Equal(FunctionParity.Odd, Proved(negativeAmplitude.Parity));
    }

    [Fact]
    public void ExactCosinePhaseVariantsRetainExactValuesAndProofs()
    {
        InputExpression expression = Add(
            Variable(),
            Multiply(
                Named("pi"),
                Function("cos", Add(Variable(), Number(1)))));
        AnalysisRequest request = Request(expression, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        Assert.Equal(FunctionParity.Neither, Proved(report.Parity));
        ExactReal intercept = Proved(report.YIntercept).Value!;
        Assert.Contains("pi:1:0", ExactRealCanonical.Format(intercept), StringComparison.Ordinal);
        Assert.Contains("cos", ExactRealCanonical.Format(intercept), StringComparison.Ordinal);
        Assert.Single(Proved(report.Minima));
        Assert.Single(Proved(report.Maxima));
        Assert.Single(Proved(report.InflectionPoints));
        Assert.Equal(ProofState.Unknown, report.Monotonicity.State);
        AssertReplay(request, report.Expression!, report.YIntercept);
        AssertReplay(request, report.Expression!, report.Minima);
        AssertReplay(request, report.Expression!, report.Maxima);

        AnalysisReport negativeFrequency = AnalyzeInternal(Add(
            Variable(),
            Multiply(
                Named("pi"),
                Function(
                    "cos",
                    Subtract(Negate(Variable()), Number(1))))));
        AssertSameClaims(report, negativeFrequency);

        AnalysisReport negativeAmplitude = AnalyzeInternal(Subtract(
            Variable(),
            Multiply(
                Named("pi"),
                Function("cos", Add(Variable(), Number(1))))));
        Assert.Single(Proved(negativeAmplitude.Minima));
        Assert.Single(Proved(negativeAmplitude.Maxima));
        Assert.Equal(FunctionParity.Neither, Proved(negativeAmplitude.Parity));
    }

    [Fact]
    public void UnsupportedExactComparisonKeepsIndependentProofs()
    {
        InputExpression expression = Add(
            Variable(),
            Multiply(Function("exp", Number(1)), Function("sin", Variable())));
        AnalysisRequest request = Request(expression, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        Assert.Equal(ProofState.Proved, report.Domain.State);
        Assert.Equal(ProofState.Proved, report.Range.State);
        Assert.Equal(FunctionParity.Odd, Proved(report.Parity));
        Assert.Equal(ProofState.Unknown, report.Zeros.State);
        Assert.Equal(ProofState.Proved, report.YIntercept.State);
        Assert.Equal(ProofState.Unknown, report.Minima.State);
        Assert.Equal(ProofState.Unknown, report.Maxima.State);
        Assert.Equal(ProofState.Proved, report.InflectionPoints.State);
        Assert.Equal(ProofState.Proved, report.VerticalAsymptotes.State);
        Assert.Equal(ProofState.Proved, report.HorizontalAsymptotes.State);
        Assert.Equal(ProofState.Proved, report.ObliqueAsymptotes.State);
        Assert.Equal(ProofState.Unknown, report.Monotonicity.State);
        Assert.Equal(ProofState.Proved, report.Period.State);
        AssertReplay(request, report.Expression!, report.Range);
        AssertReplay(request, report.Expression!, report.InflectionPoints);
    }

    [Fact]
    public void ExactAmplitudeCertificatesReplayAndRejectMutations()
    {
        InputExpression expression = Add(
            Variable(),
            Multiply(Named("pi"), Function("sin", Variable())));
        AnalysisRequest request = Request(
            expression,
            AnalysisFeatures.Minima | AnalysisFeatures.Maxima);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        AssertReplay(request, report.Expression!, report.Minima);
        AssertReplay(request, report.Expression!, report.Maxima);

        var certificate = Assert.IsType<TheoremProofCertificate>(report.Minima.Certificate);
        TheoremProofCertificate changedParameters = certificate with
        {
            Parameters = certificate.Parameters.SetItem(
                0,
                certificate.Parameters[0] + ":changed-bound")
        };
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<ImmutableArray<FeaturePoint>>.Proved(
                Proved(report.Minima),
                changedParameters)));

        var point = Assert.IsType<IntegerAffineFeaturePoint>(Assert.Single(Proved(report.Minima)));
        ImmutableArray<FeaturePoint> changedPoints =
        [
            point with { YOffset = new RationalReal(BigRational.Zero) }
        ];
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<ImmutableArray<FeaturePoint>>.Proved(
                changedPoints,
                report.Minima.Certificate!)));
    }

    [Fact]
    public void OversizedExactAmplitudeReturnsDeterministicBudgetUnknown()
    {
        InputExpression huge = Power(Number(2), Number(int.MaxValue));
        InputExpression expression = Add(
            Variable(),
            Multiply(huge, Function("sin", Variable())));

        AnalysisReport report = AnalysisEngine.Analyze(Request(
            expression,
            AnalysisFeatures.Range | AnalysisFeatures.Minima));

        Assert.Equal(ProofState.Unknown, report.Range.State);
        Assert.Equal(UnknownReason.BudgetExceeded, report.Range.UnknownReason);
        Assert.Equal(ProofState.Unknown, report.Minima.State);
        Assert.Equal(UnknownReason.BudgetExceeded, report.Minima.UnknownReason);
    }

    private static AnalysisReport AnalyzeInternal(InputExpression expression) =>
        AnalysisEngine.Analyze(Request(expression, AnalysisFeatures.All));

    private static void AssertSameClaims(AnalysisReport left, AnalysisReport right)
    {
        Assert.Equal(ClaimCanonical.ForObject(Proved(left.Range)), ClaimCanonical.ForObject(Proved(right.Range)));
        Assert.Equal(ClaimCanonical.ForObject(Proved(left.Parity)), ClaimCanonical.ForObject(Proved(right.Parity)));
        Assert.Equal(ClaimCanonical.ForObject(Proved(left.Minima)), ClaimCanonical.ForObject(Proved(right.Minima)));
        Assert.Equal(ClaimCanonical.ForObject(Proved(left.Maxima)), ClaimCanonical.ForObject(Proved(right.Maxima)));
        Assert.Equal(
            ClaimCanonical.ForObject(Proved(left.InflectionPoints)),
            ClaimCanonical.ForObject(Proved(right.InflectionPoints)));
    }

    private static void AssertComparison(ExactScalar scalar, int rational, int expected)
    {
        AssertComparison(scalar, new BigRational(rational), expected);
    }

    private static void AssertComparison(
        ExactScalar scalar,
        BigRational rational,
        int expected)
    {
        Assert.True(scalar.TryCompareAbsoluteTo(
            rational,
            new ResourceBudget(),
            out int actual));
        Assert.Equal(expected, actual);
    }

    private static ExactScalar Scalar(InputExpression expression)
    {
        SemanticExpression semantic = new SemanticGraphBuilder(new ResourceBudget()).Build(expression);
        Assert.True(ExactScalar.TryCreate(semantic.Value, new ResourceBudget(), out ExactScalar scalar));
        return scalar;
    }

    private static void AssertReplay<T>(
        AnalysisRequest request,
        SemanticExpression expression,
        ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.True(CertificateChecker.Check(request, expression, outcome));
    }

    private static T Proved<T>(ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        return Assert.IsAssignableFrom<T>(outcome.Value);
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

    private static int FeatureBit(AnalysisType type) => type switch
    {
        AnalysisType.Zeros => 16,
        AnalysisType.Period => 8,
        AnalysisType.Monotonicity => 4096,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static AnalysisRequest Request(
        InputExpression expression,
        AnalysisFeatures features) =>
        new(expression, features, AngleUnit.Radians, "x", static () => true);

    private static InputExpression Variable() => InputExpression.Variable("x", Source);

    private static InputExpression Named(string name) => InputExpression.Variable(name, Source);

    private static InputExpression Number(int value) => InputExpression.Number(new BigRational(value), Source);

    private static InputExpression Add(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Add, left, right, Source);

    private static InputExpression Subtract(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Subtract, left, right, Source);

    private static InputExpression Multiply(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Multiply, left, right, Source);

    private static InputExpression Divide(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Divide, left, right, Source);

    private static InputExpression Power(InputExpression basis, InputExpression exponent) =>
        InputExpression.Binary(InputExpressionKind.Power, basis, exponent, Source);

    private static InputExpression Negate(InputExpression value) =>
        InputExpression.Unary(InputExpressionKind.Negate, value, Source);

    private static InputExpression Function(string name, params InputExpression[] arguments) =>
        InputExpression.Function(name, arguments.ToImmutableArray(), Source);
}
