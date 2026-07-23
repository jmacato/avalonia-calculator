namespace Graphing.Symbolics;

internal static class AffineReciprocalTrigZeroAnalyzer
{
    internal const string Rule = "guarded-affine-reciprocal-trigonometric-zero-inverse";
    public static bool TryAnalyze<T>(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures feature, ResourceBudget budget, out ProofOutcome<T> outcome)
    {
        if (feature != AnalysisFeatures.Zeros || !AffineReciprocalTrigZeroContext.TryCreate(request, expression, budget, out AffineReciprocalTrigZeroContext context) || !AffineReciprocalTrigZeroTheorems.TryCompute(context, request.AngleUnit, budget, out RealSet zeros) || zeros is not T typed)
        {
            outcome = null!;
            return false;
        }

        var certificate = new AffineReciprocalTrigZeroProofCertificate(feature, expression.Value.Canonical, ClaimCanonical.For(zeros), context.Pattern.Function, request.AngleUnit, context.Pattern.Canonical, context.DenominatorTarget.Canonical, expression.DefinedWhen.Canonical, context.Domain.Canonical, Rule);
        outcome = ProofOutcome<T>.Proved(typed, certificate);
        return true;
    }
}
