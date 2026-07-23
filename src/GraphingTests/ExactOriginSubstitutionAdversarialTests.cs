using System.Collections.Immutable;
using Graphing;
using Graphing.Analyzer;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class ExactOriginSubstitutionAdversarialTests
{
    private const int YInterceptTooComplexBit = 32;
    private static readonly SourceRange Source = new(0, 1);

    [Fact]
    public void MixedSymbolicTrigonometryPreservesItsExactOriginValue()
    {
        AssertPublicYIntercept(
            "2*sin(x+sqrt(x+1))-cos(x)+tan(x)",
            "y = 2sin(1) − 1");

        InputExpression x = Variable();
        InputExpression expression = Add(
            Subtract(
                Multiply(
                    Number(2),
                    Sin(Add(x, Sqrt(Add(x, Number(1)))))),
                Cos(x)),
            Tan(x));

        AssertExactOrigin(expression, expectedHasValue: true);
    }

    [Theory]
    [InlineData((int)EvalTrigUnitMode.Radians, "y = 2sin(1) − 1")]
    [InlineData((int)EvalTrigUnitMode.Degrees, "y = 2sin(π/180) − 1")]
    [InlineData((int)EvalTrigUnitMode.Grads, "y = 2sin(π/200) − 1")]
    public void MixedSymbolicTrigonometryFormatsTheSelectedAngleUnitExactly(
        int angleUnitValue,
        string expected)
    {
        AssertPublicYIntercept(
            "2*sin(x+sqrt(x+1))-cos(x)+tan(x)",
            expected,
            (EvalTrigUnitMode)angleUnitValue);
    }

    [Fact]
    public void NonPerfectRadicalPreservesItsExactOriginValue()
    {
        AssertPublicYIntercept(
            "sqrt(x+2)+cos(x)-1",
            "y = sqrt(2)");

        InputExpression x = Variable();
        InputExpression expression = Subtract(
            Add(Sqrt(Add(x, Number(2))), Cos(x)),
            Number(1));

        AssertExactOrigin(expression, expectedHasValue: true);
    }

    [Fact]
    public void NestedCompositionsReduceExactlyAtTheOrigin()
    {
        AssertPublicYIntercept(
            "cos(sqrt(x+1)-1)+tan(sin(x))",
            "y = 1");

        InputExpression x = Variable();
        InputExpression expression = Add(
            Cos(Subtract(Sqrt(Add(x, Number(1))), Number(1))),
            Tan(Sin(x)));

        AssertExactOrigin(expression, expectedHasValue: true, expectedCanonical: "q:1");
    }

    [Fact]
    public void CancelledReciprocalGuardStillMakesTheOriginUndefined()
    {
        AssertPublicYIntercept(
            "0*(1/x)+sqrt(x+1)",
            string.Empty);

        InputExpression x = Variable();
        InputExpression expression = Add(
            Multiply(Number(0), Divide(Number(1), x)),
            Sqrt(Add(x, Number(1))));

        AssertExactOrigin(expression, expectedHasValue: false);
    }

    [Fact]
    public void CancelledTrigonometricRatioGuardStillMakesTheOriginUndefined()
    {
        AssertPublicYIntercept(
            "sin(x)/sin(x)+sqrt(x+1)",
            string.Empty);

        InputExpression x = Variable();
        InputExpression sine = Sin(x);
        InputExpression expression = Add(
            Divide(sine, sine),
            Sqrt(Add(x, Number(1))));

        AssertExactOrigin(expression, expectedHasValue: false);
    }

    [Theory]
    [InlineData(
        "sum(log10(x+10),pow(x+2,2),root(x+4),min(x+1,2),max(x+1,2))",
        "y = 10")]
    [InlineData(
        "plus(minus(power(x+2,2),divide(x+4,2)),times(cos(x),root(x+1)))",
        "y = 3")]
    public void PublicOperatorFunctionVocabularyReachesExactOriginSubstitution(
        string formula,
        string expected)
    {
        AssertPublicYIntercept(formula, expected);
    }

    [Fact]
    public void MinimumIntegralExponentAvoidsUnboundedExpansion()
    {
        InputExpression x = Variable();
        InputExpression bounded = Add(
            Power(Number(-1), Number(int.MinValue)),
            Tan(Sin(x)));
        var boundedRequest = new AnalysisRequest(
            bounded,
            AnalysisFeatures.YIntercept,
            AngleUnit.Radians,
            "x",
            static () => true);
        SemanticExpression boundedSemantic = new SemanticGraphBuilder(
            new ResourceBudget()).Build(bounded);
        Assert.True(ExactOriginAnalyzer.TryAnalyze(
            boundedRequest,
            boundedSemantic,
            AnalysisFeatures.YIntercept,
            new ResourceBudget(),
            out ProofOutcome<OptionalValue<ExactReal>> boundedOutcome));
        Assert.Equal(
            "q:1",
            ExactRealCanonical.Format(boundedOutcome.Value!.Value!));
        Assert.True(CertificateChecker.Check(
            boundedRequest,
            boundedSemantic,
            boundedOutcome));
        AnalysisReport boundedIntegrated = AnalysisEngine.Analyze(boundedRequest);
        Assert.Equal(ProofState.Proved, boundedIntegrated.YIntercept.State);
        Assert.Equal(
            "q:1",
            ExactRealCanonical.Format(boundedIntegrated.YIntercept.Value!.Value!));
        Assert.IsType<ExactOriginProofCertificate>(
            boundedIntegrated.YIntercept.Certificate);

        InputExpression oversized = Add(
            Power(Add(x, Number(2)), Number(int.MinValue)),
            Tan(Sin(x)));
        var request = new AnalysisRequest(
            oversized,
            AnalysisFeatures.YIntercept,
            AngleUnit.Radians,
            "x",
            static () => true);
        SemanticExpression semantic = new SemanticGraphBuilder(
            new ResourceBudget()).Build(oversized);

        Assert.Throws<BudgetExceededException>(() =>
            ExactOriginAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.YIntercept,
                new ResourceBudget(),
                out ProofOutcome<OptionalValue<ExactReal>> _));
    }

    private static void AssertPublicYIntercept(
        string formula,
        string expected,
        EvalTrigUnitMode angleUnit = EvalTrigUnitMode.Radians)
    {
        GraphFunctionAnalysisData result = AnalyzePublic(formula, angleUnit);

        Assert.Equal(expected, result.YIntercept);
        Assert.Equal(0, result.TooComplexFeatures & YInterceptTooComplexBit);
    }

    private static void AssertExactOrigin(
        InputExpression expression,
        bool expectedHasValue,
        string? expectedCanonical = null)
    {
        var request = new AnalysisRequest(
            expression,
            AnalysisFeatures.YIntercept,
            AngleUnit.Radians,
            "x",
            static () => true);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        Assert.Equal(ProofState.Proved, report.YIntercept.State);
        OptionalValue<ExactReal> intercept = report.YIntercept.Value!;
        Assert.Equal(expectedHasValue, intercept.HasValue);
        if (expectedCanonical is not null)
        {
            Assert.True(intercept.HasValue);
            Assert.Equal(expectedCanonical, ExactRealCanonical.Format(intercept.Value!));
        }

        var certificate = Assert.IsType<ExactOriginProofCertificate>(
            report.YIntercept.Certificate);
        Assert.Equal(ExactOriginAnalyzer.Rule, certificate.Rule);
        Assert.NotNull(report.Expression);
        Assert.Equal(
            report.Expression.DefinedWhen.Canonical,
            certificate.DefinednessCanonical);
        Assert.True(CertificateChecker.Check(
            request,
            report.Expression,
            report.YIntercept));
    }

    private static GraphFunctionAnalysisData AnalyzePublic(
        string formula,
        EvalTrigUnitMode angleUnit)
    {
        IMathSolver solver = MathSolver.CreateMathSolver();
        solver.EvalOptions().SetTrigUnitMode(angleUnit);
        IExpression expression = solver.ParseInput(formula, out int errorCode, out int errorType)
            ?? throw new InvalidOperationException($"Parse failed: {errorCode}/{errorType}");
        IGraph graph = solver.CreateGrapher();
        Assert.NotNull(graph.TryInitialize(expression));
        IGraphAnalyzer analyzer = graph.GetAnalyzer();
        Assert.True(analyzer.CanFunctionAnalysisBePerformed(out bool variableIsNotX));
        Assert.False(variableIsNotX);
        Assert.Equal(
            GraphStatus.Ok,
            analyzer.PerformFunctionAnalysis(
                (uint)PerformAnalysisType.InterceptionPointsWithXAndYAxis));
        return solver.Analyze(analyzer);
    }

    private static InputExpression Variable()
    {
        return InputExpression.Variable("x", Source);
    }

    private static InputExpression Number(int value)
    {
        return InputExpression.Number(new BigRational(value), Source);
    }

    private static InputExpression Add(InputExpression left, InputExpression right)
    {
        return InputExpression.Binary(InputExpressionKind.Add, left, right, Source);
    }

    private static InputExpression Subtract(InputExpression left, InputExpression right)
    {
        return InputExpression.Binary(InputExpressionKind.Subtract, left, right, Source);
    }

    private static InputExpression Multiply(InputExpression left, InputExpression right)
    {
        return InputExpression.Binary(InputExpressionKind.Multiply, left, right, Source);
    }

    private static InputExpression Divide(InputExpression left, InputExpression right)
    {
        return InputExpression.Binary(InputExpressionKind.Divide, left, right, Source);
    }

    private static InputExpression Power(InputExpression basis, InputExpression exponent)
    {
        return InputExpression.Binary(InputExpressionKind.Power, basis, exponent, Source);
    }

    private static InputExpression Sin(InputExpression value)
    {
        return Function("sin", value);
    }

    private static InputExpression Cos(InputExpression value)
    {
        return Function("cos", value);
    }

    private static InputExpression Tan(InputExpression value)
    {
        return Function("tan", value);
    }

    private static InputExpression Sqrt(InputExpression value)
    {
        return Function("sqrt", value);
    }

    private static InputExpression Function(string name, params InputExpression[] arguments)
    {
        return InputExpression.Function(name, arguments.ToImmutableArray(), Source);
    }
}
