namespace Graphing.Symbolics;

internal sealed record UnaryCompositionProofCertificate(AnalysisFeatures ProvenFeature, string Subject, string Claim, UnaryCompositionKind Kind, string OuterFunction, string InnerFunction, string PatternCanonical, string DefinednessCanonical, string Rule) : ProofCertificate(ProvenFeature, Subject, Claim);
