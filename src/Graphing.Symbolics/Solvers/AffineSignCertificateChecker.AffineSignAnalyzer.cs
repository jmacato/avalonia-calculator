using System.Collections.Immutable;

namespace Graphing.Symbolics;
/// <summary>
/// Production replay for affine-sign certificates. It does not call the
/// analyzer or trust its selected branch: it re-extracts the normalized
/// coefficients and instantiates the named theorem from those premises.
/// </summary>
internal static class AffineSignCertificateChecker
{
    private const string Rule = "exact-affine-sign-piecewise-constant";
    public static bool Check(AnalysisRequest request, SemanticExpression expression, AffineSignProofCertificate certificate, string claim, ResourceBudget budget)
    {
        budget.Charge();
        if (!HasValidEnvelope(request, expression, certificate, claim) || !TryCreateReplayContext(expression, request.Variable, budget, out AffineSignCertificateCheckerAffineSignReplayContext? context) || !string.Equals(certificate.ArgumentCanonical, context.ArgumentCanonical, StringComparison.Ordinal) || !string.Equals(certificate.NormalizedArgumentCanonical, context.NormalizedArgumentCanonical, StringComparison.Ordinal) || !string.Equals(certificate.SlopeCanonical, context.Slope.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.InterceptCanonical, context.Intercept.Canonical, StringComparison.Ordinal) || !TryReconstructClaim(context, certificate.Feature, budget, out object expected))
        {
            return false;
        }

        return string.Equals(ClaimCanonical.ForObject(expected), claim, StringComparison.Ordinal);
    }

    private static bool HasValidEnvelope(AnalysisRequest request, SemanticExpression expression, AffineSignProofCertificate certificate, string claim) => certificate.Feature == certificate.ProvenFeature && AffineDiscontinuousCertificateReplay.IsSingleFeature(certificate.Feature) && request.Features.HasFlag(certificate.Feature) && string.Equals(certificate.Subject, expression.Value.Canonical, StringComparison.Ordinal) && string.Equals(certificate.SubjectCanonical, expression.Value.Canonical, StringComparison.Ordinal) && string.Equals(certificate.Claim, claim, StringComparison.Ordinal) && string.Equals(certificate.ClaimCanonical, claim, StringComparison.Ordinal) && string.Equals(certificate.Rule, Rule, StringComparison.Ordinal) && string.Equals(certificate.DefinednessCanonical, expression.DefinedWhen.Canonical, StringComparison.Ordinal);
    private static bool TryCreateReplayContext(SemanticExpression expression, string variable, ResourceBudget budget, out AffineSignCertificateCheckerAffineSignReplayContext context)
    {
        if (!AffineDiscontinuousCertificateReplay.TryGetArgument(expression, "sign", budget, out SemanticExpression argument) || !ExactRationalExtractor.TryExtract(argument.Value, variable, budget, out ExactRationalExtraction extraction) || !extraction.DomainExclusions.IsEmpty || !extraction.Denominator.IsConstant || extraction.Denominator[0].IsZero || extraction.Numerator.Degree > 1)
        {
            context = null!;
            return false;
        }

        ExactScalar reciprocal = extraction.Denominator[0].Reciprocal(budget);
        ExactScalar slope = extraction.Numerator[1].Multiply(reciprocal, budget);
        ExactScalar intercept = extraction.Numerator[0].Multiply(reciprocal, budget);
        context = new AffineSignCertificateCheckerAffineSignReplayContext(argument.Value.Canonical, slope, intercept);
        return true;
    }

    private static bool TryReconstructClaim(AffineSignCertificateCheckerAffineSignReplayContext context, AnalysisFeatures feature, ResourceBudget budget, out object value)
    {
        budget.Charge();
        bool constant = context.Slope.IsZero;
        ExactReal constantValue = SignValue(context.Intercept.Sign);
        value = feature switch
        {
            AnalysisFeatures.Domain => AllRealSet.Instance,
            AnalysisFeatures.Range => constant ? RealSets.Points([constantValue]) : RealSets.Points([SignValue(-1), SignValue(0), SignValue(1)]),
            AnalysisFeatures.Parity => Parity(context),
            AnalysisFeatures.Zeros => Zeros(context, budget),
            AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.Some(constantValue),
            AnalysisFeatures.Minima or AnalysisFeatures.Maxima or AnalysisFeatures.InflectionPoints => ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.VerticalAsymptotes or AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.HorizontalAsymptotes => HorizontalAsymptotes(context),
            AnalysisFeatures.Monotonicity => ImmutableArray.Create(new MonotoneRegion(AllRealSet.Instance, constant ? Monotonicity.Constant : context.Slope.Sign > 0 ? Monotonicity.Increasing : Monotonicity.Decreasing)),
            AnalysisFeatures.Period => new Periodicity(constant ? PeriodicityKind.PeriodicWithoutFundamentalPeriod : PeriodicityKind.NotPeriodic, null),
            _ => null!
        };
        return value is not null;
    }

    private static RealSet Zeros(AffineSignCertificateCheckerAffineSignReplayContext context, ResourceBudget budget)
    {
        if (context.Slope.IsZero)
        {
            return context.Intercept.IsZero ? AllRealSet.Instance : EmptySet.Instance;
        }

        ExactScalar root = context.Intercept.Negate().Multiply(context.Slope.Reciprocal(budget), budget);
        return RealSets.Points([root.Value]);
    }

    private static FunctionParity Parity(AffineSignCertificateCheckerAffineSignReplayContext context)
    {
        if (context.Slope.IsZero)
        {
            return context.Intercept.IsZero ? FunctionParity.Both : FunctionParity.Even;
        }

        return context.Intercept.IsZero ? FunctionParity.Odd : FunctionParity.Neither;
    }

    private static ImmutableArray<Asymptote> HorizontalAsymptotes(AffineSignCertificateCheckerAffineSignReplayContext context)
    {
        if (context.Slope.IsZero)
        {
            return [Horizontal(SignValue(context.Intercept.Sign))];
        }

        int rightLimit = context.Slope.Sign;
        return [Horizontal(SignValue(rightLimit)), Horizontal(SignValue(-rightLimit))];
    }

    private static Asymptote Horizontal(ExactReal value) => new(AsymptoteOrientation.Horizontal, new SingletonReal(value), null, value);
    private static Graphing.Symbolics.RationalReal SignValue(int sign) => new RationalReal(new BigRational(sign));
}
