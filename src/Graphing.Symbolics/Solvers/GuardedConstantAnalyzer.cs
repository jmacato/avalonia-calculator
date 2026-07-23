namespace Graphing.Symbolics;

internal static class GuardedConstantAnalyzer
{
    internal const string Rule = "guarded-constant-periodic-punctures";
    public static bool TryAnalyze<T>(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures feature, ResourceBudget budget, out ProofOutcome<T> outcome)
    {
        if (!GuardedConstantContext.TryCreate(expression, request.Variable, request.AngleUnit, budget, out GuardedConstantContext context) || !GuardedConstantProofKernel.TryCompute(context, feature, out object value) || value is not T typed)
        {
            outcome = null!;
            return false;
        }

        var certificate = new GuardedConstantProofCertificate(feature, expression.Value.Canonical, ClaimCanonical.ForObject(value), context.Scalar.Canonical, expression.DefinedWhen.Canonical, context.Domain.Canonical, Rule);
        outcome = ProofOutcome<T>.Proved(typed, certificate);
        return true;
    }
}
