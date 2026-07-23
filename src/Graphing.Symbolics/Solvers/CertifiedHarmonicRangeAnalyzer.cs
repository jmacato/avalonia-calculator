namespace Graphing.Symbolics;

internal static class CertifiedHarmonicRangeAnalyzer
{
    public static bool TryAnalyze<T>(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures feature, ResourceBudget budget, out ProofOutcome<T> outcome)
    {
        if (SingleHarmonicRangeAnalyzer.TryAnalyze(request, expression, feature, budget, out outcome))
        {
            return true;
        }

        return QuadraticHarmonicRangeAnalyzer.TryAnalyze(request, expression, feature, budget, out outcome);
    }
}
