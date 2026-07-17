using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class SingleHoleSineCertificateChecker
{
    public static bool Check(AnalysisRequest request, SemanticExpression expression, SingleHoleSineProofCertificate certificate, string claim, ResourceBudget budget) => SingleHoleSineCertificateReplay.Check(request, expression, certificate, claim, budget);
}
