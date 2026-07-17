using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class AffinePhaseSineCompositionCertificateChecker
{
    public static bool Check(AnalysisRequest request, SemanticExpression expression, AffinePhaseSineCompositionProofCertificate certificate, string claim, ResourceBudget budget) => AffinePhaseSineCompositionCertificateReplay.Check(request, expression, certificate, claim, budget);
}
