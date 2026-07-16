using System.Globalization;
using CalcEngine;

namespace GraphingImpl;

/// <summary>
/// An immutable exact rational backed by the calculator's RatPak engine.
/// Floating-point conversion is deliberately deferred until VM compilation.
/// </summary>
internal readonly struct ExactRational : IComparable<ExactRational>, IEquatable<ExactRational>
{
    private const uint Radix = 10;
    private const int MaximumDecimalExpansionDigits = 1_234;
    private const int OperationPrecision = (MaximumDecimalExpansionDigits * 2) + 32;

    private static readonly object Gate = new();
    private static readonly RatPak Pak = new();
    private static readonly EngineNumber ZeroNumber = new();
    private static readonly EngineNumber OneNumber = new(1, 0, 1, [1]);

    private readonly EngineNumber? _numerator;
    private readonly EngineNumber? _denominator;
    private readonly bool _useDecimalFormat;

    private ExactRational(EngineNumber numerator, EngineNumber denominator, bool useDecimalFormat)
    {
        _numerator = numerator;
        _denominator = denominator;
        _useDecimalFormat = useDecimalFormat;
    }

    public static ExactRational Zero => default;

    public static ExactRational One { get; } = FromInt32Core(1);

    private EngineNumber Numerator => _numerator ?? ZeroNumber;

    private EngineNumber Denominator => _denominator ?? OneNumber;

    public bool IsInteger => IsOne(Denominator);

    public bool DenominatorIsEven =>
        Denominator.Exp > 0 || (Denominator.Mantissa[0] & 1u) == 0;

    public ExactRational NumeratorInteger =>
        new(Numerator, OneNumber, useDecimalFormat: true);

    public ExactRational DenominatorInteger =>
        new(Denominator, OneNumber, useDecimalFormat: true);

    public int Sign => Numerator.IsZero() ? 0 : Numerator.Sign * Denominator.Sign;

    public string AbsoluteNumeratorText
    {
        get
        {
            lock (Gate)
            {
                EngineNumber absolute = new(1, Numerator.Exp, Numerator.CDigits, Numerator.Mantissa);
                return FormatIntegerLocked(absolute);
            }
        }
    }

    public string DenominatorText
    {
        get
        {
            lock (Gate)
            {
                return FormatIntegerLocked(Denominator);
            }
        }
    }

    public static ExactRational ParseDecimal(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);

        int exponentMarker = text.IndexOfAny(['e', 'E']);
        if (exponentMarker >= 0 && text.IndexOfAny(['e', 'E'], exponentMarker + 1) >= 0)
        {
            throw new FormatException("The numeric literal contains multiple exponents.");
        }

        string mantissa = exponentMarker < 0 ? text : text[..exponentMarker];
        string exponentText = exponentMarker < 0 ? string.Empty : text[(exponentMarker + 1)..];
        bool negative = mantissa.StartsWith("-", StringComparison.Ordinal);
        if (negative || mantissa.StartsWith("+", StringComparison.Ordinal))
        {
            mantissa = mantissa[1..];
        }

        int decimalPoint = mantissa.IndexOf('.');
        if (decimalPoint >= 0 && mantissa.IndexOf('.', decimalPoint + 1) >= 0)
        {
            throw new FormatException("The numeric literal contains multiple decimal points.");
        }

        string digits = decimalPoint < 0 ? mantissa : mantissa.Remove(decimalPoint, 1);
        if (digits.Length == 0 || digits.Any(character => !char.IsAsciiDigit(character)))
        {
            throw new FormatException("The numeric literal contains an invalid digit.");
        }

        long exponent = 0;
        if (exponentMarker >= 0 &&
            !long.TryParse(exponentText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent))
        {
            throw new FormatException("The decimal exponent is invalid.");
        }

        if (digits.All(character => character == '0'))
        {
            return Zero;
        }

        int fractionalDigits = decimalPoint < 0 ? 0 : mantissa.Length - decimalPoint - 1;
        long scale;
        try
        {
            scale = checked(fractionalDigits - exponent);
        }
        catch (OverflowException)
        {
            throw new OverflowException("The decimal exponent exceeds the exact-value budget.");
        }

        if (Math.Abs(scale) > MaximumDecimalExpansionDigits)
        {
            throw new OverflowException("The decimal exponent exceeds the exact-value budget.");
        }

        int boundedExponent = checked((int)exponent);
        string unsignedExponent = Math.Abs(boundedExponent).ToString(CultureInfo.InvariantCulture);

        lock (Gate)
        {
            RAT? parsed = Pak.StringToRat(
                negative,
                mantissa,
                boundedExponent < 0,
                boundedExponent == 0 ? string.Empty : unsignedExponent,
                Radix,
                OperationPrecision);
            if (parsed is null)
            {
                throw new FormatException("The numeric literal is invalid.");
            }

            try
            {
                return CreateNormalizedLocked(ref parsed, useDecimalFormat: true);
            }
            finally
            {
                RatPak.destroyrat(ref parsed);
            }
        }
    }

    public bool TryGetInt64(out long value)
    {
        if (IsInteger && long.TryParse(ToString(), NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    public ExactRational Floor()
    {
        if (IsInteger)
        {
            return this;
        }

        ExactRational integral;
        lock (Gate)
        {
            RAT? value = ToRat();
            try
            {
                Pak.intrat(ref value, Radix, OperationPrecision);
                integral = CreateNormalizedLocked(ref value, useDecimalFormat: true);
            }
            finally
            {
                RatPak.destroyrat(ref value);
            }
        }

        return Sign < 0 ? integral - One : integral;
    }

    public ExactRational Ceiling()
    {
        if (IsInteger)
        {
            return this;
        }

        ExactRational integral;
        lock (Gate)
        {
            RAT? value = ToRat();
            try
            {
                Pak.intrat(ref value, Radix, OperationPrecision);
                integral = CreateNormalizedLocked(ref value, useDecimalFormat: true);
            }
            finally
            {
                RatPak.destroyrat(ref value);
            }
        }

        return Sign > 0 ? integral + One : integral;
    }

    public double ToDouble()
    {
        lock (Gate)
        {
            RAT? value = ToRat();
            try
            {
                string text = Pak.RatToString(ref value, NumberFormat.Scientific, Radix, 17);
                return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            }
            finally
            {
                RatPak.destroyrat(ref value);
            }
        }
    }

    public int CompareTo(ExactRational other)
    {
        lock (Gate)
        {
            RAT? left = ToRat();
            RAT? right = other.ToRat();
            try
            {
                if (Pak.RatEqu(left, right, OperationPrecision))
                {
                    return 0;
                }

                return Pak.RatLt(left, right, OperationPrecision) ? -1 : 1;
            }
            finally
            {
                RatPak.destroyrat(ref left);
                RatPak.destroyrat(ref right);
            }
        }
    }

    public bool Equals(ExactRational other) => CompareTo(other) == 0;

    public override bool Equals(object? obj) => obj is ExactRational other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = HashNumber(Numerator);
            return (hash * 397) ^ HashNumber(Denominator);
        }
    }

    public override string ToString()
    {
        lock (Gate)
        {
            RAT? value = ToRat();
            try
            {
                if (IsInteger)
                {
                    return FormatIntegerLocked(Numerator);
                }

                if (_useDecimalFormat)
                {
                    return Pak.RatToString(ref value, NumberFormat.FloatingPoint, Radix, OperationPrecision);
                }

                return FormatIntegerLocked(Numerator) + "/" + FormatIntegerLocked(Denominator);
            }
            finally
            {
                RatPak.destroyrat(ref value);
            }
        }
    }

    public static ExactRational operator -(ExactRational value)
    {
        if (value.Sign == 0)
        {
            return value;
        }

        lock (Gate)
        {
            RAT? rat = value.ToRat();
            try
            {
                rat.Pp.Sign *= -1;
                return CreateNormalizedLocked(ref rat, value._useDecimalFormat);
            }
            finally
            {
                RatPak.destroyrat(ref rat);
            }
        }
    }

    public static ExactRational operator +(ExactRational left, ExactRational right) =>
        ApplyBinary(left, right, BinaryOperation.Add);

    public static ExactRational operator -(ExactRational left, ExactRational right) =>
        ApplyBinary(left, right, BinaryOperation.Subtract);

    public static ExactRational operator *(ExactRational left, ExactRational right) =>
        ApplyBinary(left, right, BinaryOperation.Multiply);

    public static ExactRational operator /(ExactRational left, ExactRational right)
    {
        if (right.Sign == 0)
        {
            throw new DivideByZeroException();
        }

        return ApplyBinary(left, right, BinaryOperation.Divide);
    }

    public static bool operator ==(ExactRational left, ExactRational right) => left.Equals(right);

    public static bool operator !=(ExactRational left, ExactRational right) => !left.Equals(right);

    public static bool operator <(ExactRational left, ExactRational right) => left.CompareTo(right) < 0;

    public static bool operator >(ExactRational left, ExactRational right) => left.CompareTo(right) > 0;

    public static bool operator <=(ExactRational left, ExactRational right) => left.CompareTo(right) <= 0;

    public static bool operator >=(ExactRational left, ExactRational right) => left.CompareTo(right) >= 0;

    public static implicit operator ExactRational(int value) => value switch
    {
        0 => Zero,
        1 => One,
        _ => FromInt32Core(value)
    };

    public static implicit operator ExactRational(long value)
    {
        if (value is >= int.MinValue and <= int.MaxValue)
        {
            return (int)value;
        }

        return ParseDecimal(value.ToString(CultureInfo.InvariantCulture));
    }

    private static ExactRational ApplyBinary(
        ExactRational left,
        ExactRational right,
        BinaryOperation operation)
    {
        lock (Gate)
        {
            RAT? leftRat = left.ToRat();
            RAT? rightRat = right.ToRat();
            try
            {
                switch (operation)
                {
                    case BinaryOperation.Add:
                        Pak.addrat(ref leftRat, rightRat, OperationPrecision);
                        break;
                    case BinaryOperation.Subtract:
                        Pak.subrat(ref leftRat, rightRat, OperationPrecision);
                        break;
                    case BinaryOperation.Multiply:
                        Pak.mulrat(ref leftRat, rightRat, OperationPrecision);
                        break;
                    case BinaryOperation.Divide:
                        Pak.divrat(ref leftRat, rightRat, OperationPrecision);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(operation));
                }

                bool useDecimalFormat = operation != BinaryOperation.Divide &&
                                        (left._numerator is null || left._useDecimalFormat) &&
                                        (right._numerator is null || right._useDecimalFormat);
                return CreateNormalizedLocked(ref leftRat, useDecimalFormat);
            }
            finally
            {
                RatPak.destroyrat(ref leftRat);
                RatPak.destroyrat(ref rightRat);
            }
        }
    }

    private static ExactRational FromInt32Core(int value)
    {
        lock (Gate)
        {
            RAT? rat = RatPak.i32torat(value);
            try
            {
                return CreateNormalizedLocked(ref rat, useDecimalFormat: true);
            }
            finally
            {
                RatPak.destroyrat(ref rat);
            }
        }
    }

    private static ExactRational CreateNormalizedLocked(ref RAT? value, bool useDecimalFormat)
    {
        if (value is null || RatPak.zernum(value.Pq))
        {
            throw new DivideByZeroException();
        }

        Pak.gcdrat(ref value, OperationPrecision);
        if (value.Pq.Sign < 0)
        {
            value.Pp.Sign *= -1;
            value.Pq.Sign *= -1;
        }

        if (BitLength(value.Pp) > GraphLimits.MaximumExactValueBits ||
            BitLength(value.Pq) > GraphLimits.MaximumExactValueBits)
        {
            throw new OverflowException("The rational value exceeds the exact-value budget.");
        }

        return new ExactRational(
            new EngineNumber(value.Pp),
            new EngineNumber(value.Pq),
            useDecimalFormat);
    }

    private RAT ToRat()
    {
        RAT result = RatPak.createrat();
        result.Pp = Numerator.ToPNUMBER();
        result.Pq = Denominator.ToPNUMBER();
        return result;
    }

    private static string FormatIntegerLocked(EngineNumber number)
    {
        RAT? value = RatPak.createrat();
        value.Pp = number.ToPNUMBER();
        value.Pq = OneNumber.ToPNUMBER();
        try
        {
            return Pak.RatToString(ref value, NumberFormat.FloatingPoint, Radix, OperationPrecision);
        }
        finally
        {
            RatPak.destroyrat(ref value);
        }
    }

    private static bool IsOne(EngineNumber value) =>
        value.Sign == 1 &&
        value.Exp == 0 &&
        value.CDigits == 1 &&
        value.Mantissa[0] == 1;

    private static int BitLength(NUMBER value)
    {
        if (RatPak.zernum(value))
        {
            return 0;
        }

        uint mostSignificant = value.Mant[value.Cdigit - 1];
        int leadingBits = 0;
        while (mostSignificant != 0)
        {
            mostSignificant >>= 1;
            leadingBits++;
        }

        return checked(((value.Cdigit + value.Exp - 1) * 31) + leadingBits);
    }

    private static int HashNumber(EngineNumber value)
    {
        unchecked
        {
            int hash = value.Sign;
            hash = (hash * 397) ^ value.Exp;
            hash = (hash * 397) ^ value.CDigits;
            for (int index = 0; index < value.CDigits; index++)
            {
                hash = (hash * 397) ^ (int)value.Mantissa[index];
            }

            return hash;
        }
    }

    private enum BinaryOperation
    {
        Add,
        Subtract,
        Multiply,
        Divide
    }
}
