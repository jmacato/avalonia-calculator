using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class AffineFloorAnalysisTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Fact]
    public void FloorOfIdentityHasExactStepFunctionSemanticsAndReplayableCertificates()
    {
        (AnalysisRequest request, SemanticExpression semantic) = Build(Floor(Variable()));
        AffineFloorContext context = Context(request, semantic);

        Assert.Equal(BigRational.One, context.Slope);
        Assert.Equal(BigRational.Zero, context.Intercept);
        Assert.Equal("affine-floor[1,0]", context.NormalizedArgumentCanonical);
        AssertEveryFeatureReplays(request, semantic);

        Assert.IsType<AllRealSet>(Proved<RealSet>(
            request,
            semantic,
            AnalysisFeatures.Domain));

        var range = Assert.IsType<IntegerLatticeSet>(Proved<RealSet>(
            request,
            semantic,
            AnalysisFeatures.Range));
        Assert.Equal("n", range.Expression);
        Assert.Equal(["n"], range.Parameters);
        Assert.Equal(["n ∈ ℤ"], range.Predicates);
        Assert.Equal("integer-lattice[n;n;n ∈ ℤ]", range.Canonical);

        Assert.Equal(
            "interval[q:0,1,q:1,0]",
            Proved<RealSet>(request, semantic, AnalysisFeatures.Zeros).Canonical);
        Assert.Equal(FunctionParity.Neither, Proved<FunctionParity>(
            request,
            semantic,
            AnalysisFeatures.Parity));
        Assert.Equal(
            ExactInteger.Zero,
            Integer(Proved<OptionalValue<ExactReal>>(
                request,
                semantic,
                AnalysisFeatures.YIntercept).Value!));

        Assert.Empty(Proved<ImmutableArray<FeaturePoint>>(
            request,
            semantic,
            AnalysisFeatures.Minima));
        Assert.Empty(Proved<ImmutableArray<FeaturePoint>>(
            request,
            semantic,
            AnalysisFeatures.Maxima));
        Assert.Empty(Proved<ImmutableArray<FeaturePoint>>(
            request,
            semantic,
            AnalysisFeatures.InflectionPoints));
        Assert.Empty(Proved<ImmutableArray<Asymptote>>(
            request,
            semantic,
            AnalysisFeatures.VerticalAsymptotes));
        Assert.Empty(Proved<ImmutableArray<Asymptote>>(
            request,
            semantic,
            AnalysisFeatures.HorizontalAsymptotes));
        Assert.Empty(Proved<ImmutableArray<Asymptote>>(
            request,
            semantic,
            AnalysisFeatures.ObliqueAsymptotes));

        MonotoneRegion monotone = Assert.Single(Proved<ImmutableArray<MonotoneRegion>>(
            request,
            semantic,
            AnalysisFeatures.Monotonicity));
        Assert.Equal(Monotonicity.Constant, monotone.Direction);
        AssertPeriodicCells(
            Assert.IsType<PeriodicIntervalSet>(monotone.Region),
            BigRational.One,
            BigRational.Zero,
            true,
            BigRational.One,
            false);

        Periodicity period = Proved<Periodicity>(
            request,
            semantic,
            AnalysisFeatures.Period);
        Assert.Equal(PeriodicityKind.NotPeriodic, period.Kind);
        Assert.Null(period.FundamentalPeriod);
    }

    [Fact]
    public void ProductionEnginePublishesOnlyCentrallyAcceptedAffineFloorProofs()
    {
        (AnalysisRequest request, _) = Build(Floor(
            Add(Multiply(Number(2), Variable()), Number(1))));
        AnalysisReport report = AnalysisEngine.Analyze(request);
        SemanticExpression semantic = Assert.IsType<SemanticExpression>(report.Expression);

        Assert.Equal(ProofState.Proved, report.Domain.State);
        Assert.True(report.Domain.Certificate is
            DomainProofCertificate or AffineFloorProofCertificate);
        Assert.True(CertificateChecker.Check(request, semantic, report.Domain));

        AssertAffineFloor(report.Range);
        AssertAffineFloor(report.Parity);
        AssertAffineFloor(report.Zeros);
        AssertAffineFloor(report.YIntercept);
        AssertAffineFloor(report.Minima);
        AssertAffineFloor(report.Maxima);
        AssertAffineFloor(report.InflectionPoints);
        AssertAffineFloor(report.VerticalAsymptotes);
        AssertAffineFloor(report.HorizontalAsymptotes);
        AssertAffineFloor(report.ObliqueAsymptotes);
        AssertAffineFloor(report.Monotonicity);
        AssertAffineFloor(report.Period);
        return;

        void AssertAffineFloor<T>(ProofOutcome<T> outcome)
        {
            Assert.Equal(ProofState.Proved, outcome.State);
            var certificate = Assert.IsType<AffineFloorProofCertificate>(outcome.Certificate);
            Assert.Equal(outcome.Certificate!.Feature, certificate.ProvenFeature);
            Assert.True(CertificateChecker.Check(request, semantic, outcome));
        }
    }

    [Fact]
    public void PositiveRationalAffineFloorHasExactHalfOpenCells()
    {
        InputExpression affine = Divide(
            Subtract(Multiply(Number(3), Variable()), Number(2)),
            Number(4));
        (AnalysisRequest request, SemanticExpression semantic) = Build(Floor(affine));
        AffineFloorContext context = Context(request, semantic);

        Assert.Equal(new BigRational(3, 4), context.Slope);
        Assert.Equal(new BigRational(-1, 2), context.Intercept);
        Assert.Equal(
            "interval[q:2/3,1,q:2,0]",
            Proved<RealSet>(request, semantic, AnalysisFeatures.Zeros).Canonical);
        Assert.Equal(
            new ExactInteger(-1),
            Integer(Proved<OptionalValue<ExactReal>>(
                request,
                semantic,
                AnalysisFeatures.YIntercept).Value!));
        Assert.Equal(FunctionParity.Neither, Proved<FunctionParity>(
            request,
            semantic,
            AnalysisFeatures.Parity));

        MonotoneRegion monotone = Assert.Single(Proved<ImmutableArray<MonotoneRegion>>(
            request,
            semantic,
            AnalysisFeatures.Monotonicity));
        AssertPeriodicCells(
            Assert.IsType<PeriodicIntervalSet>(monotone.Region),
            new BigRational(4, 3),
            new BigRational(2, 3),
            true,
            new BigRational(2),
            false);
        AssertEveryFeatureReplays(request, semantic);
    }

    [Fact]
    public void ReflectedAffineFloorReversesCellEndpointClosureWithoutLosingExactness()
    {
        InputExpression affine = Add(
            Multiply(Number(-3), Variable()),
            Number(new BigRational(5, 2)));
        (AnalysisRequest request, SemanticExpression semantic) = Build(Floor(affine));
        AffineFloorContext context = Context(request, semantic);

        Assert.Equal(new BigRational(-3), context.Slope);
        Assert.Equal(new BigRational(5, 2), context.Intercept);
        Assert.Equal(
            "interval[q:1/2,0,q:5/6,1]",
            Proved<RealSet>(request, semantic, AnalysisFeatures.Zeros).Canonical);
        Assert.Equal(
            new ExactInteger(2),
            Integer(Proved<OptionalValue<ExactReal>>(
                request,
                semantic,
                AnalysisFeatures.YIntercept).Value!));

        MonotoneRegion monotone = Assert.Single(Proved<ImmutableArray<MonotoneRegion>>(
            request,
            semantic,
            AnalysisFeatures.Monotonicity));
        AssertPeriodicCells(
            Assert.IsType<PeriodicIntervalSet>(monotone.Region),
            new BigRational(1, 3),
            new BigRational(1, 2),
            false,
            new BigRational(5, 6),
            true);
        AssertEveryFeatureReplays(request, semantic);
    }

    [Theory]
    [InlineData(7, 3, 2, false, 1)]
    [InlineData(-7, 3, -3, false, 1)]
    [InlineData(1, 2, 0, true, 2)]
    [InlineData(-1, 2, -1, false, 1)]
    public void ConstantFloorsUseExactConstantFunctionSemantics(
        int numerator,
        int denominator,
        int expectedValue,
        bool zerosAreAllReals,
        int expectedParity)
    {
        (AnalysisRequest request, SemanticExpression semantic) = Build(
            Floor(Number(new BigRational(numerator, denominator))));

        Assert.Equal(
            $"points[q:{expectedValue}]",
            Proved<RealSet>(request, semantic, AnalysisFeatures.Range).Canonical);
        RealSet zeros = Proved<RealSet>(request, semantic, AnalysisFeatures.Zeros);
        Assert.Equal(zerosAreAllReals, zeros is AllRealSet);
        if (!zerosAreAllReals)
        {
            Assert.IsType<EmptySet>(zeros);
        }

        Assert.Equal((FunctionParity)expectedParity, Proved<FunctionParity>(
            request,
            semantic,
            AnalysisFeatures.Parity));
        Assert.Equal(
            new ExactInteger(expectedValue),
            Integer(Proved<OptionalValue<ExactReal>>(
                request,
                semantic,
                AnalysisFeatures.YIntercept).Value!));

        MonotoneRegion monotone = Assert.Single(Proved<ImmutableArray<MonotoneRegion>>(
            request,
            semantic,
            AnalysisFeatures.Monotonicity));
        Assert.IsType<AllRealSet>(monotone.Region);
        Assert.Equal(Monotonicity.Constant, monotone.Direction);

        Periodicity period = Proved<Periodicity>(
            request,
            semantic,
            AnalysisFeatures.Period);
        Assert.Equal(PeriodicityKind.PeriodicWithoutFundamentalPeriod, period.Kind);
        Assert.Null(period.FundamentalPeriod);

        Asymptote horizontal = Assert.Single(Proved<ImmutableArray<Asymptote>>(
            request,
            semantic,
            AnalysisFeatures.HorizontalAsymptotes));
        Assert.Equal(new ExactInteger(expectedValue), Integer(
            Assert.IsType<SingletonReal>(horizontal.Coordinate).Value));
        Assert.Equal(new ExactInteger(expectedValue), Integer(horizontal.Intercept!));
        AssertEveryFeatureReplays(request, semantic);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(1, -1)]
    [InlineData(2, 1)]
    [InlineData(-1, 0)]
    [InlineData(-2, 3)]
    public void EveryNonconstantAffineFloorIsProvedNeitherEvenNorOdd(
        int slope,
        int intercept)
    {
        InputExpression affine = Add(
            Multiply(Number(slope), Variable()),
            Number(intercept));
        (AnalysisRequest request, SemanticExpression semantic) = Build(Floor(affine));

        Assert.Equal(FunctionParity.Neither, Proved<FunctionParity>(
            request,
            semantic,
            AnalysisFeatures.Parity));
        AssertEveryFeatureReplays(request, semantic);
    }

    [Fact]
    public void GeneratedRationalAffineCorpusPreservesEveryCellBoundaryExactly()
    {
        for (int slopeNumerator = -3; slopeNumerator <= 3; slopeNumerator++)
        {
            if (slopeNumerator == 0)
            {
                continue;
            }

            for (int slopeDenominator = 1; slopeDenominator <= 2; slopeDenominator++)
            {
                BigRational slope = new(slopeNumerator, slopeDenominator);
                for (int interceptNumerator = -3; interceptNumerator <= 3; interceptNumerator++)
                {
                    BigRational intercept = new(interceptNumerator, 2);
                    InputExpression affine = Add(
                        Multiply(Number(slope), Variable()),
                        Number(intercept));
                    (AnalysisRequest request, SemanticExpression semantic) = Build(Floor(affine));

                    var zeros = Assert.IsType<IntervalSet>(Proved<RealSet>(
                        request,
                        semantic,
                        AnalysisFeatures.Zeros));
                    BigRational zeroBoundary = -intercept / slope;
                    BigRational oneBoundary = (BigRational.One - intercept) / slope;
                    BigRational expectedLower = slope.Sign > 0
                        ? zeroBoundary
                        : oneBoundary;
                    BigRational expectedUpper = slope.Sign > 0
                        ? oneBoundary
                        : zeroBoundary;
                    Assert.Equal(expectedLower, Rational(zeros.Lower.Value!));
                    Assert.Equal(slope.Sign > 0, zeros.IncludesLower);
                    Assert.Equal(expectedUpper, Rational(zeros.Upper.Value!));
                    Assert.Equal(slope.Sign < 0, zeros.IncludesUpper);

                    MonotoneRegion monotone = Assert.Single(
                        Proved<ImmutableArray<MonotoneRegion>>(
                            request,
                            semantic,
                            AnalysisFeatures.Monotonicity));
                    AssertPeriodicCells(
                        Assert.IsType<PeriodicIntervalSet>(monotone.Region),
                        slope.Reciprocal().Abs(),
                        expectedLower,
                        slope.Sign > 0,
                        expectedUpper,
                        slope.Sign < 0);
                    Assert.Equal(
                        intercept.Floor(),
                        Integer(Proved<OptionalValue<ExactReal>>(
                            request,
                            semantic,
                            AnalysisFeatures.YIntercept).Value!));
                    Assert.Equal(FunctionParity.Neither, Proved<FunctionParity>(
                        request,
                        semantic,
                        AnalysisFeatures.Parity));
                    Assert.IsType<IntegerLatticeSet>(Proved<RealSet>(
                        request,
                        semantic,
                        AnalysisFeatures.Range));

                    Replay<RealSet>(AnalysisFeatures.Zeros);
                    Replay<ImmutableArray<MonotoneRegion>>(AnalysisFeatures.Monotonicity);
                    Replay<OptionalValue<ExactReal>>(AnalysisFeatures.YIntercept);
                    Replay<FunctionParity>(AnalysisFeatures.Parity);

                    void Replay<T>(AnalysisFeatures feature)
                    {
                        ProofOutcome<T> outcome = Analyze<T>(request, semantic, feature);
                        Assert.True(Check(request, semantic, outcome));
                    }
                }
            }
        }
    }

    [Fact]
    public void UppercaseSourceSpellingIsAcceptedOnlyAfterSemanticCanonicalization()
    {
        (AnalysisRequest request, SemanticExpression semantic) = Build(
            Function("FLOOR", Add(Multiply(Number(2), Variable()), Number(1))));

        Assert.Equal("floor", semantic.Value.Name);
        Assert.True(AffineFloorAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Range,
            new ResourceBudget(),
            out ProofOutcome<RealSet> outcome));
        Assert.IsType<IntegerLatticeSet>(outcome.Value);
        Assert.True(Check(request, semantic, outcome));
    }

    [Theory]
    [MemberData(nameof(UnsupportedExpressions))]
    public void NonAffinePartialOrNoncanonicalArgumentsAreRejected(object expressionValue)
    {
        var expression = Assert.IsType<InputExpression>(expressionValue);
        (AnalysisRequest request, SemanticExpression semantic) = Build(expression);

        foreach (AnalysisFeatures feature in new[]
                 {
                     AnalysisFeatures.Domain,
                     AnalysisFeatures.Range,
                     AnalysisFeatures.Zeros,
                     AnalysisFeatures.Monotonicity
                 })
        {
            Assert.False(AffineFloorAnalyzer.TryAnalyze(
                request,
                semantic,
                feature,
                new ResourceBudget(),
                out ProofOutcome<object> _));
        }
    }

    [Fact]
    public void RetainedHoleIsNotErasedWhenValueNormalizesToAConstant()
    {
        InputExpression x = Variable();
        InputExpression guardedZero = Multiply(
            Number(0),
            Divide(Number(1), x));
        (AnalysisRequest request, SemanticExpression semantic) = Build(Floor(guardedZero));

        Assert.Equal("q:0", semantic.Value.Operands[0].Canonical);
        Assert.NotEqual("true", semantic.DefinedWhen.Canonical);
        Assert.False(AffineFloorAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Domain,
            new ResourceBudget(),
            out ProofOutcome<RealSet> _));
    }

    [Fact]
    public void CertificateRejectsEveryStoredPremiseAndClaimMutation()
    {
        (AnalysisRequest request, SemanticExpression semantic) = Build(Floor(
            Add(Multiply(Number(2), Variable()), Number(-4))));
        ProofOutcome<RealSet> range = Analyze<RealSet>(
            request,
            semantic,
            AnalysisFeatures.Range);
        var certificate = Assert.IsType<AffineFloorProofCertificate>(range.Certificate);
        string claim = ClaimCanonical.ForObject(range.Value!);

        Assert.True(Check(request, semantic, range));
        Assert.True(CertificateChecker.Check(request, semantic, range));
        AssertRejected(certificate with { Feature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { Subject = "mutated" });
        AssertRejected(certificate with { SubjectCanonical = "mutated" });
        AssertRejected(certificate with { ArgumentCanonical = "mutated" });
        AssertRejected(certificate with { ArgumentDefinednessCanonical = "false" });
        AssertRejected(certificate with { NormalizedArgumentCanonical = "mutated" });
        AssertRejected(certificate with { Slope = new BigRational(-2) });
        AssertRejected(certificate with { Intercept = new BigRational(4) });
        AssertRejected(certificate with { DefinednessCanonical = "false" });
        AssertRejected(certificate with { Rule = "untrusted" });
        AssertRejected(certificate with { ProvenFeature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { Claim = EmptySet.Instance.Canonical });
        AssertRejected(certificate with { ClaimCanonical = EmptySet.Instance.Canonical });
        Assert.False(AffineFloorCertificateChecker.Check(
            request,
            semantic,
            certificate,
            EmptySet.Instance.Canonical,
            new ResourceBudget()));

        (_, SemanticExpression different) = Build(Floor(
            Add(Multiply(Number(3), Variable()), Number(-4))));
        Assert.False(AffineFloorCertificateChecker.Check(
            request,
            different,
            certificate,
            claim,
            new ResourceBudget()));
        return;

        void AssertRejected(AffineFloorProofCertificate changed)
        {
            Assert.False(AffineFloorCertificateChecker.Check(
                request,
                semantic,
                changed,
                claim,
                new ResourceBudget()));
            Assert.False(CertificateChecker.Check(
                request,
                semantic,
                ProofOutcome<RealSet>.Proved(range.Value!, changed)));
        }
    }

    [Fact]
    public void AnalyzerAndCheckerEnforceCancellationAndWorkBudget()
    {
        (AnalysisRequest request, SemanticExpression semantic) = Build(Floor(Variable()));
        ProofOutcome<RealSet> range = Analyze<RealSet>(
            request,
            semantic,
            AnalysisFeatures.Range);
        var certificate = Assert.IsType<AffineFloorProofCertificate>(range.Certificate);
        string claim = ClaimCanonical.ForObject(range.Value!);

        Assert.Throws<AnalysisCancelledException>(() =>
            AffineFloorAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.Range,
                new ResourceBudget(static () => false),
                out ProofOutcome<RealSet> _));
        Assert.Throws<AnalysisCancelledException>(() =>
            AffineFloorCertificateChecker.Check(
                request,
                semantic,
                certificate,
                claim,
                new ResourceBudget(static () => false)));

        var exhaustedAnalyzer = new ResourceBudget();
        exhaustedAnalyzer.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            AffineFloorAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.Range,
                exhaustedAnalyzer,
                out ProofOutcome<RealSet> _));
        var exhaustedChecker = new ResourceBudget();
        exhaustedChecker.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            AffineFloorCertificateChecker.Check(
                request,
                semantic,
                certificate,
                claim,
                exhaustedChecker));
    }

    [Fact]
    public void OversizedCoefficientFailsAtTheDeterministicLimit()
    {
        ExactInteger oversized = ExactInteger.One << AnalysisLimits.CoefficientBits;
        InputExpression expression = Floor(
            Add(Multiply(Number(new BigRational(oversized)), Variable()), Number(1)));

        Assert.Throws<BudgetExceededException>(() =>
            new SemanticGraphBuilder(new ResourceBudget()).Build(expression));
    }

    [Fact]
    public void UnsupportedFeatureAndMismatchedResultTypeFailClosed()
    {
        (AnalysisRequest request, SemanticExpression semantic) = Build(Floor(Variable()));

        Assert.False(AffineFloorAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.None,
            new ResourceBudget(),
            out ProofOutcome<object> _));
        Assert.False(AffineFloorAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Parity,
            new ResourceBudget(),
            out ProofOutcome<RealSet> _));
    }

    public static IEnumerable<object[]> UnsupportedExpressions()
    {
        InputExpression x = Variable();
        yield return [Floor(Power(x, 2))];
        yield return [Floor(Function("sin", x))];
        yield return [Floor(Divide(x, x))];
        yield return [Floor(Divide(Subtract(Power(x, 2), Number(1)), Subtract(x, Number(1))))];
        yield return [Floor(Multiply(Named("pi"), x))];
        yield return [Function("ceil", x)];
        yield return [Function("int", x)];
    }

    private static void AssertEveryFeatureReplays(
        AnalysisRequest request,
        SemanticExpression semantic)
    {
        Replay<RealSet>(AnalysisFeatures.Domain);
        Replay<RealSet>(AnalysisFeatures.Range);
        Replay<FunctionParity>(AnalysisFeatures.Parity);
        Replay<RealSet>(AnalysisFeatures.Zeros);
        Replay<OptionalValue<ExactReal>>(AnalysisFeatures.YIntercept);
        Replay<ImmutableArray<FeaturePoint>>(AnalysisFeatures.Minima);
        Replay<ImmutableArray<FeaturePoint>>(AnalysisFeatures.Maxima);
        Replay<ImmutableArray<FeaturePoint>>(AnalysisFeatures.InflectionPoints);
        Replay<ImmutableArray<Asymptote>>(AnalysisFeatures.VerticalAsymptotes);
        Replay<ImmutableArray<Asymptote>>(AnalysisFeatures.HorizontalAsymptotes);
        Replay<ImmutableArray<Asymptote>>(AnalysisFeatures.ObliqueAsymptotes);
        Replay<ImmutableArray<MonotoneRegion>>(AnalysisFeatures.Monotonicity);
        Replay<Periodicity>(AnalysisFeatures.Period);
        return;

        void Replay<T>(AnalysisFeatures feature)
        {
            ProofOutcome<T> outcome = Analyze<T>(request, semantic, feature);
            Assert.IsType<AffineFloorProofCertificate>(outcome.Certificate);
            Assert.True(Check(request, semantic, outcome));
        }
    }

    private static AffineFloorContext Context(
        AnalysisRequest request,
        SemanticExpression semantic)
    {
        Assert.True(AffineFloorContext.TryCreate(
            semantic,
            request.Variable,
            request.AngleUnit,
            new ResourceBudget(),
            out AffineFloorContext context));
        return context;
    }

    private static T Proved<T>(
        AnalysisRequest request,
        SemanticExpression semantic,
        AnalysisFeatures feature)
    {
        return Analyze<T>(request, semantic, feature).Value!;
    }

    private static ProofOutcome<T> Analyze<T>(
        AnalysisRequest request,
        SemanticExpression semantic,
        AnalysisFeatures feature)
    {
        Assert.True(AffineFloorAnalyzer.TryAnalyze(
            request,
            semantic,
            feature,
            new ResourceBudget(),
            out ProofOutcome<T> outcome));
        Assert.Equal(ProofState.Proved, outcome.State);
        return outcome;
    }

    private static bool Check<T>(
        AnalysisRequest request,
        SemanticExpression semantic,
        ProofOutcome<T> outcome)
    {
        var certificate = Assert.IsType<AffineFloorProofCertificate>(outcome.Certificate);
        return AffineFloorCertificateChecker.Check(
            request,
            semantic,
            certificate,
            ClaimCanonical.ForObject(outcome.Value!),
            new ResourceBudget());
    }

    private static void AssertPeriodicCells(
        PeriodicIntervalSet set,
        BigRational period,
        BigRational lower,
        bool includesLower,
        BigRational upper,
        bool includesUpper)
    {
        Assert.Equal(period, Rational(set.Period));
        Assert.Equal("n", set.Parameter);
        Assert.Equal("n", set.Constraint.Parameter);
        Assert.True(set.Constraint.Bound.IsUnbounded);
        PeriodicInterval interval = Assert.Single(set.Intervals);
        Assert.Equal(lower, Rational(interval.LowerOffset));
        Assert.Equal(includesLower, interval.IncludesLower);
        Assert.Equal(upper, Rational(interval.UpperOffset));
        Assert.Equal(includesUpper, interval.IncludesUpper);
    }

    private static (AnalysisRequest Request, SemanticExpression Semantic) Build(
        InputExpression expression)
    {
        var request = new AnalysisRequest(
            expression,
            AnalysisFeatures.All,
            AngleUnit.Radians,
            "x",
            static () => true);
        return (request, new SemanticGraphBuilder(new ResourceBudget()).Build(expression));
    }

    private static ExactInteger Integer(ExactReal value)
    {
        BigRational rational = Rational(value);
        Assert.True(rational.IsInteger);
        return rational.Numerator;
    }

    private static BigRational Rational(ExactReal value)
    {
        return Assert.IsType<RationalReal>(value).Value;
    }

    private static InputExpression Variable()
    {
        return InputExpression.Variable("x", Source);
    }

    private static InputExpression Named(string name)
    {
        return InputExpression.Variable(name, Source);
    }

    private static InputExpression Number(int value)
    {
        return Number(new BigRational(value));
    }

    private static InputExpression Number(BigRational value)
    {
        return InputExpression.Number(value, Source);
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

    private static InputExpression Floor(InputExpression argument)
    {
        return Function("floor", argument);
    }

    private static InputExpression Function(string name, params InputExpression[] arguments)
    {
        return InputExpression.Function(name, arguments.ToImmutableArray(), Source);
    }
}
