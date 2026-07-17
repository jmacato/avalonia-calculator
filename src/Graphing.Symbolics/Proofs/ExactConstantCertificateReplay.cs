using System.Collections.Immutable;

namespace Graphing.Symbolics;

/// <summary>
/// Reconstructs exact-constant claims directly from the semantic value and an
/// everywhere-defined premise.  It intentionally does not invoke the
/// constant analyzer or the generic theorem dispatcher.
/// </summary>
internal static class ExactConstantCertificateReplay
{
    public static bool Check(
        AnalysisRequest request,
        SemanticExpression expression,
        TheoremProofCertificate certificate,
        string claim,
        ResourceBudget budget)
    {
        budget.Charge();
        if (certificate.Parameters.Length != 4 ||
            !string.Equals(certificate.Parameters[0], "exact-constant", StringComparison.Ordinal) ||
            !ExactScalar.TryCreate(expression.Value, budget, out ExactScalar scalar) ||
            !ExactFormulaVerifier.IsAlwaysTrue(expression.DefinedWhen, budget) ||
            !string.Equals(certificate.Parameters[1], scalar.Canonical, StringComparison.Ordinal) ||
            !string.Equals(
                certificate.Parameters[2],
                expression.DefinedWhen.Canonical,
                StringComparison.Ordinal) ||
            !string.Equals(certificate.Parameters[3], request.AngleUnit.ToString(), StringComparison.Ordinal) ||
            !TryReconstruct(certificate.Feature, scalar, out object expected))
        {
            return false;
        }

        return string.Equals(
            ClaimCanonical.ForObject(expected),
            claim,
            StringComparison.Ordinal);
    }

    private static bool TryReconstruct(
        AnalysisFeatures feature,
        ExactScalar scalar,
        out object value)
    {
        switch (feature)
        {
            case AnalysisFeatures.Domain:
                value = AllRealSet.Instance;
                return true;
            case AnalysisFeatures.Range:
                value = RealSets.Points([scalar.Value]);
                return true;
            case AnalysisFeatures.Parity:
                value = scalar.IsZero ? FunctionParity.Both : FunctionParity.Even;
                return true;
            case AnalysisFeatures.Zeros:
                value = scalar.IsZero ? AllRealSet.Instance : EmptySet.Instance;
                return true;
            case AnalysisFeatures.YIntercept:
                value = OptionalValue<ExactReal>.Some(scalar.Value);
                return true;
            case AnalysisFeatures.Minima:
            case AnalysisFeatures.Maxima:
            case AnalysisFeatures.InflectionPoints:
                value = ImmutableArray<FeaturePoint>.Empty;
                return true;
            case AnalysisFeatures.VerticalAsymptotes:
            case AnalysisFeatures.ObliqueAsymptotes:
                value = ImmutableArray<Asymptote>.Empty;
                return true;
            case AnalysisFeatures.HorizontalAsymptotes:
                value = ImmutableArray.Create(
                    new Asymptote(
                        AsymptoteOrientation.Horizontal,
                        new SingletonReal(scalar.Value),
                        null,
                        scalar.Value));
                return true;
            case AnalysisFeatures.Monotonicity:
                value = ImmutableArray.Create(
                    new MonotoneRegion(AllRealSet.Instance, Monotonicity.Constant));
                return true;
            case AnalysisFeatures.Period:
                value = new Periodicity(
                    PeriodicityKind.PeriodicWithoutFundamentalPeriod,
                    null);
                return true;
            default:
                value = null!;
                return false;
        }
    }
}
