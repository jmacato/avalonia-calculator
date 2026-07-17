using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class MonotoneTrigonometricPhaseCertificateReplayTests
{
    private static readonly SourceRange Source = new(0, 1);

    private static readonly AnalysisFeatures[] SupportedFeatures =
    [
        AnalysisFeatures.Domain,
        AnalysisFeatures.Range,
        AnalysisFeatures.Parity,
        AnalysisFeatures.Zeros,
        AnalysisFeatures.YIntercept,
        AnalysisFeatures.Minima,
        AnalysisFeatures.Maxima,
        AnalysisFeatures.VerticalAsymptotes,
        AnalysisFeatures.HorizontalAsymptotes,
        AnalysisFeatures.ObliqueAsymptotes,
        AnalysisFeatures.Monotonicity,
        AnalysisFeatures.Period
    ];

    [Fact]
    public void EveryPublishedFeatureReplaysThroughDedicatedAndCentralCheckers()
    {
        InputExpression input = Sin(Power(Variable(), 3));

        foreach (AnalysisFeatures feature in SupportedFeatures)
        {
            (AnalysisRequest request, SemanticExpression expression,
                ProofOutcome<object> outcome) = Produce<object>(input, feature);
            var certificate = Assert.IsType<MonotoneTrigonometricPhaseProofCertificate>(
                outcome.Certificate);
            string claim = ClaimCanonical.ForObject(outcome.Value!);

            Assert.Equal(feature, certificate.Feature);
            Assert.Equal(feature, certificate.ProvenFeature);
            Assert.True(MonotoneTrigonometricPhaseCertificateChecker.Check(
                request,
                expression,
                certificate,
                claim,
                new ResourceBudget()), feature.ToString());
            Assert.True(CertificateChecker.Check(request, expression, outcome), feature.ToString());
        }
    }

    [Fact]
    public void ReplayRejectsEveryCertificateBoundFieldMutation()
    {
        InputExpression input = Sin(Add(Power(Variable(), 3), Sqrt(Variable())));
        (AnalysisRequest request, SemanticExpression expression,
            ProofOutcome<RealSet> outcome) = Produce<RealSet>(
                input,
                AnalysisFeatures.Range);
        RealSet range = Assert.IsAssignableFrom<RealSet>(outcome.Value);
        var certificate = Assert.IsType<MonotoneTrigonometricPhaseProofCertificate>(
            outcome.Certificate);
        string claim = ClaimCanonical.For(range);

        Assert.True(certificate.ParameterIsNonnegative);
        Assert.Equal(PhaseOrientation.Increasing, certificate.Orientation);
        Assert.NotEmpty(certificate.PhaseTerms);
        Assert.IsType<EmptySet>(certificate.DerivativeViolationCells.Result);

        PuiseuxTerm firstTerm = certificate.PhaseTerms[0];
        UnivariatePolynomial changedParameterPolynomial =
            certificate.ParameterPolynomial.Add(
                UnivariatePolynomial.One,
                new ResourceBudget());
        AnalysisFeatures multipleFeatures =
            AnalysisFeatures.Range | AnalysisFeatures.Zeros;

        AssertRejected(certificate with { ProvenFeature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { Feature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with
        {
            ProvenFeature = multipleFeatures,
            Feature = multipleFeatures
        });
        AssertRejected(certificate with { Subject = certificate.Subject + ":forged" });
        AssertRejected(certificate with
        {
            SubjectCanonical = certificate.SubjectCanonical + ":forged"
        });
        AssertRejected(certificate with { Claim = certificate.Claim + ":forged" });
        AssertRejected(certificate with
        {
            ClaimCanonical = certificate.ClaimCanonical + ":forged"
        });
        AssertRejected(certificate with { Variable = "y" });
        AssertRejected(certificate with { AngleUnit = AngleUnit.Degrees });
        AssertRejected(certificate with { OuterFunction = "cos" });
        AssertRejected(certificate with
        {
            PhaseCanonical = certificate.PhaseCanonical + ":forged"
        });
        AssertRejected(certificate with
        {
            PhaseTerms = certificate.PhaseTerms.SetItem(
                0,
                firstTerm with
                {
                    Coefficient = firstTerm.Coefficient + BigRational.One
                })
        });
        AssertRejected(certificate with
        {
            SubstitutionDegree = certificate.SubstitutionDegree + 1
        });
        AssertRejected(certificate with
        {
            CoordinateOffset = certificate.CoordinateOffset + BigRational.One
        });
        AssertRejected(certificate with
        {
            ParameterPolynomial = changedParameterPolynomial
        });
        AssertRejected(certificate with
        {
            ParameterIsNonnegative = !certificate.ParameterIsNonnegative
        });
        AssertRejected(certificate with
        {
            BoundaryIncluded = !certificate.BoundaryIncluded
        });
        AssertRejected(certificate with
        {
            DomainFormula = new PolynomialBoolean(false)
        });
        AssertRejected(certificate with
        {
            DomainCells = certificate.DomainCells with
            {
                Result = AllRealSet.Instance
            }
        });
        AssertRejected(certificate with
        {
            ParameterOrientation = PhaseOrientation.Decreasing
        });
        AssertRejected(certificate with
        {
            Orientation = PhaseOrientation.Decreasing
        });
        AssertRejected(certificate with
        {
            DerivativeViolationCells = certificate.DerivativeViolationCells with
            {
                Result = AllRealSet.Instance
            }
        });
        AssertRejected(certificate with
        {
            DefinednessCanonical = certificate.DefinednessCanonical + ":forged"
        });
        AssertRejected(certificate with
        {
            ContinuityCanonical = certificate.ContinuityCanonical + ":forged"
        });
        AssertRejected(certificate with
        {
            DifferentiabilityCanonical = certificate.DifferentiabilityCanonical + ":forged"
        });
        AssertRejected(certificate with { Rule = certificate.Rule + ":forged" });
        return;

        void AssertRejected(MonotoneTrigonometricPhaseProofCertificate changed)
        {
            Assert.False(MonotoneTrigonometricPhaseCertificateChecker.Check(
                request,
                expression,
                changed,
                claim,
                new ResourceBudget()));
            Assert.False(CertificateChecker.Check(
                request,
                expression,
                ProofOutcome<RealSet>.Proved(range, changed)));
        }
    }

    [Fact]
    public void ReplayRejectsWrongRequestExpressionClaimAndOutcome()
    {
        InputExpression input = Sin(Power(Variable(), 3));
        (AnalysisRequest request, SemanticExpression expression,
            ProofOutcome<RealSet> outcome) = Produce<RealSet>(
                input,
                AnalysisFeatures.Range);
        RealSet range = Assert.IsAssignableFrom<RealSet>(outcome.Value);
        var certificate = Assert.IsType<MonotoneTrigonometricPhaseProofCertificate>(
            outcome.Certificate);
        string claim = ClaimCanonical.For(range);

        Assert.False(Check(request with { Features = AnalysisFeatures.Zeros }, expression, claim));
        Assert.False(Check(request with { AngleUnit = AngleUnit.Degrees }, expression, claim));
        Assert.False(Check(request with { Variable = "y" }, expression, claim));
        Assert.False(Check(
            request with { Variable = "y" },
            expression,
            claim,
            certificate with { Variable = "y" }));

        InputExpression otherInput = Sin(Power(Variable(), 5));
        AnalysisRequest otherRequest = Request(
            otherInput,
            AnalysisFeatures.Range,
            AngleUnit.Radians);
        SemanticExpression otherExpression = Build(otherInput);
        Assert.False(Check(otherRequest, otherExpression, claim));

        Assert.False(Check(request, expression, claim + ":forged"));

        RealSet wrongRange = new IntervalSet(
            RealBound.Finite(new RationalReal(new BigRational(-2))),
            true,
            RealBound.Finite(new RationalReal(new BigRational(2))),
            true);
        Assert.False(CertificateChecker.Check(
            request,
            expression,
            ProofOutcome<RealSet>.Proved(wrongRange, certificate)));
        Assert.False(CertificateChecker.Check(
            request,
            expression,
            AnalysisFeatures.Zeros,
            outcome,
            new ResourceBudget()));
        Assert.False(CertificateChecker.Check(
            request,
            expression,
            ProofOutcome<Periodicity>.Proved(
                new Periodicity(PeriodicityKind.NotPeriodic, null),
                certificate)));

        bool Check(
            AnalysisRequest changedRequest,
            SemanticExpression changedExpression,
            string changedClaim,
            MonotoneTrigonometricPhaseProofCertificate? changedCertificate = null) =>
            MonotoneTrigonometricPhaseCertificateChecker.Check(
                changedRequest,
                changedExpression,
                changedCertificate ?? certificate,
                changedClaim,
                new ResourceBudget());
    }

    [Fact]
    public void MonotonicityCanonicalBindsTheSelectedAngleUnit()
    {
        InputExpression input = Sin(Power(Variable(), 3));
        var claims = new HashSet<string>(StringComparer.Ordinal);
        var outcomes = new Dictionary<AngleUnit, (
            AnalysisRequest Request,
            SemanticExpression Expression,
            ProofOutcome<ImmutableArray<MonotoneRegion>> Outcome)>();

        foreach (AngleUnit unit in new[]
                 {
                     AngleUnit.Radians,
                     AngleUnit.Degrees,
                     AngleUnit.Grads
                 })
        {
            var produced = Produce<ImmutableArray<MonotoneRegion>>(
                input,
                AnalysisFeatures.Monotonicity,
                unit);
            outcomes.Add(unit, produced);
            ImmutableArray<MonotoneRegion> regions = Assert.IsType<
                ImmutableArray<MonotoneRegion>>(produced.Outcome.Value);
            Assert.Equal(2, regions.Length);
            Assert.All(regions, region =>
            {
                var sign = Assert.IsType<PolynomialPhaseSignSet>(region.Region);
                Assert.Equal(unit, sign.AngleUnit);
            });
            claims.Add(ClaimCanonical.For(regions));
        }

        Assert.Equal(3, claims.Count);

        var radians = outcomes[AngleUnit.Radians];
        var degrees = outcomes[AngleUnit.Degrees];
        var radiansCertificate = Assert.IsType<MonotoneTrigonometricPhaseProofCertificate>(
            radians.Outcome.Certificate);
        string radiansClaim = ClaimCanonical.For(radians.Outcome.Value!);
        Assert.False(MonotoneTrigonometricPhaseCertificateChecker.Check(
            degrees.Request,
            degrees.Expression,
            radiansCertificate with { AngleUnit = AngleUnit.Degrees },
            radiansClaim,
            new ResourceBudget()));
    }

    [Fact]
    public void ReplayHonorsCancellationAndDeterministicWorkBudget()
    {
        InputExpression input = Sin(Add(Power(Variable(), 3), Sqrt(Variable())));
        (AnalysisRequest request, SemanticExpression expression,
            ProofOutcome<RealSet> outcome) = Produce<RealSet>(
                input,
                AnalysisFeatures.Zeros);
        var certificate = Assert.IsType<MonotoneTrigonometricPhaseProofCertificate>(
            outcome.Certificate);
        string claim = ClaimCanonical.For(outcome.Value!);

        var first = new ResourceBudget();
        var second = new ResourceBudget();
        Assert.True(MonotoneTrigonometricPhaseCertificateChecker.Check(
            request,
            expression,
            certificate,
            claim,
            first));
        Assert.True(MonotoneTrigonometricPhaseCertificateChecker.Check(
            request,
            expression,
            certificate,
            claim,
            second));
        Assert.True(first.WorkUsed > 0);
        Assert.Equal(first.WorkUsed, second.WorkUsed);

        Assert.Throws<AnalysisCancelledException>(() =>
            MonotoneTrigonometricPhaseCertificateChecker.Check(
                request,
                expression,
                certificate,
                claim,
                new ResourceBudget(static () => false)));

        var exhausted = new ResourceBudget();
        exhausted.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            MonotoneTrigonometricPhaseCertificateChecker.Check(
                request,
                expression,
                certificate,
                claim,
                exhausted));
    }

    [Fact]
    public void RenderedPhaseTermsAreBoundIntoTheCheckedClaim()
    {
        InputExpression input = Sin(Add(Power(Variable(), 3), Sqrt(Variable())));
        (AnalysisRequest request, SemanticExpression expression,
            ProofOutcome<RealSet> outcome) = Produce<RealSet>(
                input,
                AnalysisFeatures.Zeros);
        var certificate = Assert.IsType<MonotoneTrigonometricPhaseProofCertificate>(
            outcome.Certificate);
        var zeros = Assert.IsType<PolynomialPhasePreimageSet>(outcome.Value);
        PuiseuxTerm first = zeros.Preimage.PhaseTerms[0];
        var forged = new PolynomialPhasePreimageSet(zeros.Preimage with
        {
            PhaseTerms = zeros.Preimage.PhaseTerms.SetItem(
                0,
                first with { Coefficient = first.Coefficient + BigRational.One })
        });
        string forgedClaim = ClaimCanonical.For(forged);

        Assert.NotEqual(ClaimCanonical.For(zeros), forgedClaim);
        Assert.False(MonotoneTrigonometricPhaseCertificateChecker.Check(
            request,
            expression,
            certificate,
            forgedClaim,
            new ResourceBudget()));
        Assert.False(CertificateChecker.Check(
            request,
            expression,
            ProofOutcome<RealSet>.Proved(forged, certificate)));
    }

    private static (
        AnalysisRequest Request,
        SemanticExpression Expression,
        ProofOutcome<T> Outcome) Produce<T>(
        InputExpression input,
        AnalysisFeatures feature,
        AngleUnit angleUnit = AngleUnit.Radians)
    {
        AnalysisRequest request = Request(input, feature, angleUnit);
        var budget = new ResourceBudget();
        SemanticExpression expression = new SemanticGraphBuilder(budget).Build(input);
        Assert.True(MonotoneTrigonometricPhaseAnalyzer.TryAnalyze(
            request,
            expression,
            feature,
            budget,
            out ProofOutcome<T> outcome),
            $"{expression.Value.Canonical}; {feature}; {angleUnit}");
        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.NotNull(outcome.Value);
        return (request, expression, outcome);
    }

    private static AnalysisRequest Request(
        InputExpression input,
        AnalysisFeatures feature,
        AngleUnit angleUnit) =>
        new(input, feature, angleUnit, "x", static () => true);

    private static SemanticExpression Build(InputExpression input) =>
        new SemanticGraphBuilder(new ResourceBudget()).Build(input);

    private static InputExpression Variable() =>
        InputExpression.Variable("x", Source);

    private static InputExpression Number(int value) =>
        InputExpression.Number(new BigRational(value), Source);

    private static InputExpression Add(
        InputExpression left,
        InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Add, left, right, Source);

    private static InputExpression Power(InputExpression basis, int exponent) =>
        InputExpression.Binary(
            InputExpressionKind.Power,
            basis,
            Number(exponent),
            Source);

    private static InputExpression Sin(InputExpression argument) =>
        InputExpression.Function("sin", [argument], Source);

    private static InputExpression Sqrt(InputExpression argument) =>
        InputExpression.Function("sqrt", [argument], Source);
}
