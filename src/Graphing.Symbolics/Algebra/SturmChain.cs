using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed class SturmChain
{
    private SturmChain(UnivariatePolynomial polynomial, ImmutableArray<UnivariatePolynomial> sequence)
    {
        Polynomial = polynomial;
        Sequence = sequence;
    }

    public UnivariatePolynomial Polynomial { get; }
    public ImmutableArray<UnivariatePolynomial> Sequence { get; }

    public static SturmChain Create(UnivariatePolynomial polynomial, ResourceBudget budget)
    {
        if (polynomial.IsZero)
        {
            throw new ArgumentException("The zero polynomial has no Sturm chain.", nameof(polynomial));
        }

        UnivariatePolynomial squareFree = polynomial.SquareFreePart(budget).PrimitivePositive(budget);
        var sequence = ImmutableArray.CreateBuilder<UnivariatePolynomial>();
        sequence.Add(squareFree);
        UnivariatePolynomial derivative = squareFree.Derivative(budget);
        if (!derivative.IsZero)
        {
            sequence.Add(derivative);
        }

        while (!derivative.IsZero)
        {
            budget.Charge();
            UnivariatePolynomial previous = sequence[^2];
            (_, UnivariatePolynomial remainder) = previous.Divide(derivative, budget);
            if (remainder.IsZero)
            {
                break;
            }

            derivative = remainder.Negate(budget);
            sequence.Add(derivative);
        }

        return new SturmChain(squareFree, sequence.ToImmutable());
    }

    public int Variations(BigRational point, ResourceBudget budget)
    {
        int variations = 0;
        int previousSign = 0;
        foreach (UnivariatePolynomial polynomial in Sequence)
        {
            int sign = polynomial.Evaluate(point, budget).Sign;
            if (sign == 0)
            {
                continue;
            }

            if (previousSign != 0 && sign != previousSign)
            {
                variations++;
            }

            previousSign = sign;
        }

        return variations;
    }

    public int CountRoots(BigRational lower, BigRational upper, ResourceBudget budget)
    {
        if (lower >= upper)
        {
            return 0;
        }

        if (Polynomial.Evaluate(lower, budget).IsZero || Polynomial.Evaluate(upper, budget).IsZero)
        {
            throw new ArgumentException("Sturm interval endpoints must not be roots.");
        }

        return Variations(lower, budget) - Variations(upper, budget);
    }

    public bool IsValid(ResourceBudget budget)
    {
        if (Sequence.IsEmpty || !Sequence[0].Equals(Polynomial))
        {
            return false;
        }

        if (Sequence.Length == 1)
        {
            return Polynomial.Degree == 0;
        }

        if (!Sequence[1].Equals(Polynomial.Derivative(budget)))
        {
            return false;
        }

        for (int index = 2; index < Sequence.Length; index++)
        {
            (_, UnivariatePolynomial remainder) = Sequence[index - 2].Divide(Sequence[index - 1], budget);
            if (!Sequence[index].Equals(remainder.Negate(budget)))
            {
                return false;
            }
        }

        return true;
    }
}
