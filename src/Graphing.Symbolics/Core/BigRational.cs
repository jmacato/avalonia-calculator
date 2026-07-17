using System.Globalization;

namespace Graphing.Symbolics;

/// <summary>
/// An immutable canonical exact rational. Numerator and denominator are
/// RatPak-backed exact integers; no proof-critical operation uses RatPak's
/// precision-trimming transcendental path.
/// </summary>
internal readonly struct BigRational :
    IComparable<BigRational>,
    IEquatable<BigRational>
{
    private readonly ExactInteger _numerator;
    private readonly ExactInteger _denominator;

    private BigRational(ExactInteger numerator, ExactInteger denominator, bool canonical)
    {
        if (!canonical || denominator.Sign <= 0)
        {
            throw new ArgumentException("The rational components must already be canonical.");
        }

        _numerator = numerator;
        _denominator = numerator.IsZero ? ExactInteger.One : denominator;
    }

    public BigRational(ExactInteger numerator, ExactInteger denominator)
    {
        if (denominator.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (numerator.IsZero)
        {
            _numerator = ExactInteger.Zero;
            _denominator = ExactInteger.One;
            return;
        }

        if (denominator.Sign < 0)
        {
            numerator = -numerator;
            denominator = -denominator;
        }

        if (denominator.IsOne)
        {
            _numerator = numerator;
            _denominator = ExactInteger.One;
            return;
        }

        if (ExactInteger.Abs(numerator) == denominator)
        {
            _numerator = numerator.Sign < 0 ? ExactInteger.MinusOne : ExactInteger.One;
            _denominator = ExactInteger.One;
            return;
        }

        if (ExactInteger.Abs(numerator).IsOne)
        {
            _numerator = numerator;
            _denominator = denominator;
            return;
        }

        ExactInteger divisor = ExactInteger.GreatestCommonDivisor(
            ExactInteger.Abs(numerator),
            denominator);
        _numerator = ExactInteger.DivideExactly(numerator, divisor);
        _denominator = ExactInteger.DivideExactly(denominator, divisor);
    }

    public BigRational(ExactInteger value)
    {
        _numerator = value;
        _denominator = ExactInteger.One;
    }

    public BigRational(int numerator, int denominator)
        : this(new ExactInteger(numerator), new ExactInteger(denominator))
    {
    }

    public BigRational(long numerator, long denominator)
        : this(new ExactInteger(numerator), new ExactInteger(denominator))
    {
    }

    public BigRational(int value)
        : this(new ExactInteger(value))
    {
    }

    public BigRational(long value)
        : this(new ExactInteger(value))
    {
    }

    public static BigRational Zero => default;

    public static BigRational One { get; } = new(ExactInteger.One);

    public static BigRational MinusOne { get; } = new(ExactInteger.MinusOne);

    public ExactInteger Numerator => _numerator;

    public ExactInteger Denominator => _denominator.IsZero ? ExactInteger.One : _denominator;

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

            ExactInteger numerator = ExactInteger.Parse(source[..slash], CultureInfo.InvariantCulture);
            ExactInteger denominator = ExactInteger.Parse(source[(slash + 1)..], CultureInfo.InvariantCulture);
            return new BigRational(numerator, denominator);
        }

        int exponentMarker = source.IndexOfAny('e', 'E');
        if (exponentMarker >= 0 && source[(exponentMarker + 1)..].IndexOfAny('e', 'E') >= 0)
        {
            throw new FormatException("The rational literal contains multiple exponents.");
        }

        ReadOnlySpan<char> mantissa = exponentMarker < 0 ? source : source[..exponentMarker];
        ReadOnlySpan<char> exponentText = exponentMarker < 0
            ? ReadOnlySpan<char>.Empty
            : source[(exponentMarker + 1)..];

        bool negative = !mantissa.IsEmpty && mantissa[0] == '-';
        if (!mantissa.IsEmpty && (mantissa[0] == '-' || mantissa[0] == '+'))
        {
            mantissa = mantissa[1..];
        }

        int point = mantissa.IndexOf('.');
        if (point >= 0 && mantissa[(point + 1)..].IndexOf('.') >= 0)
        {
            throw new FormatException("The rational literal contains multiple decimal points.");
        }

        string digits = point < 0
            ? mantissa.ToString()
            : string.Concat(mantissa[..point], mantissa[(point + 1)..]);
        if (digits.Length == 0 || !digits.All(char.IsAsciiDigit))
        {
            throw new FormatException("The rational literal is invalid.");
        }

        int exponent = 0;
        if (exponentMarker >= 0 &&
            !int.TryParse(exponentText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent))
        {
            throw new FormatException("The rational exponent is invalid.");
        }

        (ExactInteger numeratorValue, ExactInteger denominatorValue) =
            ExactInteger.ParseDecimalRational(
                negative,
                digits,
                point < 0 ? 0 : mantissa.Length - point - 1,
                exponent);
        return new BigRational(numeratorValue, denominatorValue);
    }

    public BigRational Abs() => Sign < 0 ? -this : this;

    public BigRational Reciprocal()
    {
        if (IsZero)
        {
            throw new DivideByZeroException();
        }

        return Numerator.Sign < 0
            ? new BigRational(-Denominator, -Numerator, canonical: true)
            : new BigRational(Denominator, Numerator, canonical: true);
    }

    public BigRational Pow(int exponent)
    {
        if (exponent == 0)
        {
            return One;
        }

        if (exponent == int.MinValue)
        {
            BigRational reciprocal = Reciprocal();
            return reciprocal.Pow(int.MaxValue) * reciprocal;
        }

        if (exponent < 0)
        {
            return Reciprocal().Pow(-exponent);
        }

        return new BigRational(
            ExactInteger.Pow(Numerator, exponent),
            ExactInteger.Pow(Denominator, exponent),
            canonical: true);
    }

    public ExactInteger Floor()
    {
        ExactInteger quotient = ExactInteger.DivRem(Numerator, Denominator, out ExactInteger remainder);
        return Sign < 0 && !remainder.IsZero ? quotient - ExactInteger.One : quotient;
    }

    public ExactInteger Ceiling()
    {
        ExactInteger quotient = ExactInteger.DivRem(Numerator, Denominator, out ExactInteger remainder);
        return Sign > 0 && !remainder.IsZero ? quotient + ExactInteger.One : quotient;
    }

    internal ExactInteger Truncate() => Numerator / Denominator;

    public static bool TrySquareRoot(BigRational value, out BigRational result)
    {
        if (value.Sign < 0 ||
            !ExactInteger.TrySquareRoot(value.Numerator, out ExactInteger numerator) ||
            !ExactInteger.TrySquareRoot(value.Denominator, out ExactInteger denominator))
        {
            result = default;
            return false;
        }

        result = new BigRational(numerator, denominator);
        return true;
    }

    public int CompareTo(BigRational other)
    {
        int sign = Sign;
        int otherSign = other.Sign;
        if (sign != otherSign)
        {
            return sign.CompareTo(otherSign);
        }

        if (sign == 0 || Equals(other))
        {
            return 0;
        }

        if (Denominator == other.Denominator)
        {
            return Numerator.CompareTo(other.Numerator);
        }

        ExactInteger left = Numerator * other.Denominator;
        ExactInteger right = other.Numerator * Denominator;
        return left.CompareTo(right);
    }

    public bool Equals(BigRational other) =>
        Numerator.Equals(other.Numerator) && Denominator.Equals(other.Denominator);

    public override bool Equals(object? obj) => obj is BigRational other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Numerator, Denominator);

    public override string ToString() => Denominator.IsOne
        ? Numerator.ToString(CultureInfo.InvariantCulture)
        : string.Create(CultureInfo.InvariantCulture, $"{Numerator}/{Denominator}");

    public static BigRational operator +(BigRational left, BigRational right)
    {
        if (left.IsZero)
        {
            return right;
        }

        if (right.IsZero)
        {
            return left;
        }

        if (left.Denominator == right.Denominator)
        {
            return new BigRational(left.Numerator + right.Numerator, left.Denominator);
        }

        ExactInteger denominatorGcd = ExactInteger.GreatestCommonDivisor(
            left.Denominator,
            right.Denominator);
        ExactInteger leftScale = ExactInteger.DivideExactly(right.Denominator, denominatorGcd);
        ExactInteger rightScale = ExactInteger.DivideExactly(left.Denominator, denominatorGcd);
        return new BigRational(
            left.Numerator * leftScale + right.Numerator * rightScale,
            left.Denominator * leftScale);
    }

    public static BigRational operator -(BigRational left, BigRational right) => left + -right;

    public static BigRational operator *(BigRational left, BigRational right)
    {
        if (left.IsZero || right.IsZero)
        {
            return Zero;
        }

        if (left.IsOne)
        {
            return right;
        }

        if (right.IsOne)
        {
            return left;
        }

        if (left == MinusOne)
        {
            return -right;
        }

        if (right == MinusOne)
        {
            return -left;
        }

        ExactInteger firstCancellation = ExactInteger.GreatestCommonDivisor(
            ExactInteger.Abs(left.Numerator),
            right.Denominator);
        ExactInteger secondCancellation = ExactInteger.GreatestCommonDivisor(
            ExactInteger.Abs(right.Numerator),
            left.Denominator);
        return new BigRational(
            ExactInteger.DivideExactly(left.Numerator, firstCancellation) *
            ExactInteger.DivideExactly(right.Numerator, secondCancellation),
            ExactInteger.DivideExactly(left.Denominator, secondCancellation) *
            ExactInteger.DivideExactly(right.Denominator, firstCancellation),
            canonical: true);
    }

    public static BigRational operator /(BigRational left, BigRational right)
    {
        if (right.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (left.IsZero)
        {
            return Zero;
        }

        if (right.IsOne)
        {
            return left;
        }

        if (right == MinusOne)
        {
            return -left;
        }

        if (left == right)
        {
            return One;
        }

        ExactInteger numeratorCancellation = ExactInteger.GreatestCommonDivisor(
            ExactInteger.Abs(left.Numerator),
            ExactInteger.Abs(right.Numerator));
        ExactInteger denominatorCancellation = ExactInteger.GreatestCommonDivisor(
            left.Denominator,
            right.Denominator);
        ExactInteger numerator =
            ExactInteger.DivideExactly(left.Numerator, numeratorCancellation) *
            ExactInteger.DivideExactly(right.Denominator, denominatorCancellation);
        ExactInteger denominator =
            ExactInteger.DivideExactly(left.Denominator, denominatorCancellation) *
            ExactInteger.DivideExactly(right.Numerator, numeratorCancellation);
        if (denominator.Sign < 0)
        {
            numerator = -numerator;
            denominator = -denominator;
        }

        return new BigRational(numerator, denominator, canonical: true);
    }

    public static BigRational operator -(BigRational value) => value.IsZero
        ? value
        : new BigRational(-value.Numerator, value.Denominator, canonical: true);

    public static implicit operator BigRational(int value) => new(value);

    public static implicit operator BigRational(long value) => new(value);

    public static bool operator ==(BigRational left, BigRational right) => left.Equals(right);

    public static bool operator !=(BigRational left, BigRational right) => !left.Equals(right);

    public static bool operator <(BigRational left, BigRational right) => left.CompareTo(right) < 0;

    public static bool operator <=(BigRational left, BigRational right) => left.CompareTo(right) <= 0;

    public static bool operator >(BigRational left, BigRational right) => left.CompareTo(right) > 0;

    public static bool operator >=(BigRational left, BigRational right) => left.CompareTo(right) >= 0;
}
