namespace Graphing.Symbolics;

internal static class SingleHoleSineCertificateChecker
{
    public static bool Check(AnalysisRequest request, SemanticExpression expression, SingleHoleSineProofCertificate certificate, string claim, ResourceBudget budget)
    {
        return SingleHoleSineCertificateReplay.Check(request, expression, certificate, claim, budget);
    }
}
