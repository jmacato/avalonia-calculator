namespace Graphing.Symbolics;

/// <summary>
/// Replayable evidence for the zero set of a nontrivially shifted affine
/// reciprocal-trigonometric function. The denominator target is obtained
/// only after retaining the original sine/cosine nonzero guard.
/// </summary>
internal sealed record AffineReciprocalTrigZeroProofCertificate(
    AnalysisFeatures ProvenFeature,
    string Subject,
    string Claim,
    string Function,
    AngleUnit AngleUnit,
    string PatternCanonical,
    string TargetCanonical,
    string DefinednessCanonical,
    string DomainCanonical,
    string Rule)
    : ProofCertificate(ProvenFeature, Subject, Claim);
