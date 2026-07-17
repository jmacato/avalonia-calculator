using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class AffineFloorAnalyzer
{
    internal const string Rule = "exact-rational-affine-floor-cells";
    public static bool TryAnalyze<T>(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures feature, ResourceBudget budget, out ProofOutcome<T> outcome)
    {
        if (!AffineFloorContext.TryCreate(expression, request.Variable, request.AngleUnit, budget, out AffineFloorContext context) || !AffineFloorTheorems.TryCompute(context, feature, budget, out object value) || value is not T typed)
        {
            outcome = null!;
            return false;
        }

        var certificate = new AffineFloorProofCertificate(feature, expression.Value.Canonical, ClaimCanonical.ForObject(value), context.Argument.Value.Canonical, context.Argument.DefinedWhen.Canonical, context.NormalizedArgumentCanonical, context.Slope, context.Intercept, expression.DefinedWhen.Canonical, Rule);
        outcome = ProofOutcome<T>.Proved(typed, certificate);
        return true;
    }
}
