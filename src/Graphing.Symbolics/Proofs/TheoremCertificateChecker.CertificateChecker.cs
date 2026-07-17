using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class TheoremCertificateChecker
{
    public static bool Check(AnalysisRequest request, SemanticExpression expression, TheoremProofCertificate certificate, string claim, ResourceBudget budget)
    {
        if (!string.Equals(certificate.Subject, certificate.SubjectCanonical, StringComparison.Ordinal) || !string.Equals(certificate.Claim, claim, StringComparison.Ordinal))
        {
            return false;
        }

        return TheoremCertificateReplay.Check(request, expression, certificate, claim, budget);
    }
}
