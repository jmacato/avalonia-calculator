using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record AffineSignCertificateCheckerAffineSignReplayContext(string ArgumentCanonical, ExactScalar Slope, ExactScalar Intercept)
{
    public string NormalizedArgumentCanonical => $"affine-sign[{Slope.Canonical},{Intercept.Canonical}]";
}
