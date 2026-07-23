namespace Graphing.Symbolics;

internal sealed record AffineSquareLogCertificateCheckerAffineSquareLogReplayContext(BigRational Slope, BigRational Intercept, BigRational Center)
{
    public string PatternCanonical => $"base-ten-log-affine-square:{Slope}:{Intercept}";
}
