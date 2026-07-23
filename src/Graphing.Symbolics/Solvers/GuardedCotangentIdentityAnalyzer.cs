namespace Graphing.Symbolics;

internal static class GuardedCotangentIdentityAnalyzer
{
    internal const string Rule = "pythagorean-identity-over-centered-tangent";
    public static bool TryAnalyze<T>(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures feature, ResourceBudget budget, out ProofOutcome<T> outcome)
    {
        if (!GuardedCotangentIdentityContext.TryCreate(expression, request.Variable, request.AngleUnit, budget, out GuardedCotangentIdentityContext context) || !GuardedCotangentIdentityTheorems.TryCompute(context, feature, out object value) || value is not T typed)
        {
            outcome = null!;
            return false;
        }

        var certificate = new GuardedCotangentIdentityProofCertificate(feature, expression.Value.Canonical, ClaimCanonical.ForObject(value), context.Numerator.Value.Canonical, context.Denominator.Value.Canonical, context.PatternCanonical, expression.DefinedWhen.Canonical, context.Domain.Canonical, Rule);
        outcome = ProofOutcome<T>.Proved(typed, certificate);
        return true;
    }
}
