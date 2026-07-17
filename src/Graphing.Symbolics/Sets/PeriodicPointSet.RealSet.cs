using System.Collections.Immutable;
using System.Text;

namespace Graphing.Symbolics;

internal sealed record PeriodicPointSet(ExactReal Offset, ExactReal Period, string Parameter, IntegerConstraint Constraint) : RealSet
{
    public override string Canonical => $"periodic-points[{ExactRealCanonical.Format(Offset)},{ExactRealCanonical.Format(Period)},{Parameter},{Constraint.Canonical}]";
}
