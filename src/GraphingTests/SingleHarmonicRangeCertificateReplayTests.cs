using Graphing.Symbolics;

namespace GraphingTests;

public sealed class SingleHarmonicRangeCertificateReplayTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Theory]
    [InlineData((int)AngleUnit.Radians)]
    [InlineData((int)AngleUnit.Degrees)]
    [InlineData((int)AngleUnit.Grads)]
    public void ExactRationalAmplitudeReplaysInEveryAngleUnit(int angleUnitValue)
    {
        AngleUnit angleUnit = (AngleUnit)angleUnitValue;
        InputExpression x = Variable();
        InputExpression twiceX = Multiply(Number(2), x);
        InputExpression input = Add(
            Add(
                Multiply(Number(new BigRational(3, 5)), Sin(twiceX)),
                Multiply(Number(new BigRational(-4, 5)), Cos(twiceX))),
            Number(7));
        AnalysisRequest request = Request(input, angleUnit);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        SemanticExpression semantic = Assert.IsType<SemanticExpression>(report.Expression);
        RealSet range = AssertProved(report.Range);
        var certificate = Assert.IsType<SingleHarmonicRangeProofCertificate>(
            report.Range.Certificate);

        Assert.Equal("interval[q:6,1,q:8,1]", range.Canonical);
        Assert.Equal(2, certificate.Frequency);
        Assert.Equal(new BigRational(7), certificate.Constant);
        Assert.Equal(new BigRational(-4, 5), certificate.CosineCoefficient);
        Assert.Equal(new BigRational(3, 5), certificate.SineCoefficient);
        Assert.Equal(BigRational.One, certificate.RadiusSquared);
        AssertReplay(request, semantic, report.Range);
    }

    [Fact]
    public void CheckerOwnedReductionCoversStructuralSineCosineCombinations()
    {
        InputExpression x = Variable();
        (InputExpression Input, string Range)[] cases =
        [
            (
                Add(Sin(x), Cos(x)),
                "interval[fn:negate(fn:sqrt(q:2)),1,fn:sqrt(q:2),1]"),
            (
                Multiply(Sin(x), Cos(x)),
                "interval[q:-1/2,1,q:1/2,1]"),
            (
                Subtract(Power(Cos(x), 2), Power(Sin(x), 2)),
                "interval[q:-1,1,q:1,1]"),
            (
                Power(Add(Sin(x), Cos(x)), 2),
                "interval[q:0,1,q:2,1]"),
            (
                Add(
                    Multiply(Number(2), Sin(Divide(Multiply(Number(4), x), Number(2)))),
                    Multiply(Number(3), Cos(Multiply(Number(-2), x)))),
                "interval[fn:negate(fn:sqrt(q:13)),1,fn:sqrt(q:13),1]")
        ];

        foreach ((InputExpression input, string expectedRange) in cases)
        {
            AnalysisRequest request = Request(input, AngleUnit.Radians);
            SemanticExpression semantic = Build(input);
            Assert.True(SingleHarmonicRangeAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.Range,
                new ResourceBudget(),
                out ProofOutcome<RealSet> outcome));
            Assert.Equal(expectedRange, AssertProved(outcome).Canonical);
            AssertReplay(request, semantic, outcome);
        }
    }

    [Fact]
    public void CellProvedTotalPremiseIsAcceptedButRetainedHolesAreRejected()
    {
        InputExpression x = Variable();
        InputExpression harmonic = Add(Sin(x), Cos(x));
        InputExpression totalGuard = Add(
            harmonic,
            Multiply(
                Number(0),
                Divide(Number(1), Add(Power(x, 2), Number(1)))));
        SemanticExpression totalSemantic = Build(totalGuard);
        AnalysisRequest totalRequest = Request(totalGuard, AngleUnit.Radians);

        Assert.Equal(Build(harmonic).Value.Canonical, totalSemantic.Value.Canonical);
        Assert.NotEqual(Formula.True.Canonical, totalSemantic.DefinedWhen.Canonical);
        Assert.True(SingleHarmonicRangeAnalyzer.TryAnalyze(
            totalRequest,
            totalSemantic,
            AnalysisFeatures.Range,
            new ResourceBudget(),
            out ProofOutcome<RealSet> totalOutcome));
        AssertReplay(totalRequest, totalSemantic, totalOutcome);

        var validCertificate = Assert.IsType<SingleHarmonicRangeProofCertificate>(
            totalOutcome.Certificate);
        InputExpression rationalHole = Add(
            harmonic,
            Multiply(Number(0), Divide(Number(1), x)));
        AssertRetainedHoleRejected(rationalHole, validCertificate, totalOutcome.Value!);

        InputExpression tangentHole = Add(
            harmonic,
            Multiply(Number(0), Tan(x)));
        AssertRetainedHoleRejected(tangentHole, validCertificate, totalOutcome.Value!);
    }

    [Fact]
    public void ReplayRejectsEveryBoundFieldAndReconstructedClaimMutation()
    {
        InputExpression input = Add(Sin(Variable()), Cos(Variable()));
        AnalysisRequest request = Request(input, AngleUnit.Radians);
        SemanticExpression semantic = Build(input);
        Assert.True(SingleHarmonicRangeAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Range,
            new ResourceBudget(),
            out ProofOutcome<RealSet> outcome));
        RealSet range = AssertProved(outcome);
        var certificate = Assert.IsType<SingleHarmonicRangeProofCertificate>(
            outcome.Certificate);
        string claim = ClaimCanonical.For(range);

        AssertRejected(certificate with { Frequency = certificate.Frequency + 1 });
        AssertRejected(certificate with { Constant = certificate.Constant + BigRational.One });
        AssertRejected(certificate with
        {
            CosineCoefficient = certificate.CosineCoefficient + BigRational.One
        });
        AssertRejected(certificate with
        {
            SineCoefficient = certificate.SineCoefficient + BigRational.One
        });
        AssertRejected(certificate with
        {
            RadiusSquared = certificate.RadiusSquared + BigRational.One
        });
        AssertRejected(certificate with { FourierCanonical = "forged" });
        AssertRejected(certificate with { DefinednessCanonical = "false" });
        AssertRejected(certificate with { Rule = "untrusted" });
        AssertRejected(certificate with { Subject = "v:y" });
        AssertRejected(certificate with { ProvenFeature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { Feature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { Claim = "empty" });

        AnalysisRequest notRequested = request with
        {
            Features = AnalysisFeatures.Zeros
        };
        Assert.False(SingleHarmonicRangeCertificateChecker.Check(
            notRequested,
            semantic,
            certificate,
            claim,
            new ResourceBudget()));

        RealSet forgedRange = new IntervalSet(
            RealBound.Finite(new RationalReal(new BigRational(-2))),
            true,
            RealBound.Finite(new RationalReal(new BigRational(2))),
            true);
        var forgedClaim = new SingleHarmonicRangeProofCertificate(
            AnalysisFeatures.Range,
            semantic.Value.Canonical,
            ClaimCanonical.For(forgedRange),
            certificate.Frequency,
            certificate.Constant,
            certificate.CosineCoefficient,
            certificate.SineCoefficient,
            certificate.RadiusSquared,
            certificate.FourierCanonical,
            certificate.DefinednessCanonical,
            certificate.Rule);
        Assert.False(CertificateChecker.Check(
            request,
            semantic,
            ProofOutcome<RealSet>.Proved(forgedRange, forgedClaim)));

        void AssertRejected(SingleHarmonicRangeProofCertificate changed)
        {
            Assert.False(SingleHarmonicRangeCertificateChecker.Check(
                request,
                semantic,
                changed,
                claim,
                new ResourceBudget()));
            Assert.False(CertificateChecker.Check(
                request,
                semantic,
                ProofOutcome<RealSet>.Proved(range, changed)));
        }
    }

    [Fact]
    public void ReplayHonorsCancellationAndDeterministicWorkBudget()
    {
        InputExpression input = Add(Sin(Variable()), Cos(Variable()));
        AnalysisRequest request = Request(input, AngleUnit.Radians);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        SemanticExpression semantic = Assert.IsType<SemanticExpression>(report.Expression);
        var certificate = Assert.IsType<SingleHarmonicRangeProofCertificate>(
            report.Range.Certificate);
        string claim = ClaimCanonical.For(report.Range.Value!);

        Assert.Throws<AnalysisCancelledException>(() =>
            SingleHarmonicRangeCertificateChecker.Check(
                request,
                semantic,
                certificate,
                claim,
                new ResourceBudget(static () => false)));

        var exhausted = new ResourceBudget();
        exhausted.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            SingleHarmonicRangeCertificateChecker.Check(
                request,
                semantic,
                certificate,
                claim,
                exhausted));
    }

    private static void AssertRetainedHoleRejected(
        InputExpression input,
        SingleHarmonicRangeProofCertificate validCertificate,
        RealSet range)
    {
        SemanticExpression semantic = Build(input);
        AnalysisRequest request = Request(input, AngleUnit.Radians);
        SingleHarmonicRangeProofCertificate forged = validCertificate with
        {
            DefinednessCanonical = semantic.DefinedWhen.Canonical
        };

        Assert.Equal(validCertificate.Subject, semantic.Value.Canonical);
        Assert.NotEqual(Formula.True.Canonical, semantic.DefinedWhen.Canonical);
        Assert.False(SingleHarmonicRangeCertificateChecker.Check(
            request,
            semantic,
            forged,
            ClaimCanonical.For(range),
            new ResourceBudget()));
        Assert.False(CertificateChecker.Check(
            request,
            semantic,
            ProofOutcome<RealSet>.Proved(range, forged)));
    }

    private static void AssertReplay(
        AnalysisRequest request,
        SemanticExpression semantic,
        ProofOutcome<RealSet> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        var certificate = Assert.IsType<SingleHarmonicRangeProofCertificate>(
            outcome.Certificate);
        string claim = ClaimCanonical.For(AssertProved(outcome));
        Assert.True(SingleHarmonicRangeCertificateChecker.Check(
            request,
            semantic,
            certificate,
            claim,
            new ResourceBudget()));
        Assert.True(CertificateChecker.Check(request, semantic, outcome));
    }

    private static T AssertProved<T>(ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        return Assert.IsAssignableFrom<T>(outcome.Value);
    }

    private static AnalysisRequest Request(
        InputExpression input,
        AngleUnit angleUnit) =>
        new(
            input,
            AnalysisFeatures.Range,
            angleUnit,
            "x",
            static () => true);

    private static SemanticExpression Build(InputExpression input) =>
        new SemanticGraphBuilder(new ResourceBudget()).Build(input);

    private static InputExpression Variable() => InputExpression.Variable("x", Source);

    private static InputExpression Number(int value) => Number(new BigRational(value));

    private static InputExpression Number(BigRational value) =>
        InputExpression.Number(value, Source);

    private static InputExpression Add(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Add, left, right, Source);

    private static InputExpression Subtract(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Subtract, left, right, Source);

    private static InputExpression Multiply(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Multiply, left, right, Source);

    private static InputExpression Divide(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Divide, left, right, Source);

    private static InputExpression Power(InputExpression basis, int exponent) =>
        InputExpression.Binary(
            InputExpressionKind.Power,
            basis,
            Number(exponent),
            Source);

    private static InputExpression Sin(InputExpression argument) => Function("sin", argument);

    private static InputExpression Cos(InputExpression argument) => Function("cos", argument);

    private static InputExpression Tan(InputExpression argument) => Function("tan", argument);

    private static InputExpression Function(string name, InputExpression argument) =>
        InputExpression.Function(name, [argument], Source);
}
