namespace Graphing.Symbolics;

internal sealed record AffineFloorCertificateCheckerAffineFloorReplayContext(string ArgumentCanonical, string ArgumentDefinednessCanonical, BigRational Slope, BigRational Intercept)
{
    public string NormalizedArgumentCanonical => $"affine-floor[{Slope},{Intercept}]";
}
