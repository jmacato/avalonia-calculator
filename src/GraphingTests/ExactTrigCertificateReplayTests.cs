using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class ExactTrigCertificateReplayTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Theory]
    [InlineData((int)AngleUnit.Radians)]
    [InlineData((int)AngleUnit.Degrees)]
    [InlineData((int)AngleUnit.Grads)]
    public void IndependentReplayCoversEveryFeatureForEveryAffineTrigKind(int unitValue)
    {
        AngleUnit angleUnit = (AngleUnit)unitValue;
        foreach (string function in new[] { "sin", "cos", "tan" })
        {
            InputExpression expression = Add(
                Multiply(
                    Symbol("pi"),
                    Function(
                        function,
                        Multiply(Number(2), Variable()))),
                Number(1));
            AnalysisRequest request = Request(
                expression,
                AnalysisFeatures.All,
                angleUnit);
            AnalysisReport report = AnalysisEngine.Analyze(request);

            AssertReplay(request, report, report.Domain, function, exactCoefficient: false);
            Assert.True(ExactCoefficientAnalyzer.TryAnalyze(
                request,
                report.Expression!,
                AnalysisFeatures.Domain,
                new ResourceBudget(),
                out ProofOutcome<RealSet> exactDomain));
            AssertReplay(request, report, exactDomain, function);
            AssertReplay(request, report, report.Range, function);
            AssertReplay(request, report, report.Parity, function);
            AssertReplay(request, report, report.Zeros, function);
            AssertReplay(request, report, report.YIntercept, function);
            AssertReplay(request, report, report.Minima, function);
            AssertReplay(request, report, report.Maxima, function);
            AssertReplay(request, report, report.InflectionPoints, function);
            AssertReplay(request, report, report.VerticalAsymptotes, function);
            AssertReplay(request, report, report.HorizontalAsymptotes, function);
            AssertReplay(request, report, report.ObliqueAsymptotes, function);
            AssertReplay(request, report, report.Monotonicity, function);
            AssertReplay(request, report, report.Period, function);
        }
    }

    [Theory]
    [InlineData((int)AngleUnit.Degrees, 180, 360)]
    [InlineData((int)AngleUnit.Grads, 200, 400)]
    public void ReplayRejectsLegacyRawRadianInverseRootCoordinates(
        int unitValue,
        int halfTurn,
        int fullTurn)
    {
        AngleUnit angleUnit = (AngleUnit)unitValue;
        InputExpression expression = Subtract(
            Multiply(Symbol("pi"), Function("sin", Variable())),
            Number(1));
        AnalysisRequest request = Request(
            expression,
            AnalysisFeatures.Zeros,
            angleUnit);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        var certificate = Assert.IsType<ExactCoefficientProofCertificate>(
            Proved(report.Zeros).Certificate);

        ExactReal target = ExactRealArithmetic.Divide(
            new RationalReal(BigRational.One),
            new AffinePiReal(BigRational.One, BigRational.Zero));
        ExactReal rawRadianPrincipal = new FunctionReal("asin", [target]);
        ExactReal reflected = ExactRealArithmetic.Subtract(
            new RationalReal(new BigRational(halfTurn)),
            rawRadianPrincipal);
        ExactReal period = new RationalReal(new BigRational(fullTurn));
        RealSet legacyWrongZeros = RealSets.Union(
            new PeriodicPointSet(
                rawRadianPrincipal,
                period,
                "m",
                IntegerConstraint.All("m")),
            new PeriodicPointSet(
                reflected,
                period,
                "m",
                IntegerConstraint.All("m")));
        string wrongClaim = ClaimCanonical.For(legacyWrongZeros);
        ExactCoefficientProofCertificate forged = certificate with
        {
            Claim = wrongClaim
        };

        Assert.NotEqual(report.Zeros.Value!.Canonical, legacyWrongZeros.Canonical);
        Assert.False(ExactCoefficientCertificateChecker.Check(
            request,
            report.Expression!,
            forged,
            wrongClaim,
            new ResourceBudget()));
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<RealSet>.Proved(legacyWrongZeros, forged)));
    }

    [Theory]
    [InlineData((int)AngleUnit.Degrees)]
    [InlineData((int)AngleUnit.Grads)]
    public void ReplayRejectsLegacyRawRequestUnitForwardTrigArguments(int unitValue)
    {
        AngleUnit angleUnit = (AngleUnit)unitValue;
        InputExpression expression = Multiply(
            Symbol("pi"),
            Function("sin", Add(Variable(), Number(1))));
        AnalysisRequest request = Request(
            expression,
            AnalysisFeatures.YIntercept,
            angleUnit);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        var certificate = Assert.IsType<ExactCoefficientProofCertificate>(
            Proved(report.YIntercept).Certificate);

        ExactReal legacyWrongValue = ExactRealArithmetic.Multiply(
            new AffinePiReal(BigRational.One, BigRational.Zero),
            new FunctionReal(
                "sin",
                [new RationalReal(BigRational.One)]));
        OptionalValue<ExactReal> legacyWrongIntercept =
            OptionalValue<ExactReal>.Some(legacyWrongValue);
        string wrongClaim = ClaimCanonical.ForObject(legacyWrongIntercept);
        ExactCoefficientProofCertificate forged = certificate with
        {
            Claim = wrongClaim
        };

        Assert.NotEqual(
            ExactRealCanonical.Format(report.YIntercept.Value!.Value!),
            ExactRealCanonical.Format(legacyWrongValue));
        Assert.False(ExactCoefficientCertificateChecker.Check(
            request,
            report.Expression!,
            forged,
            wrongClaim,
            new ResourceBudget()));
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<OptionalValue<ExactReal>>.Proved(
                legacyWrongIntercept,
                forged)));
    }

    [Fact]
    public void IndependentReplayHonorsCancellationAndWorkBudget()
    {
        InputExpression expression = Subtract(
            Multiply(Symbol("pi"), Function("sin", Variable())),
            Number(1));
        AnalysisRequest request = Request(
            expression,
            AnalysisFeatures.Zeros,
            AngleUnit.Degrees);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        RealSet zeros = Proved(report.Zeros).Value!;
        var certificate = Assert.IsType<ExactCoefficientProofCertificate>(
            report.Zeros.Certificate);
        string claim = ClaimCanonical.For(zeros);

        Assert.Throws<AnalysisCancelledException>(() =>
            ExactCoefficientCertificateChecker.Check(
                request,
                report.Expression!,
                certificate,
                claim,
                new ResourceBudget(static () => false)));

        var exhausted = new ResourceBudget();
        exhausted.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            ExactCoefficientCertificateChecker.Check(
                request,
                report.Expression!,
                certificate,
                claim,
                exhausted));
    }

    private static void AssertReplay<T>(
        AnalysisRequest request,
        AnalysisReport report,
        ProofOutcome<T> outcome,
        string function,
        bool exactCoefficient = true)
    {
        Assert.True(
            outcome.State == ProofState.Proved,
            $"{function}/{typeof(T).Name} was {outcome.State}/{outcome.UnknownReason}.");
        if (exactCoefficient)
        {
            Assert.IsType<ExactCoefficientProofCertificate>(outcome.Certificate);
        }

        Assert.True(CertificateChecker.Check(request, report.Expression!, outcome));
    }

    private static ProofOutcome<T> Proved<T>(ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.NotNull(outcome.Certificate);
        return outcome;
    }

    private static AnalysisRequest Request(
        InputExpression expression,
        AnalysisFeatures features,
        AngleUnit angleUnit)
    {
        return new AnalysisRequest(expression, features, angleUnit, "x", static () => true);
    }

    private static InputExpression Variable()
    {
        return InputExpression.Variable("x", Source);
    }

    private static InputExpression Symbol(string name)
    {
        return InputExpression.Variable(name, Source);
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

    private static InputExpression Function(
        string name,
        params InputExpression[] arguments)
    {
        return InputExpression.Function(name, arguments.ToImmutableArray(), Source);
    }
}
