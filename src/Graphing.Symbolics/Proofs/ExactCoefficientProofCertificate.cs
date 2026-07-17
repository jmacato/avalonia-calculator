using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record ExactCoefficientProofCertificate(AnalysisFeatures ProvenFeature, string Subject, string Claim, ExactCoefficientPatternKind PatternKind, string PatternCanonical, ImmutableArray<ExactOrderWitness> OrderWitnesses, string Rule) : ProofCertificate(ProvenFeature, Subject, Claim);
