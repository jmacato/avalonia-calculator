namespace Graphing.Symbolics;

internal static class ZeroBaseAffinePowerAnalyzer
{
    internal const string Rule = "zero-base-positive-affine-exponent";
    public static bool TryAnalyze<T>(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures feature, ResourceBudget budget, out ProofOutcome<T> outcome)
    {
        if (!ZeroBaseAffinePowerContext.TryCreate(expression, request.Variable, request.AngleUnit, budget, out ZeroBaseAffinePowerContext context) || !ZeroBaseAffinePowerTheorems.TryCompute(context, feature, budget, out object value) || value is not T typed)
        {
            outcome = null!;
            return false;
        }

        var certificate = new ZeroBaseAffinePowerProofCertificate(feature, expression.Value.Canonical, ClaimCanonical.ForObject(value), context.Exponent.Value.Canonical, context.Slope.Canonical, context.Intercept.Canonical, expression.DefinedWhen.Canonical, context.Domain.Canonical, Rule);
        outcome = ProofOutcome<T>.Proved(typed, certificate);
        return true;
    }
}
