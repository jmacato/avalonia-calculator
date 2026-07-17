// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;

namespace CalcEngine;

/// <summary>
/// Decimal parsing and presentation helpers that keep converter arithmetic in
/// RatPak from source value through the final rounding step.
/// </summary>
public static class RatPakDecimal
{
    public const uint Radix = 10;
    public const int Precision = 128;

    public static Rational Parse(RatPak ratPak, string invariantValue)
    {
        if (ratPak is null)
        {
            throw new ArgumentNullException(nameof(ratPak));
        }

        if (string.IsNullOrWhiteSpace(invariantValue))
        {
            throw new FormatException("A decimal value is required.");
        }

        string value = invariantValue.Trim();
        int divisionSeparator = value.IndexOf('/');
        if (divisionSeparator >= 0)
        {
            if (divisionSeparator != value.LastIndexOf('/'))
            {
                throw new FormatException($"'{invariantValue}' is not a decimal value.");
            }

            Rational numerator = Parse(ratPak, value.Substring(0, divisionSeparator));
            Rational denominator = Parse(ratPak, value.Substring(divisionSeparator + 1));
            return numerator / denominator;
        }

        bool mantissaIsNegative = value[0] == '-';
        if (mantissaIsNegative || value[0] == '+')
        {
            value = value.Substring(1);
        }

        int exponentSeparator = value.IndexOf('e');
        int upperExponentSeparator = value.IndexOf('E');
        if (exponentSeparator < 0 ||
            (upperExponentSeparator >= 0 && upperExponentSeparator < exponentSeparator))
        {
            exponentSeparator = upperExponentSeparator;
        }
        string mantissa = exponentSeparator < 0 ? value : value.Substring(0, exponentSeparator);
        string exponent = exponentSeparator < 0 ? string.Empty : value.Substring(exponentSeparator + 1);
        bool exponentIsNegative = exponent.StartsWith("-", StringComparison.Ordinal);
        if (exponentIsNegative || exponent.StartsWith("+", StringComparison.Ordinal))
        {
            exponent = exponent.Substring(1);
        }

        if (mantissa.Length == 0 || (exponentSeparator >= 0 && exponent.Length == 0))
        {
            throw new FormatException($"'{invariantValue}' is not a decimal value.");
        }

        PRAT? parsed = ratPak.StringToRat(
            mantissaIsNegative,
            mantissa,
            exponentIsNegative,
            exponent,
            Radix,
            Precision);
        if (parsed is null)
        {
            throw new FormatException($"'{invariantValue}' is not a decimal value.");
        }

        try
        {
            return new Rational(ratPak, parsed);
        }
        finally
        {
            RatPak.destroyrat(ref parsed);
        }
    }

    public static Rational Round(
        RatPak ratPak,
        Rational value,
        int fractionDigits,
        RatPakRoundingMode midpointRounding)
    {
        if (ratPak is null)
        {
            throw new ArgumentNullException(nameof(ratPak));
        }

        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (fractionDigits < 0 || fractionDigits >= Precision)
        {
            throw new ArgumentOutOfRangeException(nameof(fractionDigits));
        }

        Rational zero = new(ratPak, 0);
        Rational one = new(ratPak, 1);
        Rational two = new(ratPak, 2);
        Rational scale = Parse(ratPak, "1" + new string('0', fractionDigits));
        Rational scaled = value * scale;
        Rational integral = RationalMath.Integral(ratPak, scaled);
        Rational remainder = RationalMath.Abs(ratPak, scaled - integral);
        Rational half = one / two;

        bool increment = remainder > half;
        if (remainder == half)
        {
            switch (midpointRounding)
            {
                case RatPakRoundingMode.AwayFromZero:
                    increment = true;
                    break;
                case RatPakRoundingMode.ToEven:
                    increment = RationalMath.Abs(ratPak, integral) % two != zero;
                    break;
                case RatPakRoundingMode.HalfDown:
                    increment = false;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(midpointRounding));
            }
        }

        if (increment)
        {
            integral = value < zero ? integral - one : integral + one;
        }

        return integral / scale;
    }

    public static string FormatFixed(
        RatPak ratPak,
        Rational value,
        int fractionDigits,
        RatPakRoundingMode midpointRounding = RatPakRoundingMode.ToEven)
    {
        Rational rounded = Round(ratPak, value, fractionDigits, midpointRounding);
        string plain = ExpandScientific(
            rounded.ToString(Radix, NumberFormat.FloatingPoint, Precision));

        bool isNegative = plain.StartsWith("-", StringComparison.Ordinal);
        string unsigned = isNegative ? plain.Substring(1) : plain;
        int decimalSeparator = unsigned.IndexOf('.');
        string whole = decimalSeparator < 0 ? unsigned : unsigned.Substring(0, decimalSeparator);
        string fraction = decimalSeparator < 0 ? string.Empty : unsigned.Substring(decimalSeparator + 1);

        if (fraction.Length > fractionDigits)
        {
            fraction = fraction.Substring(0, fractionDigits);
        }
        else if (fraction.Length < fractionDigits)
        {
            fraction += new string('0', fractionDigits - fraction.Length);
        }

        string result = fractionDigits == 0 ? whole : whole + "." + fraction;
        return isNegative ? "-" + result : result;
    }

    public static string FormatScientific(
        RatPak ratPak,
        Rational value,
        int fractionDigits,
        RatPakRoundingMode midpointRounding = RatPakRoundingMode.ToEven)
    {
        Rational zero = new(ratPak, 0);
        if (value == zero)
        {
            return FormatFixed(ratPak, value, fractionDigits, midpointRounding) + "e+0";
        }

        int exponent = GetDecimalExponent(ratPak, value);
        Rational scale = Pow10(ratPak, Math.Abs(exponent));
        Rational mantissa = exponent >= 0 ? value / scale : value * scale;
        mantissa = Round(ratPak, mantissa, fractionDigits, midpointRounding);

        Rational ten = new(ratPak, 10);
        if (RationalMath.Abs(ratPak, mantissa) >= ten)
        {
            mantissa /= ten;
            exponent++;
        }

        string exponentSign = exponent < 0 ? "-" : "+";
        return FormatFixed(ratPak, mantissa, fractionDigits, midpointRounding)
               + "e"
               + exponentSign
               + Math.Abs(exponent).ToString(CultureInfo.InvariantCulture);
    }

    public static int GetWholeDigitCount(RatPak ratPak, Rational value)
    {
        Rational absolute = RationalMath.Abs(ratPak, value);
        Rational one = new(ratPak, 1);
        return absolute < one ? 1 : GetDecimalExponent(ratPak, absolute) + 1;
    }

    public static int GetDecimalExponent(RatPak ratPak, Rational value)
    {
        Rational absolute = RationalMath.Abs(ratPak, value);
        Rational zero = new(ratPak, 0);
        if (absolute == zero)
        {
            return 0;
        }

        Rational one = new(ratPak, 1);
        Rational ten = new(ratPak, 10);
        int exponent = 0;
        if (absolute >= one)
        {
            while (absolute >= ten)
            {
                absolute /= ten;
                exponent++;
            }
        }
        else
        {
            while (absolute < one)
            {
                absolute *= ten;
                exponent--;
            }
        }

        return exponent;
    }

    private static Rational Pow10(RatPak ratPak, int exponent) =>
        Parse(ratPak, "1" + new string('0', exponent));

    private static string ExpandScientific(string value)
    {
        int exponentSeparator = value.IndexOf('e');
        if (exponentSeparator < 0)
        {
            return value;
        }

        bool isNegative = value.StartsWith("-", StringComparison.Ordinal);
        string mantissa = value.Substring(isNegative ? 1 : 0, exponentSeparator - (isNegative ? 1 : 0));
        int exponent = int.Parse(value.Substring(exponentSeparator + 1), CultureInfo.InvariantCulture);
        int decimalSeparator = mantissa.IndexOf('.');
        int decimalPosition = decimalSeparator < 0 ? mantissa.Length : decimalSeparator;
        string digits = decimalSeparator < 0 ? mantissa : mantissa.Remove(decimalSeparator, 1);
        int newDecimalPosition = decimalPosition + exponent;

        string result;
        if (newDecimalPosition <= 0)
        {
            result = "0." + new string('0', -newDecimalPosition) + digits;
        }
        else if (newDecimalPosition >= digits.Length)
        {
            result = digits + new string('0', newDecimalPosition - digits.Length);
        }
        else
        {
            result = digits.Insert(newDecimalPosition, ".");
        }

        return isNegative ? "-" + result : result;
    }
}
