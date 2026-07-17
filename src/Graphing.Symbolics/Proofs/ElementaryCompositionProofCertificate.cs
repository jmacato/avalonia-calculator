namespace Graphing.Symbolics;

internal sealed record ElementaryCompositionProofCertificate(AnalysisFeatures ProvenFeature, string Subject, string Claim, ElementaryCompositionKind Kind, string PatternCanonical, string DefinednessCanonical, AngleUnit AngleUnit, string Rule) : ProofCertificate(ProvenFeature, Subject, Claim);
