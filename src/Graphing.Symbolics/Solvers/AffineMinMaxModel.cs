namespace Graphing.Symbolics;

internal sealed record AffineMinMaxModel(bool IsMinimum, bool HasKink, RationalAffineLine EffectiveLine, RationalAffineLine LeftTail, RationalAffineLine RightTail, BigRational KinkX, BigRational KinkY);
