namespace Graphing.Symbolics;

internal sealed record AffineSquareLogProofCertificate(
    AnalysisFeatures ProvenFeature,
    string Subject,
    string Claim,
    BigRational Slope,
    BigRational Intercept,
    string PatternCanonical,
    string DefinednessCanonical,
    string Rule)
    : ProofCertificate(ProvenFeature, Subject, Claim);
