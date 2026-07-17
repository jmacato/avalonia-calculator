using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal readonly record struct AffineMinMaxCertificateReplayReplayLine(BigRational Slope, BigRational Intercept)
{
    public string Canonical => $"affine[{Slope},{Intercept}]";
}
