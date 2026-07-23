namespace Graphing.Symbolics;

internal sealed record AbsoluteCompositionProofCertificate(AnalysisFeatures ProvenFeature, string Subject, string Claim, AbsoluteCompositionForm Form, string PatternCanonical, string Rule) : ProofCertificate(ProvenFeature, Subject, Claim);
