namespace Graphing.Symbolics;

internal static class ElementaryCompositionCertificateChecker
{
    public static bool Check(AnalysisRequest request, SemanticExpression expression, ElementaryCompositionProofCertificate certificate, string claim, ResourceBudget budget)
    {
        return ElementaryCompositionCertificateReplay.Check(request, expression, certificate, claim, budget);
    }
}
