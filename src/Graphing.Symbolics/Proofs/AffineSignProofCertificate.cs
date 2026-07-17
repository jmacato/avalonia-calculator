namespace Graphing.Symbolics;

/// <summary>
/// Evidence for the exact piecewise-constant semantics of
/// <c>sign(a*x + b)</c>. The normalized coefficients have independently
/// proved signs; replay re-extracts them from the original semantic graph.
/// </summary>
internal sealed record AffineSignProofCertificate(
    AnalysisFeatures ProvenFeature,
    string Subject,
    string Claim,
    string ArgumentCanonical,
    string NormalizedArgumentCanonical,
    string SlopeCanonical,
    string InterceptCanonical,
    string DefinednessCanonical,
    string Rule)
    : ProofCertificate(ProvenFeature, Subject, Claim);
