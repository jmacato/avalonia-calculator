using Graphing.Symbolics;

namespace GraphingImpl;

internal readonly record struct AlgebraicDisplaySimplifierQuadraticValue(BigRational RationalPart, BigRational RadicalCoefficient, ExactInteger Radicand)
{
    public bool IsRational => RadicalCoefficient.IsZero;
    public bool IsZero => RationalPart.IsZero && RadicalCoefficient.IsZero;

    public int Sign
    {
        get
        {
            if (RadicalCoefficient.IsZero)
            {
                return RationalPart.Sign;
            }

            if (RationalPart.IsZero || RationalPart.Sign == RadicalCoefficient.Sign)
            {
                return RadicalCoefficient.Sign;
            }

            BigRational rationalSquare = RationalPart * RationalPart;
            BigRational radicalSquare = RadicalCoefficient * RadicalCoefficient * new BigRational(Radicand);
            int comparison = rationalSquare.CompareTo(radicalSquare);
            return comparison * RationalPart.Sign;
        }
    }

    public static AlgebraicDisplaySimplifierQuadraticValue Rational(BigRational value)
    {
        return new AlgebraicDisplaySimplifierQuadraticValue(value, BigRational.Zero, ExactInteger.Zero);
    }

    public static AlgebraicDisplaySimplifierQuadraticValue Create(BigRational rational, BigRational radicalCoefficient, ExactInteger radicand)
    {
        return radicalCoefficient.IsZero
            ? Rational(rational)
            : new AlgebraicDisplaySimplifierQuadraticValue(rational, radicalCoefficient, radicand);
    }

    public AlgebraicDisplaySimplifierQuadraticValue Negate()
    {
        return Create(-RationalPart, -RadicalCoefficient, Radicand);
    }

    public AlgebraicDisplaySimplifierQuadraticValue Add(AlgebraicDisplaySimplifierQuadraticValue other)
    {
        (AlgebraicDisplaySimplifierQuadraticValue left, AlgebraicDisplaySimplifierQuadraticValue right, ExactInteger radicand) = Align(this, other);
        return Create(left.RationalPart + right.RationalPart, left.RadicalCoefficient + right.RadicalCoefficient, radicand);
    }

    public AlgebraicDisplaySimplifierQuadraticValue Subtract(AlgebraicDisplaySimplifierQuadraticValue other)
    {
        return Add(other.Negate());
    }

    public AlgebraicDisplaySimplifierQuadraticValue Multiply(BigRational scalar)
    {
        return Create(RationalPart * scalar, RadicalCoefficient * scalar, Radicand);
    }

    public AlgebraicDisplaySimplifierQuadraticValue Multiply(AlgebraicDisplaySimplifierQuadraticValue other)
    {
        (AlgebraicDisplaySimplifierQuadraticValue left, AlgebraicDisplaySimplifierQuadraticValue right, ExactInteger radicand) = Align(this, other);
        return Create(left.RationalPart * right.RationalPart + left.RadicalCoefficient * right.RadicalCoefficient * new BigRational(radicand), left.RationalPart * right.RadicalCoefficient + left.RadicalCoefficient * right.RationalPart, radicand);
    }

    public static bool TryDivide(AlgebraicDisplaySimplifierQuadraticValue numerator, AlgebraicDisplaySimplifierQuadraticValue denominator, out AlgebraicDisplaySimplifierQuadraticValue quotient)
    {
        if (denominator.IsZero)
        {
            quotient = default;
            return false;
        }

        (AlgebraicDisplaySimplifierQuadraticValue alignedNumerator, AlgebraicDisplaySimplifierQuadraticValue alignedDenominator, ExactInteger radicand) = Align(numerator, denominator);
        BigRational norm = alignedDenominator.RationalPart * alignedDenominator.RationalPart - alignedDenominator.RadicalCoefficient * alignedDenominator.RadicalCoefficient * new BigRational(radicand);
        if (norm.IsZero)
        {
            quotient = default;
            return false;
        }

        AlgebraicDisplaySimplifierQuadraticValue conjugate = Create(alignedDenominator.RationalPart, -alignedDenominator.RadicalCoefficient, radicand);
        quotient = alignedNumerator.Multiply(conjugate).Multiply(norm.Reciprocal());
        return true;
    }

    private static (AlgebraicDisplaySimplifierQuadraticValue Left, AlgebraicDisplaySimplifierQuadraticValue Right, ExactInteger Radicand) Align(AlgebraicDisplaySimplifierQuadraticValue left, AlgebraicDisplaySimplifierQuadraticValue right)
    {
        if (left.IsRational)
        {
            return (Create(left.RationalPart, BigRational.Zero, right.Radicand), right, right.Radicand);
        }

        if (right.IsRational)
        {
            return (left, Create(right.RationalPart, BigRational.Zero, left.Radicand), left.Radicand);
        }

        if (left.Radicand != right.Radicand)
        {
            throw new InvalidOperationException("Quadratic-field operands have different radicands.");
        }

        return (left, right, left.Radicand);
    }
}
