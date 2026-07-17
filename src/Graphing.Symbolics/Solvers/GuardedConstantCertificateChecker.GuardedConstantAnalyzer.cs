using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class GuardedConstantCertificateChecker
{
    public static bool Check(AnalysisRequest request, SemanticExpression expression, GuardedConstantProofCertificate certificate, string claim, ResourceBudget budget) => GuardedConstantCertificateReplay.Check(request, expression, certificate, claim, budget);
}
