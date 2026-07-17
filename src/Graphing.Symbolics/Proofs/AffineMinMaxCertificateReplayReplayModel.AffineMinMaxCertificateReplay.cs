using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record AffineMinMaxCertificateReplayReplayModel(bool IsMinimum, bool HasKink, AffineMinMaxCertificateReplayReplayLine EffectiveLine, AffineMinMaxCertificateReplayReplayLine LeftTail, AffineMinMaxCertificateReplayReplayLine RightTail, BigRational KinkX, BigRational KinkY);
