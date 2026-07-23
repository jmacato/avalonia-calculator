using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record TheoremProofCertificate(AnalysisFeatures ProvenFeature, string Subject, string Claim, TheoremRule Theorem, ImmutableArray<string> Parameters) : ProofCertificate(ProvenFeature, Subject, Claim);
