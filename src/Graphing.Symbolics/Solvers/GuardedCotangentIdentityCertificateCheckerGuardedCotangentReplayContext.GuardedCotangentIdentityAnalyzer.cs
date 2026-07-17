using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record GuardedCotangentIdentityCertificateCheckerGuardedCotangentReplayContext(SemanticExpression Numerator, SemanticExpression Denominator, AffineTrigPattern Tangent, ExactScalar CotangentScale, PeriodicIntervalSet Domain, ExactReal FunctionPeriod)
{
    public string PatternCanonical => $"guarded-cotangent-identity:{Tangent.Canonical}:{CotangentScale.Canonical}";
}
