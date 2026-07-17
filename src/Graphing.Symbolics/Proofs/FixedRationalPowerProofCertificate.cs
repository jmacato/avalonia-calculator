using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record FixedRationalPowerProofCertificate(AnalysisFeatures ProvenFeature, string Subject, string Claim, BigRational Exponent, UnivariatePolynomial BaseNumerator, UnivariatePolynomial BaseDenominator, PolynomialFormula DomainFormula, CellDecompositionCertificate SignChart, ImmutableArray<CellDecompositionCertificate> AuxiliaryCells, ImmutableArray<BigRational> RangeBoundaries, ImmutableArray<RationalPowerRangeFiberWitness> RangeFibers, string Rule) : ProofCertificate(ProvenFeature, Subject, Claim);
