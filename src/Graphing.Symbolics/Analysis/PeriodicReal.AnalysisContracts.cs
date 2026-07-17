using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record PeriodicReal(ExactReal Offset, ExactReal Period, string Parameter, IntegerConstraint Constraint) : RealFamily;
