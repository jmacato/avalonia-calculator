using System.Collections.Immutable;

namespace Graphing.Symbolics;
/// <summary>
/// Replay reconstructs the original total affine operands and recomputes the
/// envelope. Stored coefficients and branch selection are never trusted.
/// </summary>
internal static class AffineMinMaxCertificateChecker
{
    public static bool Check(AnalysisRequest request, SemanticExpression expression, AffineMinMaxProofCertificate certificate, string claim, ResourceBudget budget) => AffineMinMaxCertificateReplay.Check(request, expression, certificate, claim, budget);
}
