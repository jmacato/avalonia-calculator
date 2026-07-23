namespace Graphing.Symbolics;

internal static class ExactTermEvaluator
{
    public static bool TryEvaluate(ValueTerm term, string variable, BigRational variableValue, ResourceBudget budget, out BigRational value)
    {
        budget.Charge();
        switch (term.Kind)
        {
            case ValueKind.Constant:
                value = term.Constant;
                return true;
            case ValueKind.Variable when term.Name.Equals(variable, StringComparison.OrdinalIgnoreCase):
                value = variableValue;
                return true;
            case ValueKind.Negate:
                if (TryEvaluate(term.Operands[0], variable, variableValue, budget, out BigRational negated))
                {
                    value = -negated;
                    return true;
                }

                break;
            case ValueKind.Add:
            case ValueKind.Subtract:
            case ValueKind.Multiply:
            case ValueKind.Divide:
                if (TryEvaluate(term.Operands[0], variable, variableValue, budget, out BigRational left) && TryEvaluate(term.Operands[1], variable, variableValue, budget, out BigRational right) && (term.Kind != ValueKind.Divide || !right.IsZero))
                {
                    value = term.Kind switch
                    {
                        ValueKind.Add => left + right,
                        ValueKind.Subtract => left - right,
                        ValueKind.Multiply => left * right,
                        ValueKind.Divide => left / right,
                        _ => throw new InvalidOperationException()
                    };
                    return true;
                }

                break;
            case ValueKind.Power:
                if (TryEvaluate(term.Operands[0], variable, variableValue, budget, out BigRational basis) && TryEvaluate(term.Operands[1], variable, variableValue, budget, out BigRational exponent) && exponent.IsInteger && exponent.Numerator >= int.MinValue && exponent.Numerator <= int.MaxValue && (!basis.IsZero || exponent.Sign > 0))
                {
                    value = basis.Pow((int)exponent.Numerator);
                    return true;
                }

                break;
            case ValueKind.Function:
                if (TryFunction(term, variable, variableValue, budget, out value))
                {
                    return true;
                }

                break;
        }

        value = default;
        return false;
    }

    private static bool TryFunction(ValueTerm term, string variable, BigRational variableValue, ResourceBudget budget, out BigRational value)
    {
        if (term.Operands.Length != 1 || !TryEvaluate(term.Operands[0], variable, variableValue, budget, out BigRational argument))
        {
            value = default;
            return false;
        }

        switch (term.Name)
        {
            case "abs":
                value = argument.Abs();
                return true;
            case "sqrt" when BigRational.TrySquareRoot(argument, out BigRational squareRoot):
                value = squareRoot;
                return true;
            case "sin" when argument.IsZero:
            case "tan" when argument.IsZero:
            case "asin" when argument.IsZero:
            case "atan" when argument.IsZero:
                value = BigRational.Zero;
                return true;
            case "cos" when argument.IsZero:
            case "exp" when argument.IsZero:
                value = BigRational.One;
                return true;
            case "log" when argument.IsOne:
            case "ln" when argument.IsOne:
                value = BigRational.Zero;
                return true;
            default:
                value = default;
                return false;
        }
    }
}
