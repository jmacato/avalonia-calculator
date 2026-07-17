namespace Graphing.Symbolics;

/// <summary>
/// Evidence for the exact envelope of two total rational-affine branches.
/// Replay re-extracts both source operands and recomputes the selected tails,
/// crossing point, and requested feature without trusting stored coefficients.
/// </summary>
internal sealed record AffineMinMaxProofCertificate(
    AnalysisFeatures ProvenFeature,
    string Subject,
    string Claim,
    string Function,
    string FirstOperandCanonical,
    string SecondOperandCanonical,
    string FirstDefinednessCanonical,
    string SecondDefinednessCanonical,
    string PatternCanonical,
    BigRational FirstSlope,
    BigRational FirstIntercept,
    BigRational SecondSlope,
    BigRational SecondIntercept,
    string DefinednessCanonical,
    string Rule)
    : ProofCertificate(ProvenFeature, Subject, Claim);
