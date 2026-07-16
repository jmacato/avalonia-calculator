using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record RationalFunction(
    UnivariatePolynomial Numerator,
    UnivariatePolynomial Denominator)
{
    public static RationalFunction Constant(BigRational value, ResourceBudget budget) =>
        new(UnivariatePolynomial.Create([value], budget), UnivariatePolynomial.One);

    public static RationalFunction Variable { get; } =
        new(UnivariatePolynomial.Variable, UnivariatePolynomial.One);

    public RationalFunction Add(RationalFunction other, ResourceBudget budget) =>
        CreateReduced(
            Numerator.Multiply(other.Denominator, budget)
                .Add(other.Numerator.Multiply(Denominator, budget), budget),
            Denominator.Multiply(other.Denominator, budget),
            budget);

    public RationalFunction Subtract(RationalFunction other, ResourceBudget budget) =>
        CreateReduced(
            Numerator.Multiply(other.Denominator, budget)
                .Subtract(other.Numerator.Multiply(Denominator, budget), budget),
            Denominator.Multiply(other.Denominator, budget),
            budget);

    public RationalFunction Multiply(RationalFunction other, ResourceBudget budget) =>
        CreateReduced(
            Numerator.Multiply(other.Numerator, budget),
            Denominator.Multiply(other.Denominator, budget),
            budget);

    public RationalFunction Divide(RationalFunction other, ResourceBudget budget)
    {
        if (other.Numerator.IsZero)
        {
            throw new DivideByZeroException();
        }

        return CreateReduced(
            Numerator.Multiply(other.Denominator, budget),
            Denominator.Multiply(other.Numerator, budget),
            budget);
    }

    public RationalFunction Negate(ResourceBudget budget) =>
        new(Numerator.Negate(budget), Denominator);

    public RationalFunction Pow(int exponent, ResourceBudget budget)
    {
        if (exponent >= 0)
        {
            return CreateReduced(
                Numerator.Pow(exponent, budget),
                Denominator.Pow(exponent, budget),
                budget);
        }

        if (Numerator.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (exponent == int.MinValue)
        {
            throw new BudgetExceededException(nameof(AnalysisLimits.UnivariateDegree));
        }

        int positive = -exponent;
        return CreateReduced(
            Denominator.Pow(positive, budget),
            Numerator.Pow(positive, budget),
            budget);
    }

    public RationalFunction Derivative(ResourceBudget budget)
    {
        UnivariatePolynomial numerator = Numerator.Derivative(budget)
            .Multiply(Denominator, budget)
            .Subtract(Numerator.Multiply(Denominator.Derivative(budget), budget), budget);
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

    public static RationalFunction CreateReduced(
        UnivariatePolynomial numerator,
        UnivariatePolynomial denominator,
        ResourceBudget budget)
    {
        if (denominator.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (numerator.IsZero)
        {
            return new RationalFunction(UnivariatePolynomial.Zero, UnivariatePolynomial.One);
        }

        UnivariatePolynomial gcd = UnivariatePolynomial.GreatestCommonDivisor(
            numerator,
            denominator,
            budget);
        (UnivariatePolynomial reducedNumerator, UnivariatePolynomial numeratorRemainder) =
            numerator.Divide(gcd, budget);
        (UnivariatePolynomial reducedDenominator, UnivariatePolynomial denominatorRemainder) =
            denominator.Divide(gcd, budget);
        if (!numeratorRemainder.IsZero || !denominatorRemainder.IsZero)
        {
            throw new InvalidOperationException("The exact rational-function gcd did not divide both terms.");
        }

        BigRational scale = reducedDenominator.LeadingCoefficient.Reciprocal();
        return new RationalFunction(
            reducedNumerator.Multiply(scale, budget),
            reducedDenominator.Multiply(scale, budget));
    }
}

internal sealed record RationalExtraction(
    RationalFunction Function,
    ImmutableArray<UnivariatePolynomial> DomainExclusions);

internal static class RationalFunctionExtractor
{
    public static bool TryExtract(
        ValueTerm term,
        string variable,
        ResourceBudget budget,
        out RationalExtraction extraction)
    {
        var exclusions = ImmutableArray.CreateBuilder<UnivariatePolynomial>();
        if (!TryExtractCore(term, variable, exclusions, budget, out RationalFunction? function))
        {
            extraction = null!;
            return false;
        }

        extraction = new RationalExtraction(
            function,
            exclusions
                .Where(static polynomial => !polynomial.IsConstant)
                .DistinctBy(static polynomial => polynomial.Canonical)
                .OrderBy(static polynomial => polynomial.Canonical, StringComparer.Ordinal)
                .ToImmutableArray());
        return true;
    }

    private static bool TryExtractCore(
        ValueTerm term,
        string variable,
        ImmutableArray<UnivariatePolynomial>.Builder exclusions,
        ResourceBudget budget,
        out RationalFunction function)
    {
        budget.Charge();
        switch (term.Kind)
        {
            case ValueKind.Constant:
                function = RationalFunction.Constant(term.Constant, budget);
                return true;
            case ValueKind.Variable when term.Name.Equals(variable, StringComparison.OrdinalIgnoreCase):
                function = RationalFunction.Variable;
                return true;
            case ValueKind.Negate:
                if (TryExtractCore(term.Operands[0], variable, exclusions, budget, out RationalFunction negated))
                {
                    function = negated.Negate(budget);
                    return true;
                }

                break;
            case ValueKind.Add:
            case ValueKind.Subtract:
            case ValueKind.Multiply:
            case ValueKind.Divide:
                if (TryExtractCore(term.Operands[0], variable, exclusions, budget, out RationalFunction left) &&
                    TryExtractCore(term.Operands[1], variable, exclusions, budget, out RationalFunction right))
                {
                    if (term.Kind == ValueKind.Divide)
                    {
                        exclusions.Add(right.Numerator);
                    }

                    function = term.Kind switch
                    {
                        ValueKind.Add => left.Add(right, budget),
                        ValueKind.Subtract => left.Subtract(right, budget),
                        ValueKind.Multiply => left.Multiply(right, budget),
                        ValueKind.Divide when !right.Numerator.IsZero => left.Divide(right, budget),
                        _ => null!
                    };
                    return function is not null;
                }

                break;
            case ValueKind.Power when
                term.Operands[1].Kind == ValueKind.Constant &&
                term.Operands[1].Constant.IsInteger &&
                term.Operands[1].Constant.Numerator >= int.MinValue &&
                term.Operands[1].Constant.Numerator <= int.MaxValue:
                if (TryExtractCore(term.Operands[0], variable, exclusions, budget, out RationalFunction basis))
                {
                    int exponent = (int)term.Operands[1].Constant.Numerator;
                    if (exponent <= 0)
                    {
                        exclusions.Add(basis.Numerator);
                    }

                    function = basis.Pow(exponent, budget);
                    return true;
                }

                break;
        }

        function = null!;
        return false;
    }
}
