using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record SemialgebraicUnaryProofCertificate(AnalysisFeatures ProvenFeature, string Subject, string Claim, SemialgebraicUnaryKind Kind, UnivariatePolynomial InnerNumerator, UnivariatePolynomial InnerDenominator, PolynomialFormula DomainFormula, UnarySignChartCertificate Chart, ImmutableArray<CellDecompositionCertificate> AuxiliaryCells, ImmutableArray<BigRational> RangeBoundaries, ImmutableArray<UnaryRangeFiberWitness> RangeFibers, string Rule) : ProofCertificate(ProvenFeature, Subject, Claim);
