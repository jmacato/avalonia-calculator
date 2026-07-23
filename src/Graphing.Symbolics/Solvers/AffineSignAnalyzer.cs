namespace Graphing.Symbolics;

internal static class AffineSignAnalyzer
{
    internal const string Rule = "exact-affine-sign-piecewise-constant";
    public static bool TryAnalyze<T>(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures feature, ResourceBudget budget, out ProofOutcome<T> outcome)
    {
        if (!AffineSignContext.TryCreate(expression, request.Variable, request.AngleUnit, budget, out AffineSignContext context) || !AffineSignTheorems.TryCompute(context, feature, budget, out object value) || value is not T typed)
        {
            outcome = null!;
            return false;
        }

        var certificate = new AffineSignProofCertificate(feature, expression.Value.Canonical, ClaimCanonical.ForObject(value), context.Argument.Value.Canonical, context.NormalizedArgumentCanonical, context.Slope.Canonical, context.Intercept.Canonical, expression.DefinedWhen.Canonical, Rule);
        outcome = ProofOutcome<T>.Proved(typed, certificate);
        return true;
    }
}
