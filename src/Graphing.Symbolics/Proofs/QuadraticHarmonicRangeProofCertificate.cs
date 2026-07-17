namespace Graphing.Symbolics;
/// <summary>
/// Evidence that a total real trigonometric polynomial is exactly a rational
/// quadratic in one sine or cosine harmonic. Its image is therefore the image
/// of that quadratic on the closed interval [-1, 1].
/// </summary>
internal sealed record QuadraticHarmonicRangeProofCertificate(AnalysisFeatures ProvenFeature, string Subject, string Claim, QuadraticHarmonicBasis Basis, int Frequency, BigRational QuadraticCoefficient, BigRational LinearCoefficient, BigRational Constant, string FourierCanonical, string DefinednessCanonical, string Rule) : ProofCertificate(ProvenFeature, Subject, Claim);
