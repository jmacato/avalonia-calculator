namespace Graphing.Symbolics;

internal static class ExactFormulaAtRational
{
    public static bool TryEvaluate(Formula formula, string variable, BigRational variableValue, ResourceBudget budget, out bool value)
    {
        budget.Charge();
        switch (formula)
        {
            case BooleanFormula boolean:
                value = boolean.Value;
                return true;
            case ComparisonFormula comparison when ExactTermEvaluator.TryEvaluate(comparison.Left, variable, variableValue, budget, out BigRational left) && ExactTermEvaluator.TryEvaluate(comparison.Right, variable, variableValue, budget, out BigRational right):
                value = Compare(left, comparison.Comparison, right);
                return true;
            case NotFormula not when TryEvaluate(not.Operand, variable, variableValue, budget, out bool operand):
                value = !operand;
                return true;
            case JunctionFormula junction:
                return TryEvaluateJunction(junction, variable, variableValue, budget, out value);
            default:
                value = false;
                return false;
        }
    }

    private static bool TryEvaluateJunction(JunctionFormula junction, string variable, BigRational variableValue, ResourceBudget budget, out bool value)
    {
        value = junction.IsConjunction;
        foreach (Formula operand in junction.Operands)
        {
            if (!TryEvaluate(operand, variable, variableValue, budget, out bool operandValue))
            {
                value = false;
                return false;
            }

            value = junction.IsConjunction ? value && operandValue : value || operandValue;
        }

        return true;
    }

    private static bool Compare(BigRational left, Comparison comparison, BigRational right)
    {
        return comparison switch
        {
            Comparison.Equal => left == right,
            Comparison.NotEqual => left != right,
            Comparison.Less => left < right,
            Comparison.LessOrEqual => left <= right,
            Comparison.Greater => left > right,
            Comparison.GreaterOrEqual => left >= right,
            _ => throw new ArgumentOutOfRangeException(nameof(comparison))
        };
    }
}
