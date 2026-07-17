using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class AffineMinMaxAnalysisTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Theory]
    [InlineData("min", "interval[-inf,0,q:1,1]", "points[q:0]", 0, 0, 2)]
    [InlineData("max", "interval[q:1,1,+inf,0]", "empty", 1, 2, 0)]
    public void LineAndConstantBaselinesProveEveryFeature(
        string function,
        string expectedRange,
        string expectedZeros,
        int expectedY,
        int firstDirection,
        int secondDirection)
    {
        InputExpression input = MinMax(function, Variable(), Number(1));
        (AnalysisRequest request, SemanticExpression semantic) = Build(input);

        AssertEveryFeatureReplays(request, semantic);
        Assert.IsType<AllRealSet>(Proved<RealSet>(
            request,
            semantic,
            AnalysisFeatures.Domain));
        Assert.Equal(expectedRange, Proved<RealSet>(
            request,
            semantic,
            AnalysisFeatures.Range).Canonical);
        Assert.Equal(expectedZeros, Proved<RealSet>(
            request,
            semantic,
            AnalysisFeatures.Zeros).Canonical);
        Assert.Equal(FunctionParity.Neither, Proved<FunctionParity>(
            request,
            semantic,
            AnalysisFeatures.Parity));
        Assert.Equal(
            new BigRational(expectedY),
            Rational(Proved<OptionalValue<ExactReal>>(
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

        Asymptote horizontal = Assert.Single(Proved<ImmutableArray<Asymptote>>(
            request,
            semantic,
            AnalysisFeatures.HorizontalAsymptotes));
        AssertHorizontal(horizontal, BigRational.One);
        Asymptote oblique = Assert.Single(Proved<ImmutableArray<Asymptote>>(
            request,
            semantic,
            AnalysisFeatures.ObliqueAsymptotes));
        AssertOblique(oblique, BigRational.One, BigRational.Zero);

        ImmutableArray<MonotoneRegion> monotonicity =
            Proved<ImmutableArray<MonotoneRegion>>(
                request,
                semantic,
                AnalysisFeatures.Monotonicity);
        AssertRegions(
            monotonicity,
            ("interval[-inf,0,q:1,0]", (Monotonicity)firstDirection),
            ("interval[q:1,0,+inf,0]", (Monotonicity)secondDirection));
        Periodicity period = Proved<Periodicity>(
            request,
            semantic,
            AnalysisFeatures.Period);
        Assert.Equal(PeriodicityKind.NotPeriodic, period.Kind);
        Assert.Null(period.FundamentalPeriod);
    }

    [Theory]
    [InlineData("min")]
    [InlineData("max")]
    public void ProductionEnginePublishesAndCentrallyReplaysEveryFeature(
        string function)
    {
        InputExpression input = MinMax(function, Variable(), Number(1));
        AnalysisRequest request = new(
            input,
            AnalysisFeatures.All,
            AngleUnit.Radians,
            "x",
            static () => true);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        Assert.NotNull(report.Expression);
        AssertCentral(report.Domain);
        AssertCentral(report.Range);
        AssertCentral(report.Parity);
        AssertCentral(report.Zeros);
        AssertCentral(report.YIntercept);
        AssertCentral(report.Minima);
        AssertCentral(report.Maxima);
        AssertCentral(report.InflectionPoints);
        AssertCentral(report.VerticalAsymptotes);
        AssertCentral(report.HorizontalAsymptotes);
        AssertCentral(report.ObliqueAsymptotes);
        AssertCentral(report.Monotonicity);
        AssertCentral(report.Period);
        return;

        void AssertCentral<T>(ProofOutcome<T> outcome)
        {
            Assert.Equal(ProofState.Proved, outcome.State);
            Assert.IsType<AffineMinMaxProofCertificate>(outcome.Certificate);
            Assert.True(CertificateChecker.Check(request, report.Expression!, outcome));
        }
    }

    [Theory]
    [InlineData("min", false, true, -1)]
    [InlineData("max", true, false, 1)]
    public void OppositeLinesHaveOneStrictKinkExtremumAndEvenParity(
        string function,
        bool hasMinimum,
        bool hasMaximum,
        int firstTailSlope)
    {
        InputExpression x = Variable();
        (AnalysisRequest request, SemanticExpression semantic) = Build(
            MinMax(function, x, Negate(x)));

        Assert.Equal(FunctionParity.Even, Proved<FunctionParity>(
            request,
            semantic,
            AnalysisFeatures.Parity));
        Assert.Equal(
            function == "min"
                ? "interval[-inf,0,q:0,1]"
                : "interval[q:0,1,+inf,0]",
            Proved<RealSet>(request, semantic, AnalysisFeatures.Range).Canonical);
        Assert.Equal("points[q:0]", Proved<RealSet>(
            request,
            semantic,
            AnalysisFeatures.Zeros).Canonical);

        ImmutableArray<FeaturePoint> minima = Proved<ImmutableArray<FeaturePoint>>(
            request,
            semantic,
            AnalysisFeatures.Minima);
        ImmutableArray<FeaturePoint> maxima = Proved<ImmutableArray<FeaturePoint>>(
            request,
            semantic,
            AnalysisFeatures.Maxima);
        Assert.Equal(hasMinimum, minima.Length == 1);
        Assert.Equal(hasMaximum, maxima.Length == 1);
        AssertKink(hasMinimum ? Assert.Single(minima) : Assert.Single(maxima), 0, 0);

        ImmutableArray<Asymptote> oblique = Proved<ImmutableArray<Asymptote>>(
            request,
            semantic,
            AnalysisFeatures.ObliqueAsymptotes);
        Assert.Equal(2, oblique.Length);
        AssertOblique(oblique[0], new BigRational(firstTailSlope), BigRational.Zero);
        AssertOblique(oblique[1], new BigRational(-firstTailSlope), BigRational.Zero);
        Assert.Empty(Proved<ImmutableArray<Asymptote>>(
            request,
            semantic,
            AnalysisFeatures.HorizontalAsymptotes));

        AssertRegions(
            Proved<ImmutableArray<MonotoneRegion>>(
                request,
                semantic,
                AnalysisFeatures.Monotonicity),
            ("interval[-inf,0,q:0,0]", function == "min"
                ? Monotonicity.Increasing
                : Monotonicity.Decreasing),
            ("interval[q:0,0,+inf,0]", function == "min"
                ? Monotonicity.Decreasing
                : Monotonicity.Increasing));
        AssertEveryFeatureReplays(request, semantic);
    }

    [Theory]
    [InlineData("min", "interval[-inf,0,q:3,1]", true, "points[q:-3/2,q:3/2]")]
    [InlineData("max", "interval[q:3,1,+inf,0]", false, "empty")]
    public void OppositeSlopesWithSharedInterceptRemainExactlyEven(
        string function,
        string expectedRange,
        bool maximum,
        string expectedZeros)
    {
        InputExpression x = Variable();
        InputExpression first = Add(Multiply(Number(2), x), Number(3));
        InputExpression second = Add(Multiply(Number(-2), x), Number(3));
        (AnalysisRequest request, SemanticExpression semantic) = Build(
            MinMax(function, first, second));

        Assert.Equal(FunctionParity.Even, Proved<FunctionParity>(
            request,
            semantic,
            AnalysisFeatures.Parity));
        Assert.Equal(expectedRange, Proved<RealSet>(
            request,
            semantic,
            AnalysisFeatures.Range).Canonical);
        Assert.Equal(expectedZeros, Proved<RealSet>(
            request,
            semantic,
            AnalysisFeatures.Zeros).Canonical);
        ImmutableArray<FeaturePoint> extrema = Proved<ImmutableArray<FeaturePoint>>(
            request,
            semantic,
            maximum ? AnalysisFeatures.Maxima : AnalysisFeatures.Minima);
        AssertKink(Assert.Single(extrema), 0, 3);
        AssertEveryFeatureReplays(request, semantic);
    }

    [Fact]
    public void RationalCrossingProducesTwoRootsAndAnExactMaximum()
    {
        InputExpression x = Variable();
        InputExpression first = Divide(
            Subtract(Multiply(Number(3), x), Number(2)),
            Number(4));
        InputExpression second = Add(
            Multiply(Number(new BigRational(-1, 2)), x),
            Number(new BigRational(5, 3)));
        (AnalysisRequest request, SemanticExpression semantic) = Build(
            MinMax("min", first, second));

        var context = AssertContext(request, semantic);
        AffineMinMaxModel model = context.CreateModel(new ResourceBudget());
        Assert.Equal(new BigRational(26, 15), model.KinkX);
        Assert.Equal(new BigRational(4, 5), model.KinkY);
        Assert.Equal(
            "interval[-inf,0,q:4/5,1]",
            Proved<RealSet>(request, semantic, AnalysisFeatures.Range).Canonical);
        Assert.Equal(
            "points[q:2/3,q:10/3]",
            Proved<RealSet>(request, semantic, AnalysisFeatures.Zeros).Canonical);
        Assert.Equal(
            new BigRational(-1, 2),
            Rational(Proved<OptionalValue<ExactReal>>(
                request,
                semantic,
                AnalysisFeatures.YIntercept).Value!));
        AssertKink(
            Assert.Single(Proved<ImmutableArray<FeaturePoint>>(
                request,
                semantic,
                AnalysisFeatures.Maxima)),
            new BigRational(26, 15),
            new BigRational(4, 5));
        AssertEveryFeatureReplays(request, semantic);
    }

    [Theory]
    [InlineData("min", "interval[q:0,1,+inf,0]", 0, 2)]
    [InlineData("max", "interval[-inf,0,q:0,1]", 2, 0)]
    public void ZeroPlateausProduceClosedHalfLineFibersButNoStrictExtremum(
        string function,
        string expectedZeros,
        int firstDirection,
        int secondDirection)
    {
        (AnalysisRequest request, SemanticExpression semantic) = Build(
            MinMax(function, Number(0), Variable()));

        Assert.Equal(expectedZeros, Proved<RealSet>(
            request,
            semantic,
            AnalysisFeatures.Zeros).Canonical);
        Assert.Empty(Proved<ImmutableArray<FeaturePoint>>(
            request,
            semantic,
            AnalysisFeatures.Minima));
        Assert.Empty(Proved<ImmutableArray<FeaturePoint>>(
            request,
            semantic,
            AnalysisFeatures.Maxima));
        AssertRegions(
            Proved<ImmutableArray<MonotoneRegion>>(
                request,
                semantic,
                AnalysisFeatures.Monotonicity),
            ("interval[-inf,0,q:0,0]", (Monotonicity)firstDirection),
            ("interval[q:0,0,+inf,0]", (Monotonicity)secondDirection));
        AssertEveryFeatureReplays(request, semantic);
    }

    [Fact]
    public void UpperEnvelopeCanCrossTheAxisTwice()
    {
        InputExpression x = Variable();
        InputExpression first = Subtract(x, Number(1));
        InputExpression second = Subtract(Negate(x), Number(1));
        (AnalysisRequest request, SemanticExpression semantic) = Build(
            MinMax("max", first, second));

        Assert.Equal("points[q:-1,q:1]", Proved<RealSet>(
            request,
            semantic,
            AnalysisFeatures.Zeros).Canonical);
        Assert.Equal("interval[q:-1,1,+inf,0]", Proved<RealSet>(
            request,
            semantic,
            AnalysisFeatures.Range).Canonical);
        AssertKink(
            Assert.Single(Proved<ImmutableArray<FeaturePoint>>(
                request,
                semantic,
                AnalysisFeatures.Minima)),
            0,
            -1);
        AssertEveryFeatureReplays(request, semantic);
    }

    [Theory]
    [InlineData("min")]
    [InlineData("max")]
    public void SameSignCrossingSlopesMergeIntoOneMaximalMonotoneRegion(
        string function)
    {
        InputExpression x = Variable();
        (AnalysisRequest request, SemanticExpression semantic) = Build(
            MinMax(function, x, Multiply(Number(2), x)));

        MonotoneRegion region = Assert.Single(Proved<ImmutableArray<MonotoneRegion>>(
            request,
            semantic,
            AnalysisFeatures.Monotonicity));
        Assert.IsType<AllRealSet>(region.Region);
        Assert.Equal(Monotonicity.Increasing, region.Direction);
        Assert.Equal(FunctionParity.Neither, Proved<FunctionParity>(
            request,
            semantic,
            AnalysisFeatures.Parity));
        AssertEveryFeatureReplays(request, semantic);
    }

    [Fact]
    public void ParallelIdenticalAndConstantEnvelopesReduceExactly()
    {
        InputExpression x = Variable();
        AssertAffineSelection(
            MinMax(
                "min",
                Add(x, Number(2)),
                Subtract(x, Number(3))),
            BigRational.One,
            new BigRational(-3),
            FunctionParity.Neither);
        AssertAffineSelection(
            MinMax(
                "max",
                Add(x, Number(2)),
                Subtract(x, Number(3))),
            BigRational.One,
            new BigRational(2),
            FunctionParity.Neither);
        AssertAffineSelection(
            MinMax("min", x, x),
            BigRational.One,
            BigRational.Zero,
            FunctionParity.Odd);
        AssertAffineSelection(
            MinMax("max", Multiply(Number(-2), x), Multiply(Number(-2), x)),
            new BigRational(-2),
            BigRational.Zero,
            FunctionParity.Odd);

        (AnalysisRequest constantRequest, SemanticExpression constantSemantic) = Build(
            MinMax("min", Number(2), Number(3)));
        Assert.Equal("points[q:2]", Proved<RealSet>(
            constantRequest,
            constantSemantic,
            AnalysisFeatures.Range).Canonical);
        Assert.Equal(FunctionParity.Even, Proved<FunctionParity>(
            constantRequest,
            constantSemantic,
            AnalysisFeatures.Parity));
        Assert.IsType<EmptySet>(Proved<RealSet>(
            constantRequest,
            constantSemantic,
            AnalysisFeatures.Zeros));
        AssertHorizontal(
            Assert.Single(Proved<ImmutableArray<Asymptote>>(
                constantRequest,
                constantSemantic,
                AnalysisFeatures.HorizontalAsymptotes)),
            new BigRational(2));
        Assert.Equal(
            PeriodicityKind.PeriodicWithoutFundamentalPeriod,
            Proved<Periodicity>(
                constantRequest,
                constantSemantic,
                AnalysisFeatures.Period).Kind);
        AssertEveryFeatureReplays(constantRequest, constantSemantic);

        (AnalysisRequest zeroRequest, SemanticExpression zeroSemantic) = Build(
            MinMax("max", Number(0), Number(-1)));
        Assert.Equal(FunctionParity.Both, Proved<FunctionParity>(
            zeroRequest,
            zeroSemantic,
            AnalysisFeatures.Parity));
        Assert.IsType<AllRealSet>(Proved<RealSet>(
            zeroRequest,
            zeroSemantic,
            AnalysisFeatures.Zeros));
        AssertEveryFeatureReplays(zeroRequest, zeroSemantic);
    }

    [Fact]
    public void GeneratedSmallAffineGrammarIsOperandOrderInvariantAndReplayable()
    {
        int visited = 0;
        int ordinal = 0;
        int[] slopes = [-2, -1, 0, 1, 2];
        int[] intercepts = [-2, 0, 3];
        foreach (string function in new[] { "min", "max" })
            foreach (int firstSlope in slopes)
                foreach (int firstIntercept in intercepts)
                    foreach (int secondSlope in slopes)
                        foreach (int secondIntercept in intercepts)
                        {
                            if (ordinal++ % 7 != 0)
                            {
                                continue;
                            }

                            InputExpression first = Line(firstSlope, firstIntercept);
                            InputExpression second = Line(secondSlope, secondIntercept);
                            InputExpression forward = MinMax(function, first, second);
                            InputExpression reversed = MinMax(function, second, first);
                            foreach (AnalysisFeatures feature in Features())
                            {
                                Assert.Equal(
                                    Claim(forward, feature),
                                    Claim(reversed, feature));
                            }

                            OptionalValue<ExactReal> intercept = Analyze<OptionalValue<ExactReal>>(
                                forward,
                                AnalysisFeatures.YIntercept);
                            int expected = function == "min"
                                ? Math.Min(firstIntercept, secondIntercept)
                                : Math.Max(firstIntercept, secondIntercept);
                            Assert.Equal(new BigRational(expected), Rational(intercept.Value!));
                            visited++;
                        }

        Assert.Equal(65, visited);
    }

    [Fact]
    public void HiddenHolesNonAffineOperandsAndUnsupportedShapesFailClosed()
    {
        InputExpression x = Variable();
        InputExpression hole = Divide(Number(1), Subtract(x, Number(2)));
        InputExpression hidden = Add(x, Multiply(Number(0), hole));
        InputExpression[] unsupported =
        [
            MinMax("min", hidden, Number(1)),
            MinMax("max", Power(x, 2), Number(1)),
            MinMax("min", Function("sin", x), Number(1)),
            MinMax("max", Multiply(Named("pi"), x), Number(1)),
            Function("minimum", x, Number(1)),
            Function("min", x),
            Function("max", x, Number(1), Number(2))
        ];

        foreach (InputExpression input in unsupported)
        {
            (AnalysisRequest request, SemanticExpression semantic) = Build(input);
            Assert.False(AffineMinMaxAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.Range,
                new ResourceBudget(),
                out ProofOutcome<RealSet> _));
        }

        (AnalysisRequest validRequest, SemanticExpression validSemantic) = Build(
            MinMax("min", x, Number(1)));
        Assert.False(AffineMinMaxAnalyzer.TryAnalyze(
            validRequest,
            validSemantic,
            AnalysisFeatures.None,
            new ResourceBudget(),
            out ProofOutcome<object> _));
        Assert.False(AffineMinMaxAnalyzer.TryAnalyze(
            validRequest,
            validSemantic,
            AnalysisFeatures.Parity,
            new ResourceBudget(),
            out ProofOutcome<RealSet> _));
    }

    [Fact]
    public void CheckerRejectsEveryStoredPremiseClaimMutationAndHiddenHoleReplay()
    {
        InputExpression x = Variable();
        InputExpression input = MinMax("min", x, Number(1));
        (AnalysisRequest request, SemanticExpression semantic) = Build(input);
        ProofOutcome<RealSet> range = Outcome<RealSet>(
            request,
            semantic,
            AnalysisFeatures.Range);
        var certificate = Assert.IsType<AffineMinMaxProofCertificate>(range.Certificate);
        string claim = ClaimCanonical.ForObject(range.Value!);

        AssertRejected(certificate with { Function = "max" });
        AssertRejected(certificate with { Feature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { FirstOperandCanonical = "changed" });
        AssertRejected(certificate with { SecondOperandCanonical = "changed" });
        AssertRejected(certificate with { FirstDefinednessCanonical = "false" });
        AssertRejected(certificate with { SecondDefinednessCanonical = "false" });
        AssertRejected(certificate with { PatternCanonical = "changed" });
        AssertRejected(certificate with { FirstSlope = new BigRational(2) });
        AssertRejected(certificate with { FirstIntercept = new BigRational(2) });
        AssertRejected(certificate with { SecondSlope = new BigRational(2) });
        AssertRejected(certificate with { SecondIntercept = new BigRational(2) });
        AssertRejected(certificate with { DefinednessCanonical = "true" });
        AssertRejected(certificate with { Rule = "untrusted" });
        AssertRejected(certificate with { Subject = "changed" });
        AssertRejected(certificate with { SubjectCanonical = "changed" });
        AssertRejected(certificate with { ProvenFeature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { Claim = "empty" });
        AssertRejected(certificate with { ClaimCanonical = "empty" });
        Assert.False(AffineMinMaxCertificateChecker.Check(
            request with { Features = AnalysisFeatures.Domain },
            semantic,
            certificate,
            claim,
            new ResourceBudget()));
        Assert.False(AffineMinMaxCertificateChecker.Check(
            request with { Variable = "t" },
            semantic,
            certificate,
            claim,
            new ResourceBudget()));

        InputExpression hole = Divide(Number(1), Subtract(x, Number(2)));
        InputExpression hidden = MinMax(
            "min",
            Add(x, Multiply(Number(0), hole)),
            Number(1));
        (AnalysisRequest hiddenRequest, SemanticExpression hiddenSemantic) = Build(hidden);
        Assert.Equal(semantic.Value.Canonical, hiddenSemantic.Value.Canonical);
        Assert.NotEqual(semantic.DefinedWhen.Canonical, hiddenSemantic.DefinedWhen.Canonical);
        Assert.False(AffineMinMaxCertificateChecker.Check(
            hiddenRequest,
            hiddenSemantic,
            certificate,
            claim,
            new ResourceBudget()));
        return;

        void AssertRejected(AffineMinMaxProofCertificate changed) =>
            Assert.False(AffineMinMaxCertificateChecker.Check(
                request,
                semantic,
                changed,
                claim,
                new ResourceBudget()));
    }

    [Fact]
    public void CheckerRejectsForgedEnvelopeAndAllowsConservativeOperandRegularity()
    {
        InputExpression input = MinMax("max", Variable(), Number(1));
        (AnalysisRequest request, SemanticExpression semantic) = Build(input);
        ProofOutcome<RealSet> range = Outcome<RealSet>(
            request,
            semantic,
            AnalysisFeatures.Range);
        var certificate = Assert.IsType<AffineMinMaxProofCertificate>(range.Certificate);
        string claim = ClaimCanonical.ForObject(range.Value!);
        SemanticExpression first = semantic.SourceOperands[0];
        SemanticExpression second = semantic.SourceOperands[1];

        AssertRejected(semantic with { ContinuousWhen = Formula.False });
        AssertRejected(semantic with { DifferentiableWhen = Formula.False });
        AssertRejected(semantic with
        {
            RewriteHistory =
            [
                new RewriteStep(
                    "forged",
                    semantic.Value.Canonical,
                    semantic.Value.Canonical,
                    Formula.True)
            ]
        });

        SemanticExpression discontinuousFirst = first with
        {
            ContinuousWhen = Formula.False
        };
        AssertAccepted(semantic with
        {
            SourceOperands = [discontinuousFirst, second],
            ContinuousWhen = Formula.And(
                discontinuousFirst.ContinuousWhen,
                second.ContinuousWhen,
                Formula.Predicate(
                    ExactPredicate.FunctionIsContinuous,
                    semantic.Value))
        });

        SemanticExpression nondifferentiableSecond = second with
        {
            DifferentiableWhen = Formula.False
        };
        AssertAccepted(semantic with
        {
            SourceOperands = [first, nondifferentiableSecond],
            DifferentiableWhen = Formula.And(
                first.DifferentiableWhen,
                nondifferentiableSecond.DifferentiableWhen,
                Formula.Predicate(
                    ExactPredicate.FunctionIsDifferentiable,
                    semantic.Value))
        });

        Formula duplicateContinuity = new JunctionFormula(
            true,
            [semantic.ContinuousWhen, semantic.ContinuousWhen]);
        AssertRejected(semantic with { ContinuousWhen = duplicateContinuity });
        return;

        void AssertRejected(SemanticExpression changed) =>
            Assert.False(AffineMinMaxCertificateChecker.Check(
                request,
                changed,
                certificate,
                claim,
                new ResourceBudget()));

        void AssertAccepted(SemanticExpression changed) =>
            Assert.True(AffineMinMaxCertificateChecker.Check(
                request,
                changed,
                certificate,
                claim,
                new ResourceBudget()));
    }

    [Fact]
    public void WorkCoefficientAndRevisionLimitsFailClosedDeterministically()
    {
        InputExpression input = MinMax("max", Variable(), Number(1));
        (AnalysisRequest request, SemanticExpression semantic) = Build(input);

        var first = new ResourceBudget();
        Assert.True(AffineMinMaxAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Range,
            first,
            out ProofOutcome<RealSet> firstOutcome));
        var second = new ResourceBudget();
        Assert.True(AffineMinMaxAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Range,
            second,
            out ProofOutcome<RealSet> secondOutcome));
        Assert.Equal(first.WorkUsed, second.WorkUsed);
        Assert.Equal(
            ClaimCanonical.ForObject(firstOutcome.Value!),
            ClaimCanonical.ForObject(secondOutcome.Value!));

        var certificate = Assert.IsType<AffineMinMaxProofCertificate>(firstOutcome.Certificate);
        string claim = ClaimCanonical.ForObject(firstOutcome.Value!);
        Assert.Throws<AnalysisCancelledException>(() =>
            AffineMinMaxAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.Range,
                new ResourceBudget(static () => false),
                out ProofOutcome<RealSet> _));
        Assert.Throws<AnalysisCancelledException>(() =>
            AffineMinMaxCertificateChecker.Check(
                request,
                semantic,
                certificate,
                claim,
                new ResourceBudget(static () => false)));

        var exhaustedAnalyzer = new ResourceBudget();
        exhaustedAnalyzer.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            AffineMinMaxAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.Range,
                exhaustedAnalyzer,
                out ProofOutcome<RealSet> _));
        var exhaustedChecker = new ResourceBudget();
        exhaustedChecker.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            AffineMinMaxCertificateChecker.Check(
                request,
                semantic,
                certificate,
                claim,
                exhaustedChecker));

        ExactInteger huge = ExactInteger.One << AnalysisLimits.CoefficientBits;
        InputExpression oversized = MinMax(
            "min",
            Multiply(Number(new BigRational(huge)), Variable()),
            Number(1));
        (AnalysisRequest oversizedRequest, SemanticExpression oversizedSemantic) = Build(oversized);
        Assert.Throws<BudgetExceededException>(() =>
            AffineMinMaxAnalyzer.TryAnalyze(
                oversizedRequest,
                oversizedSemantic,
                AnalysisFeatures.Range,
                new ResourceBudget(),
                out ProofOutcome<RealSet> _));

        ExactInteger denominator = ExactInteger.One << (AnalysisLimits.CoefficientBits / 2);
        InputExpression oversizedDifference = MinMax(
            "min",
            Line(new BigRational(1, denominator), BigRational.Zero),
            Line(new BigRational(1, denominator + 1), BigRational.One));
        (AnalysisRequest differenceRequest, SemanticExpression differenceSemantic) =
            Build(oversizedDifference);
        Assert.Throws<BudgetExceededException>(() =>
            AffineMinMaxAnalyzer.TryAnalyze(
                differenceRequest,
                differenceSemantic,
                AnalysisFeatures.Range,
                new ResourceBudget(),
                out ProofOutcome<RealSet> _));
    }

    private static void AssertAffineSelection(
        InputExpression input,
        BigRational expectedSlope,
        BigRational expectedIntercept,
        FunctionParity expectedParity)
    {
        (AnalysisRequest request, SemanticExpression semantic) = Build(input);
        Assert.IsType<AllRealSet>(Proved<RealSet>(
            request,
            semantic,
            AnalysisFeatures.Range));
        Assert.Equal(expectedParity, Proved<FunctionParity>(
            request,
            semantic,
            AnalysisFeatures.Parity));
        Assert.Equal(expectedIntercept, Rational(Proved<OptionalValue<ExactReal>>(
            request,
            semantic,
            AnalysisFeatures.YIntercept).Value!));
        Asymptote oblique = Assert.Single(Proved<ImmutableArray<Asymptote>>(
            request,
            semantic,
            AnalysisFeatures.ObliqueAsymptotes));
        AssertOblique(oblique, expectedSlope, expectedIntercept);
        MonotoneRegion monotonicity = Assert.Single(Proved<ImmutableArray<MonotoneRegion>>(
            request,
            semantic,
            AnalysisFeatures.Monotonicity));
        Assert.IsType<AllRealSet>(monotonicity.Region);
        Assert.Equal(
            expectedSlope.Sign > 0 ? Monotonicity.Increasing : Monotonicity.Decreasing,
            monotonicity.Direction);
        AssertEveryFeatureReplays(request, semantic);
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
            ProofOutcome<T> outcome = Outcome<T>(request, semantic, feature);
            Assert.True(Check(request, semantic, outcome));
        }
    }

    private static T Analyze<T>(
        InputExpression input,
        AnalysisFeatures feature)
    {
        (AnalysisRequest request, SemanticExpression semantic) = Build(input);
        return Outcome<T>(request, semantic, feature).Value!;
    }

    private static string Claim(InputExpression input, AnalysisFeatures feature) => feature switch
    {
        AnalysisFeatures.Domain or AnalysisFeatures.Range or AnalysisFeatures.Zeros =>
            ClaimFor<RealSet>(input, feature),
        AnalysisFeatures.Parity => ClaimFor<FunctionParity>(input, feature),
        AnalysisFeatures.YIntercept => ClaimFor<OptionalValue<ExactReal>>(input, feature),
        AnalysisFeatures.Minima or AnalysisFeatures.Maxima or AnalysisFeatures.InflectionPoints =>
            ClaimFor<ImmutableArray<FeaturePoint>>(input, feature),
        AnalysisFeatures.VerticalAsymptotes or
        AnalysisFeatures.HorizontalAsymptotes or
        AnalysisFeatures.ObliqueAsymptotes => ClaimFor<ImmutableArray<Asymptote>>(input, feature),
        AnalysisFeatures.Monotonicity => ClaimFor<ImmutableArray<MonotoneRegion>>(input, feature),
        AnalysisFeatures.Period => ClaimFor<Periodicity>(input, feature),
        _ => throw new ArgumentOutOfRangeException(nameof(feature))
    };

    private static string ClaimFor<T>(InputExpression input, AnalysisFeatures feature)
    {
        (AnalysisRequest request, SemanticExpression semantic) = Build(input);
        ProofOutcome<T> outcome = Outcome<T>(request, semantic, feature);
        Assert.True(Check(request, semantic, outcome));
        return ClaimCanonical.ForObject(outcome.Value!);
    }

    private static T Proved<T>(
        AnalysisRequest request,
        SemanticExpression semantic,
        AnalysisFeatures feature) => Outcome<T>(request, semantic, feature).Value!;

    private static ProofOutcome<T> Outcome<T>(
        AnalysisRequest request,
        SemanticExpression semantic,
        AnalysisFeatures feature)
    {
        Assert.True(AffineMinMaxAnalyzer.TryAnalyze(
            request,
            semantic,
            feature,
            new ResourceBudget(),
            out ProofOutcome<T> outcome));
        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.IsType<AffineMinMaxProofCertificate>(outcome.Certificate);
        return outcome;
    }

    private static bool Check<T>(
        AnalysisRequest request,
        SemanticExpression semantic,
        ProofOutcome<T> outcome) =>
        AffineMinMaxCertificateChecker.Check(
            request,
            semantic,
            Assert.IsType<AffineMinMaxProofCertificate>(outcome.Certificate),
            ClaimCanonical.ForObject(outcome.Value!),
            new ResourceBudget());

    private static AffineMinMaxContext AssertContext(
        AnalysisRequest request,
        SemanticExpression semantic)
    {
        Assert.True(AffineMinMaxContext.TryCreate(
            semantic,
            request.Variable,
            request.AngleUnit,
            new ResourceBudget(),
            out AffineMinMaxContext context));
        return context;
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

    private static void AssertKink(
        FeaturePoint point,
        int x,
        int y) => AssertKink(point, new BigRational(x), new BigRational(y));

    private static void AssertKink(
        FeaturePoint point,
        BigRational x,
        BigRational y)
    {
        var feature = Assert.IsType<ConstantYFeaturePoint>(point);
        Assert.Equal(x, Rational(Assert.IsType<SingletonReal>(feature.X).Value));
        Assert.Equal(y, Rational(feature.Y));
    }

    private static void AssertHorizontal(Asymptote asymptote, BigRational intercept)
    {
        Assert.Equal(AsymptoteOrientation.Horizontal, asymptote.Orientation);
        Assert.Null(asymptote.Slope);
        Assert.Equal(intercept, Rational(asymptote.Intercept!));
        Assert.Equal(intercept, Rational(Assert.IsType<SingletonReal>(asymptote.Coordinate).Value));
    }

    private static void AssertOblique(
        Asymptote asymptote,
        BigRational slope,
        BigRational intercept)
    {
        Assert.Equal(AsymptoteOrientation.Oblique, asymptote.Orientation);
        Assert.Equal(slope, Rational(asymptote.Slope!));
        Assert.Equal(intercept, Rational(asymptote.Intercept!));
        Assert.Equal(intercept, Rational(Assert.IsType<SingletonReal>(asymptote.Coordinate).Value));
    }

    private static IEnumerable<AnalysisFeatures> Features()
    {
        yield return AnalysisFeatures.Domain;
        yield return AnalysisFeatures.Range;
        yield return AnalysisFeatures.Parity;
        yield return AnalysisFeatures.Zeros;
        yield return AnalysisFeatures.YIntercept;
        yield return AnalysisFeatures.Minima;
        yield return AnalysisFeatures.Maxima;
        yield return AnalysisFeatures.InflectionPoints;
        yield return AnalysisFeatures.VerticalAsymptotes;
        yield return AnalysisFeatures.HorizontalAsymptotes;
        yield return AnalysisFeatures.ObliqueAsymptotes;
        yield return AnalysisFeatures.Monotonicity;
        yield return AnalysisFeatures.Period;
    }

    private static (AnalysisRequest Request, SemanticExpression Semantic) Build(
        InputExpression input)
    {
        var request = new AnalysisRequest(
            input,
            AnalysisFeatures.All,
            AngleUnit.Radians,
            "x",
            static () => true);
        return (request, new SemanticGraphBuilder(new ResourceBudget()).Build(input));
    }

    private static BigRational Rational(ExactReal value) =>
        Assert.IsType<RationalReal>(value).Value;

    private static InputExpression Line(int slope, int intercept) =>
        Line(new BigRational(slope), new BigRational(intercept));

    private static InputExpression Line(BigRational slope, BigRational intercept) =>
        Add(Multiply(Number(slope), Variable()), Number(intercept));

    private static InputExpression Variable() => InputExpression.Variable("x", Source);

    private static InputExpression Named(string name) => InputExpression.Variable(name, Source);

    private static InputExpression Number(int value) => Number(new BigRational(value));

    private static InputExpression Number(BigRational value) =>
        InputExpression.Number(value, Source);

    private static InputExpression Negate(InputExpression value) =>
        InputExpression.Unary(InputExpressionKind.Negate, value, Source);

    private static InputExpression Add(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Add, left, right, Source);

    private static InputExpression Subtract(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Subtract, left, right, Source);

    private static InputExpression Multiply(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Multiply, left, right, Source);

    private static InputExpression Divide(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Divide, left, right, Source);

    private static InputExpression Power(InputExpression basis, int exponent) =>
        InputExpression.Binary(InputExpressionKind.Power, basis, Number(exponent), Source);

    private static InputExpression MinMax(
        string function,
        InputExpression first,
        InputExpression second) => Function(function, first, second);

    private static InputExpression Function(string name, params InputExpression[] arguments) =>
        InputExpression.Function(name, arguments.ToImmutableArray(), Source);
}
