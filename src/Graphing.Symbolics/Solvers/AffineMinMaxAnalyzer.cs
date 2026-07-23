namespace Graphing.Symbolics;

internal static class AffineMinMaxAnalyzer
{
    internal const string Rule = "exact-total-rational-affine-minmax-envelope";
    public static bool TryAnalyze<T>(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures feature, ResourceBudget budget, out ProofOutcome<T> outcome)
    {
        if (!AffineMinMaxContext.TryCreate(expression, request.Variable, request.AngleUnit, budget, out AffineMinMaxContext context) || !AffineMinMaxTheorems.TryCompute(context, feature, budget, out object value) || value is not T typed)
        {
            outcome = null!;
            return false;
        }

        var certificate = new AffineMinMaxProofCertificate(feature, expression.Value.Canonical, ClaimCanonical.ForObject(value), context.Function, context.FirstOperand.Value.Canonical, context.SecondOperand.Value.Canonical, context.FirstOperand.DefinedWhen.Canonical, context.SecondOperand.DefinedWhen.Canonical, context.PatternCanonical, context.FirstLine.Slope, context.FirstLine.Intercept, context.SecondLine.Slope, context.SecondLine.Intercept, expression.DefinedWhen.Canonical, Rule);
        outcome = ProofOutcome<T>.Proved(typed, certificate);
        return true;
    }
}
