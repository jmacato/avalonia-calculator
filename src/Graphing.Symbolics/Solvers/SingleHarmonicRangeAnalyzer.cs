namespace Graphing.Symbolics;

internal static class SingleHarmonicRangeAnalyzer
{
    internal const string Rule = "single-fourier-harmonic-amplitude-range";
    public static bool TryAnalyze<T>(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures feature, ResourceBudget budget, out ProofOutcome<T> outcome)
    {
        if (feature != AnalysisFeatures.Range || !SingleHarmonicRangeContext.TryCreate(expression, request.Variable, request.AngleUnit, budget, out SingleHarmonicRangeContext context) || !SingleHarmonicRangeTheorems.TryCompute(context, budget, out RealSet range) || range is not T typed)
        {
            outcome = null!;
            return false;
        }

        var certificate = new SingleHarmonicRangeProofCertificate(feature, expression.Value.Canonical, ClaimCanonical.For(range), context.Frequency, context.Constant, context.CosineCoefficient, context.SineCoefficient, context.RadiusSquared, context.FourierCanonical, expression.DefinedWhen.Canonical, Rule);
        outcome = ProofOutcome<T>.Proved(typed, certificate);
        return true;
    }
}
