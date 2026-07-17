using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record GuardedConstantCertificateReplayReplayEvidence(ExactScalar Scalar, PeriodicIntervalSet Domain, ExactReal Period, bool Symmetric, bool ContainsZero);
