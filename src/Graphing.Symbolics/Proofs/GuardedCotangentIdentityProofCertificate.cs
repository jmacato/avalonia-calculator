namespace Graphing.Symbolics;

/// <summary>
/// Evidence that an exact trigonometric identity numerator divided by a
/// centered affine tangent is a scaled cotangent on the original, stricter
/// domain that excludes both sine and cosine zeros.
/// </summary>
internal sealed record GuardedCotangentIdentityProofCertificate(
    AnalysisFeatures ProvenFeature,
    string Subject,
    string Claim,
    string NumeratorCanonical,
    string DenominatorCanonical,
    string PatternCanonical,
    string DefinednessCanonical,
    string DomainCanonical,
    string Rule)
    : ProofCertificate(ProvenFeature, Subject, Claim);
