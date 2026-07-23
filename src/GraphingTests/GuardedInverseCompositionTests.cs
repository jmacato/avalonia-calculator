using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class GuardedInverseCompositionTests
{
    private static readonly SourceRange Source = new(0, 1);

    public static TheoryData<string, string, string> ExactInversePairs => new()
    {
        { "sin", "asin", "sine-arcsine-identity" },
        { "cos", "acos", "cosine-arccosine-identity" },
        { "tan", "atan", "tangent-arctangent-identity" },
        { "exp", "ln", "exponential-logarithm-identity" },
        { "ln", "exp", "logarithm-exponential-identity" }
    };

    [Theory]
    [MemberData(nameof(ExactInversePairs))]
    public void ExactInverseCompositionRewritesValueWithoutErasingDomain(
        string outer,
        string inner,
        string rule)
    {
        InputExpression input = Function(outer, Function(inner, Variable()));
        var budget = new ResourceBudget();
        SemanticExpression expression = new SemanticGraphBuilder(budget).Build(input);

        Assert.Equal("v:x", expression.Value.Canonical);
        RewriteStep rewrite = Assert.Single(expression.RewriteHistory);
        Assert.Equal(rule, rewrite.Rule);
        Assert.Equal(expression.DefinedWhen.Canonical, rewrite.Guard.Canonical);

        AnalysisReport report = AnalysisEngine.Analyze(Request(input, AnalysisFeatures.Domain));
        RealSet domain = AssertProved(report.Domain);
        if (inner is "asin" or "acos")
        {
            var interval = Assert.IsType<IntervalSet>(domain);
            Assert.True(interval.IncludesLower);
            Assert.True(interval.IncludesUpper);
            Assert.Equal(new RationalReal(BigRational.MinusOne), interval.Lower.Value);
            Assert.Equal(new RationalReal(BigRational.One), interval.Upper.Value);
        }
        else if (inner == "ln")
        {
            var interval = Assert.IsType<IntervalSet>(domain);
            Assert.False(interval.IncludesLower);
            Assert.Equal(new RationalReal(BigRational.Zero), interval.Lower.Value);
            Assert.Equal(BoundKind.PositiveInfinity, interval.Upper.Kind);
        }
        else
        {
            Assert.IsType<AllRealSet>(domain);
        }

        Assert.True(CertificateChecker.Check(
            Request(input, AnalysisFeatures.Domain),
            report.Expression!,
            report.Domain));
    }

    [Fact]
    public void ExponentialLogarithmBecomesIdentityOnlyOnPositiveDomain()
    {
        InputExpression input = Function("exp", Function("ln", Variable()));
        AnalysisRequest request = Request(input, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        var domain = Assert.IsType<IntervalSet>(AssertProved(report.Domain));
        Assert.Equal(new RationalReal(BigRational.Zero), domain.Lower.Value);
        Assert.False(domain.IncludesLower);
        Assert.IsType<EmptySet>(AssertProved(report.Zeros));
        Assert.False(AssertProved(report.YIntercept).HasValue);
        Assert.Equal(FunctionParity.Neither, AssertProved(report.Parity));
        Assert.Equal(
            Monotonicity.Increasing,
            Assert.Single(AssertProved(report.Monotonicity)).Direction);
    }

    [Fact]
    public void LogarithmExponentialBecomesEverywhereIdentity()
    {
        InputExpression input = Function("ln", Function("exp", Variable()));
        AnalysisRequest request = Request(input, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        Assert.IsType<AllRealSet>(AssertProved(report.Domain));
        Assert.IsType<AllRealSet>(AssertProved(report.Range));
        Assert.Equal(FunctionParity.Odd, AssertProved(report.Parity));
        Assert.IsType<PointSet>(AssertProved(report.Zeros));
        Assert.Equal(
            new RationalReal(BigRational.Zero),
            AssertProved(report.YIntercept).Value);
        Assert.Equal(
            Monotonicity.Increasing,
            Assert.Single(AssertProved(report.Monotonicity)).Direction);
        Assert.Equal(PeriodicityKind.NotPeriodic, AssertProved(report.Period).Kind);
    }

    [Theory]
    [InlineData("asin", "sin")]
    [InlineData("acos", "cos")]
    [InlineData("atan", "tan")]
    public void ReverseCompositionIsNotUnsafelyCancelled(string outer, string inner)
    {
        SemanticExpression expression = new SemanticGraphBuilder(new ResourceBudget()).Build(
            Function(outer, Function(inner, Variable())));

        Assert.Equal(ValueKind.Function, expression.Value.Kind);
        Assert.Equal(outer, expression.Value.Name);
        Assert.DoesNotContain(
            expression.RewriteHistory,
            static rewrite => rewrite.Rule.EndsWith("identity", StringComparison.Ordinal));
    }

    [Fact]
    public void HyperbolicTangentCarriesTotalPrimitiveSemantics()
    {
        SemanticExpression expression = new SemanticGraphBuilder(new ResourceBudget()).Build(
            Function("tanh", Variable()));

        Assert.Equal(Formula.True.Canonical, expression.DefinedWhen.Canonical);
        Assert.Equal(Formula.True.Canonical, expression.ContinuousWhen.Canonical);
        Assert.Equal(Formula.True.Canonical, expression.DifferentiableWhen.Canonical);
    }

    private static AnalysisRequest Request(InputExpression expression, AnalysisFeatures features)
    {
        return new AnalysisRequest(expression, features, AngleUnit.Radians, "x", static () => true);
    }

    private static T AssertProved<T>(ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.NotNull(outcome.Value);
        Assert.NotNull(outcome.Certificate);
        return outcome.Value;
    }

    private static InputExpression Variable()
    {
        return InputExpression.Variable("x", Source);
    }

    private static InputExpression Function(string name, params InputExpression[] operands)
    {
        return InputExpression.Function(name, operands.ToImmutableArray(), Source);
    }
}
