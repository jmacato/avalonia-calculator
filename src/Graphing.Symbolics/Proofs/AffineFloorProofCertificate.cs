namespace Graphing.Symbolics;

/// <summary>
/// Evidence for the exact step-function semantics of
/// <c>floor(a*x + b)</c> with rational <c>a</c> and <c>b</c>. Replay does not
/// trust these stored coefficients: it re-extracts them from the guarded
/// semantic graph and recomputes the requested claim.
/// </summary>
internal sealed record AffineFloorProofCertificate(
    AnalysisFeatures ProvenFeature,
    string Subject,
    string Claim,
    string ArgumentCanonical,
    string ArgumentDefinednessCanonical,
    string NormalizedArgumentCanonical,
    BigRational Slope,
    BigRational Intercept,
    string DefinednessCanonical,
    string Rule)
    : ProofCertificate(ProvenFeature, Subject, Claim);
