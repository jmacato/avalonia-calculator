namespace Graphing.Symbolics;
/// <summary>
/// Production replay for ordered-coefficient certificates.  It does not call
/// <see cref = "ExactCoefficientAnalyzer"/> or trust its selected branch: it
/// independently re-extracts the normalized pattern, checks every rational
/// enclosure witness, then instantiates the named theorem and compares the
/// canonical claim.
/// </summary>
internal static class ExactCoefficientCertificateChecker
{
    public static bool Check(AnalysisRequest request, SemanticExpression expression, ExactCoefficientProofCertificate certificate, string claim, ResourceBudget budget)
    {
        budget.Charge();
        if (certificate.Feature != certificate.ProvenFeature || !request.Features.HasFlag(certificate.Feature) || !string.Equals(certificate.Subject, expression.Value.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.SubjectCanonical, expression.Value.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.Claim, claim, StringComparison.Ordinal) || !string.Equals(certificate.ClaimCanonical, claim, StringComparison.Ordinal) || !string.Equals(certificate.Rule, ExactCoefficientEvidence.Rule, StringComparison.Ordinal) || !ExactCoefficientCertificatePatternReplay.TryExtract(expression, request.Variable, request.AngleUnit, budget, out ExactCoefficientPattern pattern) || pattern.Kind != certificate.PatternKind || !string.Equals(pattern.Canonical, certificate.PatternCanonical, StringComparison.Ordinal) || !ExactCoefficientEvidence.Verify(pattern, certificate.OrderWitnesses, budget) || !TryInstantiateTheorem(pattern, request.AngleUnit, certificate.Feature, budget, out object expected))
        {
            return false;
        }

        return string.Equals(ClaimCanonical.ForObject(expected), claim, StringComparison.Ordinal);
    }

    private static bool TryInstantiateTheorem(ExactCoefficientPattern pattern, AngleUnit angleUnit, AnalysisFeatures feature, ResourceBudget budget, out object value)
    {
        // This is a theorem instantiator, not solver selection: the pattern
        // kind and every order premise have already been independently checked.
        return pattern switch
        {
            ExactRationalPattern rational => ExactRationalCertificateReplay.TryCompute(rational, feature, budget, out value),
            ExactTrigPattern trig => ExactTrigCertificateReplay.TryCompute(trig, angleUnit, feature, budget, out value),
            _ => Fail(out value)
        };
    }

    private static bool Fail(out object value)
    {
        value = null!;
        return false;
    }
}
