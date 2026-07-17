using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record AffineReciprocalTrigZeroCertificateCheckerReplayEvidence(AffineReciprocalTrigPattern Pattern, ExactScalar Target, int UnitIntervalComparison, RealSet Domain);
