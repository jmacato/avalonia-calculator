using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class FourierReducer
{
    public static bool TryReduce(ValueTerm term, string variable, ResourceBudget budget, out FourierPolynomial polynomial)
    {
        budget.Charge();
        switch (term.Kind)
        {
            case ValueKind.Constant:
                polynomial = From((0, new GaussianRational(term.Constant, BigRational.Zero)));
                return true;
            case ValueKind.Negate:
                if (TryReduce(term.Operands[0], variable, budget, out FourierPolynomial negated))
                {
                    polynomial = Map(negated, static value => -value);
                    return true;
                }

                break;
            case ValueKind.Add:
            case ValueKind.Subtract:
            case ValueKind.Multiply:
                if (TryReduce(term.Operands[0], variable, budget, out FourierPolynomial left) && TryReduce(term.Operands[1], variable, budget, out FourierPolynomial right))
                {
                    polynomial = term.Kind switch
                    {
                        ValueKind.Add => Add(left, right, subtract: false),
                        ValueKind.Subtract => Add(left, right, subtract: true),
                        ValueKind.Multiply => Multiply(left, right, budget),
                        _ => throw new InvalidOperationException()
                    };
                    return true;
                }

                break;
            case ValueKind.Power when term.Operands[1].Kind == ValueKind.Constant && term.Operands[1].Constant.IsInteger && term.Operands[1].Constant.Sign >= 0 && term.Operands[1].Constant.Numerator <= int.MaxValue:
                if (TryReduce(term.Operands[0], variable, budget, out FourierPolynomial basis))
                {
                    polynomial = Pow(basis, (int)term.Operands[1].Constant.Numerator, budget);
                    return true;
                }

                break;
            case ValueKind.Function when IntegerHarmonic.TryExtract(term, variable, budget, out IntegerHarmonic harmonic):
                polynomial = Harmonic(harmonic);
                return true;
        }

        polynomial = null!;
        return false;
    }

    private static FourierPolynomial Add(FourierPolynomial left, FourierPolynomial right, bool subtract)
    {
        var result = left.Coefficients.ToBuilder();
        foreach ((int frequency, GaussianRational coefficient) in right.Coefficients)
        {
            GaussianRational value = subtract ? -coefficient : coefficient;
            result[frequency] = result.TryGetValue(frequency, out GaussianRational existing) ? existing + value : value;
            if (result[frequency].IsZero)
            {
                result.Remove(frequency);
            }
        }

        return new FourierPolynomial(result.ToImmutable());
    }

    private static FourierPolynomial Multiply(FourierPolynomial left, FourierPolynomial right, ResourceBudget budget)
    {
        var result = ImmutableSortedDictionary.CreateBuilder<int, GaussianRational>();
        foreach ((int leftFrequency, GaussianRational leftCoefficient) in left.Coefficients)
        {
            foreach ((int rightFrequency, GaussianRational rightCoefficient) in right.Coefficients)
            {
                budget.Charge();
                int frequency = checked(leftFrequency + rightFrequency);
                GaussianRational product = leftCoefficient * rightCoefficient;
                result[frequency] = result.TryGetValue(frequency, out GaussianRational existing) ? existing + product : product;
                if (result[frequency].IsZero)
                {
                    result.Remove(frequency);
                }
            }
        }

        if (result.Count > AnalysisLimits.Monomials)
        {
            throw new BudgetExceededException(nameof(AnalysisLimits.Monomials));
        }

        return new FourierPolynomial(result.ToImmutable());
    }

    private static FourierPolynomial Pow(FourierPolynomial basis, int exponent, ResourceBudget budget)
    {
        FourierPolynomial result = From((0, new GaussianRational(BigRational.One, BigRational.Zero)));
        FourierPolynomial factor = basis;
        int remaining = exponent;
        while (remaining > 0)
        {
            if ((remaining & 1) != 0)
            {
                result = Multiply(result, factor, budget);
            }

            remaining >>= 1;
            if (remaining > 0)
            {
                factor = Multiply(factor, factor, budget);
            }
        }

        return result;
    }

    private static FourierPolynomial Map(FourierPolynomial source, Func<GaussianRational, GaussianRational> transform)
    {
        return new FourierPolynomial(source.Coefficients.ToImmutableSortedDictionary(static item => item.Key,
            item => transform(item.Value)));
    }

    private static FourierPolynomial From(params (int Frequency, GaussianRational Coefficient)[] values)
    {
        return new FourierPolynomial(values.Where(static item => !item.Coefficient.IsZero)
            .ToImmutableSortedDictionary(static item => item.Frequency, static item => item.Coefficient));
    }

    private static FourierPolynomial Harmonic(IntegerHarmonic harmonic)
    {
        int frequency = harmonic.Frequency;
        if (frequency == 0)
        {
            return harmonic.Function == "sin" ? From() : From((0, new GaussianRational(BigRational.One, BigRational.Zero)));
        }

        return harmonic.Function == "sin" ? From((frequency, new GaussianRational(BigRational.Zero, new BigRational(-1, 2))), (-frequency, new GaussianRational(BigRational.Zero, new BigRational(1, 2)))) : From((frequency, new GaussianRational(new BigRational(1, 2), BigRational.Zero)), (-frequency, new GaussianRational(new BigRational(1, 2), BigRational.Zero)));
    }
}
