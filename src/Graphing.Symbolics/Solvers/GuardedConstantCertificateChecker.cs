namespace Graphing.Symbolics;

internal static class GuardedConstantCertificateChecker
{
    public static bool Check(AnalysisRequest request, SemanticExpression expression, GuardedConstantProofCertificate certificate, string claim, ResourceBudget budget)
    {
        return GuardedConstantCertificateReplay.Check(request, expression, certificate, claim, budget);
    }
}
