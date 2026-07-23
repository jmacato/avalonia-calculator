using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class IntegerHarmonicAnalysisTests
{
    private static readonly SourceRange Source = new(0, 1);
    private const AnalysisFeatures ProvedFeatures =
        AnalysisFeatures.All & ~AnalysisFeatures.Range;

    [Theory]
    [InlineData("sin", "sin", null)]
    [InlineData("cos", "cos", "interval[q:-9/8,1,q:2,1]")]
    [InlineData("sin", "cos", "interval[q:-2,1,q:9/8,1]")]
    public void MixedIntegerHarmonicsProveEveryEstablishedFeature(
        string first,
        string second,
        string? expectedRange)
    {
        InputExpression expression = Add(
            Function(first, Variable()),
            Function(second, Multiply(Number(2), Variable())));
        AnalysisRequest request = Request(expression, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        if (expectedRange is null)
        {
            Assert.Equal(ProofState.Unknown, report.Range.State);
            Assert.Equal(UnknownReason.UnsupportedFragment, report.Range.UnknownReason);
        }
        else
        {
            Assert.Equal(ProofState.Proved, report.Range.State);
            Assert.Equal(expectedRange, report.Range.Value!.Canonical);
            Assert.IsType<QuadraticHarmonicRangeProofCertificate>(report.Range.Certificate);
        }
        AssertProved(report.Domain);
        AssertProved(report.Parity);
        AssertProved(report.Zeros);
        AssertProved(report.YIntercept);
        AssertProved(report.Minima);
        AssertProved(report.Maxima);
        AssertProved(report.InflectionPoints);
        AssertProved(report.VerticalAsymptotes);
        AssertProved(report.HorizontalAsymptotes);
        AssertProved(report.ObliqueAsymptotes);
        AssertProved(report.Monotonicity);
        AssertProved(report.Period);
        AssertReplay(request, report);

        var zeroCertificate = Assert.IsType<TheoremProofCertificate>(report.Zeros.Certificate);
        Assert.Equal(TheoremRule.TrigonometricPolynomial, zeroCertificate.Theorem);
    }

    [Fact]
    public void EquivalentIntegerFrequencySyntaxProducesTheSameClaims()
    {
        InputExpression x = Variable();
        AnalysisReport multiplied = AnalysisEngine.Analyze(Request(
            Add(Function("sin", x), Function("sin", Multiply(Number(2), x))),
            ProvedFeatures));
        AnalysisReport added = AnalysisEngine.Analyze(Request(
            Add(Function("sin", x), Function("sin", Add(x, x))),
            ProvedFeatures));

        Assert.Equal(Proved(multiplied.Domain).Canonical, Proved(added.Domain).Canonical);
        Assert.Equal(Proved(multiplied.Parity), Proved(added.Parity));
        Assert.Equal(Proved(multiplied.Zeros).Canonical, Proved(added.Zeros).Canonical);
        Assert.Equal(
            ClaimCanonical.ForObject(Proved(multiplied.Minima)),
            ClaimCanonical.ForObject(Proved(added.Minima)));
        Assert.Equal(
            ClaimCanonical.ForObject(Proved(multiplied.Maxima)),
            ClaimCanonical.ForObject(Proved(added.Maxima)));
        Assert.Equal(
            ClaimCanonical.ForObject(Proved(multiplied.InflectionPoints)),
            ClaimCanonical.ForObject(Proved(added.InflectionPoints)));
        Assert.Equal(
            ClaimCanonical.ForObject(Proved(multiplied.Monotonicity)),
            ClaimCanonical.ForObject(Proved(added.Monotonicity)));
        Assert.Equal(
            ExactRealCanonical.Format(Proved(multiplied.Period).FundamentalPeriod!),
            ExactRealCanonical.Format(Proved(added.Period).FundamentalPeriod!));
    }

    [Fact]
    public void NegativeIntegerFrequenciesRespectSineOddnessAndCosineEvenness()
    {
        InputExpression x = Variable();
        AnalysisReport negativeSine = AnalysisEngine.Analyze(Request(
            Add(Function("sin", x), Function("sin", Multiply(Number(-2), x))),
            ProvedFeatures));
        AnalysisReport explicitSine = AnalysisEngine.Analyze(Request(
            Subtract(Function("sin", x), Function("sin", Multiply(Number(2), x))),
            ProvedFeatures));
        AnalysisReport negativeCosine = AnalysisEngine.Analyze(Request(
            Add(Function("sin", x), Function("cos", Multiply(Number(-2), x))),
            ProvedFeatures));
        AnalysisReport positiveCosine = AnalysisEngine.Analyze(Request(
            Add(Function("sin", x), Function("cos", Multiply(Number(2), x))),
            ProvedFeatures));

        AssertSameClaims(negativeSine, explicitSine);
        AssertSameClaims(negativeCosine, positiveCosine);
    }

    [Fact]
    public void HarmonicCertificateReplayRejectsModelAndClaimMutation()
    {
        InputExpression expression = Add(
            Function("sin", Variable()),
            Function("sin", Multiply(Number(2), Variable())));
        AnalysisRequest request = Request(expression, AnalysisFeatures.Zeros);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        RealSet zeros = Proved(report.Zeros);
        var certificate = Assert.IsType<TheoremProofCertificate>(report.Zeros.Certificate);

        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Zeros));
        var changedModel = certificate with
        {
            Parameters = certificate.Parameters.SetItem(
                1,
                certificate.Parameters[1] + ":mutated")
        };
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<RealSet>.Proved(zeros, changedModel)));
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<RealSet>.Proved(EmptySet.Instance, certificate)));
    }

    [Fact]
    public void HarmonicDegreeLimitAndCancellationAreDeterministic()
    {
        InputExpression overLimit = Add(
            Function("sin", Variable()),
            Function(
                "sin",
                Multiply(Number(AnalysisLimits.UnivariateDegree + 1), Variable())));
        AnalysisReport limited = AnalysisEngine.Analyze(Request(
            overLimit,
            AnalysisFeatures.Zeros));
        Assert.Equal(ProofState.Unknown, limited.Zeros.State);
        Assert.Equal(UnknownReason.BudgetExceeded, limited.Zeros.UnknownReason);

        var cancelled = new AnalysisRequest(
            Add(
                Function("sin", Variable()),
                Function("sin", Multiply(Number(2), Variable()))),
            AnalysisFeatures.All,
            AngleUnit.Radians,
            "x",
            static () => false);
        Assert.Throws<AnalysisCancelledException>(() => AnalysisEngine.Analyze(cancelled));
    }

    private static void AssertSameClaims(AnalysisReport left, AnalysisReport right)
    {
        Assert.Equal(Proved(left.Domain).Canonical, Proved(right.Domain).Canonical);
        Assert.Equal(Proved(left.Parity), Proved(right.Parity));
        Assert.Equal(Proved(left.Zeros).Canonical, Proved(right.Zeros).Canonical);
        Assert.Equal(ClaimCanonical.ForObject(Proved(left.YIntercept)), ClaimCanonical.ForObject(Proved(right.YIntercept)));
        Assert.Equal(ClaimCanonical.ForObject(Proved(left.Minima)), ClaimCanonical.ForObject(Proved(right.Minima)));
        Assert.Equal(ClaimCanonical.ForObject(Proved(left.Maxima)), ClaimCanonical.ForObject(Proved(right.Maxima)));
        Assert.Equal(ClaimCanonical.ForObject(Proved(left.InflectionPoints)), ClaimCanonical.ForObject(Proved(right.InflectionPoints)));
        Assert.Equal(ClaimCanonical.ForObject(Proved(left.VerticalAsymptotes)), ClaimCanonical.ForObject(Proved(right.VerticalAsymptotes)));
        Assert.Equal(ClaimCanonical.ForObject(Proved(left.HorizontalAsymptotes)), ClaimCanonical.ForObject(Proved(right.HorizontalAsymptotes)));
        Assert.Equal(ClaimCanonical.ForObject(Proved(left.ObliqueAsymptotes)), ClaimCanonical.ForObject(Proved(right.ObliqueAsymptotes)));
        Assert.Equal(ClaimCanonical.ForObject(Proved(left.Monotonicity)), ClaimCanonical.ForObject(Proved(right.Monotonicity)));
        Assert.Equal(ClaimCanonical.ForObject(Proved(left.Period)), ClaimCanonical.ForObject(Proved(right.Period)));
    }

    private static void AssertReplay(AnalysisRequest request, AnalysisReport report)
    {
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Domain));
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Parity));
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Zeros));
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.YIntercept));
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Minima));
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Maxima));
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.InflectionPoints));
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.VerticalAsymptotes));
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.HorizontalAsymptotes));
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.ObliqueAsymptotes));
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Monotonicity));
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Period));
    }

    private static void AssertProved<T>(ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
    }

    private static T Proved<T>(ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        return outcome.Value!;
    }

    private static AnalysisRequest Request(
        InputExpression expression,
        AnalysisFeatures features)
    {
        return new AnalysisRequest(expression, features, AngleUnit.Radians, "x", static () => true);
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

    private static InputExpression Function(string name, InputExpression argument)
    {
        return InputExpression.Function(name, ImmutableArray.Create(argument), Source);
    }
}
