using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class ZeroBaseTangentPowerAnalyzer
{
    internal const string Rule = "zero-base-positive-centered-tangent-exponent";
    public static bool TryAnalyze<T>(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures feature, ResourceBudget budget, out ProofOutcome<T> outcome)
    {
        if (!ZeroBaseTangentPowerContext.TryCreate(expression, request.Variable, request.AngleUnit, budget, out ZeroBaseTangentPowerContext context) || !ZeroBaseTangentPowerTheorems.TryCompute(context, feature, out object value) || value is not T typed)
        {
            outcome = null!;
            return false;
        }

        var certificate = new ZeroBaseTangentPowerProofCertificate(feature, expression.Value.Canonical, ClaimCanonical.ForObject(value), context.Exponent.Value.Canonical, context.Pattern.Canonical, expression.DefinedWhen.Canonical, context.Domain.Canonical, Rule);
        outcome = ProofOutcome<T>.Proved(typed, certificate);
        return true;
    }
}
