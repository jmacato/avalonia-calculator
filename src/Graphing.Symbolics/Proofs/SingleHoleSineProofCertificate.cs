namespace Graphing.Symbolics;

/// <summary>
/// Evidence for a centered affine sine whose value survived multiplication by
/// the guarded identity x/x. The origin hole is retained in every feature.
/// </summary>
internal sealed record SingleHoleSineProofCertificate(
    AnalysisFeatures ProvenFeature,
    string Subject,
    string Claim,
    string SineCanonical,
    string GuardCanonical,
    string PatternCanonical,
    string DefinednessCanonical,
    string DomainCanonical,
    string Rule)
    : ProofCertificate(ProvenFeature, Subject, Claim);
