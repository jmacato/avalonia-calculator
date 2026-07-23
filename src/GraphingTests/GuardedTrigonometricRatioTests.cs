using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class GuardedTrigonometricRatioTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Fact]
    public void SineOverCosineRewritesToTangentWithoutErasingThePoleGuard()
    {
        SemanticExpression semantic = new SemanticGraphBuilder(new ResourceBudget()).Build(
            Ratio(Variable(), Variable()));

        Assert.Equal(ValueKind.Function, semantic.Value.Kind);
        Assert.Equal("tan", semantic.Value.Name);
        Assert.NotEqual(Formula.True.Canonical, semantic.DefinedWhen.Canonical);
        RewriteStep rewrite = Assert.Single(semantic.RewriteHistory);
        Assert.Equal("guarded-sine-cosine-to-tangent", rewrite.Rule);
        Assert.Equal(semantic.DefinedWhen.Canonical, rewrite.Guard.Canonical);

        var request = new AnalysisRequest(
            Ratio(Variable(), Variable()),
            AnalysisFeatures.Domain,
            AngleUnit.Radians,
            "x",
            static () => true);
        RealSet domain = Proved(AnalysisEngine.Analyze(request).Domain);
        Assert.Equal(
            "periodic-intervals[pi:1:0,m,m:Z,[pi:-1/2:0,0,pi:1/2:0,0]]",
            domain.Canonical);
    }

    [Fact]
    public void RatioAndTangentPublishTheSameCheckedMathematicalClaims()
    {
        AnalysisRequest ratioRequest = Request(Ratio(Variable(), Variable()));
        AnalysisRequest tangentRequest = Request(Function("tan", Variable()));
        AnalysisReport ratio = AnalysisEngine.Analyze(ratioRequest);
        AnalysisReport tangent = AnalysisEngine.Analyze(tangentRequest);

        AssertSame(ratio.Domain, tangent.Domain);
        AssertSame(ratio.Range, tangent.Range);
        AssertSame(ratio.Parity, tangent.Parity);
        AssertSame(ratio.Zeros, tangent.Zeros);
        AssertSame(ratio.YIntercept, tangent.YIntercept);
        AssertSame(ratio.Minima, tangent.Minima);
        AssertSame(ratio.Maxima, tangent.Maxima);
        AssertSame(ratio.InflectionPoints, tangent.InflectionPoints);
        AssertSame(ratio.VerticalAsymptotes, tangent.VerticalAsymptotes);
        AssertSame(ratio.HorizontalAsymptotes, tangent.HorizontalAsymptotes);
        AssertSame(ratio.ObliqueAsymptotes, tangent.ObliqueAsymptotes);
        AssertSame(ratio.Monotonicity, tangent.Monotonicity);
        AssertSame(ratio.Period, tangent.Period);
        return;

        void AssertSame<T>(ProofOutcome<T> actual, ProofOutcome<T> expected)
        {
            Assert.Equal(ProofState.Proved, actual.State);
            Assert.Equal(ProofState.Proved, expected.State);
            Assert.Equal(
                ClaimCanonical.ForObject(expected.Value!),
                ClaimCanonical.ForObject(actual.Value!));
            Assert.True(CertificateChecker.Check(ratioRequest, ratio.Expression!, actual));
            Assert.True(CertificateChecker.Check(tangentRequest, tangent.Expression!, expected));
        }
    }

    [Fact]
    public void DifferentArgumentsNeverTriggerTheGuardedRatioIdentity()
    {
        InputExpression x = Variable();
        SemanticExpression semantic = new SemanticGraphBuilder(new ResourceBudget()).Build(
            Ratio(
                x,
                InputExpression.Binary(
                    InputExpressionKind.Multiply,
                    Number(2),
                    x,
                    Source)));

        Assert.Equal(ValueKind.Divide, semantic.Value.Kind);
        Assert.DoesNotContain(
            semantic.RewriteHistory,
            static rewrite => rewrite.Rule == "guarded-sine-cosine-to-tangent");
    }

    private static AnalysisRequest Request(InputExpression expression)
    {
        return new AnalysisRequest(expression, AnalysisFeatures.All, AngleUnit.Radians, "x", static () => true);
    }

    private static T Proved<T>(ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        return outcome.Value!;
    }

    private static InputExpression Ratio(
        InputExpression sineArgument,
        InputExpression cosineArgument)
    {
        return InputExpression.Binary(
            InputExpressionKind.Divide,
            Function("sin", sineArgument),
            Function("cos", cosineArgument),
            Source);
    }

    private static InputExpression Function(string name, InputExpression argument)
    {
        return InputExpression.Function(name, ImmutableArray.Create(argument), Source);
    }

    private static InputExpression Variable()
    {
        return InputExpression.Variable("x", Source);
    }

    private static InputExpression Number(int value)
    {
        return InputExpression.Number(new BigRational(value), Source);
    }
}
