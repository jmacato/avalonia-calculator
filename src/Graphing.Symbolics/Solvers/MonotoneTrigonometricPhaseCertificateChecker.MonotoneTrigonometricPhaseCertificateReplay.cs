using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class MonotoneTrigonometricPhaseCertificateChecker
{
    public static bool Check(AnalysisRequest request, SemanticExpression expression, MonotoneTrigonometricPhaseProofCertificate certificate, string claim, ResourceBudget budget) => MonotoneTrigonometricPhaseCertificateReplay.Check(request, expression, certificate, claim, budget);
}
