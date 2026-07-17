using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class ZeroBaseTangentPowerCertificateChecker
{
    public static bool Check(AnalysisRequest request, SemanticExpression expression, ZeroBaseTangentPowerProofCertificate certificate, string claim, ResourceBudget budget) => ZeroBaseTangentPowerCertificateReplay.Check(request, expression, certificate, claim, budget);
}
