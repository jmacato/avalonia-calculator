using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class HalfAngleReducer
{
    public static bool TryReduce(ValueTerm term, string variable, ResourceBudget budget, out RationalFunction function)
    {
        budget.Charge();
        switch (term.Kind)
        {
            case ValueKind.Constant:
                function = RationalFunction.Constant(term.Constant, budget);
                return true;
            case ValueKind.Negate:
                if (TryReduce(term.Operands[0], variable, budget, out RationalFunction negated))
                {
                    function = negated.Negate(budget);
                    return true;
                }

                break;
            case ValueKind.Add:
            case ValueKind.Subtract:
            case ValueKind.Multiply:
                if (TryReduce(term.Operands[0], variable, budget, out RationalFunction left) && TryReduce(term.Operands[1], variable, budget, out RationalFunction right))
                {
                    function = term.Kind switch
                    {
                        ValueKind.Add => left.Add(right, budget),
                        ValueKind.Subtract => left.Subtract(right, budget),
                        ValueKind.Multiply => left.Multiply(right, budget),
                        _ => throw new InvalidOperationException()
                    };
                    return true;
                }

                break;
            case ValueKind.Power when term.Operands[1].Kind == ValueKind.Constant && term.Operands[1].Constant.IsInteger && term.Operands[1].Constant.Sign >= 0 && term.Operands[1].Constant.Numerator <= int.MaxValue:
                if (TryReduce(term.Operands[0], variable, budget, out RationalFunction basis))
                {
                    function = basis.Pow((int)term.Operands[1].Constant.Numerator, budget);
                    return true;
                }

                break;
            case ValueKind.Function when IntegerHarmonic.TryExtract(term, variable, budget, out IntegerHarmonic harmonic):
                (RationalFunction sine, RationalFunction cosine) = MultipleAngle(harmonic.Frequency, budget);
                function = harmonic.Function == "sin" ? sine : cosine;
                return true;
        }

        function = null!;
        return false;
    }

    private static RationalFunction Sine(ResourceBudget budget) => RationalFunction.CreateReduced(UnivariatePolynomial.Create([BigRational.Zero, new BigRational(2)], budget), UnivariatePolynomial.Create([BigRational.One, BigRational.Zero, BigRational.One], budget), budget);
    private static RationalFunction Cosine(ResourceBudget budget) => RationalFunction.CreateReduced(UnivariatePolynomial.Create([BigRational.One, BigRational.Zero, BigRational.MinusOne], budget), UnivariatePolynomial.Create([BigRational.One, BigRational.Zero, BigRational.One], budget), budget);
    private static (RationalFunction Sine, RationalFunction Cosine) MultipleAngle(int frequency, ResourceBudget budget)
    {
        RationalFunction zero = RationalFunction.Constant(BigRational.Zero, budget);
        RationalFunction one = RationalFunction.Constant(BigRational.One, budget);
        if (frequency == 0)
        {
            return (zero, one);
        }

        int remaining = Math.Abs(frequency);
        RationalFunction resultSine = zero;
        RationalFunction resultCosine = one;
        RationalFunction factorSine = Sine(budget);
        RationalFunction factorCosine = Cosine(budget);
        while (remaining > 0)
        {
            budget.Charge();
            if ((remaining & 1) != 0)
            {
                (resultSine, resultCosine) = AddAngles(resultSine, resultCosine, factorSine, factorCosine, budget);
            }

            remaining >>= 1;
            if (remaining > 0)
            {
                (factorSine, factorCosine) = AddAngles(factorSine, factorCosine, factorSine, factorCosine, budget);
            }
        }

        return frequency < 0 ? (resultSine.Negate(budget), resultCosine) : (resultSine, resultCosine);
    }

    private static (RationalFunction Sine, RationalFunction Cosine) AddAngles(RationalFunction leftSine, RationalFunction leftCosine, RationalFunction rightSine, RationalFunction rightCosine, ResourceBudget budget)
    {
        RationalFunction sine = leftSine.Multiply(rightCosine, budget).Add(leftCosine.Multiply(rightSine, budget), budget);
        RationalFunction cosine = leftCosine.Multiply(rightCosine, budget).Subtract(leftSine.Multiply(rightSine, budget), budget);
        return (sine, cosine);
    }
}
