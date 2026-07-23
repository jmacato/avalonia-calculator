namespace Graphing.Symbolics;

internal sealed record LinearDriftTrigPattern(BigRational Slope, LinearDriftExactOffset Intercept, AffineTrigPattern Trig, LinearDriftDerivativeRegime DerivativeRegime)
{
    public string Canonical => $"linear-drift:{Slope}:{Intercept.Canonical}:{Trig.Canonical}:{(int)DerivativeRegime}";
}
