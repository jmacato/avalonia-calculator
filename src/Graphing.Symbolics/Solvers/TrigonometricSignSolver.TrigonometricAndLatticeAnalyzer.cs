using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class TrigonometricSignSolver
{
    public static bool IsPrimitive(ValueTerm term, string function, string variable) => term.Kind == ValueKind.Function && term.Name == function && term.Operands.Length == 1 && term.Operands[0].Kind == ValueKind.Variable && term.Operands[0].Name.Equals(variable, StringComparison.OrdinalIgnoreCase);
    public static RealSet PositiveSineWithTangentDefined(ExactReal halfPi, ExactReal pi, ExactReal twoPi) => new PeriodicIntervalSet(twoPi, "m", IntegerConstraint.All("m"), [new PeriodicInterval(new RationalReal(BigRational.Zero), false, halfPi, false), new PeriodicInterval(halfPi, false, pi, false)]);
}
