namespace Graphing.Symbolics;

/// <summary>
/// Checker-owned evaluation of variable-independent logical premises.  It is
/// deliberately structural and never invokes domain solving or cell
/// decomposition.
/// </summary>
internal static class ExactFormulaVerifier
{
    public static bool IsAlwaysTrue(Formula formula, ResourceBudget budget) =>
        TryEvaluateConstant(formula, budget, out bool value) && value;

    public static bool MatchesSingleGuard(
        Formula formula,
        Func<Formula, bool> guardMatcher,
        ResourceBudget budget)
    {
        budget.Charge();
        bool foundGuard = false;
        IEnumerable<Formula> operands = formula is JunctionFormula { IsConjunction: true } conjunction
            ? conjunction.Operands
            : [formula];
        foreach (Formula operand in operands)
        {
            budget.Charge();
            if (guardMatcher(operand))
            {
                if (foundGuard)
                {
                    return false;
                }

                foundGuard = true;
                continue;
            }

            if (!IsAlwaysTrue(operand, budget))
            {
                return false;
            }
        }

        return foundGuard;
    }

    public static bool MatchesRequiredGuards(
        Formula formula,
        IEnumerable<Formula> requiredGuards,
        ResourceBudget budget)
    {
        budget.Charge();
        var remaining = requiredGuards.ToDictionary(
            static guard => guard.Canonical,
            static guard => guard,
            StringComparer.Ordinal);
        IEnumerable<Formula> operands = formula is JunctionFormula { IsConjunction: true } conjunction
            ? conjunction.Operands
            : [formula];
        foreach (Formula operand in operands)
        {
            budget.Charge();
            if (remaining.Remove(operand.Canonical))
            {
                continue;
            }

            if (!IsAlwaysTrue(operand, budget))
            {
                return false;
            }
        }

        return remaining.Count == 0;
    }

    public static bool TryEvaluateConstant(
        Formula formula,
        ResourceBudget budget,
        out bool value)
    {
        budget.Charge();
        switch (formula)
        {
            case BooleanFormula boolean:
                value = boolean.Value;
                return true;
            case ComparisonFormula comparison:
                if (ExactScalar.TryCreate(comparison.Left, budget, out ExactScalar left) &&
                    ExactScalar.TryCreate(comparison.Right, budget, out ExactScalar right) &&
                    ExactScalar.TryAdd(left, right.Negate(), budget, out ExactScalar difference))
                {
                    value = CompareSign(difference.Sign, comparison.Comparison);
                    return true;
                }

                break;
            case PredicateFormula { Terms.Length: 1 } predicate:
                if (ExactScalar.TryCreate(predicate.Terms[0], budget, out ExactScalar scalar) &&
                    scalar.RationalValue is { } rational)
                {
                    value = predicate.Kind switch
                    {
                        ExactPredicate.IsInteger => rational.IsInteger,
                        ExactPredicate.IsOddInteger =>
                            rational.IsInteger && !rational.Numerator.IsEven,
                        _ => false
                    };
                    return predicate.Kind is ExactPredicate.IsInteger or ExactPredicate.IsOddInteger;
                }

                break;
            case NotFormula not:
                if (TryEvaluateConstant(not.Operand, budget, out bool negated))
                {
                    value = !negated;
                    return true;
                }

                break;
            case JunctionFormula junction:
                bool aggregate = junction.IsConjunction;
                foreach (Formula operand in junction.Operands)
                {
                    if (!TryEvaluateConstant(operand, budget, out bool operandValue))
                    {
                        value = false;
                        return false;
                    }

                    aggregate = junction.IsConjunction
                        ? aggregate && operandValue
                        : aggregate || operandValue;
                }

                value = aggregate;
                return true;
        }

        value = false;
        return false;
    }

    private static bool CompareSign(int sign, Comparison comparison) => comparison switch
    {
        Comparison.Equal => sign == 0,
        Comparison.NotEqual => sign != 0,
        Comparison.Less => sign < 0,
        Comparison.LessOrEqual => sign <= 0,
        Comparison.Greater => sign > 0,
        Comparison.GreaterOrEqual => sign >= 0,
        _ => throw new ArgumentOutOfRangeException(nameof(comparison))
    };
}
