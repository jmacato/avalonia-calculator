using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record AffineSquareLogPattern(BigRational Slope, BigRational Intercept)
{
    public string Canonical => $"base-ten-log-affine-square:{Slope}:{Intercept}";
    public BigRational Center => -Intercept / Slope;
}
