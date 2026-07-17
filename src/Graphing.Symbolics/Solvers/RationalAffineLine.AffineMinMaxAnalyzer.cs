using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal readonly record struct RationalAffineLine(BigRational Slope, BigRational Intercept)
{
    public string Canonical => $"affine[{Slope},{Intercept}]";

    public BigRational Evaluate(BigRational x) => (Slope * x) + Intercept;
}
