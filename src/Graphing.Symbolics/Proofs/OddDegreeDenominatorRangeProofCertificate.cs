namespace Graphing.Symbolics;

internal sealed record OddDegreeDenominatorRangeProofCertificate(
    AnalysisFeatures ProvenFeature,
    string Subject,
    string Claim,
    UnivariatePolynomial Numerator,
    UnivariatePolynomial Denominator,
    string DefinednessCanonical,
    PolynomialFormula DomainFormula,
    CellDecompositionCertificate DomainCells,
    CellDecompositionCertificate DenominatorDomain,
    string Rule)
    : ProofCertificate(ProvenFeature, Subject, Claim);
