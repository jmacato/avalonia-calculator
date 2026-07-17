using System.Collections.Immutable;
using System.Text;

namespace Graphing.Symbolics;

internal sealed record IntervalSet(RealBound Lower, bool IncludesLower, RealBound Upper, bool IncludesUpper) : RealSet
{
    public override string Canonical => $"interval[{Lower.Canonical},{(IncludesLower ? 1 : 0)},{Upper.Canonical},{(IncludesUpper ? 1 : 0)}]";
}
