using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record RationalRangeProofCertificate(string Subject, string Claim, UnivariatePolynomial Numerator, UnivariatePolynomial Denominator, PolynomialFormula DomainFormula, UnivariatePolynomial PartitionPolynomial, RootIsolationCertificate PartitionRoots, ImmutableArray<BigRational> Boundaries, ImmutableArray<RationalRangeFiberWitness> Fibers, string Rule) : ProofCertificate(AnalysisFeatures.Range, Subject, Claim);
