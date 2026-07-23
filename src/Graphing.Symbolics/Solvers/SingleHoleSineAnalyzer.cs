namespace Graphing.Symbolics;

internal static class SingleHoleSineAnalyzer
{
    internal const string Rule = "centered-affine-sine-times-origin-self-division";
    public static bool TryAnalyze<T>(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures feature, ResourceBudget budget, out ProofOutcome<T> outcome)
    {
        if (!SingleHoleSineContext.TryCreate(expression, request.Variable, request.AngleUnit, budget, out SingleHoleSineContext context) || !SingleHoleSineTheorems.TryCompute(context, request.AngleUnit, feature, out object value) || value is not T typed)
        {
            outcome = null!;
            return false;
        }

        var certificate = new SingleHoleSineProofCertificate(feature, expression.Value.Canonical, ClaimCanonical.ForObject(value), context.Sine.Value.Canonical, context.Guard.DefinedWhen.Canonical, context.PatternCanonical, expression.DefinedWhen.Canonical, context.Domain.Canonical, Rule);
        outcome = ProofOutcome<T>.Proved(typed, certificate);
        return true;
    }
}
