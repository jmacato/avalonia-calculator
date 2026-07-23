namespace Graphing.Symbolics;

internal static class ExactRealArithmetic
{
    public static ExactReal Negate(ExactReal value)
    {
        return value switch
        {
            RationalReal rational => new RationalReal(-rational.Value),
            AffinePiReal affine => new AffinePiReal(-affine.PiCoefficient, -affine.Constant),
            FunctionReal { Function: "negate", Arguments.Length: 1 } function => function.Arguments[0],
            _ => new FunctionReal("negate", [value])
        };
    }

    public static ExactReal Add(ExactReal left, ExactReal right)
    {
        return (left, right) switch
        {
            (RationalReal { Value.IsZero: true }, _) => right,
            (_, RationalReal { Value.IsZero: true }) => left,
            (RationalReal a, RationalReal b) => new RationalReal(a.Value + b.Value),
            (AffinePiReal a, RationalReal b) => a with { Constant = a.Constant + b.Value },
            (RationalReal a, AffinePiReal b) => b with { Constant = b.Constant + a.Value },
            (AffinePiReal a, AffinePiReal b) => new AffinePiReal(a.PiCoefficient + b.PiCoefficient,
                a.Constant + b.Constant),
            _ => new FunctionReal("add", [left, right])
        };
    }

    public static ExactReal AddRational(ExactReal value, BigRational addend)
    {
        return Add(value, new RationalReal(addend));
    }

    public static ExactReal Subtract(ExactReal left, ExactReal right)
    {
        return Add(left, Negate(right));
    }

    public static ExactReal Multiply(ExactReal left, ExactReal right)
    {
        return (left, right) switch
        {
            (RationalReal a, _) => Scale(right, a.Value),
            (_, RationalReal b) => Scale(left, b.Value),
            _ => new FunctionReal("multiply", [left, right])
        };
    }

    public static ExactReal Divide(ExactReal numerator, ExactReal denominator)
    {
        return denominator is RationalReal rational
            ? Scale(numerator, rational.Value.Reciprocal())
            : new FunctionReal("divide", [numerator, denominator]);
    }

    public static ExactReal Power(ExactReal basis, int exponent)
    {
        return new FunctionReal("power", [basis, new RationalReal(new BigRational(exponent))]);
    }

    public static ExactReal Scale(ExactReal value, BigRational factor)
    {
        if (factor.IsZero)
        {
            return new RationalReal(BigRational.Zero);
        }

        if (factor.IsOne)
        {
            return value;
        }

        if (factor == BigRational.MinusOne)
        {
            return Negate(value);
        }

        return value switch
        {
            RationalReal rational => new RationalReal(rational.Value * factor),
            AffinePiReal affine => new AffinePiReal(affine.PiCoefficient * factor, affine.Constant * factor),
            _ => new FunctionReal("scale", [value, new RationalReal(factor)])
        };
    }
}
