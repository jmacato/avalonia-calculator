using System.Collections.Immutable;

namespace Graphing.Symbolics;
/// <summary>
/// Production replay for affine-floor certificates. The checker re-extracts
/// the affine argument, proves that neither the argument nor the outer floor
/// has a retained domain restriction, and instantiates the theorem directly
/// from those freshly derived premises.
/// </summary>
internal static class AffineFloorCertificateChecker
{
    private const string IntegerParameter = "n";
    private const string Rule = "exact-rational-affine-floor-cells";
    public static bool Check(AnalysisRequest request, SemanticExpression expression, AffineFloorProofCertificate certificate, string claim, ResourceBudget budget)
    {
        budget.Charge();
        if (!HasValidEnvelope(request, expression, certificate, claim) || !TryCreateReplayContext(expression, request.Variable, budget, out AffineFloorCertificateCheckerAffineFloorReplayContext? context) || !string.Equals(certificate.ArgumentCanonical, context.ArgumentCanonical, StringComparison.Ordinal) || !string.Equals(certificate.ArgumentDefinednessCanonical, context.ArgumentDefinednessCanonical, StringComparison.Ordinal) || !string.Equals(certificate.NormalizedArgumentCanonical, context.NormalizedArgumentCanonical, StringComparison.Ordinal) || certificate.Slope != context.Slope || certificate.Intercept != context.Intercept || !TryReconstructClaim(context, certificate.Feature, budget, out object expected))
        {
            return false;
        }

        return string.Equals(ClaimCanonical.ForObject(expected), claim, StringComparison.Ordinal);
    }

    private static bool HasValidEnvelope(AnalysisRequest request, SemanticExpression expression, AffineFloorProofCertificate certificate, string claim)
    {
        return certificate.Feature == certificate.ProvenFeature &&
               AffineDiscontinuousCertificateReplay.IsSingleFeature(certificate.Feature) &&
               request.Features.HasFlag(certificate.Feature) &&
               string.Equals(certificate.Subject, expression.Value.Canonical, StringComparison.Ordinal) &&
               string.Equals(certificate.SubjectCanonical, expression.Value.Canonical, StringComparison.Ordinal) &&
               string.Equals(certificate.Claim, claim, StringComparison.Ordinal) &&
               string.Equals(certificate.ClaimCanonical, claim, StringComparison.Ordinal) &&
               string.Equals(certificate.Rule, Rule, StringComparison.Ordinal) && string.Equals(
                   certificate.DefinednessCanonical, expression.DefinedWhen.Canonical, StringComparison.Ordinal);
    }

    private static bool TryCreateReplayContext(SemanticExpression expression, string variable, ResourceBudget budget, out AffineFloorCertificateCheckerAffineFloorReplayContext context)
    {
        if (!AffineDiscontinuousCertificateReplay.TryGetArgument(expression, "floor", budget, out SemanticExpression argument) || !RationalFunctionExtractor.TryExtract(argument.Value, variable, budget, out RationalExtraction extraction) || !extraction.DomainExclusions.IsEmpty || !extraction.Function.Denominator.IsConstant || extraction.Function.Denominator[0].IsZero || extraction.Function.Numerator.Degree > 1)
        {
            context = null!;
            return false;
        }

        BigRational reciprocal = extraction.Function.Denominator[0].Reciprocal();
        BigRational slope = extraction.Function.Numerator[1] * reciprocal;
        BigRational intercept = extraction.Function.Numerator[0] * reciprocal;
        budget.CheckCoefficient(slope);
        budget.CheckCoefficient(intercept);
        context = new AffineFloorCertificateCheckerAffineFloorReplayContext(argument.Value.Canonical, argument.DefinedWhen.Canonical, slope, intercept);
        return true;
    }

    private static bool TryReconstructClaim(AffineFloorCertificateCheckerAffineFloorReplayContext context, AnalysisFeatures feature, ResourceBudget budget, out object value)
    {
        budget.Charge();
        bool constant = context.Slope.IsZero;
        ExactReal constantValue = IntegerValue(context.Intercept.Floor());
        value = feature switch
        {
            AnalysisFeatures.Domain => AllRealSet.Instance,
            AnalysisFeatures.Range => constant ? RealSets.Points([constantValue]) : IntegerRange(),
            AnalysisFeatures.Parity => constant ? context.Intercept.Floor().IsZero ? FunctionParity.Both : FunctionParity.Even : FunctionParity.Neither,
            AnalysisFeatures.Zeros => Zeros(context, budget),
            AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.Some(constantValue),
            AnalysisFeatures.Minima or AnalysisFeatures.Maxima or AnalysisFeatures.InflectionPoints => ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.VerticalAsymptotes or AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.HorizontalAsymptotes => constant ? ImmutableArray.Create(Horizontal(constantValue)) : ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Monotonicity => ImmutableArray.Create(new MonotoneRegion(constant ? AllRealSet.Instance : ConstantCells(context, budget), Monotonicity.Constant)),
            AnalysisFeatures.Period => new Periodicity(constant ? PeriodicityKind.PeriodicWithoutFundamentalPeriod : PeriodicityKind.NotPeriodic, null),
            _ => null!
        };
        return value is not null;
    }

    private static IntegerLatticeSet IntegerRange()
    {
        return new IntegerLatticeSet(IntegerParameter, [IntegerParameter], [$"{IntegerParameter} ∈ ℤ"]);
    }

    private static RealSet Zeros(AffineFloorCertificateCheckerAffineFloorReplayContext context, ResourceBudget budget)
    {
        if (context.Slope.IsZero)
        {
            return context.Intercept.Floor().IsZero ? AllRealSet.Instance : EmptySet.Instance;
        }

        (BigRational lower, bool includesLower, BigRational upper, bool includesUpper) = ZeroCell(context, budget);
        return Interval(lower, includesLower, upper, includesUpper);
    }

    private static PeriodicIntervalSet ConstantCells(AffineFloorCertificateCheckerAffineFloorReplayContext context, ResourceBudget budget)
    {
        (BigRational lower, bool includesLower, BigRational upper, bool includesUpper) = ZeroCell(context, budget);
        BigRational period = context.Slope.Sign > 0 ? context.Slope.Reciprocal() : -context.Slope.Reciprocal();
        budget.CheckCoefficient(period);
        return new PeriodicIntervalSet(Rational(period), IntegerParameter, IntegerConstraint.All(IntegerParameter), [new PeriodicInterval(Rational(lower), includesLower, Rational(upper), includesUpper)]);
    }

    private static (BigRational Lower, bool IncludesLower, BigRational Upper, bool IncludesUpper) ZeroCell(AffineFloorCertificateCheckerAffineFloorReplayContext context, ResourceBudget budget)
    {
        BigRational zeroBoundary = -context.Intercept / context.Slope;
        BigRational oneBoundary = (BigRational.One - context.Intercept) / context.Slope;
        budget.CheckCoefficient(zeroBoundary);
        budget.CheckCoefficient(oneBoundary);
        return context.Slope.Sign > 0 ? (zeroBoundary, true, oneBoundary, false) : (oneBoundary, false, zeroBoundary, true);
    }

    private static IntervalSet Interval(BigRational lower, bool includesLower, BigRational upper, bool includesUpper)
    {
        return new IntervalSet(RealBound.Finite(Rational(lower)), includesLower, RealBound.Finite(Rational(upper)), includesUpper);
    }

    private static Asymptote Horizontal(ExactReal value)
    {
        return new Asymptote(AsymptoteOrientation.Horizontal, new SingletonReal(value), null, value);
    }

    private static RationalReal IntegerValue(ExactInteger value)
    {
        return Rational(new BigRational(value));
    }

    private static RationalReal Rational(BigRational value)
    {
        return new RationalReal(value);
    }
}
