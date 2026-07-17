using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class BoundedRadicalTangentProductAnalyzer
{
    internal const string Rule = "bounded-radical-tangent-product-before-first-pole";
    public static bool TryAnalyze<T>(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures feature, ResourceBudget budget, out ProofOutcome<T> outcome)
    {
        if (!BoundedRadicalTangentProductContext.TryCreate(expression, request.Variable, request.AngleUnit, budget, out BoundedRadicalTangentProductContext context) || !BoundedRadicalTangentProductTheorems.TryCompute(context, feature, out object value) || value is not T typed)
        {
            outcome = null!;
            return false;
        }

        var certificate = new BoundedRadicalTangentProductProofCertificate(feature, expression.Value.Canonical, ClaimCanonical.ForObject(value), context.UpperEndpoint, context.Variable.Value.Canonical, context.FactorCanonicals, context.SourceShapeCanonical, expression.DefinedWhen.Canonical, expression.ContinuousWhen.Canonical, expression.DifferentiableWhen.Canonical, context.Domain.Canonical, Rule);
        outcome = ProofOutcome<T>.Proved(typed, certificate);
        return true;
    }
}
