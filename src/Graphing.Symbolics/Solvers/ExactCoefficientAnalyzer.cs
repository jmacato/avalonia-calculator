using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class ExactCoefficientAnalyzer
{
    public static bool TryAnalyze<T>(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures feature, ResourceBudget budget, out ProofOutcome<T> outcome)
    {
        if (!ExactCoefficientPatternExtractor.TryExtract(expression, request.Variable, request.AngleUnit, budget, out ExactCoefficientPattern pattern) || !TryCompute(pattern, request.AngleUnit, feature, budget, out object value) || value is not T typed || !ExactCoefficientEvidence.TryCreate(pattern, budget, out ImmutableArray<ExactOrderWitness> witnesses))
        {
            outcome = null!;
            return false;
        }

        var certificate = new ExactCoefficientProofCertificate(feature, expression.Value.Canonical, ClaimCanonical.ForObject(value), pattern.Kind, pattern.Canonical, witnesses, ExactCoefficientEvidence.Rule);
        outcome = ProofOutcome<T>.Proved(typed, certificate);
        return true;
    }

    private static bool TryCompute(ExactCoefficientPattern pattern, AngleUnit angleUnit, AnalysisFeatures feature, ResourceBudget budget, out object value) => pattern switch
    {
        ExactRationalPattern rational => ExactRationalCoefficientTheorems.TryCompute(rational, feature, budget, out value),
        ExactTrigPattern trig => ExactTrigCoefficientTheorems.TryCompute(trig, angleUnit, feature, budget, out value),
        _ => Fail(out value)
    };
    private static bool Fail(out object value)
    {
        value = null!;
        return false;
    }
}
