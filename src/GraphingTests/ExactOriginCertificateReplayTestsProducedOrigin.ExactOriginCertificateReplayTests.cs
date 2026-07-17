using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

internal sealed record ExactOriginCertificateReplayTestsProducedOrigin(AnalysisRequest Request, SemanticExpression Semantic, ProofOutcome<OptionalValue<ExactReal>> Outcome, ExactOriginProofCertificate Certificate);
