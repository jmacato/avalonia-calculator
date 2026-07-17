using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

internal sealed record ExactOriginSubstitutionCoverageTestsProducedOrigin(AnalysisRequest Request, SemanticExpression Semantic, ProofOutcome<OptionalValue<ExactReal>> Outcome);
