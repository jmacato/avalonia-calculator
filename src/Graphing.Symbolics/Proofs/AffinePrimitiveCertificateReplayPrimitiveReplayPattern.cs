namespace Graphing.Symbolics;

internal readonly record struct AffinePrimitiveCertificateReplayPrimitiveReplayPattern(string Function, BigRational InnerSlope, BigRational InnerIntercept, BigRational OuterScale, BigRational OuterShift, int RootDegree, ValueTerm Core)
{
    public string Canonical => $"primitive:{Function}:{InnerSlope}:{InnerIntercept}:{OuterScale}:{OuterShift}:{RootDegree}:{Core.Canonical}";
}
