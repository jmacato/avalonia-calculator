using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record MonotoneTrigonometricPhaseProofCertificate(AnalysisFeatures ProvenFeature, string Subject, string Claim, string Variable, AngleUnit AngleUnit, string OuterFunction, string PhaseCanonical, ImmutableArray<PuiseuxTerm> PhaseTerms, BigRational CoordinateOffset, int SubstitutionDegree, UnivariatePolynomial ParameterPolynomial, bool ParameterIsNonnegative, bool BoundaryIncluded, PolynomialFormula DomainFormula, CellDecompositionCertificate DomainCells, PhaseOrientation ParameterOrientation, PhaseOrientation Orientation, CellDecompositionCertificate DerivativeViolationCells, string DefinednessCanonical, string ContinuityCanonical, string DifferentiabilityCanonical, string Rule) : ProofCertificate(ProvenFeature, Subject, Claim);
