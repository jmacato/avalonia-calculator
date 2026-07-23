namespace Graphing.Symbolics;

internal static class AbsoluteCompositionCertificateChecker
{
    public static bool Check(AnalysisRequest request, SemanticExpression expression, AbsoluteCompositionProofCertificate certificate, string claim, ResourceBudget budget)
    {
        return AbsoluteCompositionCertificateReplay.Check(request, expression, certificate, claim, budget);
    }
}
