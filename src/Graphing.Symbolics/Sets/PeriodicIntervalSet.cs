using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record PeriodicIntervalSet(ExactReal Period, string Parameter, IntegerConstraint Constraint, ImmutableArray<PeriodicInterval> Intervals) : RealSet
{
    public override string Canonical => $"periodic-intervals[{ExactRealCanonical.Format(Period)},{Parameter},{Constraint.Canonical},{string.Join(',', Intervals.Select(static interval => interval.Canonical))}]";
}
