using System.Collections.Immutable;

namespace Graphing.Symbolics;

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

    public static PresburgerFormula Normalize(PresburgerFormula formula)
    {
        return formula switch
        {
            PresburgerDivisibility divisibility when divisibility.Divisor.IsZero => new PresburgerComparison(
                divisibility.Expression, IntegerRelation.Equal),
            PresburgerDivisibility divisibility when divisibility.Divisor.Sign < 0 => divisibility with
            {
                Divisor = ExactInteger.Abs(divisibility.Divisor)
            },
            PresburgerJunction { IsConjunction: true } conjunction => And(conjunction.Operands.Select(Normalize)),
            PresburgerJunction junction => junction with
            {
                Operands = junction.Operands.Select(Normalize).ToImmutableArray()
            },
            PresburgerNot not => not with { Operand = Normalize(not.Operand) },
            _ => formula
        };
    }
}
