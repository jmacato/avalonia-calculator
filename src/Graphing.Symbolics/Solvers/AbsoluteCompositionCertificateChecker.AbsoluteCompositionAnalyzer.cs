using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class AbsoluteCompositionCertificateChecker
{
    public static bool Check(AnalysisRequest request, SemanticExpression expression, AbsoluteCompositionProofCertificate certificate, string claim, ResourceBudget budget) => AbsoluteCompositionCertificateReplay.Check(request, expression, certificate, claim, budget);
}
