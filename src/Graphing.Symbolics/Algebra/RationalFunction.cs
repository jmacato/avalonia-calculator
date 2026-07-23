namespace Graphing.Symbolics;

internal sealed record RationalFunction(UnivariatePolynomial Numerator, UnivariatePolynomial Denominator)
{
    public static RationalFunction Constant(BigRational value, ResourceBudget budget)
    {
        return new RationalFunction(UnivariatePolynomial.Create([value], budget), UnivariatePolynomial.One);
    }

    public static RationalFunction Variable { get; } = new(UnivariatePolynomial.Variable, UnivariatePolynomial.One);

    public RationalFunction Add(RationalFunction other, ResourceBudget budget)
    {
        return CreateReduced(
            Numerator.Multiply(other.Denominator, budget).Add(other.Numerator.Multiply(Denominator, budget), budget),
            Denominator.Multiply(other.Denominator, budget), budget);
    }

    public RationalFunction Subtract(RationalFunction other, ResourceBudget budget)
    {
        return CreateReduced(
            Numerator.Multiply(other.Denominator, budget)
                .Subtract(other.Numerator.Multiply(Denominator, budget), budget),
            Denominator.Multiply(other.Denominator, budget), budget);
    }

    public RationalFunction Multiply(RationalFunction other, ResourceBudget budget)
    {
        return CreateReduced(Numerator.Multiply(other.Numerator, budget),
            Denominator.Multiply(other.Denominator, budget), budget);
    }

    public RationalFunction Divide(RationalFunction other, ResourceBudget budget)
    {
        if (other.Numerator.IsZero)
        {
            throw new DivideByZeroException();
        }

        return CreateReduced(Numerator.Multiply(other.Denominator, budget), Denominator.Multiply(other.Numerator, budget), budget);
    }

    public RationalFunction Negate(ResourceBudget budget)
    {
        return new RationalFunction(Numerator.Negate(budget), Denominator);
    }

    public RationalFunction Pow(int exponent, ResourceBudget budget)
    {
        if (exponent >= 0)
        {
            return CreateReduced(Numerator.Pow(exponent, budget), Denominator.Pow(exponent, budget), budget);
        }

        if (Numerator.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (exponent == int.MinValue)
        {
            throw BudgetExceededException.ForLimit(nameof(AnalysisLimits.UnivariateDegree));
        }

        int positive = -exponent;
        return CreateReduced(Denominator.Pow(positive, budget), Numerator.Pow(positive, budget), budget);
    }

    public RationalFunction Derivative(ResourceBudget budget)
    {
        UnivariatePolynomial numerator = Numerator.Derivative(budget).Multiply(Denominator, budget).Subtract(Numerator.Multiply(Denominator.Derivative(budget), budget), budget);
        return CreateReduced(numerator, Denominator.Pow(2, budget), budget);
    }

    public BigRational Evaluate(BigRational value, ResourceBudget budget)
    {
        BigRational denominator = Denominator.Evaluate(value, budget);
        if (denominator.IsZero)
        {
            throw new DivideByZeroException();
        }

        return Numerator.Evaluate(value, budget) / denominator;
    }

    public static RationalFunction CreateReduced(UnivariatePolynomial numerator, UnivariatePolynomial denominator, ResourceBudget budget)
    {
        if (denominator.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (numerator.IsZero)
        {
            return new RationalFunction(UnivariatePolynomial.Zero, UnivariatePolynomial.One);
        }

        UnivariatePolynomial gcd = UnivariatePolynomial.GreatestCommonDivisor(numerator, denominator, budget);
        (UnivariatePolynomial reducedNumerator, UnivariatePolynomial numeratorRemainder) = numerator.Divide(gcd, budget);
        (UnivariatePolynomial reducedDenominator, UnivariatePolynomial denominatorRemainder) = denominator.Divide(gcd, budget);
        if (!numeratorRemainder.IsZero || !denominatorRemainder.IsZero)
        {
            throw new InvalidOperationException("The exact rational-function gcd did not divide both terms.");
        }

        BigRational scale = reducedDenominator.LeadingCoefficient.Reciprocal();
        return new RationalFunction(reducedNumerator.Multiply(scale, budget), reducedDenominator.Multiply(scale, budget));
    }
}
