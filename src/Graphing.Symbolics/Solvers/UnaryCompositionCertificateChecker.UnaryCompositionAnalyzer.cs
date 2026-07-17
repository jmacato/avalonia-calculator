using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class UnaryCompositionCertificateChecker
{
    public static bool Check(AnalysisRequest request, SemanticExpression expression, UnaryCompositionProofCertificate certificate, string claim, ResourceBudget budget) => UnaryCompositionCertificateReplay.Check(request, expression, certificate, claim, budget);
}
