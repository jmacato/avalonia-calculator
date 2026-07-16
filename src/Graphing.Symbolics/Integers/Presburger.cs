using System.Collections.Immutable;
using System.Numerics;

namespace Graphing.Symbolics;

internal sealed record LinearIntegerExpression(
    ImmutableSortedDictionary<string, BigInteger> Coefficients,
    BigInteger Constant)
{
    public static LinearIntegerExpression From(
        BigInteger constant,
        params (string Variable, BigInteger Coefficient)[] terms) =>
        new(
            terms
                .Where(static term => !term.Coefficient.IsZero)
                .GroupBy(static term => term.Variable, StringComparer.Ordinal)
                .Select(static group => new KeyValuePair<string, BigInteger>(
                    group.Key,
                    group.Aggregate(
                        BigInteger.Zero,
                        static (sum, term) => sum + term.Coefficient)))
                .Where(static term => !term.Value.IsZero)
                .ToImmutableSortedDictionary(
                    static term => term.Key,
                    static term => term.Value,
                    StringComparer.Ordinal),
            constant);

    public BigInteger Coefficient(string variable) =>
        Coefficients.TryGetValue(variable, out BigInteger value) ? value : BigInteger.Zero;

    public LinearIntegerExpression Without(string variable) =>
        this with { Coefficients = Coefficients.Remove(variable) };

    public LinearIntegerExpression Add(LinearIntegerExpression other)
    {
        var coefficients = Coefficients.ToBuilder();
        foreach ((string variable, BigInteger coefficient) in other.Coefficients)
        {
            coefficients[variable] = coefficients.TryGetValue(variable, out BigInteger existing)
                ? existing + coefficient
                : coefficient;
            if (coefficients[variable].IsZero)
            {
                coefficients.Remove(variable);
            }
        }

        return new LinearIntegerExpression(coefficients.ToImmutable(), Constant + other.Constant);
    }

    public LinearIntegerExpression Scale(BigInteger factor) => new(
        Coefficients
            .Where(term => !(term.Value * factor).IsZero)
            .ToImmutableSortedDictionary(
                static term => term.Key,
                term => term.Value * factor,
                StringComparer.Ordinal),
        Constant * factor);
}

internal enum IntegerRelation
{
    Equal,
    NotEqual,
    LessOrEqual
}

internal abstract record PresburgerFormula;

internal sealed record PresburgerBoolean(bool Value) : PresburgerFormula;

internal sealed record PresburgerComparison(
    LinearIntegerExpression Expression,
    IntegerRelation Relation) : PresburgerFormula;

internal sealed record PresburgerDivisibility(
    BigInteger Divisor,
    LinearIntegerExpression Expression) : PresburgerFormula;

internal sealed record PresburgerNot(PresburgerFormula Operand) : PresburgerFormula;

internal sealed record PresburgerJunction(
    bool IsConjunction,
    ImmutableArray<PresburgerFormula> Operands) : PresburgerFormula;

internal static class PresburgerNormalizer
{
    public static PresburgerFormula And(IEnumerable<PresburgerFormula> operands)
    {
        var flattened = new List<PresburgerFormula>();
        foreach (PresburgerFormula operand in operands)
        {
            if (operand is PresburgerBoolean { Value: false })
            {
                return new PresburgerBoolean(false);
            }

            if (operand is PresburgerBoolean { Value: true })
            {
                continue;
            }

            if (operand is PresburgerJunction { IsConjunction: true } conjunction)
            {
                flattened.AddRange(conjunction.Operands);
            }
            else
            {
                flattened.Add(operand);
            }
        }

        return flattened.Count switch
        {
            0 => new PresburgerBoolean(true),
            1 => flattened[0],
            _ => new PresburgerJunction(true, flattened.ToImmutableArray())
        };
    }

    public static PresburgerFormula Normalize(PresburgerFormula formula) => formula switch
    {
        PresburgerDivisibility divisibility when divisibility.Divisor.IsZero =>
            new PresburgerComparison(divisibility.Expression, IntegerRelation.Equal),
        PresburgerDivisibility divisibility when divisibility.Divisor.Sign < 0 =>
            divisibility with { Divisor = BigInteger.Abs(divisibility.Divisor) },
        PresburgerJunction { IsConjunction: true } conjunction =>
            And(conjunction.Operands.Select(Normalize)),
        PresburgerJunction junction => junction with
        {
            Operands = junction.Operands.Select(Normalize).ToImmutableArray()
        },
        PresburgerNot not => not with { Operand = Normalize(not.Operand) },
        _ => formula
    };
}

internal static class CooperEliminator
{
    public static bool TryEliminateExists(
        string variable,
        PresburgerFormula source,
        ResourceBudget budget,
        out PresburgerFormula result)
    {
        PresburgerFormula normalized = PresburgerNormalizer.Normalize(source);
        ImmutableArray<PresburgerFormula> operands = normalized is PresburgerJunction
            {
                IsConjunction: true
            } conjunction
            ? conjunction.Operands
            : [normalized];

        PresburgerComparison? equality = operands
            .OfType<PresburgerComparison>()
            .FirstOrDefault(comparison =>
                comparison.Relation == IntegerRelation.Equal &&
                !comparison.Expression.Coefficient(variable).IsZero);
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

    private static PresburgerFormula EliminateWithEquality(
        string variable,
        PresburgerComparison equality,
        ImmutableArray<PresburgerFormula> operands,
        ResourceBudget budget)
    {
        BigInteger coefficient = equality.Expression.Coefficient(variable);
        LinearIntegerExpression remainder = equality.Expression.Without(variable);
        BigInteger positiveCoefficient = BigInteger.Abs(coefficient);
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
                    result.Add(SubstituteComparison(
                        variable,
                        coefficient,
                        remainder,
                        comparison));
                    break;
                case PresburgerDivisibility divisibility:
                    result.Add(SubstituteDivisibility(
                        variable,
                        coefficient,
                        remainder,
                        divisibility));
                    break;
                default:
                    throw new InvalidOperationException(
                        "Normalized equality elimination received a non-atomic conjunction.");
            }
        }

        return PresburgerNormalizer.And(result);
    }

    private static PresburgerFormula SubstituteComparison(
        string variable,
        BigInteger equalityCoefficient,
        LinearIntegerExpression equalityRemainder,
        PresburgerComparison comparison)
    {
        BigInteger coefficient = comparison.Expression.Coefficient(variable);
        LinearIntegerExpression remainder = comparison.Expression.Without(variable);
        LinearIntegerExpression substituted = remainder.Scale(equalityCoefficient)
            .Add(equalityRemainder.Scale(-coefficient));
        if (equalityCoefficient.Sign < 0 && comparison.Relation == IntegerRelation.LessOrEqual)
        {
            substituted = substituted.Scale(BigInteger.MinusOne);
        }

        return new PresburgerComparison(substituted, comparison.Relation);
    }

    private static PresburgerFormula SubstituteDivisibility(
        string variable,
        BigInteger equalityCoefficient,
        LinearIntegerExpression equalityRemainder,
        PresburgerDivisibility divisibility)
    {
        BigInteger coefficient = divisibility.Expression.Coefficient(variable);
        LinearIntegerExpression remainder = divisibility.Expression.Without(variable);
        LinearIntegerExpression substituted = remainder.Scale(equalityCoefficient)
            .Add(equalityRemainder.Scale(-coefficient));
        return new PresburgerDivisibility(
            BigInteger.Abs(divisibility.Divisor * equalityCoefficient),
            substituted);
    }

    private static bool TryEliminateUnitBounds(
        string variable,
        ImmutableArray<PresburgerFormula> operands,
        ResourceBudget budget,
        out PresburgerFormula result)
    {
        var lowers = new List<LinearIntegerExpression>();
        var uppers = new List<LinearIntegerExpression>();
        var independent = new List<PresburgerFormula>();
        foreach (PresburgerFormula operand in operands)
        {
            budget.Charge();
            if (operand is not PresburgerComparison
                {
                    Relation: IntegerRelation.LessOrEqual
                } comparison)
            {
                result = null!;
                return false;
            }

            BigInteger coefficient = comparison.Expression.Coefficient(variable);
            LinearIntegerExpression remainder = comparison.Expression.Without(variable);
            if (coefficient.IsZero)
            {
                independent.Add(comparison);
            }
            else if (coefficient.IsOne)
            {
                uppers.Add(remainder);
            }
            else if (coefficient == BigInteger.MinusOne)
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
                independent.Add(new PresburgerComparison(
                    lower.Add(upper),
                    IntegerRelation.LessOrEqual));
            }
        }

        result = PresburgerNormalizer.And(independent);
        return true;
    }
}
