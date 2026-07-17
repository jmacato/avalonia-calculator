namespace Graphing.Symbolics;

/// <summary>
/// Evidence that a total real trigonometric polynomial has exactly one
/// nonzero Fourier frequency and therefore has the exact image
/// c + [-sqrt(a^2+b^2), sqrt(a^2+b^2)].
/// </summary>
internal sealed record SingleHarmonicRangeProofCertificate(
    AnalysisFeatures ProvenFeature,
    string Subject,
    string Claim,
    int Frequency,
    BigRational Constant,
    BigRational CosineCoefficient,
    BigRational SineCoefficient,
    BigRational RadiusSquared,
    string FourierCanonical,
    string DefinednessCanonical,
    string Rule)
    : ProofCertificate(ProvenFeature, Subject, Claim);
