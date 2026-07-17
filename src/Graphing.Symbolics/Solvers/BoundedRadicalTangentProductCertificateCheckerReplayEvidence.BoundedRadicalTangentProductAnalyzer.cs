using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record BoundedRadicalTangentProductCertificateCheckerReplayEvidence(BigRational UpperEndpoint, string VariableCanonical, ImmutableArray<string> FactorCanonicals, string SourceShapeCanonical, IntervalSet Domain);
