namespace Graphing.Symbolics;

internal sealed record AbsoluteCompositionPattern(AbsoluteCompositionForm Form, string Function, ExactScalar Scale, BigRational Slope, BigRational Intercept)
{
    public string Canonical => $"absolute-composition:{(int)Form}:{Function}:{Scale.Canonical}:{Slope}:{Intercept}";
}
