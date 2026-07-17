using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record GuardedConstantContext(SemanticExpression Expression, ExactScalar Scalar, PeriodicIntervalSet Domain, ExactReal Period, bool Symmetric, bool ContainsZero)
{
    public static bool TryCreate(SemanticExpression expression, string variable, AngleUnit angleUnit, ResourceBudget budget, out GuardedConstantContext context)
    {
        budget.Charge();
        if (!ExactScalar.TryCreate(expression.Value, budget, out ExactScalar scalar) || !MixedTrigonometricSolver.TryDomain(expression, variable, angleUnit, budget, out RealSet solved) || solved is not PeriodicIntervalSet { Constraint.Bound.IsUnbounded: true, Intervals.Length: 1 } periodic || periodic.Intervals[0] is not { IncludesLower: false, IncludesUpper: false } interval || !PeriodicPunctureFacts.TryCreate(periodic.Period, interval.LowerOffset, interval.UpperOffset, budget, out bool symmetric) || !ExactFormulaAtRational.TryEvaluate(expression.DefinedWhen, variable, BigRational.Zero, budget, out bool containsZero))
        {
            context = null!;
            return false;
        }

        context = new GuardedConstantContext(expression, scalar, periodic, periodic.Period, symmetric, containsZero);
        return true;
    }
}
