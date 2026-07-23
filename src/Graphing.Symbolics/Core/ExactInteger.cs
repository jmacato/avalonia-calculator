using System.Globalization;
using CalcEngine;

namespace Graphing.Symbolics;

/// <summary>
/// An immutable exact integer backed by RatPak's arbitrary-precision NUMBER.
/// This is the integer kernel used by the symbolic rational representation.
/// </summary>
internal readonly struct ExactInteger :
    IComparable<ExactInteger>,
    IEquatable<ExactInteger>,
    IFormattable
{
    private const uint InternalRadix = 0x80000000;
    private const uint DisplayRadix = 10;
    private const int MaximumParsedDecimalDigits =
        AnalysisLimits.CoefficientBits * 30103 / 100000 + 2;

    [ThreadStatic]
    private static RatPak? s_threadPak;

    private static readonly EngineNumber ZeroNumber = new();
    private static readonly EngineNumber OneNumber = new(1, 0, 1, [1]);

    private readonly EngineNumber? _number;

    private ExactInteger(EngineNumber number)
    {
        _number = number;
    }

    public ExactInteger(int value)
    {
        if (value == 0)
        {
            _number = null;
            return;
        }

        long magnitude = Math.Abs((long)value);
        if (magnitude == InternalRadix)
        {
            _number = new EngineNumber(value < 0 ? -1 : 1, 1, 1, [1]);
        }
        else
        {
            _number = new EngineNumber(
                value < 0 ? -1 : 1,
                0,
                1,
                [(uint)magnitude]);
        }
    }

    public ExactInteger(long value)
    {
        if (value is >= int.MinValue and <= int.MaxValue)
        {
            this = new ExactInteger((int)value);
            return;
        }

        ulong magnitude = value < 0
            ? unchecked((ulong)-(value + 1)) + 1UL
            : (ulong)value;
        this = FromMagnitude(magnitude, value < 0 ? -1 : 1);
    }

    private static ExactInteger FromMagnitude(ulong magnitude, int sign)
    {
        if (magnitude == 0)
        {
            return Zero;
        }

        const ulong mask = InternalRadix - 1UL;
        uint low = (uint)(magnitude & mask);
        uint middle = (uint)((magnitude >> 31) & mask);
        uint high = (uint)(magnitude >> 62);
        int exponent;
        uint[] mantissa;
        if (low != 0)
        {
            exponent = 0;
            mantissa = high != 0
                ? [low, middle, high]
                : middle != 0
                    ? [low, middle]
                    : [low];
        }
        else if (middle != 0)
        {
            exponent = 1;
            mantissa = high != 0
                ? [middle, high]
                : [middle];
        }
        else
        {
            exponent = 2;
            mantissa = [high];
        }

        return new ExactInteger(new EngineNumber(
            sign < 0 ? -1 : 1,
            exponent,
            mantissa.Length,
            mantissa));
    }

    public static ExactInteger Zero => default;

    public static ExactInteger One { get; } = new(OneNumber);

    public static ExactInteger MinusOne { get; } = -One;

    private EngineNumber Number => _number ?? ZeroNumber;

    private static RatPak Pak => s_threadPak ??= new RatPak();

    public int Sign => _number?.Sign ?? 0;

    public bool IsZero => _number is null;

    public bool IsOne => _number is
    {
        Sign: 1,
        Exp: 0,
        CDigits: 1,
        Mantissa: var mantissa
    } && mantissa[0] == 1;

    public bool IsEven =>
        IsZero ||
        Number.Exp > 0 ||
        (Number.Mantissa[0] & 1u) == 0;

    public static ExactInteger Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Parse(text.AsSpan(), CultureInfo.InvariantCulture);
    }

    public static ExactInteger Parse(ReadOnlySpan<char> text, IFormatProvider? provider)
    {
        _ = provider;
        ReadOnlySpan<char> source = text.Trim();
        bool negative = !source.IsEmpty && source[0] == '-';
        if (!source.IsEmpty && (source[0] == '-' || source[0] == '+'))
        {
            source = source[1..];
        }

        if (source.IsEmpty || !source.ToString().All(char.IsAsciiDigit))
        {
            throw new FormatException("The exact integer literal is invalid.");
        }

        if (source.Length > MaximumParsedDecimalDigits)
        {
            throw new OverflowException("The exact integer literal exceeds the symbolic value budget.");
        }

        RAT? parsed = Pak.StringToRat(
            negative,
            source.ToString(),
            false,
            string.Empty,
            DisplayRadix,
            Math.Max(32, source.Length + 4));
        if (parsed is null || !IsOneNumber(parsed.Pq))
        {
            RatPak.destroyrat(ref parsed);
            throw new FormatException("The exact integer literal is invalid.");
        }

        try
        {
            return CreateCanonical(parsed.Pp);
        }
        finally
        {
            RatPak.destroyrat(ref parsed);
        }
    }

    internal static (ExactInteger Numerator, ExactInteger Denominator) ParseDecimalRational(
        bool negative,
        string digits,
        int fractionalDigits,
        int exponent)
    {
        long decimalScale = checked((long)fractionalDigits - exponent);
        long requiredDigits = checked((long)digits.Length + Math.Abs(decimalScale));
        if (requiredDigits > MaximumParsedDecimalDigits)
        {
            throw new OverflowException("The exact rational literal exceeds the symbolic value budget.");
        }

        ExactInteger numerator = Parse(
            negative ? string.Concat("-", digits) : digits,
            CultureInfo.InvariantCulture);
        if (numerator.IsZero)
        {
            return (Zero, One);
        }

        if (decimalScale == 0)
        {
            return (numerator, One);
        }

        ExactInteger scale = Pow(new ExactInteger(10), checked((int)Math.Abs(decimalScale)));
        if (decimalScale > 0)
        {
            return (numerator, scale);
        }

        return (numerator * scale, One);
    }

    public static ExactInteger Abs(ExactInteger value)
    {
        return value.Sign < 0 ? -value : value;
    }

    public static ExactInteger Negate(ExactInteger value)
    {
        return -value;
    }

    public static ExactInteger Pow(ExactInteger value, int exponent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(exponent);
        if (exponent == 0)
        {
            return One;
        }

        if (exponent == 1 || value.IsZero || value.IsOne)
        {
            return value;
        }

        if (value == MinusOne)
        {
            return (exponent & 1) == 0 ? One : MinusOne;
        }

        ExactInteger result = One;
        ExactInteger factor = value;
        int remaining = exponent;
        while (remaining != 0)
        {
            if ((remaining & 1) != 0)
            {
                result *= factor;
            }

            remaining >>= 1;
            if (remaining != 0)
            {
                factor *= factor;
            }
        }

        return result;
    }

    public static ExactInteger GreatestCommonDivisor(ExactInteger left, ExactInteger right)
    {
        left = Abs(left);
        right = Abs(right);
        if (left.IsZero)
        {
            return right;
        }

        if (right.IsZero)
        {
            return left;
        }

        if (left.IsOne || right.IsOne)
        {
            return One;
        }

        if (left == right)
        {
            return left;
        }

        if (left.TryGetUInt64Magnitude(out ulong leftMagnitude) &&
            right.TryGetUInt64Magnitude(out ulong rightMagnitude))
        {
            while (rightMagnitude != 0)
            {
                (leftMagnitude, rightMagnitude) =
                    (rightMagnitude, leftMagnitude % rightMagnitude);
            }

            return FromMagnitude(leftMagnitude, 1);
        }

        NUMBER leftNumber = left.ToNumber();
        NUMBER rightNumber = right.ToNumber();
        leftNumber.Sign = 1;
        rightNumber.Sign = 1;
        NUMBER gcd = RatPak.gcd(leftNumber, rightNumber);
        return CreateCanonical(gcd);
    }

    public static ExactInteger DivRem(
        ExactInteger dividend,
        ExactInteger divisor,
        out ExactInteger remainder)
    {
        if (divisor.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (dividend.IsZero)
        {
            remainder = Zero;
            return Zero;
        }

        if (divisor.IsOne)
        {
            remainder = Zero;
            return dividend;
        }

        if (divisor == MinusOne)
        {
            remainder = Zero;
            return -dividend;
        }

        if (dividend.TryGetInt64(out long smallDividend) &&
            divisor.TryGetInt64(out long smallDivisor))
        {
            long smallQuotient = smallDividend / smallDivisor;
            long smallRemainder = smallDividend % smallDivisor;
            remainder = new ExactInteger(smallRemainder);
            return new ExactInteger(smallQuotient);
        }

        remainder = Remainder(dividend, divisor);
        ExactInteger divisible = dividend - remainder;
        if (divisible.IsZero)
        {
            return Zero;
        }

        return DivideExactly(divisible, divisor);
    }

    internal static ExactInteger DivideExactly(ExactInteger dividend, ExactInteger divisor)
    {
        if (divisor.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (dividend.IsZero)
        {
            return Zero;
        }

        if (divisor.IsOne)
        {
            return dividend;
        }

        if (divisor == MinusOne)
        {
            return -dividend;
        }

        if (dividend.TryGetInt64(out long smallDividend) &&
            divisor.TryGetInt64(out long smallDivisor))
        {
            long remainder = smallDividend % smallDivisor;
            if (remainder != 0)
            {
                throw new InvalidOperationException("The exact integer division had a nonzero remainder.");
            }

            return new ExactInteger(smallDividend / smallDivisor);
        }

        NUMBER quotient = dividend.ToNumber();
        NUMBER denominator = divisor.ToNumber();
        Pak.divnumx(
            ref quotient,
            denominator,
            Math.Max(quotient.Cdigit, denominator.Cdigit) + 1);
        ExactInteger result = CreateCanonical(quotient);
        if (result * divisor != dividend)
        {
            throw new InvalidOperationException("The exact integer division had a nonzero remainder.");
        }

        return result;
    }

    public long GetBitLength()
    {
        if (IsZero)
        {
            return 0;
        }

        uint mostSignificant = Number.Mantissa[Number.CDigits - 1];
        int leadingBits = 0;
        while (mostSignificant != 0)
        {
            mostSignificant >>= 1;
            leadingBits++;
        }

        return checked(((long)Number.CDigits + Number.Exp - 1L) * 31L + leadingBits);
    }

    public int CompareTo(ExactInteger other)
    {
        int sign = Sign;
        int otherSign = other.Sign;
        if (sign != otherSign)
        {
            return sign.CompareTo(otherSign);
        }

        if (sign == 0)
        {
            return 0;
        }

        int magnitude = CompareMagnitude(Number, other.Number);
        return sign < 0 ? -magnitude : magnitude;
    }

    public bool Equals(ExactInteger other)
    {
        if (IsZero || other.IsZero)
        {
            return IsZero && other.IsZero;
        }

        EngineNumber left = Number;
        EngineNumber right = other.Number;
        if (left.Sign != right.Sign || left.Exp != right.Exp || left.CDigits != right.CDigits)
        {
            return false;
        }

        for (int index = 0; index < left.CDigits; index++)
        {
            if (left.Mantissa[index] != right.Mantissa[index])
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj)
    {
        return obj is ExactInteger other && Equals(other);
    }

    public override int GetHashCode()
    {
        if (IsZero)
        {
            return 0;
        }

        unchecked
        {
            int hash = Number.Sign;
            hash = (hash * 397) ^ Number.Exp;
            hash = (hash * 397) ^ Number.CDigits;
            for (int index = 0; index < Number.CDigits; index++)
            {
                hash = (hash * 397) ^ (int)Number.Mantissa[index];
            }

            return hash;
        }
    }

    public override string ToString()
    {
        return ToInvariantString();
    }

    private string ToInvariantString()
    {
        if (TryGetUInt64Magnitude(out ulong magnitude))
        {
            string magnitudeText = magnitude.ToString(CultureInfo.InvariantCulture);
            return Sign < 0 ? string.Concat("-", magnitudeText) : magnitudeText;
        }

        RAT? value = ToRat();
        try
        {
            int decimalDigits = checked((int)Math.Ceiling(GetBitLength() * Math.Log10(2))) + 2;
            return Pak.RatToString(
                ref value,
                NumberFormat.FloatingPoint,
                DisplayRadix,
                Math.Max(2, decimalDigits));
        }
        finally
        {
            RatPak.destroyrat(ref value);
        }
    }

    public string ToString(IFormatProvider? formatProvider)
    {
        _ = formatProvider;
        return ToInvariantString();
    }

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        if (!string.IsNullOrEmpty(format))
        {
            throw new FormatException("Exact integers do not support custom numeric formats.");
        }

        return ToString(formatProvider);
    }

    public static ExactInteger operator +(ExactInteger left, ExactInteger right)
    {
        if (left.IsZero)
        {
            return right;
        }

        if (right.IsZero)
        {
            return left;
        }

        if (left.TryGetInt64(out long smallLeft) && right.TryGetInt64(out long smallRight))
        {
            Int128 sum = (Int128)smallLeft + smallRight;
            if (sum >= (Int128)long.MinValue && sum <= (Int128)long.MaxValue)
            {
                return new ExactInteger((long)sum);
            }
        }

        NUMBER result = left.ToNumber();
        NUMBER addend = right.ToNumber();
        RatPak.addnum(ref result, addend, InternalRadix);
        return CreateCanonical(result);
    }

    public static ExactInteger operator -(ExactInteger left, ExactInteger right)
    {
        return left + -right;
    }

    public static ExactInteger operator *(ExactInteger left, ExactInteger right)
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

        if (left.TryGetInt64(out long smallLeft) && right.TryGetInt64(out long smallRight))
        {
            Int128 product = (Int128)smallLeft * smallRight;
            if (product >= (Int128)long.MinValue && product <= (Int128)long.MaxValue)
            {
                return new ExactInteger((long)product);
            }
        }

        NUMBER result = left.ToNumber();
        NUMBER factor = right.ToNumber();
        RatPak.mulnumx(ref result, factor);
        return CreateCanonical(result);
    }

    public static ExactInteger operator /(ExactInteger left, ExactInteger right)
    {
        return DivRem(left, right, out _);
    }

    public static ExactInteger operator %(ExactInteger left, ExactInteger right)
    {
        _ = DivRem(left, right, out ExactInteger remainder);
        return remainder;
    }

    public static ExactInteger operator -(ExactInteger value)
    {
        if (value.IsZero)
        {
            return value;
        }

        EngineNumber number = value.Number;
        return new ExactInteger(new EngineNumber(
            -number.Sign,
            number.Exp,
            number.CDigits,
            number.Mantissa));
    }

    public static ExactInteger operator ++(ExactInteger value)
    {
        return value + One;
    }

    public static ExactInteger operator --(ExactInteger value)
    {
        return value - One;
    }

    public static ExactInteger operator <<(ExactInteger value, int shift)
    {
        if (value.IsZero || shift == 0)
        {
            return value;
        }

        if (shift == int.MinValue)
        {
            return value.Sign < 0 ? MinusOne : Zero;
        }

        return shift < 0
            ? value >> -shift
            : value * Pow(new ExactInteger(2), shift);
    }

    public static ExactInteger operator >>(ExactInteger value, int shift)
    {
        if (value.IsZero || shift == 0)
        {
            return value;
        }

        if (shift == int.MinValue)
        {
            throw new OverflowException("The requested exact-integer shift is too large.");
        }

        if (shift < 0)
        {
            return value << -shift;
        }

        if (shift >= value.GetBitLength())
        {
            return value.Sign < 0 ? MinusOne : Zero;
        }

        ExactInteger divisor = Pow(new ExactInteger(2), shift);
        ExactInteger quotient = DivRem(value, divisor, out ExactInteger remainder);
        return value.Sign < 0 && !remainder.IsZero ? quotient - One : quotient;
    }

    public static implicit operator ExactInteger(int value)
    {
        return new ExactInteger(value);
    }

    public static implicit operator ExactInteger(long value)
    {
        return new ExactInteger(value);
    }

    public static explicit operator int(ExactInteger value)
    {
        if (!value.TryGetInt64(out long result) || result is < int.MinValue or > int.MaxValue)
        {
            throw new OverflowException("The exact integer does not fit in Int32.");
        }

        return (int)result;
    }

    public static explicit operator long(ExactInteger value)
    {
        if (!value.TryGetInt64(out long result))
        {
            throw new OverflowException("The exact integer does not fit in Int64.");
        }

        return result;
    }

    public static bool operator ==(ExactInteger left, ExactInteger right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ExactInteger left, ExactInteger right)
    {
        return !left.Equals(right);
    }

    public static bool operator <(ExactInteger left, ExactInteger right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(ExactInteger left, ExactInteger right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(ExactInteger left, ExactInteger right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(ExactInteger left, ExactInteger right)
    {
        return left.CompareTo(right) >= 0;
    }

    internal static bool TrySquareRoot(ExactInteger value, out ExactInteger root)
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

        ExactInteger candidate = One << checked((int)((value.GetBitLength() + 1) / 2));
        while (true)
        {
            ExactInteger next = (candidate + value / candidate) >> 1;
            if (next >= candidate)
            {
                root = candidate;
                return candidate * candidate == value;
            }

            candidate = next;
        }
    }

    private static ExactInteger Remainder(ExactInteger left, ExactInteger right)
    {
        if (right.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (left.IsZero || right.IsOne || right == MinusOne)
        {
            return Zero;
        }

        if (left.TryGetInt64(out long smallLeft) && right.TryGetInt64(out long smallRight))
        {
            return new ExactInteger(smallLeft % smallRight);
        }

        if (Abs(left) < Abs(right))
        {
            return left;
        }

        if (Abs(left) == Abs(right))
        {
            return Zero;
        }

        RAT? leftRat = left.ToRat();
        RAT? rightRat = right.ToRat();
        try
        {
            RatPak.remrat(ref leftRat, rightRat);
            ExactInteger numerator = CreateCanonical(leftRat.Pp);
            ExactInteger denominator = CreateCanonical(leftRat.Pq);
            ExactInteger remainder = denominator.IsOne
                ? numerator
                : DivideExactly(numerator, denominator);
            if (Abs(remainder) >= Abs(right) ||
                (!remainder.IsZero && remainder.Sign != left.Sign))
            {
                throw new InvalidOperationException("RatPak returned an invalid integer remainder.");
            }

            return remainder;
        }
        finally
        {
            RatPak.destroyrat(ref leftRat);
            RatPak.destroyrat(ref rightRat);
        }
    }

    private NUMBER ToNumber()
    {
        return Number.ToPNUMBER();
    }

    private RAT ToRat()
    {
        RAT result = RatPak.createrat();
        result.Pp = ToNumber();
        result.Pq = OneNumber.ToPNUMBER();
        return result;
    }

    private static ExactInteger CreateCanonical(NUMBER number)
    {
        EngineNumber snapshot = CanonicalSnapshot(number);
        return snapshot.IsZero() ? Zero : new ExactInteger(snapshot);
    }

    private static EngineNumber CanonicalSnapshot(NUMBER number)
    {
        if (RatPak.zernum(number))
        {
            return ZeroNumber;
        }

        const uint digitMask = InternalRadix - 1;
        var digits = new uint[number.Cdigit];
        for (int index = 0; index < digits.Length; index++)
        {
            // RatPak's internal radix is 2^31. Its long-division port can
            // leave the reserved high bit set on a quotient limb; all native
            // NUMBER consumers mask that bit as part of BASEX arithmetic.
            // Snapshot only the 31 value bits so the immutable representation
            // cannot feed the reserved bit back into later exact operations.
            digits[index] = number.Mant[index] & digitMask;
        }

        int first = 0;
        int last = digits.Length - 1;
        while (first < last && digits[first] == 0)
        {
            first++;
        }

        while (last > first && digits[last] == 0)
        {
            last--;
        }

        if (first == last && digits[first] == 0)
        {
            return ZeroNumber;
        }

        int exponent = checked(number.Exp + first);
        if (exponent < 0)
        {
            throw new InvalidOperationException("RatPak NUMBER is not an integer.");
        }

        var mantissa = new uint[last - first + 1];
        for (int index = 0; index < mantissa.Length; index++)
        {
            mantissa[index] = digits[first + index];
        }

        return new EngineNumber(number.Sign < 0 ? -1 : 1, exponent, mantissa.Length, mantissa);
    }

    private static int CompareMagnitude(EngineNumber left, EngineNumber right)
    {
        int leftLength = checked(left.CDigits + left.Exp);
        int rightLength = checked(right.CDigits + right.Exp);
        int lengthComparison = leftLength.CompareTo(rightLength);
        if (lengthComparison != 0)
        {
            return lengthComparison;
        }

        for (int position = leftLength - 1; position >= 0; position--)
        {
            uint leftDigit = position >= left.Exp && position - left.Exp < left.CDigits
                ? left.Mantissa[position - left.Exp]
                : 0;
            uint rightDigit = position >= right.Exp && position - right.Exp < right.CDigits
                ? right.Mantissa[position - right.Exp]
                : 0;
            int comparison = leftDigit.CompareTo(rightDigit);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }

    private static bool IsOneNumber(NUMBER number)
    {
        return number.Sign == 1 &&
               number.Exp == 0 &&
               number.Cdigit == 1 &&
               number.Mant[0] == 1;
    }

    private bool TryGetInt64(out long value)
    {
        if (!TryGetUInt64Magnitude(out ulong magnitude))
        {
            value = default;
            return false;
        }

        if (Sign >= 0)
        {
            if (magnitude > long.MaxValue)
            {
                value = default;
                return false;
            }

            value = (long)magnitude;
            return true;
        }

        const ulong minimumMagnitude = 1UL << 63;
        if (magnitude > minimumMagnitude)
        {
            value = default;
            return false;
        }

        value = magnitude == minimumMagnitude ? long.MinValue : -(long)magnitude;
        return true;
    }

    private bool TryGetUInt64Magnitude(out ulong magnitude)
    {
        if (IsZero)
        {
            magnitude = 0;
            return true;
        }

        EngineNumber number = Number;
        int totalDigits = checked(number.CDigits + number.Exp);
        if (totalDigits > 3)
        {
            magnitude = default;
            return false;
        }

        magnitude = 0;
        for (int position = totalDigits - 1; position >= 0; position--)
        {
            if (magnitude > ulong.MaxValue >> 31)
            {
                magnitude = default;
                return false;
            }

            magnitude <<= 31;
            uint digit = position >= number.Exp
                ? number.Mantissa[position - number.Exp]
                : 0;
            if (magnitude > ulong.MaxValue - digit)
            {
                magnitude = default;
                return false;
            }

            magnitude += digit;
        }

        return true;
    }
}
