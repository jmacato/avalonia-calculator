using System.Globalization;
using System.Numerics;

namespace Graphing.Symbolics;

internal readonly struct BigRational :
    IComparable<BigRational>,
    IEquatable<BigRational>
{
    private readonly BigInteger _numerator;
    private readonly BigInteger _denominator;

    public BigRational(BigInteger numerator, BigInteger denominator)
    {
        if (denominator.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (numerator.IsZero)
        {
            _numerator = BigInteger.Zero;
            _denominator = BigInteger.One;
            return;
        }

        if (denominator.Sign < 0)
        {
            numerator = BigInteger.Negate(numerator);
            denominator = BigInteger.Negate(denominator);
        }

        BigInteger divisor = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
        _numerator = numerator / divisor;
        _denominator = denominator / divisor;
    }

    public BigRational(BigInteger value)
        : this(value, BigInteger.One)
    {
    }

    public static BigRational Zero => default;

    public static BigRational One { get; } = new(BigInteger.One);

    public static BigRational MinusOne { get; } = new(BigInteger.MinusOne);

    public BigInteger Numerator => _numerator;

    public BigInteger Denominator => _denominator.IsZero ? BigInteger.One : _denominator;

    public int Sign => Numerator.Sign;

    public bool IsZero => Numerator.IsZero;

    public bool IsOne => Numerator.IsOne && Denominator.IsOne;

    public bool IsInteger => Denominator.IsOne;

    public static BigRational Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ReadOnlySpan<char> source = text.AsSpan().Trim();
        int slash = source.IndexOf('/');
        if (slash >= 0)
        {
            if (source[(slash + 1)..].IndexOf('/') >= 0)
            {
                throw new FormatException("A rational literal may contain one slash.");
            }

            BigInteger numerator = BigInteger.Parse(source[..slash], CultureInfo.InvariantCulture);
            BigInteger denominator = BigInteger.Parse(source[(slash + 1)..], CultureInfo.InvariantCulture);
            return new BigRational(numerator, denominator);
        }

        int exponentMarker = source.IndexOfAny('e', 'E');
        ReadOnlySpan<char> mantissa = exponentMarker < 0 ? source : source[..exponentMarker];
        int exponent = exponentMarker < 0
            ? 0
            : int.Parse(source[(exponentMarker + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);

        bool negative = !mantissa.IsEmpty && mantissa[0] == '-';
        if (!mantissa.IsEmpty && (mantissa[0] == '-' || mantissa[0] == '+'))
        {
            mantissa = mantissa[1..];
        }

        int point = mantissa.IndexOf('.');
        int fractionalDigits = point < 0 ? 0 : mantissa.Length - point - 1;
        string digits = point < 0
            ? mantissa.ToString()
            : string.Concat(mantissa[..point], mantissa[(point + 1)..]);
        if (digits.Length == 0 || !digits.All(char.IsAsciiDigit))
        {
            throw new FormatException("The rational literal is invalid.");
        }

        BigInteger value = BigInteger.Parse(digits, CultureInfo.InvariantCulture);
        if (negative)
        {
            value = BigInteger.Negate(value);
        }

        int scale = checked(fractionalDigits - exponent);
        return scale >= 0
            ? new BigRational(value, BigInteger.Pow(10, scale))
            : new BigRational(value * BigInteger.Pow(10, -scale), BigInteger.One);
    }

    public BigRational Abs() => Sign < 0 ? -this : this;

    public BigRational Reciprocal()
    {
        if (IsZero)
        {
            throw new DivideByZeroException();
        }

        return new BigRational(Denominator, Numerator);
    }

    public BigRational Pow(int exponent)
    {
        if (exponent == 0)
        {
            return One;
        }

        if (exponent < 0)
        {
            if (exponent == int.MinValue)
            {
                return Reciprocal().Pow(int.MaxValue) * Reciprocal();
            }

            return Reciprocal().Pow(-exponent);
        }

        return new BigRational(
            BigInteger.Pow(Numerator, exponent),
            BigInteger.Pow(Denominator, exponent));
    }

    public BigInteger Floor()
    {
        BigInteger quotient = BigInteger.DivRem(Numerator, Denominator, out BigInteger remainder);
        return remainder.Sign < 0 ? quotient - BigInteger.One : quotient;
    }

    public BigInteger Ceiling()
    {
        BigInteger quotient = BigInteger.DivRem(Numerator, Denominator, out BigInteger remainder);
        return remainder.Sign > 0 ? quotient + BigInteger.One : quotient;
    }

    public static bool TrySquareRoot(BigRational value, out BigRational result)
    {
        if (value.Sign < 0 ||
            !TryIntegerSquareRoot(value.Numerator, out BigInteger numerator) ||
            !TryIntegerSquareRoot(value.Denominator, out BigInteger denominator))
        {
            result = default;
            return false;
        }

        result = new BigRational(numerator, denominator);
        return true;
    }

    public int CompareTo(BigRational other) =>
        (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);

    public bool Equals(BigRational other) =>
        Numerator.Equals(other.Numerator) && Denominator.Equals(other.Denominator);

    public override bool Equals(object? obj) => obj is BigRational other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Numerator, Denominator);

    public override string ToString() => Denominator.IsOne
        ? Numerator.ToString(CultureInfo.InvariantCulture)
        : string.Create(
            CultureInfo.InvariantCulture,
            $"{Numerator}/{Denominator}");

    public static BigRational operator +(BigRational left, BigRational right) =>
        new(
            (left.Numerator * right.Denominator) + (right.Numerator * left.Denominator),
            left.Denominator * right.Denominator);

    public static BigRational operator -(BigRational left, BigRational right) =>
        new(
            (left.Numerator * right.Denominator) - (right.Numerator * left.Denominator),
            left.Denominator * right.Denominator);

    public static BigRational operator *(BigRational left, BigRational right) =>
        new(left.Numerator * right.Numerator, left.Denominator * right.Denominator);

    public static BigRational operator /(BigRational left, BigRational right) =>
        new(left.Numerator * right.Denominator, left.Denominator * right.Numerator);

    public static BigRational operator -(BigRational value) =>
        new(BigInteger.Negate(value.Numerator), value.Denominator);

    public static implicit operator BigRational(int value) => new(value);

    public static implicit operator BigRational(long value) => new(value);

    public static bool operator ==(BigRational left, BigRational right) => left.Equals(right);

    public static bool operator !=(BigRational left, BigRational right) => !left.Equals(right);

    public static bool operator <(BigRational left, BigRational right) => left.CompareTo(right) < 0;

    public static bool operator <=(BigRational left, BigRational right) => left.CompareTo(right) <= 0;

    public static bool operator >(BigRational left, BigRational right) => left.CompareTo(right) > 0;

    public static bool operator >=(BigRational left, BigRational right) => left.CompareTo(right) >= 0;

    private static bool TryIntegerSquareRoot(BigInteger value, out BigInteger root)
    {
        if (value.Sign < 0)
        {
            root = default;
            return false;
        }

        if (value < 2)
        {
            root = value;
            return true;
        }

        BigInteger candidate = BigInteger.One << checked((int)((value.GetBitLength() + 1) / 2));
        while (true)
        {
            BigInteger next = (candidate + (value / candidate)) >> 1;
            if (next >= candidate)
            {
                root = candidate;
                return candidate * candidate == value;
            }

            candidate = next;
        }
    }
}
