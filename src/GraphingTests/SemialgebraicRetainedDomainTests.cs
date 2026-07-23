using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class SemialgebraicRetainedDomainTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Theory]
    [InlineData("abs", (int)AngleUnit.Radians)]
    [InlineData("abs", (int)AngleUnit.Degrees)]
    [InlineData("abs", (int)AngleUnit.Grads)]
    [InlineData("sqrt", (int)AngleUnit.Radians)]
    [InlineData("sqrt", (int)AngleUnit.Degrees)]
    [InlineData("sqrt", (int)AngleUnit.Grads)]
    public void SimplifiedValueNeverErasesRetainedTangentHoles(
        string function,
        int angleUnitValue)
    {
        AngleUnit angleUnit = (AngleUnit)angleUnitValue;
        InputExpression core = Function(function, Variable());
        InputExpression hiddenTangentHole = Add(
            core,
            Multiply(Number(0), Function("tan", Variable())));
        AnalysisRequest request = Request(
            hiddenTangentHole,
            angleUnit,
            AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        SemanticExpression semantic = Assert.IsType<SemanticExpression>(report.Expression);

        Assert.Equal(Build(core).Value.Canonical, semantic.Value.Canonical);
        Assert.NotEqual(Formula.True.Canonical, semantic.DefinedWhen.Canonical);
        AssertDomainPreservesHole(request, report, function);
        AssertNoLegacyPrimitiveCertificate(report);

        AnalysisRequest cleanRequest = Request(core, angleUnit, AnalysisFeatures.Range);
        AnalysisReport clean = AnalysisEngine.Analyze(cleanRequest);
        Assert.Equal(ProofState.Proved, clean.Range.State);
        Assert.IsType<SemialgebraicUnaryProofCertificate>(clean.Range.Certificate);
        Assert.True(CertificateChecker.Check(
            cleanRequest,
            clean.Expression!,
            clean.Range));

        RealSet forgedDomain = UnsafePrimitiveDomain(function);
        var forgedCertificate = new TheoremProofCertificate(
            AnalysisFeatures.Domain,
            semantic.Value.Canonical,
            ClaimCanonical.For(forgedDomain),
            TheoremRule.SemialgebraicCellDecomposition,
            function == "abs"
                ? ["absolute-affine", "1", "0"]
                : ["principal-square-root"]);
        Assert.False(CertificateChecker.Check(
            request,
            semantic,
            ProofOutcome<RealSet>.Proved(forgedDomain, forgedCertificate)));
    }

    private static void AssertDomainPreservesHole(
        AnalysisRequest request,
        AnalysisReport report,
        string function)
    {
        Assert.True(
            report.Domain.State is ProofState.Proved or ProofState.Unknown,
            report.Domain.State.ToString());
        if (report.Domain.State == ProofState.Unknown)
        {
            Assert.Null(report.Domain.Certificate);
            return;
        }

        RealSet domain = Assert.IsAssignableFrom<RealSet>(report.Domain.Value);
        Assert.NotEqual(UnsafePrimitiveDomain(function).Canonical, domain.Canonical);
        Assert.Contains("periodic-", domain.Canonical, StringComparison.Ordinal);
        Assert.NotNull(report.Domain.Certificate);
        Assert.True(CertificateChecker.Check(
            request,
            report.Expression!,
            report.Domain));
    }

    private static void AssertNoLegacyPrimitiveCertificate(AnalysisReport report)
    {
        ImmutableArray<ProofCertificate?> certificates =
        [
            report.Domain.Certificate,
            report.Range.Certificate,
            report.Parity.Certificate,
            report.Zeros.Certificate,
            report.YIntercept.Certificate,
            report.Minima.Certificate,
            report.Maxima.Certificate,
            report.InflectionPoints.Certificate,
            report.VerticalAsymptotes.Certificate,
            report.HorizontalAsymptotes.Certificate,
            report.ObliqueAsymptotes.Certificate,
            report.Monotonicity.Certificate,
            report.Period.Certificate
        ];

        foreach (ProofCertificate? certificate in certificates)
        {
            bool legacy = certificate is TheoremProofCertificate
            {
                Theorem: TheoremRule.SemialgebraicCellDecomposition,
                Parameters.Length: > 0
            } theorem &&
                theorem.Parameters[0] is "absolute-affine" or "principal-square-root";
            Assert.False(legacy, certificate?.ToString());
        }
    }

    private static RealSet UnsafePrimitiveDomain(string function)
    {
        return function == "abs"
            ? AllRealSet.Instance
            : new IntervalSet(
                RealBound.Finite(new RationalReal(BigRational.Zero)),
                true,
                RealBound.PositiveInfinity,
                false);
    }

    private static AnalysisRequest Request(
        InputExpression expression,
        AngleUnit angleUnit,
        AnalysisFeatures features)
    {
        return new AnalysisRequest(expression, features, angleUnit, "x", static () => true);
    }

    private static SemanticExpression Build(InputExpression expression)
    {
        return new SemanticGraphBuilder(new ResourceBudget()).Build(expression);
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

    private static InputExpression Multiply(InputExpression left, InputExpression right)
    {
        return InputExpression.Binary(InputExpressionKind.Multiply, left, right, Source);
    }

    private static InputExpression Function(string name, InputExpression argument)
    {
        return InputExpression.Function(name, [argument], Source);
    }
}
