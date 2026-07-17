using System.Collections.Immutable;

namespace Graphing.Symbolics;
/// <summary>
/// Independent production replay. It deliberately does not call the analyzer
/// context factory or theorem dispatcher. Instead it rematches both source
/// operands, re-extracts and orders the exact affine coefficients, reconstructs
/// the locked strict-positive domain, and independently derives the claim.
/// </summary>
internal static class ZeroBaseAffinePowerCertificateChecker
{
    public static bool Check(AnalysisRequest request, SemanticExpression expression, ZeroBaseAffinePowerProofCertificate certificate, string claim, ResourceBudget budget)
    {
        if (certificate.Feature != certificate.ProvenFeature || !string.Equals(certificate.Subject, certificate.SubjectCanonical, StringComparison.Ordinal) || !string.Equals(certificate.Claim, claim, StringComparison.Ordinal) || !string.Equals(certificate.Rule, ZeroBaseAffinePowerAnalyzer.Rule, StringComparison.Ordinal) || !string.Equals(certificate.DefinednessCanonical, expression.DefinedWhen.Canonical, StringComparison.Ordinal) || !TryReplayPremises(request, expression, budget, out SemanticExpression exponent, out ExactScalar slope, out ExactScalar intercept, out RealSet domain) || !string.Equals(certificate.ExponentCanonical, exponent.Value.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.SlopeCanonical, slope.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.InterceptCanonical, intercept.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.DomainCanonical, domain.Canonical, StringComparison.Ordinal) || !TryReplayClaim(certificate.Feature, intercept, domain, budget, out object expected))
        {
            return false;
        }

        return string.Equals(ClaimCanonical.ForObject(expected), claim, StringComparison.Ordinal);
    }

    private static bool TryReplayPremises(AnalysisRequest request, SemanticExpression expression, ResourceBudget budget, out SemanticExpression exponent, out ExactScalar slope, out ExactScalar intercept, out RealSet domain)
    {
        budget.Charge();
        if (expression.Value is not { Kind: ValueKind.Power, Operands: [var basisValue, var exponentValue] } || expression.SourceOperands is not [var basis, var sourceExponent] || basis.Value.Id != basisValue.Id || sourceExponent.Value.Id != exponentValue.Id || !ExactScalar.TryCreate(basis.Value, budget, out ExactScalar scalarBasis) || !scalarBasis.IsZero || !TrigonometricAndLatticeAnalyzer.DomainMatches(basis, request.Variable, request.AngleUnit, AllRealSet.Instance, budget) || !TrigonometricAndLatticeAnalyzer.DomainMatches(sourceExponent, request.Variable, request.AngleUnit, AllRealSet.Instance, budget) || !MatchesReplayDefinedness(expression, basis, sourceExponent, budget) || !ExactRationalExtractor.TryExtract(sourceExponent.Value, request.Variable, budget, out ExactRationalExtraction extraction) || !extraction.DomainExclusions.IsEmpty || !extraction.Denominator.IsConstant || extraction.Denominator[0].IsZero || extraction.Numerator.Degree > 1)
        {
            exponent = null!;
            slope = default;
            intercept = default;
            domain = null!;
            return false;
        }

        ExactScalar denominatorReciprocal = extraction.Denominator[0].Reciprocal(budget);
        slope = extraction.Numerator[1].Multiply(denominatorReciprocal, budget);
        intercept = extraction.Numerator[0].Multiply(denominatorReciprocal, budget);
        if (slope.IsZero)
        {
            exponent = null!;
            domain = null!;
            return false;
        }

        ExactScalar thresholdScalar = slope.Sign > 0 ? intercept.Negate().Multiply(slope.Reciprocal(budget), budget) : intercept.Multiply(slope.Negate().Reciprocal(budget), budget);
        ExactReal threshold = thresholdScalar.Value;
        domain = slope.Sign > 0 ? new IntervalSet(RealBound.Finite(threshold), false, RealBound.PositiveInfinity, false) : new IntervalSet(RealBound.NegativeInfinity, false, RealBound.Finite(threshold), false);
        exponent = sourceExponent;
        return true;
    }

    private static bool MatchesReplayDefinedness(SemanticExpression expression, SemanticExpression basis, SemanticExpression exponent, ResourceBudget budget)
    {
        budget.Charge();
        string expected = Formula.Compare(exponent.Value, Comparison.Greater, basis.Value).Canonical;
        return string.Equals(expression.DefinedWhen.Canonical, expected, StringComparison.Ordinal);
    }

    private static bool TryReplayClaim(AnalysisFeatures feature, ExactScalar intercept, RealSet domain, ResourceBudget budget, out object value)
    {
        budget.Charge();
        var zero = new RationalReal(BigRational.Zero);
        value = feature switch
        {
            AnalysisFeatures.Domain => domain,
            AnalysisFeatures.Range => RealSets.Points([zero]),
            AnalysisFeatures.Parity => FunctionParity.Neither,
            AnalysisFeatures.Zeros => domain,
            AnalysisFeatures.YIntercept => intercept.Sign > 0 ? OptionalValue<ExactReal>.Some(zero) : OptionalValue<ExactReal>.None,
            AnalysisFeatures.Minima or AnalysisFeatures.Maxima or AnalysisFeatures.InflectionPoints => ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.VerticalAsymptotes or AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.HorizontalAsymptotes => ImmutableArray.Create(new Asymptote(AsymptoteOrientation.Horizontal, new SingletonReal(zero), null, zero)),
            AnalysisFeatures.Monotonicity => ImmutableArray.Create(new MonotoneRegion(domain, Monotonicity.Constant)),
            AnalysisFeatures.Period => new Periodicity(PeriodicityKind.NotPeriodic, null),
            _ => null!
        };
        return value is not null;
    }
}
