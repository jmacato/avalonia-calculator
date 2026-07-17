using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class CooperEliminator
{
    public static bool TryEliminateExists(string variable, PresburgerFormula source, ResourceBudget budget, out PresburgerFormula result)
    {
        PresburgerFormula normalized = PresburgerNormalizer.Normalize(source);
        ImmutableArray<PresburgerFormula> operands = normalized is PresburgerJunction { IsConjunction: true } conjunction ? conjunction.Operands : [normalized];
        PresburgerComparison? equality = operands.OfType<PresburgerComparison>().FirstOrDefault(comparison => comparison.Relation == IntegerRelation.Equal && !comparison.Expression.Coefficient(variable).IsZero);
        if (equality is not null)
        {
            result = EliminateWithEquality(variable, equality, operands, budget);
            return true;
        }

        if (operands.Any(operand => operand is PresburgerDivisibility || operand is PresburgerNot))
        {
            result = null!;
            return false;
        }

        return TryEliminateUnitBounds(variable, operands, budget, out result);
    }

    private static PresburgerFormula EliminateWithEquality(string variable, PresburgerComparison equality, ImmutableArray<PresburgerFormula> operands, ResourceBudget budget)
    {
        ExactInteger coefficient = equality.Expression.Coefficient(variable);
        LinearIntegerExpression remainder = equality.Expression.Without(variable);
        ExactInteger positiveCoefficient = ExactInteger.Abs(coefficient);
        var result = new List<PresburgerFormula>
        {
            new PresburgerDivisibility(positiveCoefficient, remainder)
        };
        foreach (PresburgerFormula operand in operands)
        {
            budget.Charge();
            if (ReferenceEquals(operand, equality))
            {
                continue;
            }

            switch (operand)
            {
                case PresburgerComparison comparison:
                    result.Add(SubstituteComparison(variable, coefficient, remainder, comparison));
                    break;
                case PresburgerDivisibility divisibility:
                    result.Add(SubstituteDivisibility(variable, coefficient, remainder, divisibility));
                    break;
                default:
                    throw new InvalidOperationException("Normalized equality elimination received a non-atomic conjunction.");
            }
        }

        return PresburgerNormalizer.And(result);
    }

    private static Graphing.Symbolics.PresburgerComparison SubstituteComparison(string variable, ExactInteger equalityCoefficient, LinearIntegerExpression equalityRemainder, PresburgerComparison comparison)
    {
        ExactInteger coefficient = comparison.Expression.Coefficient(variable);
        LinearIntegerExpression remainder = comparison.Expression.Without(variable);
        LinearIntegerExpression substituted = remainder.Scale(equalityCoefficient).Add(equalityRemainder.Scale(-coefficient));
        if (equalityCoefficient.Sign < 0 && comparison.Relation == IntegerRelation.LessOrEqual)
        {
            substituted = substituted.Scale(ExactInteger.MinusOne);
        }

        return new PresburgerComparison(substituted, comparison.Relation);
    }

    private static Graphing.Symbolics.PresburgerDivisibility SubstituteDivisibility(string variable, ExactInteger equalityCoefficient, LinearIntegerExpression equalityRemainder, PresburgerDivisibility divisibility)
    {
        ExactInteger coefficient = divisibility.Expression.Coefficient(variable);
        LinearIntegerExpression remainder = divisibility.Expression.Without(variable);
        LinearIntegerExpression substituted = remainder.Scale(equalityCoefficient).Add(equalityRemainder.Scale(-coefficient));
        return new PresburgerDivisibility(ExactInteger.Abs(divisibility.Divisor * equalityCoefficient), substituted);
    }

    private static bool TryEliminateUnitBounds(string variable, ImmutableArray<PresburgerFormula> operands, ResourceBudget budget, out PresburgerFormula result)
    {
        var lowers = new List<LinearIntegerExpression>();
        var uppers = new List<LinearIntegerExpression>();
        var independent = new List<PresburgerFormula>();
        foreach (PresburgerFormula operand in operands)
        {
            budget.Charge();
            if (operand is not PresburgerComparison { Relation: IntegerRelation.LessOrEqual } comparison)
            {
                result = null!;
                return false;
            }

            ExactInteger coefficient = comparison.Expression.Coefficient(variable);
            LinearIntegerExpression remainder = comparison.Expression.Without(variable);
            if (coefficient.IsZero)
            {
                independent.Add(comparison);
            }
            else if (coefficient.IsOne)
            {
                uppers.Add(remainder);
            }
            else if (coefficient == ExactInteger.MinusOne)
            {
                lowers.Add(remainder);
            }
            else
            {
                result = null!;
                return false;
            }
        }

        foreach (LinearIntegerExpression lower in lowers)
        {
            foreach (LinearIntegerExpression upper in uppers)
            {
                budget.Charge();
                independent.Add(new PresburgerComparison(lower.Add(upper), IntegerRelation.LessOrEqual));
            }
        }

        result = PresburgerNormalizer.And(independent);
        return true;
    }
}
