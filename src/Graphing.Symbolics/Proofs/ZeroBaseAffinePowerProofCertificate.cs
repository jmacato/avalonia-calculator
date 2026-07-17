namespace Graphing.Symbolics;

/// <summary>
/// Evidence that an exact zero base raised to a total affine exponent is the
/// constant zero function on precisely the open half-line where that exponent
/// is positive. Replay re-extracts the exponent and rechecks the original
/// definedness formula before accepting any claim.
/// </summary>
internal sealed record ZeroBaseAffinePowerProofCertificate(
    AnalysisFeatures ProvenFeature,
    string Subject,
    string Claim,
    string ExponentCanonical,
    string SlopeCanonical,
    string InterceptCanonical,
    string DefinednessCanonical,
    string DomainCanonical,
    string Rule)
    : ProofCertificate(ProvenFeature, Subject, Claim);
