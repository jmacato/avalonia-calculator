using Graphing.Symbolics;

namespace GraphingImpl;

internal readonly record struct AlgebraicDisplaySimplifierExtensionValue(AlgebraicDisplaySimplifierQuadraticValue Even, AlgebraicDisplaySimplifierQuadraticValue Odd)
{
    public static AlgebraicDisplaySimplifierExtensionValue Rational(BigRational value)
    {
        return new AlgebraicDisplaySimplifierExtensionValue(AlgebraicDisplaySimplifierQuadraticValue.Rational(value),
            AlgebraicDisplaySimplifierQuadraticValue.Rational(BigRational.Zero));
    }

    public static AlgebraicDisplaySimplifierExtensionValue Argument()
    {
        return new AlgebraicDisplaySimplifierExtensionValue(AlgebraicDisplaySimplifierQuadraticValue.Rational(BigRational.Zero),
            AlgebraicDisplaySimplifierQuadraticValue.Rational(BigRational.One));
    }

    public AlgebraicDisplaySimplifierExtensionValue Add(AlgebraicDisplaySimplifierExtensionValue other)
    {
        return new AlgebraicDisplaySimplifierExtensionValue(Even.Add(other.Even), Odd.Add(other.Odd));
    }

    public AlgebraicDisplaySimplifierExtensionValue Multiply(AlgebraicDisplaySimplifierExtensionValue other, AlgebraicDisplaySimplifierQuadraticValue square)
    {
        return new AlgebraicDisplaySimplifierExtensionValue(Even.Multiply(other.Even).Add(Odd.Multiply(other.Odd).Multiply(square)),
            Even.Multiply(other.Odd).Add(Odd.Multiply(other.Even)));
    }

    public static bool TryDivide(AlgebraicDisplaySimplifierExtensionValue numerator, AlgebraicDisplaySimplifierExtensionValue denominator, AlgebraicDisplaySimplifierQuadraticValue square, out AlgebraicDisplaySimplifierExtensionValue quotient)
    {
        AlgebraicDisplaySimplifierQuadraticValue norm = denominator.Even.Multiply(denominator.Even).Subtract(denominator.Odd.Multiply(denominator.Odd).Multiply(square));
        if (!AlgebraicDisplaySimplifierQuadraticValue.TryDivide(AlgebraicDisplaySimplifierQuadraticValue.Rational(BigRational.One), norm, out AlgebraicDisplaySimplifierQuadraticValue inverseNorm))
        {
            quotient = default;
            return false;
        }

        AlgebraicDisplaySimplifierExtensionValue conjugate = new(denominator.Even, denominator.Odd.Negate());
        AlgebraicDisplaySimplifierExtensionValue product = numerator.Multiply(conjugate, square);
        quotient = new(product.Even.Multiply(inverseNorm), product.Odd.Multiply(inverseNorm));
        return true;
    }
}
