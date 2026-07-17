namespace Graphing.Symbolics;

internal sealed record GuardedConstantProofCertificate(
    AnalysisFeatures ProvenFeature,
    string Subject,
    string Claim,
    string ScalarCanonical,
    string DefinednessCanonical,
    string DomainCanonical,
    string Rule)
    : ProofCertificate(ProvenFeature, Subject, Claim);
