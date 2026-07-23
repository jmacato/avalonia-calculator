using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record LinearIntegerExpression(ImmutableSortedDictionary<string, ExactInteger> Coefficients, ExactInteger Constant)
{
    public static LinearIntegerExpression From(ExactInteger constant, params (string Variable, ExactInteger Coefficient)[] terms)
    {
        return new LinearIntegerExpression(
            terms.Where(static term => !term.Coefficient.IsZero)
                .GroupBy(static term => term.Variable, StringComparer.Ordinal)
                .Select(static group => new KeyValuePair<string, ExactInteger>(group.Key,
                    group.Aggregate(ExactInteger.Zero, static (sum, term) => sum + term.Coefficient)))
                .Where(static term => !term.Value.IsZero).ToImmutableSortedDictionary(static term => term.Key,
                    static term => term.Value, StringComparer.Ordinal), constant);
    }

    public ExactInteger Coefficient(string variable)
    {
        return Coefficients.TryGetValue(variable, out ExactInteger value) ? value : ExactInteger.Zero;
    }

    public LinearIntegerExpression Without(string variable)
    {
        return this with { Coefficients = Coefficients.Remove(variable) };
    }

    public LinearIntegerExpression Add(LinearIntegerExpression other)
    {
        var coefficients = Coefficients.ToBuilder();
        foreach ((string variable, ExactInteger coefficient) in other.Coefficients)
        {
            coefficients[variable] = coefficients.TryGetValue(variable, out ExactInteger existing) ? existing + coefficient : coefficient;
            if (coefficients[variable].IsZero)
            {
                coefficients.Remove(variable);
            }
        }

        return new LinearIntegerExpression(coefficients.ToImmutable(), Constant + other.Constant);
    }

    public LinearIntegerExpression Scale(ExactInteger factor)
    {
        return new LinearIntegerExpression(
            Coefficients.Where(term => !(term.Value * factor).IsZero)
                .ToImmutableSortedDictionary(static term => term.Key, term => term.Value * factor,
                    StringComparer.Ordinal), Constant * factor);
    }
}
