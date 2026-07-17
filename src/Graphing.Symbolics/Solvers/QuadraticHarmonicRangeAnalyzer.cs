namespace Graphing.Symbolics;

internal static class QuadraticHarmonicRangeAnalyzer
{
    internal const string Rule = "quadratic-single-harmonic-closed-range";
    public static bool TryAnalyze<T>(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures feature, ResourceBudget budget, out ProofOutcome<T> outcome)
    {
        if (feature != AnalysisFeatures.Range || !QuadraticHarmonicRangeContext.TryCreate(expression, request.Variable, request.AngleUnit, budget, out QuadraticHarmonicRangeContext context) || !QuadraticHarmonicRangeTheorems.TryCompute(context, budget, out RealSet range) || range is not T typed)
        {
            outcome = null!;
            return false;
        }

        var certificate = new QuadraticHarmonicRangeProofCertificate(feature, expression.Value.Canonical, ClaimCanonical.For(range), context.Basis, context.Frequency, context.QuadraticCoefficient, context.LinearCoefficient, context.Constant, context.FourierCanonical, expression.DefinedWhen.Canonical, Rule);
        outcome = ProofOutcome<T>.Proved(typed, certificate);
        return true;
    }
}
