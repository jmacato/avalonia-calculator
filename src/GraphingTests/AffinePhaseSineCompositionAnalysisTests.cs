using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class AffinePhaseSineCompositionAnalysisTests
{
    private static readonly SourceRange Source = new(0, 1);
    private static readonly string[] ShiftedDoubleAngleMinimaOffsets =
        ["pi:-1/4:0", "pi:1/4:0"];

    [Fact]
    public void SquareRootOfShiftedDoubleAngleProvesEveryExactFeature()
    {
        InputExpression input = Composition(
            "sqrt",
            new BigRational(2),
            new BigRational(1, 2));

        Assert.Equal(
            "periodic-intervals[pi:1:0,m,m:Z,[pi:-1/4:0,1,pi:1/4:0,1]]",
            Analyze<RealSet>(input, AnalysisFeatures.Domain).Canonical);
        Assert.Equal(
            "interval[q:0,1,q:1,1]",
            Analyze<RealSet>(input, AnalysisFeatures.Range).Canonical);
        Assert.Equal(FunctionParity.Even, Analyze<FunctionParity>(
            input,
            AnalysisFeatures.Parity));
        Assert.Equal(
            "periodic-points[pi:-1/4:0,pi:1/2:0,m,m:Z]",
            Analyze<RealSet>(input, AnalysisFeatures.Zeros).Canonical);
        Assert.Equal(
            "q:1",
            ExactRealCanonical.Format(Analyze<OptionalValue<ExactReal>>(
                input,
                AnalysisFeatures.YIntercept).Value!));

        ImmutableArray<FeaturePoint> minima = Analyze<ImmutableArray<FeaturePoint>>(
            input,
            AnalysisFeatures.Minima);
        Assert.Equal(2, minima.Length);
        Assert.Equal(
            ShiftedDoubleAngleMinimaOffsets,
            minima
                .Select(static minimum => Assert.IsType<PeriodicReal>(
                    Assert.IsType<ConstantYFeaturePoint>(minimum).X))
                .Select(static family => ExactRealCanonical.Format(family.Offset)));
        Assert.All(minima, static minimum =>
        {
            ConstantYFeaturePoint point = Assert.IsType<ConstantYFeaturePoint>(minimum);
            Assert.Equal("q:0", ExactRealCanonical.Format(point.Y));
            Assert.Equal(
                "pi:1:0",
                ExactRealCanonical.Format(Assert.IsType<PeriodicReal>(point.X).Period));
        });
        ConstantYFeaturePoint maximum = Assert.IsType<ConstantYFeaturePoint>(Assert.Single(
            Analyze<ImmutableArray<FeaturePoint>>(input, AnalysisFeatures.Maxima)));
        Assert.Equal("q:1", ExactRealCanonical.Format(maximum.Y));
        Assert.Equal(
            "pi:0:0",
            ExactRealCanonical.Format(Assert.IsType<PeriodicReal>(maximum.X).Offset));

        Assert.Empty(Analyze<ImmutableArray<FeaturePoint>>(
            input,
            AnalysisFeatures.InflectionPoints));
        Assert.Empty(Analyze<ImmutableArray<Asymptote>>(
            input,
            AnalysisFeatures.VerticalAsymptotes));
        Assert.Empty(Analyze<ImmutableArray<Asymptote>>(
            input,
            AnalysisFeatures.HorizontalAsymptotes));
        Assert.Empty(Analyze<ImmutableArray<Asymptote>>(
            input,
            AnalysisFeatures.ObliqueAsymptotes));

        ImmutableArray<MonotoneRegion> monotonicity = Analyze<ImmutableArray<MonotoneRegion>>(
            input,
            AnalysisFeatures.Monotonicity);
        Assert.Equal(2, monotonicity.Length);
        Assert.Equal(Monotonicity.Increasing, monotonicity[0].Direction);
        Assert.Equal(
            "periodic-intervals[pi:1:0,m,m:Z,[pi:-1/4:0,0,pi:0:0,0]]",
            monotonicity[0].Region.Canonical);
        Assert.Equal(Monotonicity.Decreasing, monotonicity[1].Direction);
        Assert.Equal(
            "periodic-intervals[pi:1:0,m,m:Z,[pi:0:0,0,pi:1/4:0,0]]",
            monotonicity[1].Region.Canonical);
        AssertPeriod(input, "pi:1:0");
    }

    [Theory]
    [InlineData("ln")]
    [InlineData("log")]
    public void NonSpecialPositiveInterceptRetainsTheExactLogarithmBase(string outer)
    {
        InputExpression input = Composition(
            outer,
            new BigRational(2),
            new BigRational(1, 6));

        OptionalValue<ExactReal> intercept = Analyze<OptionalValue<ExactReal>>(
            input,
            AnalysisFeatures.YIntercept);
        var logarithm = Assert.IsType<FunctionReal>(intercept.Value);
        Assert.Equal(outer, logarithm.Function);
        var sine = Assert.IsType<FunctionReal>(Assert.Single(logarithm.Arguments));
        Assert.Equal("sin", sine.Function);
        Assert.Equal(
            "pi:1/6:0",
            ExactRealCanonical.Format(Assert.Single(sine.Arguments)));
    }

    [Theory]
    [InlineData("ln")]
    [InlineData("log")]
    public void LogarithmOfShiftedDoubleAngleProvesOpenCellsPolesAndMaxima(string outer)
    {
        InputExpression input = Composition(
            outer,
            new BigRational(2),
            new BigRational(1, 2));

        Assert.Equal(
            "periodic-intervals[pi:1:0,m,m:Z,[pi:-1/4:0,0,pi:1/4:0,0]]",
            Analyze<RealSet>(input, AnalysisFeatures.Domain).Canonical);
        Assert.Equal(
            "interval[-inf,0,q:0,1]",
            Analyze<RealSet>(input, AnalysisFeatures.Range).Canonical);
        Assert.Equal(FunctionParity.Even, Analyze<FunctionParity>(
            input,
            AnalysisFeatures.Parity));
        Assert.Equal(
            "periodic-points[pi:0:0,pi:1:0,m,m:Z]",
            Analyze<RealSet>(input, AnalysisFeatures.Zeros).Canonical);
        Assert.Equal(
            "q:0",
            ExactRealCanonical.Format(Analyze<OptionalValue<ExactReal>>(
                input,
                AnalysisFeatures.YIntercept).Value!));
        Assert.Empty(Analyze<ImmutableArray<FeaturePoint>>(
            input,
            AnalysisFeatures.Minima));
        Assert.Equal(
            "q:0",
            ExactRealCanonical.Format(Assert.IsType<ConstantYFeaturePoint>(Assert.Single(
                Analyze<ImmutableArray<FeaturePoint>>(
                    input,
                    AnalysisFeatures.Maxima))).Y));
        Assert.Empty(Analyze<ImmutableArray<FeaturePoint>>(
            input,
            AnalysisFeatures.InflectionPoints));

        Asymptote pole = Assert.Single(Analyze<ImmutableArray<Asymptote>>(
            input,
            AnalysisFeatures.VerticalAsymptotes));
        var coordinate = Assert.IsType<PeriodicReal>(pole.Coordinate);
        Assert.Equal("pi:-1/4:0", ExactRealCanonical.Format(coordinate.Offset));
        Assert.Equal("pi:1/2:0", ExactRealCanonical.Format(coordinate.Period));
        Assert.Empty(Analyze<ImmutableArray<Asymptote>>(
            input,
            AnalysisFeatures.HorizontalAsymptotes));
        Assert.Empty(Analyze<ImmutableArray<Asymptote>>(
            input,
            AnalysisFeatures.ObliqueAsymptotes));

        ImmutableArray<MonotoneRegion> monotonicity = Analyze<ImmutableArray<MonotoneRegion>>(
            input,
            AnalysisFeatures.Monotonicity);
        Assert.Equal(2, monotonicity.Length);
        Assert.Equal(Monotonicity.Increasing, monotonicity[0].Direction);
        Assert.Equal(Monotonicity.Decreasing, monotonicity[1].Direction);
        AssertPeriod(input, "pi:1:0");
    }

    public static TheoryData<string, int, int, int, int> GeneratedAffinePiCases => new()
    {
        { "sqrt", 1, 2, -5, 2 },
        { "sqrt", 2, 1, -1, 1 },
        { "sqrt", 3, 1, 0, 1 },
        { "sqrt", -2, 1, 1, 2 },
        { "sqrt", -3, 2, 3, 2 },
        { "ln", 1, 2, -5, 2 },
        { "ln", 2, 1, -1, 1 },
        { "ln", 3, 1, 0, 1 },
        { "ln", -2, 1, 1, 2 },
        { "log", -3, 2, 5, 3 }
    };

    [Theory]
    [MemberData(nameof(GeneratedAffinePiCases))]
    public void GeneratedRationalFrequenciesAndPiPhasesReplayEveryFeature(
        string outer,
        int frequencyNumerator,
        int frequencyDenominator,
        int phaseNumerator,
        int phaseDenominator)
    {
        BigRational frequency = new(frequencyNumerator, frequencyDenominator);
        BigRational phase = new(phaseNumerator, phaseDenominator);
        InputExpression input = Composition(outer, frequency, phase);

        foreach (AnalysisFeatures feature in Features())
        {
            AssertFeatureReplays(input, feature);
        }

        Periodicity period = Analyze<Periodicity>(input, AnalysisFeatures.Period);
        Assert.Equal(
            new AffinePiReal(new BigRational(2) / frequency.Abs(), BigRational.Zero),
            period.FundamentalPeriod);
        Assert.Equal(
            (phase - new BigRational(1, 2)).IsInteger
                ? FunctionParity.Even
                : FunctionParity.Neither,
            Analyze<FunctionParity>(input, AnalysisFeatures.Parity));

        RealSet domain = Analyze<RealSet>(input, AnalysisFeatures.Domain);
        var cells = Assert.IsType<PeriodicIntervalSet>(domain);
        Assert.Equal(outer == "sqrt", Assert.Single(cells.Intervals).IncludesLower);
        Assert.Equal(outer == "sqrt", Assert.Single(cells.Intervals).IncludesUpper);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void IndependentReplayCoversEveryFeatureAndOuterAcrossAngleUnits(
        int angleUnitValue)
    {
        var angleUnit = (AngleUnit)angleUnitValue;
        InputExpression phase = angleUnit switch
        {
            AngleUnit.Radians => PiFraction(new BigRational(1, 2)),
            AngleUnit.Degrees => Number(90),
            AngleUnit.Grads => Number(100),
            _ => throw new ArgumentOutOfRangeException(nameof(angleUnitValue))
        };

        foreach (string outer in new[] { "sqrt", "ln", "log" })
        {
            InputExpression input = Function(
                outer,
                Function(
                    "sin",
                    Add(Multiply(Number(-2), Variable()), phase)));
            foreach (AnalysisFeatures feature in Features())
            {
                AssertFeatureReplays(input, feature, angleUnit);
            }
        }
    }

    [Fact]
    public void EquivalentAffineSyntaxAndNegativeFrequencyNormalizeToIdenticalClaims()
    {
        InputExpression x = Variable();
        InputExpression piHalf = PiFraction(new BigRational(1, 2));
        InputExpression canonical = Function(
            "sqrt",
            Function("sin", Add(Multiply(Number(2), x), piHalf)));
        InputExpression reordered = Function(
            "sqrt",
            Function("sin", Add(piHalf, Multiply(x, Number(2)))));
        InputExpression reflected = Function(
            "sqrt",
            Function("sin", Add(Multiply(Number(-2), x), piHalf)));

        foreach (AnalysisFeatures feature in Features())
        {
            string expected = Claim(input: canonical, feature);
            Assert.Equal(expected, Claim(reordered, feature));
            Assert.Equal(expected, Claim(reflected, feature));
        }
    }

    [Fact]
    public void RationalPhaseRemainderKeepsExactCellsButLeavesUnprovedPointValueUnknown()
    {
        InputExpression input = Function(
            "sqrt",
            Function(
                "sin",
                Add(
                    Multiply(Number(2), Variable()),
                    Add(PiFraction(new BigRational(1, 2)), Number(1)))));

        Assert.IsType<PeriodicIntervalSet>(Analyze<RealSet>(
            input,
            AnalysisFeatures.Domain));
        Assert.Equal(FunctionParity.Neither, Analyze<FunctionParity>(
            input,
            AnalysisFeatures.Parity));
        AssertPeriod(input, "pi:1:0");
        AssertUnsupported<OptionalValue<ExactReal>>(
            input,
            AnalysisFeatures.YIntercept);
    }

    [Fact]
    public void HiddenHolesNonAffineArgumentsAndNonRationalFrequenciesAreRejected()
    {
        InputExpression x = Variable();
        InputExpression hole = Divide(Number(1), Subtract(x, Number(3)));
        InputExpression hiddenHole = Function(
            "sqrt",
            Function(
                "sin",
                Add(
                    Add(Multiply(Number(2), x), PiFraction(new BigRational(1, 2))),
                    Multiply(Number(0), hole))));
        InputExpression nonAffine = Function(
            "sqrt",
            Function("sin", Add(Power(x, 2), PiFraction(new BigRational(1, 2)))));
        InputExpression irrationalFrequency = Function(
            "ln",
            Function(
                "sin",
                Add(Multiply(Symbol("pi"), x), PiFraction(new BigRational(1, 2)))));
        InputExpression wrongOuter = Function(
            "exp",
            Function("sin", Add(Multiply(Number(2), x), PiFraction(new BigRational(1, 2)))));

        foreach (InputExpression input in new[]
                 {
                     hiddenHole,
                     nonAffine,
                     irrationalFrequency,
                     wrongOuter
                 })
        {
            AssertUnsupported<RealSet>(input, AnalysisFeatures.Range);
            AssertUnsupported<Periodicity>(input, AnalysisFeatures.Period);
        }
    }

    [Fact]
    public void DedicatedCheckerRejectsEveryMaterialCertificateMutationAndHiddenHoleReplay()
    {
        InputExpression input = Composition(
            "sqrt",
            new BigRational(2),
            new BigRational(1, 2));
        (AnalysisRequest request, SemanticExpression expression, ProofOutcome<RealSet> outcome) =
            AnalyzeOutcome<RealSet>(input, AnalysisFeatures.Range);
        RealSet range = outcome.Value!;
        var certificate = Assert.IsType<AffinePhaseSineCompositionProofCertificate>(
            outcome.Certificate);

        AssertRejected(certificate with { OuterKind = AffinePhaseSineOuterKind.NaturalLogarithm });
        AssertRejected(certificate with { AngleUnit = AngleUnit.Degrees });
        AssertRejected(certificate with { Frequency = new BigRational(3) });
        AssertRejected(certificate with
        {
            PhasePiCoefficient = certificate.PhasePiCoefficient + BigRational.One
        });
        AssertRejected(certificate with { PhaseConstant = BigRational.One });
        AssertRejected(certificate with { InnerSign = -certificate.InnerSign });
        AssertRejected(certificate with { PatternCanonical = certificate.PatternCanonical + ":changed" });
        AssertRejected(certificate with { DefinednessCanonical = "true" });
        AssertRejected(certificate with { Rule = "untrusted" });
        AssertRejected(certificate with { Subject = certificate.Subject + ":changed" });
        AssertRejected(certificate with { ProvenFeature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { Claim = "empty" });

        InputExpression x = Variable();
        InputExpression hiddenHole = Function(
            "sqrt",
            Function(
                "sin",
                Add(
                    Add(Multiply(Number(2), x), PiFraction(new BigRational(1, 2))),
                    Multiply(Number(0), Divide(Number(1), Subtract(x, Number(3)))))));
        AnalysisRequest hiddenRequest = Request(hiddenHole, AnalysisFeatures.Range);
        SemanticExpression hiddenSemantic = new SemanticGraphBuilder(new ResourceBudget()).Build(
            hiddenHole);
        Assert.Equal(expression.Value.Canonical, hiddenSemantic.Value.Canonical);
        Assert.NotEqual(
            expression.DefinedWhen.Canonical,
            hiddenSemantic.DefinedWhen.Canonical);
        Assert.False(AffinePhaseSineCompositionCertificateChecker.Check(
            hiddenRequest,
            hiddenSemantic,
            certificate,
            ClaimCanonical.For(range),
            new ResourceBudget()));

        void AssertRejected(AffinePhaseSineCompositionProofCertificate changed) =>
            Assert.False(AffinePhaseSineCompositionCertificateChecker.Check(
                request,
                expression,
                changed,
                ClaimCanonical.For(range),
                new ResourceBudget()));
    }

    [Fact]
    public void IndependentCheckerRejectsWrongUnitClaimsAndForgedDomainGuards()
    {
        InputExpression unitSensitive = Function(
            "sqrt",
            Function("sin", Add(Variable(), Number(90))));
        (AnalysisRequest degreeRequest, SemanticExpression degreeExpression,
            ProofOutcome<Periodicity> degreeOutcome) = AnalyzeOutcome<Periodicity>(
                unitSensitive,
                AnalysisFeatures.Period,
                AngleUnit.Degrees);
        var degreeCertificate = Assert.IsType<AffinePhaseSineCompositionProofCertificate>(
            degreeOutcome.Certificate);
        string degreeClaim = ClaimCanonical.For(degreeOutcome.Value!);

        Assert.False(AffinePhaseSineCompositionCertificateChecker.Check(
            degreeRequest with { AngleUnit = AngleUnit.Radians },
            degreeExpression,
            degreeCertificate with { AngleUnit = AngleUnit.Radians },
            degreeClaim,
            new ResourceBudget()));
        Assert.False(AffinePhaseSineCompositionCertificateChecker.Check(
            degreeRequest with { AngleUnit = AngleUnit.Grads },
            degreeExpression,
            degreeCertificate with { AngleUnit = AngleUnit.Grads },
            degreeClaim,
            new ResourceBudget()));

        InputExpression squareRoot = Composition(
            "sqrt",
            new BigRational(2),
            new BigRational(1, 2));
        (AnalysisRequest squareRootRequest, SemanticExpression squareRootExpression,
            ProofOutcome<RealSet> squareRootOutcome) = AnalyzeOutcome<RealSet>(
                squareRoot,
                AnalysisFeatures.Range);
        var squareRootCertificate = Assert.IsType<AffinePhaseSineCompositionProofCertificate>(
            squareRootOutcome.Certificate);
        Assert.False(AffinePhaseSineCompositionCertificateChecker.Check(
            squareRootRequest,
            squareRootExpression with { DefinedWhen = Formula.True },
            squareRootCertificate with { DefinednessCanonical = Formula.True.Canonical },
            ClaimCanonical.For(squareRootOutcome.Value!),
            new ResourceBudget()));

        InputExpression logarithm = Composition(
            "ln",
            new BigRational(2),
            new BigRational(1, 2));
        (AnalysisRequest logRequest, SemanticExpression logExpression,
            ProofOutcome<RealSet> logOutcome) = AnalyzeOutcome<RealSet>(
                logarithm,
                AnalysisFeatures.Range);
        var logCertificate = Assert.IsType<AffinePhaseSineCompositionProofCertificate>(
            logOutcome.Certificate);
        ValueTerm zero = new(
            -1,
            ValueKind.Constant,
            BigRational.Zero,
            string.Empty,
            [],
            "q:0");
        Formula closedGuard = Formula.Compare(
            logExpression.Value.Operands[0],
            Comparison.GreaterOrEqual,
            zero);
        Assert.False(AffinePhaseSineCompositionCertificateChecker.Check(
            logRequest,
            logExpression with { DefinedWhen = closedGuard },
            logCertificate with { DefinednessCanonical = closedGuard.Canonical },
            ClaimCanonical.For(logOutcome.Value!),
            new ResourceBudget()));
    }

    [Fact]
    public void AnalysisEnginePublishesEveryAffinePhaseClaimOnlyAfterCentralReplay()
    {
        InputExpression input = Composition(
            "sqrt",
            new BigRational(2),
            new BigRational(1, 2));
        AnalysisRequest request = Request(input, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        Assert.NotNull(report.Expression);

        AssertIntegrated(report.Domain);
        AssertIntegrated(report.Range);
        AssertIntegrated(report.Parity);
        AssertIntegrated(report.Zeros);
        AssertIntegrated(report.YIntercept);
        AssertIntegrated(report.Minima);
        AssertIntegrated(report.Maxima);
        AssertIntegrated(report.InflectionPoints);
        AssertIntegrated(report.VerticalAsymptotes);
        AssertIntegrated(report.HorizontalAsymptotes);
        AssertIntegrated(report.ObliqueAsymptotes);
        AssertIntegrated(report.Monotonicity);
        AssertIntegrated(report.Period);
        return;

        void AssertIntegrated<T>(ProofOutcome<T> outcome)
        {
            Assert.Equal(ProofState.Proved, outcome.State);
            var certificate = Assert.IsType<AffinePhaseSineCompositionProofCertificate>(
                outcome.Certificate);
            Assert.Equal(AngleUnit.Radians, certificate.AngleUnit);
            Assert.True(CertificateChecker.Check(request, report.Expression, outcome));
        }
    }

    [Fact]
    public void WorkCoefficientAndRevisionLimitsFailClosedDeterministically()
    {
        InputExpression input = Composition(
            "sqrt",
            new BigRational(2),
            new BigRational(1, 2));
        AnalysisRequest request = Request(input, AnalysisFeatures.Range);
        SemanticExpression semantic = new SemanticGraphBuilder(new ResourceBudget()).Build(input);

        var first = new ResourceBudget();
        Assert.True(AffinePhaseSineCompositionAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Range,
            first,
            out ProofOutcome<RealSet> firstOutcome));
        var second = new ResourceBudget();
        Assert.True(AffinePhaseSineCompositionAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Range,
            second,
            out ProofOutcome<RealSet> secondOutcome));
        Assert.Equal(first.WorkUsed, second.WorkUsed);
        Assert.Equal(
            ClaimCanonical.For(firstOutcome.Value!),
            ClaimCanonical.For(secondOutcome.Value!));

        var exhausted = new ResourceBudget();
        exhausted.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            AffinePhaseSineCompositionAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.Range,
                exhausted,
                out ProofOutcome<RealSet> _));

        var cancelled = new ResourceBudget(static () => false);
        Assert.Throws<AnalysisCancelledException>(() =>
            AffinePhaseSineCompositionAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.Range,
                cancelled,
                out ProofOutcome<RealSet> _));

        var certificate = Assert.IsType<AffinePhaseSineCompositionProofCertificate>(
            firstOutcome.Certificate);
        string claim = ClaimCanonical.For(firstOutcome.Value!);
        var checkerExhausted = new ResourceBudget();
        checkerExhausted.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            AffinePhaseSineCompositionCertificateChecker.Check(
                request,
                semantic,
                certificate,
                claim,
                checkerExhausted));
        var checkerCancelled = new ResourceBudget(static () => false);
        Assert.Throws<AnalysisCancelledException>(() =>
            AffinePhaseSineCompositionCertificateChecker.Check(
                request,
                semantic,
                certificate,
                claim,
                checkerCancelled));

        ExactInteger huge = ExactInteger.One << (AnalysisLimits.CoefficientBits + 1);
        InputExpression oversized = Composition(
            "ln",
            new BigRational(huge),
            new BigRational(1, 2));
        Assert.Throws<BudgetExceededException>(() =>
            new SemanticGraphBuilder(new ResourceBudget()).Build(oversized));
    }

    [Theory]
    [InlineData(1, 90, "q:360")]
    [InlineData(2, 100, "q:400")]
    public void DegreeAndGradHalfTurnsUseTheSelectedExactAngleUnit(
        int angleUnitValue,
        int phase,
        string expectedPeriod)
    {
        var angleUnit = (AngleUnit)angleUnitValue;
        InputExpression input = Function(
            "sqrt",
            Function("sin", Add(Variable(), Number(phase))));

        Assert.Equal(FunctionParity.Even, Analyze<FunctionParity>(
            input,
            AnalysisFeatures.Parity,
            angleUnit));
        Assert.Equal(
            "q:1",
            ExactRealCanonical.Format(Analyze<OptionalValue<ExactReal>>(
                input,
                AnalysisFeatures.YIntercept,
                angleUnit).Value!));
        Assert.Equal(
            expectedPeriod,
            ExactRealCanonical.Format(Analyze<Periodicity>(
                input,
                AnalysisFeatures.Period,
                angleUnit).FundamentalPeriod!));
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

    private static void AssertFeatureReplays(
        InputExpression input,
        AnalysisFeatures feature,
        AngleUnit angleUnit = AngleUnit.Radians)
    {
        switch (feature)
        {
            case AnalysisFeatures.Domain:
            case AnalysisFeatures.Range:
            case AnalysisFeatures.Zeros:
                _ = Analyze<RealSet>(input, feature, angleUnit);
                break;
            case AnalysisFeatures.Parity:
                _ = Analyze<FunctionParity>(input, feature, angleUnit);
                break;
            case AnalysisFeatures.YIntercept:
                _ = Analyze<OptionalValue<ExactReal>>(input, feature, angleUnit);
                break;
            case AnalysisFeatures.Minima:
            case AnalysisFeatures.Maxima:
            case AnalysisFeatures.InflectionPoints:
                _ = Analyze<ImmutableArray<FeaturePoint>>(input, feature, angleUnit);
                break;
            case AnalysisFeatures.VerticalAsymptotes:
            case AnalysisFeatures.HorizontalAsymptotes:
            case AnalysisFeatures.ObliqueAsymptotes:
                _ = Analyze<ImmutableArray<Asymptote>>(input, feature, angleUnit);
                break;
            case AnalysisFeatures.Monotonicity:
                _ = Analyze<ImmutableArray<MonotoneRegion>>(input, feature, angleUnit);
                break;
            case AnalysisFeatures.Period:
                _ = Analyze<Periodicity>(input, feature, angleUnit);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(feature));
        }
    }

    private static string Claim(InputExpression input, AnalysisFeatures feature) => feature switch
    {
        AnalysisFeatures.Domain or AnalysisFeatures.Range or AnalysisFeatures.Zeros =>
            ClaimCanonical.For(Analyze<RealSet>(input, feature)),
        AnalysisFeatures.Parity => ClaimCanonical.For(Analyze<FunctionParity>(input, feature)),
        AnalysisFeatures.YIntercept =>
            ClaimCanonical.For(Analyze<OptionalValue<ExactReal>>(input, feature)),
        AnalysisFeatures.Minima or AnalysisFeatures.Maxima or AnalysisFeatures.InflectionPoints =>
            ClaimCanonical.For(Analyze<ImmutableArray<FeaturePoint>>(input, feature)),
        AnalysisFeatures.VerticalAsymptotes or
        AnalysisFeatures.HorizontalAsymptotes or
        AnalysisFeatures.ObliqueAsymptotes =>
            ClaimCanonical.For(Analyze<ImmutableArray<Asymptote>>(input, feature)),
        AnalysisFeatures.Monotonicity =>
            ClaimCanonical.For(Analyze<ImmutableArray<MonotoneRegion>>(input, feature)),
        AnalysisFeatures.Period => ClaimCanonical.For(Analyze<Periodicity>(input, feature)),
        _ => throw new ArgumentOutOfRangeException(nameof(feature))
    };

    private static void AssertPeriod(InputExpression input, string expected)
    {
        Periodicity period = Analyze<Periodicity>(input, AnalysisFeatures.Period);
        Assert.Equal(PeriodicityKind.PeriodicWithFundamentalPeriod, period.Kind);
        Assert.Equal(expected, ExactRealCanonical.Format(period.FundamentalPeriod!));
    }

    private static T Analyze<T>(
        InputExpression input,
        AnalysisFeatures feature,
        AngleUnit angleUnit = AngleUnit.Radians)
    {
        (_, _, ProofOutcome<T> outcome) = AnalyzeOutcome<T>(input, feature, angleUnit);
        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.NotNull(outcome.Value);
        return outcome.Value;
    }

    private static (
        AnalysisRequest Request,
        SemanticExpression Expression,
        ProofOutcome<T> Outcome) AnalyzeOutcome<T>(
        InputExpression input,
        AnalysisFeatures feature,
        AngleUnit angleUnit = AngleUnit.Radians)
    {
        AnalysisRequest request = Request(input, feature, angleUnit);
        var budget = new ResourceBudget();
        SemanticExpression expression = new SemanticGraphBuilder(budget).Build(input);
        Assert.True(AffinePhaseSineCompositionAnalyzer.TryAnalyze(
            request,
            expression,
            feature,
            budget,
            out ProofOutcome<T> outcome),
            $"{expression.Value.Canonical}; defined={expression.DefinedWhen.Canonical}; feature={feature}");
        var certificate = Assert.IsType<AffinePhaseSineCompositionProofCertificate>(
            outcome.Certificate);
        Assert.True(AffinePhaseSineCompositionCertificateChecker.Check(
            request,
            expression,
            certificate,
            ClaimCanonical.For(outcome.Value!),
            new ResourceBudget()));
        return (request, expression, outcome);
    }

    private static void AssertUnsupported<T>(
        InputExpression input,
        AnalysisFeatures feature)
    {
        AnalysisRequest request = Request(input, feature);
        var budget = new ResourceBudget();
        SemanticExpression expression = new SemanticGraphBuilder(budget).Build(input);
        Assert.False(AffinePhaseSineCompositionAnalyzer.TryAnalyze(
            request,
            expression,
            feature,
            budget,
            out ProofOutcome<T> _));
    }

    private static AnalysisRequest Request(
        InputExpression expression,
        AnalysisFeatures feature,
        AngleUnit angleUnit = AngleUnit.Radians) =>
        new(expression, feature, angleUnit, "x", static () => true);

    private static InputExpression Composition(
        string outer,
        BigRational frequency,
        BigRational phasePiCoefficient) =>
        Function(
            outer,
            Function(
                "sin",
                Add(
                    Multiply(Number(frequency), Variable()),
                    PiFraction(phasePiCoefficient))));

    private static InputExpression PiFraction(BigRational coefficient) =>
        coefficient.IsOne
            ? Symbol("pi")
            : Multiply(Number(coefficient), Symbol("pi"));

    private static InputExpression Variable() => InputExpression.Variable("x", Source);

    private static InputExpression Symbol(string name) => InputExpression.Variable(name, Source);

    private static InputExpression Number(int value) => Number(new BigRational(value));

    private static InputExpression Number(BigRational value) => InputExpression.Number(value, Source);

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

    private static InputExpression Function(string name, params InputExpression[] arguments) =>
        InputExpression.Function(name, arguments.ToImmutableArray(), Source);
}
