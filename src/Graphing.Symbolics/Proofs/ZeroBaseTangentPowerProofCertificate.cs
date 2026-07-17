namespace Graphing.Symbolics;

/// <summary>
/// Evidence for a zero base raised to a centered affine tangent exponent.
/// The certificate retains both the original power definedness and the exact
/// positive-tangent interval family used by every derived claim.
/// </summary>
internal sealed record ZeroBaseTangentPowerProofCertificate(
    AnalysisFeatures ProvenFeature,
    string Subject,
    string Claim,
    string ExponentCanonical,
    string PatternCanonical,
    string DefinednessCanonical,
    string DomainCanonical,
    string Rule)
    : ProofCertificate(ProvenFeature, Subject, Claim);
