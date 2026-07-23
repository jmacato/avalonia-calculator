using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class FixedRationalPowerAnalysisTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Fact]
    public void PrincipalPositiveAndNegativeHalfPowersCertifyTheirCoreFeatures()
    {
        AnalysisRequest positiveRequest = Request(Power(Variable(), 1, 2), AnalysisFeatures.All);
        AnalysisReport positive = AnalysisEngine.Analyze(positiveRequest);

        AssertAllFeaturesProved(positive);
        Assert.Equal("interval[q:0,1,+inf,0]", Proved(positive.Domain).Canonical);
        Assert.Equal("interval[q:0,1,+inf,0]", Proved(positive.Range).Canonical);
        Assert.Equal(FunctionParity.Neither, Proved(positive.Parity));
        Assert.Equal("points[q:0]", Proved(positive.Zeros).Canonical);
        Assert.Equal("q:0", ExactRealCanonical.Format(Proved(positive.YIntercept).Value!));
        AssertPoint(Proved(positive.Minima), "q:0", "q:0");
        Assert.Empty(Proved(positive.Maxima));
        Assert.Empty(Proved(positive.InflectionPoints));
        Assert.Empty(Proved(positive.VerticalAsymptotes));
        Assert.Empty(Proved(positive.HorizontalAsymptotes));
        Assert.Empty(Proved(positive.ObliqueAsymptotes));
        AssertRegion(Proved(positive.Monotonicity), 0, "interval[q:0,0,+inf,0]", Monotonicity.Increasing);
        Assert.Equal(PeriodicityKind.NotPeriodic, Proved(positive.Period).Kind);
        AssertPowerCertificatesReplay(positiveRequest, positive);

        AnalysisRequest negativeRequest = Request(Power(Variable(), -1, 2), AnalysisFeatures.All);
        AnalysisReport negative = AnalysisEngine.Analyze(negativeRequest);

        AssertAllFeaturesProved(negative);
        Assert.Equal("interval[q:0,0,+inf,0]", Proved(negative.Domain).Canonical);
        Assert.Equal("interval[q:0,0,+inf,0]", Proved(negative.Range).Canonical);
        Assert.Equal(FunctionParity.Neither, Proved(negative.Parity));
        Assert.IsType<EmptySet>(Proved(negative.Zeros));
        Assert.False(Proved(negative.YIntercept).HasValue);
        Assert.Empty(Proved(negative.Minima));
        Assert.Empty(Proved(negative.Maxima));
        Assert.Empty(Proved(negative.InflectionPoints));
        AssertAsymptote(Proved(negative.VerticalAsymptotes), AsymptoteOrientation.Vertical, "q:0");
        AssertAsymptote(Proved(negative.HorizontalAsymptotes), AsymptoteOrientation.Horizontal, "q:0");
        Assert.Empty(Proved(negative.ObliqueAsymptotes));
        AssertRegion(Proved(negative.Monotonicity), 0, "interval[q:0,0,+inf,0]", Monotonicity.Decreasing);
        Assert.Equal(PeriodicityKind.NotPeriodic, Proved(negative.Period).Kind);
        AssertPowerCertificatesReplay(negativeRequest, negative);
    }

    [Fact]
    public void PrincipalSquareRootOfSquareIsCertifiedAsAbsoluteValueWithoutUnsafeReassociation()
    {
        InputExpression square = IntegerPower(Variable(), 2);
        AnalysisRequest request = Request(Power(square, 1, 2), AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        AssertAllFeaturesProved(report);
        Assert.IsType<AllRealSet>(Proved(report.Domain));
        Assert.Equal("interval[q:0,1,+inf,0]", Proved(report.Range).Canonical);
        Assert.Equal(FunctionParity.Even, Proved(report.Parity));
        Assert.Equal("points[q:0]", Proved(report.Zeros).Canonical);
        AssertPoint(Proved(report.Minima), "q:0", "q:0");
        Assert.Empty(Proved(report.Maxima));
        Assert.Empty(Proved(report.InflectionPoints));
        Assert.Empty(Proved(report.VerticalAsymptotes));
        Assert.Empty(Proved(report.HorizontalAsymptotes));

        ImmutableArray<Asymptote> oblique = Proved(report.ObliqueAsymptotes);
        Assert.Equal(2, oblique.Length);
        Assert.Equal("q:-1", ExactRealCanonical.Format(oblique[0].Slope!));
        Assert.Equal("q:1", ExactRealCanonical.Format(oblique[1].Slope!));
        Assert.All(oblique, item => Assert.Equal("q:0", ExactRealCanonical.Format(item.Intercept!)));

        ImmutableArray<MonotoneRegion> regions = Proved(report.Monotonicity);
        AssertRegion(regions, 0, "interval[-inf,0,q:0,0]", Monotonicity.Decreasing);
        AssertRegion(regions, 1, "interval[q:0,0,+inf,0]", Monotonicity.Increasing);
        AssertPowerCertificatesReplay(request, report);
    }

    [Fact]
    public void NonintegralPowersNeverAcquireAnOddDenominatorNegativeBaseBranch()
    {
        foreach ((InputExpression expression, string domain, FunctionParity parity) in new[]
                 {
                     (Power(Variable(), 2, 3), "interval[q:0,1,+inf,0]", FunctionParity.Neither),
                     (Power(Negate(Variable()), 1, 3), "interval[-inf,0,q:0,1]", FunctionParity.Neither)
                 })
        {
            AnalysisRequest request = Request(expression, AnalysisFeatures.All);
            AnalysisReport report = AnalysisEngine.Analyze(request);

            AssertAllFeaturesProved(report);
            Assert.Equal(domain, Proved(report.Domain).Canonical);
            Assert.Equal("interval[q:0,1,+inf,0]", Proved(report.Range).Canonical);
            Assert.Equal(parity, Proved(report.Parity));
            Assert.Equal("points[q:0]", Proved(report.Zeros).Canonical);
            AssertPoint(Proved(report.Minima), "q:0", "q:0");
            AssertPowerCertificatesReplay(request, report);
        }
    }

    [Fact]
    public void RetainedHolesRemainVisibleInEveryPowerProof()
    {
        InputExpression withHole = Add(
            Variable(),
            Multiply(
                Number(0),
                Divide(Number(1), Subtract(Variable(), Number(1)))));
        AnalysisRequest request = Request(Power(withHole, 1, 2), AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        AssertAllFeaturesProved(report);
        Assert.Equal(
            "union[interval[q:0,1,q:1,0],interval[q:1,0,+inf,0]]",
            Proved(report.Domain).Canonical);
        Assert.Equal(
            "union[interval[q:0,1,q:1,0],interval[q:1,0,+inf,0]]",
            Proved(report.Range).Canonical);
        Assert.Equal(FunctionParity.Neither, Proved(report.Parity));
        Assert.Equal("points[q:0]", Proved(report.Zeros).Canonical);
        ImmutableArray<MonotoneRegion> regions = Proved(report.Monotonicity);
        AssertRegion(regions, 0, "interval[q:0,0,q:1,0]", Monotonicity.Increasing);
        AssertRegion(regions, 1, "interval[q:1,0,+inf,0]", Monotonicity.Increasing);
        AssertPowerCertificatesReplay(request, report);
    }

    [Fact]
    public void RationalBasePowersCertifyInteriorExtremaInflectionsAndTailLimits()
    {
        InputExpression quadratic = Add(IntegerPower(Variable(), 2), Number(1));
        AnalysisRequest request = Request(Power(quadratic, -1, 2), AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        AssertAllFeaturesProved(report);
        Assert.IsType<AllRealSet>(Proved(report.Domain));
        Assert.Equal("interval[q:0,0,q:1,1]", Proved(report.Range).Canonical);
        Assert.Equal(FunctionParity.Even, Proved(report.Parity));
        Assert.IsType<EmptySet>(Proved(report.Zeros));
        Assert.Equal("q:1", ExactRealCanonical.Format(Proved(report.YIntercept).Value!));
        Assert.Empty(Proved(report.Minima));
        AssertPoint(Proved(report.Maxima), "q:0", "q:1");
        Assert.Equal(2, Proved(report.InflectionPoints).Length);
        Assert.Empty(Proved(report.VerticalAsymptotes));
        AssertAsymptote(
            Proved(report.HorizontalAsymptotes),
            AsymptoteOrientation.Horizontal,
            "q:0");
        Assert.Empty(Proved(report.ObliqueAsymptotes));
        ImmutableArray<MonotoneRegion> regions = Proved(report.Monotonicity);
        AssertRegion(regions, 0, "interval[-inf,0,q:0,0]", Monotonicity.Increasing);
        AssertRegion(regions, 1, "interval[q:0,0,+inf,0]", Monotonicity.Decreasing);
        AssertPowerCertificatesReplay(request, report);
    }

    [Fact]
    public void IsolatedAndCancelledZeroBasesPreserveTheirExactDomains()
    {
        AnalysisRequest isolatedRequest = Request(
            Power(Negate(IntegerPower(Variable(), 2)), 1, 2),
            AnalysisFeatures.All);
        AnalysisReport isolated = AnalysisEngine.Analyze(isolatedRequest);

        AssertAllFeaturesProved(isolated);
        Assert.Equal("points[q:0]", Proved(isolated.Domain).Canonical);
        Assert.Equal("points[q:0]", Proved(isolated.Range).Canonical);
        Assert.Equal(FunctionParity.Both, Proved(isolated.Parity));
        Assert.Equal("points[q:0]", Proved(isolated.Zeros).Canonical);
        Assert.Equal("q:0", ExactRealCanonical.Format(Proved(isolated.YIntercept).Value!));
        Assert.Empty(Proved(isolated.Minima));
        Assert.Empty(Proved(isolated.Maxima));
        Assert.Empty(Proved(isolated.InflectionPoints));
        Assert.Empty(Proved(isolated.Monotonicity));
        Assert.Equal(PeriodicityKind.NotPeriodic, Proved(isolated.Period).Kind);
        AssertPowerCertificatesReplay(isolatedRequest, isolated);

        InputExpression cancelled = Multiply(
            Number(0),
            Divide(Number(1), Variable()));
        AnalysisRequest cancelledRequest = Request(Power(cancelled, 3, 2), AnalysisFeatures.All);
        AnalysisReport guardedZero = AnalysisEngine.Analyze(cancelledRequest);

        AssertAllFeaturesProved(guardedZero);
        const string punctured =
            "union[interval[-inf,0,q:0,0],interval[q:0,0,+inf,0]]";
        Assert.Equal(punctured, Proved(guardedZero.Domain).Canonical);
        Assert.Equal("points[q:0]", Proved(guardedZero.Range).Canonical);
        Assert.Equal(FunctionParity.Both, Proved(guardedZero.Parity));
        Assert.Equal(punctured, Proved(guardedZero.Zeros).Canonical);
        Assert.False(Proved(guardedZero.YIntercept).HasValue);
        Assert.Empty(Proved(guardedZero.Minima));
        Assert.Empty(Proved(guardedZero.Maxima));
        Assert.Equal(2, Proved(guardedZero.Monotonicity).Length);
        Assert.All(
            Proved(guardedZero.Monotonicity),
            region => Assert.Equal(Monotonicity.Constant, region.Direction));
        AssertPowerCertificatesReplay(cancelledRequest, guardedZero);
    }

    [Fact]
    public void PowerAndNamedSquareRootNormalizationsAreMetamorphic()
    {
        InputExpression x = Variable();
        AssertSameClaims(
            AnalysisEngine.Analyze(Request(Power(x, 1, 2), AnalysisFeatures.All)),
            AnalysisEngine.Analyze(Request(Function("sqrt", x), AnalysisFeatures.All)));

        InputExpression shifted = Add(x, Number(3));
        InputExpression shiftedSquare = IntegerPower(shifted, 2);
        AssertSameClaims(
            AnalysisEngine.Analyze(Request(Power(shiftedSquare, 1, 2), AnalysisFeatures.All)),
            AnalysisEngine.Analyze(Request(Function("sqrt", shiftedSquare), AnalysisFeatures.All)));

        AnalysisReport reduced = AnalysisEngine.Analyze(Request(
            InputExpression.Binary(
                InputExpressionKind.Power,
                x,
                Number(new BigRational(2, 4)),
                Source),
            AnalysisFeatures.All));
        AnalysisReport canonical = AnalysisEngine.Analyze(Request(Power(x, 1, 2), AnalysisFeatures.All));
        AssertSameClaims(canonical, reduced);

        InputExpression parserShapedNegativeExponent = Divide(
            Negate(Number(1)),
            Number(2));
        AnalysisRequest parserRequest = Request(
            InputExpression.Binary(
                InputExpressionKind.Power,
                x,
                parserShapedNegativeExponent,
                Source),
            AnalysisFeatures.All);
        AnalysisReport parserShaped = AnalysisEngine.Analyze(parserRequest);
        AnalysisReport literalNegative = AnalysisEngine.Analyze(Request(
            Power(x, -1, 2),
            AnalysisFeatures.All));
        AssertSameClaims(literalNegative, parserShaped);
        AssertPowerCertificatesReplay(parserRequest, parserShaped);
    }

    [Fact]
    public void FixedEvenRootsUseTheSameGuardedRationalPowerProofs()
    {
        InputExpression x = Variable();
        AnalysisRequest fourthRootRequest = Request(
            Function("root", x, Number(4)),
            AnalysisFeatures.All);
        AnalysisReport fourthRoot = AnalysisEngine.Analyze(fourthRootRequest);
        AnalysisReport quarterPower = AnalysisEngine.Analyze(Request(
            Power(x, 1, 4),
            AnalysisFeatures.All));
        AssertSameClaims(quarterPower, fourthRoot);
        AssertPowerCertificatesReplay(fourthRootRequest, fourthRoot);

        AnalysisRequest negativeRootRequest = Request(
            Function("root", x, Negate(Number(2))),
            AnalysisFeatures.All);
        AnalysisReport negativeRoot = AnalysisEngine.Analyze(negativeRootRequest);
        AnalysisReport negativeHalfPower = AnalysisEngine.Analyze(Request(
            Power(x, -1, 2),
            AnalysisFeatures.All));
        AssertSameClaims(negativeHalfPower, negativeRoot);
        AssertPowerCertificatesReplay(negativeRootRequest, negativeRoot);

        InputExpression square = IntegerPower(x, 2);
        AnalysisReport evenRootOfSquare = AnalysisEngine.Analyze(Request(
            Function("root", square, Number(2)),
            AnalysisFeatures.All));
        AnalysisReport powerOfSquare = AnalysisEngine.Analyze(Request(
            Power(square, 1, 2),
            AnalysisFeatures.All));
        AssertSameClaims(powerOfSquare, evenRootOfSquare);
    }

    [Fact]
    public void GeneratedReducedExponentsReplayAcrossPositiveAndNegativePowers()
    {
        int replayed = 0;
        for (int denominator = 2; denominator <= 5; denominator++)
        {
            for (int numerator = -3; numerator <= 3; numerator++)
            {
                if (numerator == 0 ||
                    ExactInteger.GreatestCommonDivisor(
                        ExactInteger.Abs(numerator),
                        denominator) != ExactInteger.One)
                {
                    continue;
                }

                InputExpression basis = (replayed & 1) == 0
                    ? Variable()
                    : IntegerPower(Variable(), 2);
                AnalysisFeatures features =
                    AnalysisFeatures.Range |
                    AnalysisFeatures.Parity |
                    AnalysisFeatures.Zeros |
                    AnalysisFeatures.VerticalAsymptotes |
                    AnalysisFeatures.HorizontalAsymptotes |
                    AnalysisFeatures.Monotonicity |
                    AnalysisFeatures.Period;
                AnalysisRequest request = Request(
                    Power(basis, numerator, denominator),
                    features);
                AnalysisReport report = AnalysisEngine.Analyze(request);

                Assert.Equal(ProofState.Proved, report.Range.State);
                Assert.Equal(ProofState.Proved, report.Parity.State);
                Assert.Equal(ProofState.Proved, report.Zeros.State);
                Assert.Equal(ProofState.Proved, report.VerticalAsymptotes.State);
                Assert.Equal(ProofState.Proved, report.HorizontalAsymptotes.State);
                Assert.Equal(ProofState.Proved, report.Monotonicity.State);
                Assert.Equal(ProofState.Proved, report.Period.State);
                AssertReplay(request, report, report.Range, true);
                AssertReplay(request, report, report.Parity, true);
                AssertReplay(request, report, report.Zeros, true);
                AssertReplay(request, report, report.VerticalAsymptotes, true);
                AssertReplay(request, report, report.HorizontalAsymptotes, true);
                AssertReplay(request, report, report.Monotonicity, true);
                AssertReplay(request, report, report.Period, true);
                replayed++;
            }
        }

        Assert.Equal(18, replayed);
    }

    [Fact]
    public void DedicatedCertificatesRejectExponentChartFiberClaimAndRuleMutations()
    {
        InputExpression withHole = Add(
            Variable(),
            Multiply(
                Number(0),
                Divide(Number(1), Subtract(Variable(), Number(1)))));
        AnalysisRequest request = Request(Power(withHole, 2, 3), AnalysisFeatures.Range);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        RealSet range = Proved(report.Range);
        var certificate = Assert.IsType<FixedRationalPowerProofCertificate>(report.Range.Certificate);
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Range));

        AssertRejected(certificate with { Exponent = new BigRational(3, 4) });
        AssertRejected(certificate with { BaseNumerator = UnivariatePolynomial.One });
        AssertRejected(certificate with { Rule = "untrusted" });
        AssertRejected(certificate with
        {
            SignChart = certificate.SignChart with
            {
                Formula = new PolynomialBoolean(true)
            }
        });
        AssertRejected(certificate with
        {
            RangeFibers = certificate.RangeFibers.SetItem(
                0,
                certificate.RangeFibers[0] with
                {
                    Sample = certificate.RangeFibers[0].Sample + BigRational.One
                })
        });
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<RealSet>.Proved(EmptySet.Instance, certificate)));
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression! with { SourceOperands = [] },
            ProofOutcome<RealSet>.Proved(range, certificate)));

        void AssertRejected(FixedRationalPowerProofCertificate changed) =>
            Assert.False(CertificateChecker.Check(
                request,
                report.Expression!,
                ProofOutcome<RealSet>.Proved(range, changed)));
    }

    [Fact]
    public void FixedPowerLimitsAndCancellationFailDeterministically()
    {
        AnalysisReport exponent = AnalysisEngine.Analyze(Request(
            Power(Variable(), 1, AnalysisLimits.UnivariateDegree + 1),
            AnalysisFeatures.Range));
        Assert.Equal(ProofState.Unknown, exponent.Range.State);
        Assert.Equal(UnknownReason.BudgetExceeded, exponent.Range.UnknownReason);

        InputExpression highDegree = Power(
            Add(IntegerPower(Variable(), AnalysisLimits.UnivariateDegree), Number(1)),
            2,
            3);
        AnalysisReport degree = AnalysisEngine.Analyze(Request(highDegree, AnalysisFeatures.Range));
        Assert.Equal(ProofState.Unknown, degree.Range.State);
        Assert.Equal(UnknownReason.BudgetExceeded, degree.Range.UnknownReason);

        ExactInteger huge = ExactInteger.One << AnalysisLimits.CoefficientBits;
        InputExpression oversized = Power(
            Add(Multiply(Number(new BigRational(huge)), Variable()), Number(1)),
            1,
            2);
        AnalysisReport coefficient = AnalysisEngine.Analyze(Request(oversized, AnalysisFeatures.Range));
        Assert.Equal(ProofState.Unknown, coefficient.Range.State);
        Assert.Equal(UnknownReason.BudgetExceeded, coefficient.Range.UnknownReason);

        AnalysisRequest cancelled = new(
            Power(Variable(), 1, 2),
            AnalysisFeatures.All,
            AngleUnit.Radians,
            "x",
            static () => false);
        Assert.Throws<AnalysisCancelledException>(() => AnalysisEngine.Analyze(cancelled));
    }

    private static void AssertAllFeaturesProved(AnalysisReport report)
    {
        Assert.Equal(ProofState.Proved, report.Domain.State);
        Assert.Equal(ProofState.Proved, report.Range.State);
        Assert.Equal(ProofState.Proved, report.Parity.State);
        Assert.Equal(ProofState.Proved, report.Zeros.State);
        Assert.Equal(ProofState.Proved, report.YIntercept.State);
        Assert.Equal(ProofState.Proved, report.Minima.State);
        Assert.Equal(ProofState.Proved, report.Maxima.State);
        Assert.Equal(ProofState.Proved, report.InflectionPoints.State);
        Assert.Equal(ProofState.Proved, report.VerticalAsymptotes.State);
        Assert.Equal(ProofState.Proved, report.HorizontalAsymptotes.State);
        Assert.Equal(ProofState.Proved, report.ObliqueAsymptotes.State);
        Assert.True(
            report.Monotonicity.State == ProofState.Proved,
            $"Monotonicity: {report.Monotonicity.UnknownReason}");
        Assert.Equal(ProofState.Proved, report.Period.State);
    }

    private static void AssertPowerCertificatesReplay(
        AnalysisRequest request,
        AnalysisReport report)
    {
        AssertReplay(request, report, report.Domain, false);
        AssertReplay(request, report, report.Range, true);
        AssertReplay(request, report, report.Parity, true);
        AssertReplay(request, report, report.Zeros, true);
        AssertReplay(request, report, report.YIntercept, true);
        AssertReplay(request, report, report.Minima, true);
        AssertReplay(request, report, report.Maxima, true);
        AssertReplay(request, report, report.InflectionPoints, true);
        AssertReplay(request, report, report.VerticalAsymptotes, true);
        AssertReplay(request, report, report.HorizontalAsymptotes, true);
        AssertReplay(request, report, report.ObliqueAsymptotes, true);
        AssertReplay(request, report, report.Monotonicity, true);
        AssertReplay(request, report, report.Period, true);
    }

    private static void AssertReplay<T>(
        AnalysisRequest request,
        AnalysisReport report,
        ProofOutcome<T> outcome,
        bool fixedPower)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.NotNull(outcome.Certificate);
        if (fixedPower)
        {
            Assert.IsType<FixedRationalPowerProofCertificate>(outcome.Certificate);
        }

        Assert.True(CertificateChecker.Check(request, report.Expression!, outcome));
    }

    private static void AssertPoint(
        ImmutableArray<FeaturePoint> points,
        string x,
        string y)
    {
        var point = Assert.IsType<ConstantYFeaturePoint>(Assert.Single(points));
        Assert.Equal(x, ExactRealCanonical.Format(Assert.IsType<SingletonReal>(point.X).Value));
        Assert.Equal(y, ExactRealCanonical.Format(point.Y));
    }

    private static void AssertRegion(
        ImmutableArray<MonotoneRegion> regions,
        int index,
        string canonical,
        Monotonicity direction)
    {
        Assert.True(index < regions.Length);
        Assert.Equal(canonical, regions[index].Region.Canonical);
        Assert.Equal(direction, regions[index].Direction);
    }

    private static void AssertAsymptote(
        ImmutableArray<Asymptote> asymptotes,
        AsymptoteOrientation orientation,
        string coordinate)
    {
        Asymptote asymptote = Assert.Single(asymptotes);
        Assert.Equal(orientation, asymptote.Orientation);
        Assert.Equal(
            coordinate,
            ExactRealCanonical.Format(Assert.IsType<SingletonReal>(asymptote.Coordinate).Value));
    }

    private static void AssertSameClaims(AnalysisReport expected, AnalysisReport actual)
    {
        AssertSameClaim(expected.Domain, actual.Domain);
        AssertSameClaim(expected.Range, actual.Range);
        AssertSameClaim(expected.Parity, actual.Parity);
        AssertSameClaim(expected.Zeros, actual.Zeros);
        AssertSameClaim(expected.YIntercept, actual.YIntercept);
        AssertSameClaim(expected.Minima, actual.Minima);
        AssertSameClaim(expected.Maxima, actual.Maxima);
        AssertSameClaim(expected.InflectionPoints, actual.InflectionPoints);
        AssertSameClaim(expected.VerticalAsymptotes, actual.VerticalAsymptotes);
        AssertSameClaim(expected.HorizontalAsymptotes, actual.HorizontalAsymptotes);
        AssertSameClaim(expected.ObliqueAsymptotes, actual.ObliqueAsymptotes);
        AssertSameClaim(expected.Monotonicity, actual.Monotonicity);
        AssertSameClaim(expected.Period, actual.Period);
    }

    private static void AssertSameClaim<T>(ProofOutcome<T> expected, ProofOutcome<T> actual)
    {
        Assert.Equal(expected.State, actual.State);
        Assert.Equal(expected.UnknownReason, actual.UnknownReason);
        if (expected.State == ProofState.Proved)
        {
            Assert.Equal(
                ClaimCanonical.For(expected.Value!),
                ClaimCanonical.For(actual.Value!));
        }
    }

    private static T Proved<T>(ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.NotNull(outcome.Value);
        return outcome.Value!;
    }

    private static AnalysisRequest Request(InputExpression expression, AnalysisFeatures features)
    {
        return new AnalysisRequest(expression, features, AngleUnit.Radians, "x", static () => true);
    }

    private static InputExpression Variable()
    {
        return InputExpression.Variable("x", Source);
    }

    private static InputExpression Number(int value)
    {
        return Number(new BigRational(value));
    }

    private static InputExpression Number(BigRational value)
    {
        return InputExpression.Number(value, Source);
    }

    private static InputExpression Negate(InputExpression value)
    {
        return InputExpression.Unary(InputExpressionKind.Negate, value, Source);
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

    private static InputExpression Function(string name, params InputExpression[] arguments)
    {
        return InputExpression.Function(name, arguments.ToImmutableArray(), Source);
    }

    private static InputExpression IntegerPower(InputExpression basis, int exponent)
    {
        return InputExpression.Binary(InputExpressionKind.Power, basis, Number(exponent), Source);
    }

    private static InputExpression Power(InputExpression basis, int numerator, int denominator)
    {
        return InputExpression.Binary(
            InputExpressionKind.Power,
            basis,
            Number(new BigRational(numerator, denominator)),
            Source);
    }
}
