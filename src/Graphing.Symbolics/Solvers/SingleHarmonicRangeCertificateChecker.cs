namespace Graphing.Symbolics;

internal static class SingleHarmonicRangeCertificateChecker
{
    public static bool Check(AnalysisRequest request, SemanticExpression expression, SingleHarmonicRangeProofCertificate certificate, string claim, ResourceBudget budget)
    {
        return SingleHarmonicRangeCertificateReplay.Check(request, expression, certificate, claim, budget);
    }
}
