using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class RationalMonotonicityMergeTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Fact]
    public void FlatStationaryPointsDoNotSplitOneMaximalIncreasingInterval()
    {
        InputExpression x = Variable();
        (string Label, InputExpression Expression)[] cases =
        [
            ("x^3", Power(x, 3)),
            ("x^5", Power(x, 5)),
            (
                "3*x^5-10*x^3+15*x",
                Add(
                    Subtract(
                        Multiply(Number(3), Power(x, 5)),
                        Multiply(Number(10), Power(x, 3))),
                    Multiply(Number(15), x))),
            (
                "x^3/(x^2+1)",
                Divide(Power(x, 3), Add(Power(x, 2), Number(1))))
        ];

        foreach ((string label, InputExpression expression) in cases)
        {
            (AnalysisRequest request, AnalysisReport report) = Analyze(expression);
            ImmutableArray<MonotoneRegion> regions = Proved(report.Monotonicity, label);

            MonotoneRegion region = Assert.Single(regions);
            Assert.IsType<AllRealSet>(region.Region);
            Assert.Equal(Monotonicity.Increasing, region.Direction);
            Assert.IsType<RationalFunctionProofCertificate>(report.Monotonicity.Certificate);
            Assert.True(
                CertificateChecker.Check(request, report.Expression!, report.Monotonicity),
                label);
        }
    }

    [Fact]
    public void GenuineDerivativeSignChangesRemainSeparateIntervals()
    {
        InputExpression x = Variable();
        (AnalysisRequest request, AnalysisReport report) = Analyze(
            Subtract(Power(x, 3), Multiply(Number(3), x)));

        AssertRegions(
            Proved(report.Monotonicity),
            ("interval[-inf,0,q:-1,0]", Monotonicity.Increasing),
            ("interval[q:-1,0,q:1,0]", Monotonicity.Decreasing),
            ("interval[q:1,0,+inf,0]", Monotonicity.Increasing));
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Monotonicity));
    }

    [Fact]
    public void RetainedDomainHolePreventsMergingEqualDerivativeSigns()
    {
        InputExpression x = Variable();
        InputExpression expression = Add(
            Power(x, 3),
            Multiply(Number(0), Divide(Number(1), x)));
        (AnalysisRequest request, AnalysisReport report) = Analyze(expression);

        AssertRegions(
            Proved(report.Monotonicity),
            ("interval[-inf,0,q:0,0]", Monotonicity.Increasing),
            ("interval[q:0,0,+inf,0]", Monotonicity.Increasing));
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Monotonicity));
    }

    [Fact]
    public void ReplayRejectsAChangedMergedMonotonicityClaim()
    {
        (AnalysisRequest request, AnalysisReport report) = Analyze(Power(Variable(), 3));
        var certificate = Assert.IsType<RationalFunctionProofCertificate>(
            report.Monotonicity.Certificate);
        ImmutableArray<MonotoneRegion> changed =
        [
            new MonotoneRegion(AllRealSet.Instance, Monotonicity.Decreasing)
        ];

        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<ImmutableArray<MonotoneRegion>>.Proved(changed, certificate)));
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<ImmutableArray<MonotoneRegion>>.Proved(
                Proved(report.Monotonicity),
                certificate with
                {
                    OriginalDomainExclusions = certificate.OriginalDomainExclusions.Add(
                        UnivariatePolynomial.One)
                })));

        (_, AnalysisReport other) = Analyze(Add(Power(Variable(), 3), Variable()));
        var otherCertificate = Assert.IsType<RationalFunctionProofCertificate>(
            other.Monotonicity.Certificate);
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<ImmutableArray<MonotoneRegion>>.Proved(
                Proved(report.Monotonicity),
                certificate with
                {
                    RootIsolations = otherCertificate.RootIsolations
                })));
    }

    private static (AnalysisRequest Request, AnalysisReport Report) Analyze(
        InputExpression expression)
    {
        var request = new AnalysisRequest(
            expression,
            AnalysisFeatures.Monotonicity,
            AngleUnit.Radians,
            "x",
            static () => true);
        return (request, AnalysisEngine.Analyze(request));
    }

    private static void AssertRegions(
        ImmutableArray<MonotoneRegion> actual,
        params (string Region, Monotonicity Direction)[] expected)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index].Region, actual[index].Region.Canonical);
            Assert.Equal(expected[index].Direction, actual[index].Direction);
        }
    }

    private static T Proved<T>(ProofOutcome<T> outcome, string? label = null)
    {
        Assert.True(outcome.State == ProofState.Proved, $"{label}: {outcome.UnknownReason}");
        Assert.NotNull(outcome.Certificate);
        Assert.NotNull(outcome.Value);
        return outcome.Value;
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

    private static InputExpression Power(InputExpression basis, int exponent)
    {
        return InputExpression.Binary(InputExpressionKind.Power, basis, Number(exponent), Source);
    }
}
