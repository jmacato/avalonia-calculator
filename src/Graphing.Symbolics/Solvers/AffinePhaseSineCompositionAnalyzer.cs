namespace Graphing.Symbolics;

internal static class AffinePhaseSineCompositionAnalyzer
{
    internal const string Rule = "exact-affine-phase-sine-composition-v1";
    public static bool TryAnalyze<T>(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures feature, ResourceBudget budget, out ProofOutcome<T> outcome)
    {
        budget.Charge();
        if (!AffinePhaseSineCompositionExtractor.TryExtract(request, expression, budget, out AffinePhaseSineCompositionPattern? pattern) || !AffinePhaseSineCompositionProofKernel.TryCompute(pattern, request.AngleUnit, feature, budget, out object? value) || value is not T typed)
        {
            outcome = null!;
            return false;
        }

        string claim = ClaimCanonical.ForObject(value);
        var certificate = new AffinePhaseSineCompositionProofCertificate(feature, expression.Value.Canonical, claim, pattern.OuterKind, request.AngleUnit, pattern.Frequency, pattern.PhasePiCoefficient, pattern.PhaseConstant, pattern.InnerSign, pattern.Canonical, expression.DefinedWhen.Canonical, Rule);
        outcome = ProofOutcome<T>.Proved(typed, certificate);
        return true;
    }
}
