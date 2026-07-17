using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record AffinePrimitivePattern(string Function, BigRational InnerSlope, BigRational InnerIntercept, BigRational OuterScale, BigRational OuterShift, int RootDegree, ValueTerm Core)
{
    public string Canonical => $"primitive:{Function}:{InnerSlope}:{InnerIntercept}:{OuterScale}:{OuterShift}:{RootDegree}:{Core.Canonical}";
}
