namespace Graphing.Symbolics;
/// <summary>
/// Checker-owned replay for exact origin substitution. This traversal is kept
/// independent from the producer; only low-level immutable exact arithmetic
/// is shared.
/// </summary>
internal static class ExactOriginCertificateReplay
{
    private const string Rule = "exact-origin-substitution";
    public static bool Check(AnalysisRequest request, SemanticExpression expression, ExactOriginProofCertificate certificate, string claim, ResourceBudget budget)
    {
        budget.Charge();
        if (certificate.Feature != AnalysisFeatures.YIntercept || !request.Features.HasFlag(AnalysisFeatures.YIntercept) || certificate.AngleUnit != request.AngleUnit || !string.Equals(certificate.Rule, Rule, StringComparison.Ordinal) || !string.Equals(certificate.Subject, expression.Value.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.SubjectCanonical, expression.Value.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.Claim, claim, StringComparison.Ordinal) || !string.Equals(certificate.ClaimCanonical, claim, StringComparison.Ordinal) || !string.Equals(certificate.DefinednessCanonical, expression.DefinedWhen.Canonical, StringComparison.Ordinal))
        {
            return false;
        }

        var evaluator = new ExactOriginCertificateReplayReplayEvaluator(request.Variable, request.AngleUnit, budget);
        if (!evaluator.TryFormula(expression.DefinedWhen, out bool defined))
        {
            return false;
        }

        OptionalValue<ExactReal> expected;
        if (!defined)
        {
            expected = OptionalValue<ExactReal>.None;
        }
        else if (evaluator.TryTerm(expression.Value, out ExactReal value))
        {
            expected = OptionalValue<ExactReal>.Some(value);
        }
        else
        {
            return false;
        }

        return string.Equals(ClaimCanonical.For(expected), claim, StringComparison.Ordinal);
    }
}
