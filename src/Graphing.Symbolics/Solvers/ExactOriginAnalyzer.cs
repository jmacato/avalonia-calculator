namespace Graphing.Symbolics;
/// <summary>
/// Shape-independent y-intercept proof by exact structural substitution at
/// x = 0. Definedness is evaluated before the rewritten value so source holes
/// remain holes even when value-only simplification cancels them.
/// </summary>
internal static class ExactOriginAnalyzer
{
    internal const string Rule = "exact-origin-substitution";
    public static bool TryAnalyze<T>(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures feature, ResourceBudget budget, out ProofOutcome<T> outcome)
    {
        if (feature != AnalysisFeatures.YIntercept)
        {
            outcome = null!;
            return false;
        }

        var evaluator = new ExactOriginValueEvaluator(request.Variable, request.AngleUnit, budget);
        if (!evaluator.TryEvaluate(expression.DefinedWhen, out bool defined))
        {
            outcome = null!;
            return false;
        }

        OptionalValue<ExactReal> intercept;
        if (!defined)
        {
            intercept = OptionalValue<ExactReal>.None;
        }
        else if (evaluator.TryEvaluate(expression.Value, out ExactReal value))
        {
            intercept = OptionalValue<ExactReal>.Some(value);
        }
        else
        {
            outcome = null!;
            return false;
        }

        if (intercept is not T typed)
        {
            outcome = null!;
            return false;
        }

        string claim = ClaimCanonical.For(intercept);
        var certificate = new ExactOriginProofCertificate(expression.Value.Canonical, claim, expression.DefinedWhen.Canonical, request.AngleUnit, Rule);
        outcome = ProofOutcome<T>.Proved(typed, certificate);
        return true;
    }
}
