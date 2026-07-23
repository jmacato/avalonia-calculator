using System.Collections.Immutable;
using Graphing;
using Graphing.Analyzer;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class MonotoneTrigonometricPhaseAnalysisTests
{
    private const string Rule = "strict-monotone-puiseux-trigonometric-pullback-v2";
    private static readonly SourceRange Source = new(0, 1);
    private static readonly AnalysisFeatures[] SupportedFeatures = [AnalysisFeatures.Domain, AnalysisFeatures.Range, AnalysisFeatures.Parity, AnalysisFeatures.Zeros, AnalysisFeatures.YIntercept, AnalysisFeatures.Minima, AnalysisFeatures.Maxima, AnalysisFeatures.VerticalAsymptotes, AnalysisFeatures.HorizontalAsymptotes, AnalysisFeatures.ObliqueAsymptotes, AnalysisFeatures.Monotonicity, AnalysisFeatures.Period];
    [Fact]
    public void GeneralPuiseuxLoweringCertifiesRequestedAndNonPortfolioPhases()
    {
        foreach (MonotoneTrigonometricPhaseAnalysisTestsPhaseCase phaseCase in PhaseCases())
        {
            foreach (AnalysisFeatures feature in SupportedFeatures)
            {
                AnalysisRequest request = Request(phaseCase.Input, feature);
                var producerBudget = new ResourceBudget();
                SemanticExpression semantic = new SemanticGraphBuilder(producerBudget).Build(phaseCase.Input);
                Assert.True(MonotoneTrigonometricPhaseAnalyzer.TryAnalyze(request, semantic, feature, producerBudget, out ProofOutcome<object> outcome), $"{phaseCase.Name}; {feature}; {semantic.Value.Canonical}");
                Assert.Equal(ProofState.Proved, outcome.State);
                var certificate = Assert.IsType<MonotoneTrigonometricPhaseProofCertificate>(outcome.Certificate);
                Assert.Equal(feature, certificate.Feature);
                Assert.Equal(feature, certificate.ProvenFeature);
                Assert.Equal(Rule, certificate.Rule);
                Assert.Equal(phaseCase.SubstitutionDegree, certificate.SubstitutionDegree);
                Assert.Equal(phaseCase.ParameterPolynomialCanonical, certificate.ParameterPolynomial.Canonical);
                Assert.Equal(phaseCase.ParameterIsNonnegative, certificate.ParameterIsNonnegative);
                Assert.Equal(PhaseOrientation.Increasing, certificate.Orientation);
                Assert.Equal(phaseCase.Terms.ToArray(), certificate.PhaseTerms.ToArray());
                Assert.Equal(phaseCase.DomainCanonical, certificate.DomainCells.Result.Canonical);
                Assert.Equal(certificate.DomainFormula.Canonical, certificate.DomainCells.Formula.Canonical);
                Assert.True(CellDecomposer.Verify(certificate.DomainCells, new ResourceBudget()));
                Assert.IsType<EmptySet>(certificate.DerivativeViolationCells.Result);
                Assert.True(CellDecomposer.Verify(certificate.DerivativeViolationCells, new ResourceBudget()));
                string claim = ClaimCanonical.ForObject(outcome.Value!);
                Assert.True(MonotoneTrigonometricPhaseCertificateChecker.Check(request, semantic, certificate, claim, new ResourceBudget()));
                Assert.True(CertificateChecker.Check(request, semantic, outcome));
            }
        }
    }

    [Fact]
    public void ProductionEnginePublishesGeneralClaimsAndLeavesCurvatureUnknown()
    {
        foreach (MonotoneTrigonometricPhaseAnalysisTestsPhaseCase phaseCase in PhaseCases())
        {
            AnalysisRequest request = Request(phaseCase.Input, AnalysisFeatures.All);
            AnalysisReport report = AnalysisEngine.Analyze(request);
            SemanticExpression semantic = Assert.IsType<SemanticExpression>(report.Expression);
            Assert.Equal(phaseCase.DomainCanonical, Proved(report.Domain).Canonical);
            Assert.Equal("interval[q:-1,1,q:1,1]", Proved(report.Range).Canonical);
            Assert.Equal(phaseCase.Parity, Proved(report.Parity));
            Assert.Equal("q:0", ExactRealCanonical.Format(Proved(report.YIntercept).Value!));
            var zeros = Assert.IsType<PolynomialPhasePreimageSet>(Proved(report.Zeros));
            AssertPreimage(zeros.Preimage, phaseCase, "pi:0:0", "pi:1:0");
            ImmutableArray<FeaturePoint> minima = Proved(report.Minima);
            Assert.Equal(phaseCase.ParameterIsNonnegative ? 2 : 1, minima.Length);
            if (phaseCase.ParameterIsNonnegative)
            {
                var endpoint = Assert.IsType<ConstantYFeaturePoint>(minima[0]);
                Assert.Equal("q:0", ExactRealCanonical.Format(Assert.IsType<SingletonReal>(endpoint.X).Value));
                Assert.Equal("q:0", ExactRealCanonical.Format(endpoint.Y));
            }

            var minimum = Assert.IsType<ConstantYFeaturePoint>(minima[^1]);
            Assert.Equal("q:-1", ExactRealCanonical.Format(minimum.Y));
            AssertPreimage(Assert.IsType<PolynomialPhasePreimageReal>(minimum.X).Preimage, phaseCase, "pi:3/2:0", "pi:2:0");
            var maximum = Assert.IsType<ConstantYFeaturePoint>(Assert.Single(Proved(report.Maxima)));
            Assert.Equal("q:1", ExactRealCanonical.Format(maximum.Y));
            AssertPreimage(Assert.IsType<PolynomialPhasePreimageReal>(maximum.X).Preimage, phaseCase, "pi:1/2:0", "pi:2:0");
            Assert.Empty(Proved(report.VerticalAsymptotes));
            Assert.Empty(Proved(report.HorizontalAsymptotes));
            Assert.Empty(Proved(report.ObliqueAsymptotes));
            ImmutableArray<MonotoneRegion> monotonicity = Proved(report.Monotonicity);
            Assert.Equal(2, monotonicity.Length);
            Assert.Equal(Monotonicity.Increasing, monotonicity[0].Direction);
            Assert.Equal(Monotonicity.Decreasing, monotonicity[1].Direction);
            var increasing = Assert.IsType<PolynomialPhaseSignSet>(monotonicity[0].Region);
            var decreasing = Assert.IsType<PolynomialPhaseSignSet>(monotonicity[1].Region);
            Assert.Equal(Comparison.Greater, increasing.Comparison);
            Assert.Equal(Comparison.Less, decreasing.Comparison);
            Assert.Equal(AngleUnit.Radians, increasing.AngleUnit);
            Assert.Equal(phaseCase.DomainCanonical, increasing.Domain.Canonical);
            Assert.Equal(increasing.PhaseCanonical, decreasing.PhaseCanonical);
            Assert.Equal(PeriodicityKind.NotPeriodic, Proved(report.Period).Kind);
            Assert.Equal(ProofState.Unknown, report.InflectionPoints.State);
            Assert.Equal(UnknownReason.UnsupportedFragment, report.InflectionPoints.UnknownReason);
            Assert.Null(report.InflectionPoints.Certificate);
            AssertReplay(request, semantic, report.Domain);
            AssertReplay(request, semantic, report.Range);
            AssertReplay(request, semantic, report.Parity);
            AssertReplay(request, semantic, report.Zeros);
            AssertReplay(request, semantic, report.YIntercept);
            AssertReplay(request, semantic, report.Minima);
            AssertReplay(request, semantic, report.Maxima);
            AssertReplay(request, semantic, report.VerticalAsymptotes);
            AssertReplay(request, semantic, report.HorizontalAsymptotes);
            AssertReplay(request, semantic, report.ObliqueAsymptotes);
            AssertReplay(request, semantic, report.Monotonicity);
            AssertReplay(request, semantic, report.Period);
            Assert.IsType<MonotoneTrigonometricPhaseProofCertificate>(report.Range.Certificate);
            Assert.IsType<MonotoneTrigonometricPhaseProofCertificate>(report.Parity.Certificate);
            Assert.IsType<MonotoneTrigonometricPhaseProofCertificate>(report.Zeros.Certificate);
            Assert.IsType<MonotoneTrigonometricPhaseProofCertificate>(report.YIntercept.Certificate);
            Assert.IsType<MonotoneTrigonometricPhaseProofCertificate>(report.Minima.Certificate);
            Assert.IsType<MonotoneTrigonometricPhaseProofCertificate>(report.Maxima.Certificate);
            Assert.IsType<MonotoneTrigonometricPhaseProofCertificate>(report.VerticalAsymptotes.Certificate);
            Assert.IsType<MonotoneTrigonometricPhaseProofCertificate>(report.HorizontalAsymptotes.Certificate);
            Assert.IsType<MonotoneTrigonometricPhaseProofCertificate>(report.ObliqueAsymptotes.Certificate);
            Assert.IsType<MonotoneTrigonometricPhaseProofCertificate>(report.Monotonicity.Certificate);
            Assert.IsType<MonotoneTrigonometricPhaseProofCertificate>(report.Period.Certificate);
        }
    }

    [Fact]
    public void OuterFunctionRuleTableCoversSineAndCosineAcrossCoordinateKinds()
    {
        InputExpression x = Variable();
        foreach (string outer in new[]
        {
            "sin",
            "cos"
        }

        )
        {
            foreach ((InputExpression Phase, bool Ray) phaseCase in new[]
            {
                (IntegerPower(x, 3), false),
                (Add(IntegerPower(x, 3), Sqrt(x)), true)
            }

            )
            {
                InputExpression input = Function(outer, phaseCase.Phase);
                AnalysisRequest request = Request(input, AnalysisFeatures.All);
                AnalysisReport report = AnalysisEngine.Analyze(request);
                Assert.Equal("interval[q:-1,1,q:1,1]", Proved(report.Range).Canonical);
                Assert.Equal(outer == "cos" && !phaseCase.Ray ? FunctionParity.Even : phaseCase.Ray ? FunctionParity.Neither : FunctionParity.Odd, Proved(report.Parity));
                Assert.Equal(outer == "cos" ? "q:1" : "q:0", ExactRealCanonical.Format(Proved(report.YIntercept).Value!));
                var zeros = Assert.IsType<PolynomialPhasePreimageSet>(Proved(report.Zeros));
                Assert.Equal(outer == "cos" ? "pi:1/2:0" : "pi:0:0", ExactRealCanonical.Format(zeros.Preimage.TargetOffset));
                Assert.Equal("pi:1:0", ExactRealCanonical.Format(zeros.Preimage.TargetPeriod));
                var minimum = Assert.IsType<ConstantYFeaturePoint>(Proved(report.Minima)[^1]);
                var maximum = Assert.IsType<ConstantYFeaturePoint>(Proved(report.Maxima)[^1]);
                Assert.Equal(outer == "cos" ? "pi:1:0" : "pi:3/2:0", ExactRealCanonical.Format(Assert.IsType<PolynomialPhasePreimageReal>(minimum.X).Preimage.TargetOffset));
                Assert.Equal(outer == "cos" ? "pi:0:0" : "pi:1/2:0", ExactRealCanonical.Format(Assert.IsType<PolynomialPhasePreimageReal>(maximum.X).Preimage.TargetOffset));
                foreach (ProofCertificate? certificate in PublishedCertificates(report).Where(static certificate => certificate is not null))
                {
                    if (certificate is MonotoneTrigonometricPhaseProofCertificate phase)
                    {
                        Assert.Equal(outer, phase.OuterFunction);
                    }
                }

                SemanticExpression semantic = Assert.IsType<SemanticExpression>(report.Expression);
                AssertReplay(request, semantic, report.Range);
                AssertReplay(request, semantic, report.Parity);
                AssertReplay(request, semantic, report.Zeros);
                AssertReplay(request, semantic, report.Minima);
                AssertReplay(request, semantic, report.Maxima);
                AssertReplay(request, semantic, report.Monotonicity);
                AssertReplay(request, semantic, report.Period);
            }
        }
    }

    [Fact]
    public void EquivalentProductPowerAndRootSyntaxPublishIdenticalClaims()
    {
        InputExpression x = Variable();
        InputExpression product = Sin(Multiply(IntegerPower(x, 3), Sqrt(x)));
        InputExpression reordered = Sin(Multiply(Sqrt(x), IntegerPower(x, 3)));
        InputExpression rationalPower = Sin(Power(x, new BigRational(7, 2)));
        AssertSameClaims(product, reordered, rationalPower);
        InputExpression squareRootSum = Sin(Add(IntegerPower(x, 3), Sqrt(x)));
        InputExpression fixedRootSum = Sin(Add(Function("root", x, Number(2)), IntegerPower(x, 3)));
        AssertSameClaims(squareRootSum, fixedRootSum);
        InputExpression fourthRootSeventhPower = Sin(IntegerPower(Function("root", x, Number(4)), 7));
        InputExpression sevenFourthsPower = Sin(Power(x, new BigRational(7, 4)));
        AssertSameClaims(fourthRootSeventhPower, sevenFourthsPower);
        InputExpression signedOddRootPhase = Sin(Add(Function("root", x, Number(3)), x));
        InputExpression signedOddRootWithExactZero = Sin(Add(Add(Function("root", Number(0), Number(3)), Function("root", x, Number(3))), x));
        AssertSameClaims(signedOddRootPhase, signedOddRootWithExactZero);
        InputExpression negativeSignedRoot = Sin(Function("root", Multiply(Number(-8), IntegerPower(x, 3)), Number(3)));
        AnalysisRequest negativeRequest = Request(negativeSignedRoot, AnalysisFeatures.Zeros);
        SemanticExpression negativeSemantic = Build(negativeSignedRoot);
        Assert.True(MonotoneTrigonometricPhaseAnalyzer.TryAnalyze(negativeRequest, negativeSemantic, AnalysisFeatures.Zeros, new ResourceBudget(), out ProofOutcome<RealSet> negativeZeros));
        var negativeCertificate = Assert.IsType<MonotoneTrigonometricPhaseProofCertificate>(negativeZeros.Certificate);
        Assert.Equal(PhaseOrientation.Decreasing, negativeCertificate.Orientation);
        Assert.Equal([new PuiseuxTerm(BigRational.One, new BigRational(-2))], negativeCertificate.PhaseTerms);
        Assert.True(CertificateChecker.Check(negativeRequest, negativeSemantic, negativeZeros));
    }

    [Fact]
    public void OversizedPhasePowersFailWithinDeterministicLimits()
    {
        InputExpression x = Variable();
        BigRational huge = new(1_000_000_000);
        BigRational largeCoefficient = new(ExactInteger.One << 100);
        InputExpression[] phases = [Power(Multiply(Number(2), x), huge), Power(Multiply(Number(2), x), -huge), Power(Number(2), huge), Power(x, new BigRational(1, 1_000_000_000)), Power(Number(largeCoefficient), new BigRational(256)), Power(Number(largeCoefficient), new BigRational(-256))];
        foreach (InputExpression phase in phases)
        {
            SemanticExpression semantic = Build(phase);
            var budget = new ResourceBudget();
            Assert.False(PuiseuxPolynomial.TryExtract(semantic.Value, "x", budget, out _));
            Assert.InRange(budget.WorkUsed, 0, 100);
        }
    }

    [Fact]
    public void NonmonotoneAffineAndRetainedHoleBranchesFailClosed()
    {
        InputExpression x = Variable();
        InputExpression[] rejected = [Sin(IntegerPower(x, 2)), Sin(Subtract(IntegerPower(x, 3), x)), Sin(Sqrt(IntegerPower(x, 6))), Sin(Power(IntegerPower(x, 2), new BigRational(1, 2)))];
        foreach (InputExpression input in rejected)
        {
            AnalysisRequest request = Request(input, AnalysisFeatures.Range);
            var budget = new ResourceBudget();
            SemanticExpression semantic = new SemanticGraphBuilder(budget).Build(input);
            Assert.False(MonotoneTrigonometricPhaseAnalyzer.TryAnalyze(request, semantic, AnalysisFeatures.Range, budget, out ProofOutcome<object> _));
        }

        InputExpression clean = Sin(Add(IntegerPower(x, 3), Sqrt(x)));
        InputExpression retainedHole = Sin(Add(Add(IntegerPower(x, 3), Sqrt(x)), Multiply(Number(0), Divide(Number(1), Subtract(x, Number(1))))));
        SemanticExpression cleanSemantic = Build(clean);
        SemanticExpression hiddenSemantic = Build(retainedHole);
        Assert.Equal(cleanSemantic.Value.Canonical, hiddenSemantic.Value.Canonical);
        Assert.NotEqual(cleanSemantic.DefinedWhen.Canonical, hiddenSemantic.DefinedWhen.Canonical);
        AnalysisRequest hiddenRequest = Request(retainedHole, AnalysisFeatures.All);
        Assert.False(MonotoneTrigonometricPhaseAnalyzer.TryAnalyze(hiddenRequest, hiddenSemantic, AnalysisFeatures.Range, new ResourceBudget(), out ProofOutcome<object> _));
        AnalysisReport hiddenReport = AnalysisEngine.Analyze(hiddenRequest);
        Assert.Equal("union[interval[q:0,1,q:1,0],interval[q:1,0,+inf,0]]", Proved(hiddenReport.Domain).Canonical);
        Assert.DoesNotContain(PublishedCertificates(hiddenReport), static certificate => certificate is MonotoneTrigonometricPhaseProofCertificate);
    }

    [Fact]
    public void SignedOddRootPhasesUseABijectiveRealSubstitution()
    {
        InputExpression x = Variable();
        InputExpression phase = Add(Function("root", x, Number(3)), x);
        InputExpression input = Sin(phase);
        foreach (AnalysisFeatures feature in SupportedFeatures)
        {
            AnalysisRequest request = Request(input, feature);
            var budget = new ResourceBudget();
            SemanticExpression semantic = new SemanticGraphBuilder(budget).Build(input);
            Assert.True(MonotoneTrigonometricPhaseAnalyzer.TryAnalyze(request, semantic, feature, budget, out ProofOutcome<object> outcome), feature.ToString());
            var certificate = Assert.IsType<MonotoneTrigonometricPhaseProofCertificate>(outcome.Certificate);
            Assert.False(certificate.ParameterIsNonnegative);
            Assert.Equal(3, certificate.SubstitutionDegree);
            Assert.Equal("poly[0,1,0,1]", certificate.ParameterPolynomial.Canonical);
            Assert.Equal(PhaseOrientation.Increasing, certificate.Orientation);
            Assert.Equal([new PuiseuxTerm(new BigRational(1, 3), BigRational.One), new PuiseuxTerm(BigRational.One, BigRational.One)], certificate.PhaseTerms);
            Assert.True(CertificateChecker.Check(request, semantic, outcome));
        }

        AnalysisReport report = AnalysisEngine.Analyze(Request(input, AnalysisFeatures.All));
        Assert.Equal("reals", Proved(report.Domain).Canonical);
        Assert.Equal(FunctionParity.Odd, Proved(report.Parity));
        InputExpression principalPower = Sin(Power(x, new BigRational(1, 3)));
        AnalysisReport principalReport = AnalysisEngine.Analyze(Request(principalPower, AnalysisFeatures.Domain | AnalysisFeatures.Range));
        Assert.Equal("interval[q:0,1,+inf,0]", Proved(principalReport.Domain).Canonical);
    }

    [Fact]
    public void OrientationAndEndpointLimitationsAreDerivedNotShapeDispatched()
    {
        InputExpression x = Variable();
        InputExpression decreasing = Sin(Negate(Add(Add(IntegerPower(x, 5), Multiply(Number(2), IntegerPower(x, 3))), x)));
        AnalysisRequest decreasingRequest = Request(decreasing, AnalysisFeatures.Monotonicity);
        var decreasingBudget = new ResourceBudget();
        SemanticExpression decreasingSemantic = new SemanticGraphBuilder(decreasingBudget).Build(decreasing);
        Assert.True(MonotoneTrigonometricPhaseAnalyzer.TryAnalyze(decreasingRequest, decreasingSemantic, AnalysisFeatures.Monotonicity, decreasingBudget, out ProofOutcome<ImmutableArray<MonotoneRegion>> decreasingOutcome));
        var decreasingCertificate = Assert.IsType<MonotoneTrigonometricPhaseProofCertificate>(decreasingOutcome.Certificate);
        Assert.Equal(PhaseOrientation.Decreasing, decreasingCertificate.Orientation);
        Assert.Equal(Monotonicity.Decreasing, Proved(decreasingOutcome)[0].Direction);
        Assert.True(CertificateChecker.Check(decreasingRequest, decreasingSemantic, decreasingOutcome));
        InputExpression affine = Sin(Add(Multiply(Number(2), x), Number(1)));
        AnalysisRequest affineRequest = Request(affine, AnalysisFeatures.Period);
        SemanticExpression affineSemantic = Build(affine);
        Assert.True(MonotoneTrigonometricPhaseAnalyzer.TryAnalyze(affineRequest, affineSemantic, AnalysisFeatures.Period, new ResourceBudget(), out ProofOutcome<Periodicity> affinePeriod));
        Assert.Equal(PeriodicityKind.PeriodicWithFundamentalPeriod, Proved(affinePeriod).Kind);
        Assert.Equal("pi:1:0", ExactRealCanonical.Format(Proved(affinePeriod).FundamentalPeriod!));
        Assert.True(CertificateChecker.Check(affineRequest, affineSemantic, affinePeriod));
        InputExpression shiftedRay = Sin(Add(Sqrt(x), Number(1)));
        AnalysisRequest rangeRequest = Request(shiftedRay, AnalysisFeatures.Range);
        SemanticExpression shiftedSemantic = Build(shiftedRay);
        Assert.True(MonotoneTrigonometricPhaseAnalyzer.TryAnalyze(rangeRequest, shiftedSemantic, AnalysisFeatures.Range, new ResourceBudget(), out ProofOutcome<RealSet> range));
        Assert.Equal("interval[q:-1,1,q:1,1]", Proved(range).Canonical);
        Assert.True(MonotoneTrigonometricPhaseAnalyzer.TryAnalyze(Request(shiftedRay, AnalysisFeatures.Zeros), shiftedSemantic, AnalysisFeatures.Zeros, new ResourceBudget(), out ProofOutcome<RealSet> shiftedZeros));
        var shiftedPreimage = Assert.IsType<PolynomialPhasePreimageSet>(Proved(shiftedZeros));
        Assert.Equal(ExactInteger.One, shiftedPreimage.Preimage.Constraint.Bound.Value);
    }

    [Fact]
    public void ShiftedCosineAndReciprocalSineUseTheSameCertifiedPullback()
    {
        InputExpression x = Variable();
        InputExpression shiftedCosine = Function("cos", Add(x, Sqrt(Add(x, Number(1)))));
        AnalysisRequest cosineRequest = Request(shiftedCosine, AnalysisFeatures.All);
        AnalysisReport cosineReport = AnalysisEngine.Analyze(cosineRequest);
        Assert.Equal("interval[q:-1,1,+inf,0]", Proved(cosineReport.Domain).Canonical);
        var cosineZeros = Assert.IsType<PolynomialPhasePreimageSet>(Proved(cosineReport.Zeros));
        Assert.Equal(new BigRational(-1), cosineZeros.Preimage.CoordinateOffset);
        Assert.Equal(2, cosineZeros.Preimage.SubstitutionDegree);
        Assert.True(cosineZeros.Preimage.BoundaryIncluded);
        Assert.Equal("poly[-1,1,1]", cosineZeros.Preimage.ParameterPolynomial.Canonical);
        Assert.Equal("pi:1/2:0", ExactRealCanonical.Format(cosineZeros.Preimage.TargetOffset));
        Assert.Equal(ExactInteger.Zero, cosineZeros.Preimage.Constraint.Bound.Value);
        var cosineCertificate = Assert.IsType<MonotoneTrigonometricPhaseProofCertificate>(cosineReport.Zeros.Certificate);
        Assert.Equal("cos", cosineCertificate.OuterFunction);
        Assert.Equal(PhaseOrientation.Increasing, cosineCertificate.Orientation);
        Assert.True(CertificateChecker.Check(cosineRequest with { Features = AnalysisFeatures.Zeros }, cosineReport.Expression!, cosineReport.Zeros));
        SemanticExpression cosineSemantic = Build(shiftedCosine);
        AnalysisRequest directOriginRequest = Request(shiftedCosine, AnalysisFeatures.YIntercept);
        Assert.True(MonotoneTrigonometricPhaseAnalyzer.TryAnalyze(directOriginRequest, cosineSemantic, AnalysisFeatures.YIntercept, new ResourceBudget(), out ProofOutcome<OptionalValue<ExactReal>> directOrigin));
        Assert.Equal("fn:cos(q:1)", ExactRealCanonical.Format(Proved(directOrigin).Value!));
        Assert.True(CertificateChecker.Check(directOriginRequest, cosineSemantic, directOrigin));
        InputExpression reciprocalPhase = Divide(Sin(Sqrt(Divide(Number(1), x))), x);
        AnalysisRequest reciprocalRequest = Request(reciprocalPhase, AnalysisFeatures.Domain | AnalysisFeatures.Zeros | AnalysisFeatures.HorizontalAsymptotes);
        AnalysisReport reciprocalReport = AnalysisEngine.Analyze(reciprocalRequest);
        Assert.Equal("interval[q:0,0,+inf,0]", Proved(reciprocalReport.Domain).Canonical);
        var reciprocalZeros = Assert.IsType<PolynomialPhasePreimageSet>(Proved(reciprocalReport.Zeros));
        Assert.Equal(-2, reciprocalZeros.Preimage.SubstitutionDegree);
        Assert.False(reciprocalZeros.Preimage.BoundaryIncluded);
        Assert.Equal("poly[0,1]", reciprocalZeros.Preimage.ParameterPolynomial.Canonical);
        Assert.Equal(ExactInteger.One, reciprocalZeros.Preimage.Constraint.Bound.Value);
        var reciprocalCertificate = Assert.IsType<MonotoneTrigonometricPhaseProofCertificate>(reciprocalReport.Zeros.Certificate);
        Assert.Equal(PhaseOrientation.Decreasing, reciprocalCertificate.Orientation);
        Assert.True(CertificateChecker.Check(reciprocalRequest with { Features = AnalysisFeatures.Zeros }, reciprocalReport.Expression!, reciprocalReport.Zeros));
        Asymptote reciprocalHorizontal = Assert.Single(Proved(reciprocalReport.HorizontalAsymptotes));
        Assert.Equal("q:0", ExactRealCanonical.Format(reciprocalHorizontal.Intercept!));
        Assert.True(CertificateChecker.Check(reciprocalRequest with { Features = AnalysisFeatures.HorizontalAsymptotes }, reciprocalReport.Expression!, reciprocalReport.HorizontalAsymptotes));
        InputExpression reciprocalCosine = Function("cos", Sqrt(Divide(Number(1), x)));
        AnalysisRequest reciprocalCosineRequest = Request(reciprocalCosine, AnalysisFeatures.All);
        AnalysisReport reciprocalCosineReport = AnalysisEngine.Analyze(reciprocalCosineRequest);
        Assert.Equal("interval[q:-1,1,q:1,1]", Proved(reciprocalCosineReport.Range).Canonical);
        Assert.False(Proved(reciprocalCosineReport.YIntercept).HasValue);
        Assert.Equal(FunctionParity.Neither, Proved(reciprocalCosineReport.Parity));
        Asymptote cosineHorizontal = Assert.Single(Proved(reciprocalCosineReport.HorizontalAsymptotes));
        Assert.Equal("q:1", ExactRealCanonical.Format(cosineHorizontal.Intercept!));
        var reciprocalCosineCertificate = Assert.IsType<MonotoneTrigonometricPhaseProofCertificate>(reciprocalCosineReport.HorizontalAsymptotes.Certificate);
        Assert.Equal("cos", reciprocalCosineCertificate.OuterFunction);
        Assert.Equal(-2, reciprocalCosineCertificate.SubstitutionDegree);
        Assert.Equal(PhaseOrientation.Decreasing, reciprocalCosineCertificate.Orientation);
        Assert.True(CertificateChecker.Check(reciprocalCosineRequest with { Features = AnalysisFeatures.HorizontalAsymptotes }, reciprocalCosineReport.Expression!, reciprocalCosineReport.HorizontalAsymptotes));
        Assert.Equal("x = πn₁ + π/2 − (sqrt(4(πn₁ + π/2) + 5) − 1)/2, n₁ ∈ ℤ, n₁ ≥ 0", AnalyzePublic("cos(x+sqrt(x+1))").Zeros);
        Assert.Equal("x = 1/((πn₁)^2), n₁ ∈ ℤ, n₁ ≥ 1", AnalyzePublic("sin(sqrt(1/x))/x").Zeros);
        Assert.Equal("y = 0", Assert.Single(AnalyzePublic("sin(sqrt(1/x))/x").HorizontalAsymptotes));
    }

    [Fact]
    public void DecreasingRayDerivesNegativeLatticesAndEndpointMaximum()
    {
        InputExpression x = Variable();
        InputExpression input = Sin(Negate(Multiply(IntegerPower(x, 3), Sqrt(x))));
        AnalysisRequest request = Request(input, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        var zeros = Assert.IsType<PolynomialPhasePreimageSet>(Proved(report.Zeros));
        Assert.Equal(Comparison.LessOrEqual, zeros.Preimage.Constraint.Comparison);
        Assert.Equal(ExactInteger.Zero, zeros.Preimage.Constraint.Bound.Value);
        var minimum = Assert.IsType<ConstantYFeaturePoint>(Assert.Single(Proved(report.Minima)));
        var minimumPreimage = Assert.IsType<PolynomialPhasePreimageReal>(minimum.X);
        Assert.Equal(new ExactInteger(-1), minimumPreimage.Preimage.Constraint.Bound.Value);
        Assert.Equal(Comparison.LessOrEqual, minimumPreimage.Preimage.Constraint.Comparison);
        ImmutableArray<FeaturePoint> maxima = Proved(report.Maxima);
        Assert.Equal(2, maxima.Length);
        var endpoint = Assert.IsType<ConstantYFeaturePoint>(maxima[0]);
        Assert.Equal("q:0", ExactRealCanonical.Format(Assert.IsType<SingletonReal>(endpoint.X).Value));
        Assert.Equal("q:0", ExactRealCanonical.Format(endpoint.Y));
        var maximum = Assert.IsType<ConstantYFeaturePoint>(maxima[1]);
        var maximumPreimage = Assert.IsType<PolynomialPhasePreimageReal>(maximum.X);
        Assert.Equal(new ExactInteger(-1), maximumPreimage.Preimage.Constraint.Bound.Value);
        SemanticExpression semantic = Assert.IsType<SemanticExpression>(report.Expression);
        AssertReplay(request, semantic, report.Zeros);
        AssertReplay(request, semantic, report.Minima);
        AssertReplay(request, semantic, report.Maxima);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(4, 1)]
    public void LatticeBoundsAreDerivedByExactAngleComparison(int boundaryPhase, int expectedUpperIndex)
    {
        InputExpression x = Variable();
        InputExpression input = Sin(Add(Number(boundaryPhase), Negate(Sqrt(x))));
        AnalysisRequest request = Request(input, AnalysisFeatures.Zeros);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        var zeros = Assert.IsType<PolynomialPhasePreimageSet>(Proved(report.Zeros));
        Assert.Equal(Comparison.LessOrEqual, zeros.Preimage.Constraint.Comparison);
        Assert.Equal(new ExactInteger(expectedUpperIndex), zeros.Preimage.Constraint.Bound.Value);
        var certificate = Assert.IsType<MonotoneTrigonometricPhaseProofCertificate>(report.Zeros.Certificate);
        Assert.Equal(PhaseOrientation.Decreasing, certificate.ParameterOrientation);
        Assert.Equal(PhaseOrientation.Decreasing, certificate.Orientation);
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Zeros));
    }

    [Fact]
    public void ExactPublicParserCasesFormatExplicitAndImplicitPreimages()
    {
        foreach (MonotoneTrigonometricPhaseAnalysisTestsPublicCase publicCase in PublicCases())
        {
            GraphFunctionAnalysisData result = AnalyzePublic(publicCase.Formula);
            Assert.Equal(publicCase.Domain, result.Domain);
            Assert.Empty(result.Range);
            Assert.Equal(publicCase.Zeros, result.Zeros);
            Assert.Equal("y = 0", result.YIntercept);
            Assert.Equal(publicCase.Minima, string.Join(" | ", result.Minima));
            Assert.Equal(publicCase.Maxima, string.Join(" | ", result.Maxima));
            Assert.Empty(result.InflectionPoints);
            Assert.Empty(result.VerticalAsymptotes);
            Assert.Empty(result.HorizontalAsymptotes);
            Assert.Empty(result.ObliqueAsymptotes);
            Assert.Equal((int)publicCase.Parity, result.Parity);
            Assert.Equal((int)FunctionPeriodicityType.Unknown, result.PeriodicityDirection);
            Assert.Empty(result.PeriodicityExpression);
            Assert.Empty(result.MonotoneIntervals);
            Assert.Equal(4362, result.TooComplexFeatures);
        }
    }

    private static void AssertPreimage(PolynomialPhasePreimage preimage, MonotoneTrigonometricPhaseAnalysisTestsPhaseCase phaseCase, string expectedOffset, string expectedPeriod)
    {
        Assert.Equal("x", preimage.Variable);
        Assert.Equal(phaseCase.ParameterPolynomialCanonical, preimage.ParameterPolynomial.Canonical);
        Assert.Equal(phaseCase.SubstitutionDegree, preimage.SubstitutionDegree);
        Assert.Equal(phaseCase.ParameterIsNonnegative, preimage.ParameterIsNonnegative);
        Assert.Equal(expectedOffset, ExactRealCanonical.Format(preimage.TargetOffset));
        Assert.Equal(expectedPeriod, ExactRealCanonical.Format(preimage.TargetPeriod));
        Assert.Equal("n₁", preimage.Parameter);
        if (phaseCase.ParameterIsNonnegative)
        {
            Assert.False(preimage.Constraint.Bound.IsUnbounded);
            Assert.True(preimage.Constraint.Bound.Value.IsZero);
            Assert.Equal(Comparison.GreaterOrEqual, preimage.Constraint.Comparison);
        }
        else
        {
            Assert.True(preimage.Constraint.Bound.IsUnbounded);
        }
    }

    private static void AssertSameClaims(InputExpression expected, params InputExpression[] equivalents)
    {
        string[] expectedClaims = Claims(AnalysisEngine.Analyze(Request(expected, AnalysisFeatures.All)));
        foreach (InputExpression equivalent in equivalents)
        {
            Assert.Equal(expectedClaims, Claims(AnalysisEngine.Analyze(Request(equivalent, AnalysisFeatures.All))));
        }
    }

    private static string[] Claims(AnalysisReport report)
    {
        return
        [
            Proved(report.Domain).Canonical, Proved(report.Range).Canonical, ClaimCanonical.For(Proved(report.Parity)),
            Proved(report.Zeros).Canonical, ClaimCanonical.For(Proved(report.YIntercept)),
            ClaimCanonical.For(Proved(report.Minima)), ClaimCanonical.For(Proved(report.Maxima)),
            ClaimCanonical.For(Proved(report.VerticalAsymptotes)),
            ClaimCanonical.For(Proved(report.HorizontalAsymptotes)),
            ClaimCanonical.For(Proved(report.ObliqueAsymptotes)), ClaimCanonical.For(Proved(report.Monotonicity)),
            ClaimCanonical.For(Proved(report.Period))
        ];
    }

    private static IEnumerable<ProofCertificate?> PublishedCertificates(AnalysisReport report)
    {
        yield return report.Domain.Certificate;
        yield return report.Range.Certificate;
        yield return report.Parity.Certificate;
        yield return report.Zeros.Certificate;
        yield return report.YIntercept.Certificate;
        yield return report.Minima.Certificate;
        yield return report.Maxima.Certificate;
        yield return report.InflectionPoints.Certificate;
        yield return report.VerticalAsymptotes.Certificate;
        yield return report.HorizontalAsymptotes.Certificate;
        yield return report.ObliqueAsymptotes.Certificate;
        yield return report.Monotonicity.Certificate;
        yield return report.Period.Certificate;
    }

    private static IEnumerable<MonotoneTrigonometricPhaseAnalysisTestsPhaseCase> PhaseCases()
    {
        InputExpression x = Variable();
        yield return new MonotoneTrigonometricPhaseAnalysisTestsPhaseCase("cube", Sin(IntegerPower(x, 3)), [new PuiseuxTerm(new BigRational(3), BigRational.One)], 1, "poly[0,0,0,1]", false, "reals", FunctionParity.Odd);
        yield return new MonotoneTrigonometricPhaseAnalysisTestsPhaseCase("cube-times-square-root", Sin(Multiply(IntegerPower(x, 3), Sqrt(x))), [new PuiseuxTerm(new BigRational(7, 2), BigRational.One)], 2, "poly[0,0,0,0,0,0,0,1]", true, "interval[q:0,1,+inf,0]", FunctionParity.Neither);
        yield return new MonotoneTrigonometricPhaseAnalysisTestsPhaseCase("cube-plus-square-root", Sin(Add(IntegerPower(x, 3), Sqrt(x))), [new PuiseuxTerm(new BigRational(1, 2), BigRational.One), new PuiseuxTerm(new BigRational(3), BigRational.One)], 2, "poly[0,1,0,0,0,0,1]", true, "interval[q:0,1,+inf,0]", FunctionParity.Neither);
        yield return new MonotoneTrigonometricPhaseAnalysisTestsPhaseCase("nonportfolio-odd-polynomial", Sin(Add(Add(IntegerPower(x, 5), Multiply(Number(2), IntegerPower(x, 3))), x)), [new PuiseuxTerm(BigRational.One, BigRational.One), new PuiseuxTerm(new BigRational(3), new BigRational(2)), new PuiseuxTerm(new BigRational(5), BigRational.One)], 1, "poly[0,1,0,2,0,1]", false, "reals", FunctionParity.Odd);
    }

    private static IEnumerable<MonotoneTrigonometricPhaseAnalysisTestsPublicCase> PublicCases()
    {
        yield return new MonotoneTrigonometricPhaseAnalysisTestsPublicCase("sin(x^3)", "x ∈ ℝ", "x = root(πn₁, 3), n₁ ∈ ℤ", "(root(2πn₁ + 3π/2, 3), sin(3π/2)), n₁ ∈ ℤ", "(root(2πn₁ + π/2, 3), sin(π/2)), n₁ ∈ ℤ", FunctionParityType.Odd);
        yield return new MonotoneTrigonometricPhaseAnalysisTestsPublicCase("sin((x^3)*sqrt(x))", "x ≥ 0", "x = (root(πn₁, 7))^2, n₁ ∈ ℤ, n₁ ≥ 0", "(0, 0) | ((root(2πn₁ + 3π/2, 7))^2, sin(3π/2)), n₁ ∈ ℤ, n₁ ≥ 0", "((root(2πn₁ + π/2, 7))^2, sin(π/2)), n₁ ∈ ℤ, n₁ ≥ 0", FunctionParityType.None);
        yield return new MonotoneTrigonometricPhaseAnalysisTestsPublicCase("sin((x^3)+sqrt(x))", "x ≥ 0", "{ x ∈ ℝ | x ≥ 0 ∧ x^3 + sqrt(x) = πn₁ }, n₁ ∈ ℤ, n₁ ≥ 0", "(0, 0) | (x, sin(3π/2)), x^3 + sqrt(x) = 2πn₁ + 3π/2, x ≥ 0, n₁ ∈ ℤ, n₁ ≥ 0", "(x, sin(π/2)), x^3 + sqrt(x) = 2πn₁ + π/2, x ≥ 0, n₁ ∈ ℤ, n₁ ≥ 0", FunctionParityType.None);
        yield return new MonotoneTrigonometricPhaseAnalysisTestsPublicCase("sin(x^5+2*x^3+x)", "x ∈ ℝ", "{ x ∈ ℝ | x^5 + 2*x^3 + x = πn₁ }, n₁ ∈ ℤ", "(x, sin(3π/2)), x^5 + 2*x^3 + x = 2πn₁ + 3π/2, n₁ ∈ ℤ", "(x, sin(π/2)), x^5 + 2*x^3 + x = 2πn₁ + π/2, n₁ ∈ ℤ", FunctionParityType.Odd);
        yield return new MonotoneTrigonometricPhaseAnalysisTestsPublicCase("sin(root(x^5,3)+root(x,3))", "x ∈ ℝ", "{ x ∈ ℝ | (root(x, 3))^5 + root(x, 3) = πn₁ }, n₁ ∈ ℤ", "(x, sin(3π/2)), (root(x, 3))^5 + root(x, 3) = 2πn₁ + 3π/2, n₁ ∈ ℤ", "(x, sin(π/2)), (root(x, 3))^5 + root(x, 3) = 2πn₁ + π/2, n₁ ∈ ℤ", FunctionParityType.Odd);
    }

    private static void AssertReplay<T>(AnalysisRequest request, SemanticExpression semantic, ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.NotNull(outcome.Certificate);
        Assert.True(CertificateChecker.Check(request, semantic, outcome));
    }

    private static T Proved<T>(ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        return outcome.Value!;
    }

    private static GraphFunctionAnalysisData AnalyzePublic(string formula)
    {
        IMathSolver solver = MathSolver.CreateMathSolver();
        solver.ParsingOptions().SetFormatType(FormatType.Linear);
        solver.EvalOptions().SetTrigUnitMode(EvalTrigUnitMode.Radians);
        IExpression expression = solver.ParseInput(formula, out int errorCode, out int errorType) ?? throw new InvalidOperationException($"Parse failed for {formula}: {errorCode}/{errorType}");
        IGraph graph = solver.CreateGrapher();
        Assert.NotNull(graph.TryInitialize(expression));
        IGraphAnalyzer analyzer = graph.GetAnalyzer();
        Assert.True(analyzer.CanFunctionAnalysisBePerformed(out bool variableIsNotX));
        Assert.False(variableIsNotX);
        Assert.Equal(GraphStatus.Ok, analyzer.PerformFunctionAnalysis((uint)PerformAnalysisType.All));
        return solver.Analyze(analyzer);
    }

    private static AnalysisRequest Request(InputExpression input, AnalysisFeatures features)
    {
        return new AnalysisRequest(input, features, AngleUnit.Radians, "x", static () => true);
    }

    private static SemanticExpression Build(InputExpression input)
    {
        return new SemanticGraphBuilder(new ResourceBudget()).Build(input);
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

    private static InputExpression Add(InputExpression left, InputExpression right)
    {
        return InputExpression.Binary(InputExpressionKind.Add, left, right, Source);
    }

    private static InputExpression Subtract(InputExpression left, InputExpression right)
    {
        return InputExpression.Binary(InputExpressionKind.Subtract, left, right, Source);
    }

    private static InputExpression Negate(InputExpression operand)
    {
        return InputExpression.Unary(InputExpressionKind.Negate, operand, Source);
    }

    private static InputExpression Multiply(InputExpression left, InputExpression right)
    {
        return InputExpression.Binary(InputExpressionKind.Multiply, left, right, Source);
    }

    private static InputExpression Divide(InputExpression left, InputExpression right)
    {
        return InputExpression.Binary(InputExpressionKind.Divide, left, right, Source);
    }

    private static InputExpression IntegerPower(InputExpression basis, int exponent)
    {
        return Power(basis, new BigRational(exponent));
    }

    private static InputExpression Power(InputExpression basis, BigRational exponent)
    {
        return InputExpression.Binary(InputExpressionKind.Power, basis, Number(exponent), Source);
    }

    private static InputExpression Sqrt(InputExpression argument)
    {
        return Function("sqrt", argument);
    }

    private static InputExpression Sin(InputExpression argument)
    {
        return Function("sin", argument);
    }

    private static InputExpression Function(string name, params InputExpression[] arguments)
    {
        return InputExpression.Function(name, arguments.ToImmutableArray(), Source);
    }
}
