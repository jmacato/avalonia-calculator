using CalcEngine;

namespace UnitConversionManager;

public static class NumberFormattingUtils
{
    /// <summary>
    /// Trims out any trailing zeros or decimals in the given input string
    /// </summary>
    /// <param name="number">number to trim</param>
    /// TODO: Check this to be compatible with localization, especially the hardcoded '.' comparisons.
    public static void TrimTrailingZeros(ref string number)
    {
        if (number is null)
        {
            throw new ArgumentNullException(nameof(number));
        }

        // If no decimal point exists, return the original string
        if (!number.Contains('.'))
        {
            return;
        }

        // Trim trailing zeros
        string result = number.TrimEnd('0');

        // If the result ends with a decimal point, remove it
        if (result.EndsWith(".", StringComparison.Ordinal))
        {
            result = result.Substring(0, result.Length - 1);
        }

        number = result;
    }

    /// <summary>
    /// Get number of digits (whole number part + decimal part)</summary>
    /// <param name="value">the number</param>
    public static uint GetNumberDigits(string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        TrimTrailingZeros(ref value);
        var numberSignificantDigits = (uint)(value.Length);
        if (value.Contains('.'))
        {
            --numberSignificantDigits;
        }

        if (value.Contains('-'))
        {
            --numberSignificantDigits;
        }

        return numberSignificantDigits;
    }

    /// <summary>
    /// Get number of digits (whole number part only)</summary>
    /// <param name="value">the number</param>
    public static uint GetNumberDigitsWholeNumberPart(RatPak ratPak, Rational value)
    {
        return (uint)RatPakDecimal.GetWholeDigitCount(ratPak, value);
    }

    /// <summary>
    /// Rounds the given double to the given number of significant digits
    /// </summary>
    /// <param name="num">input double</param>
    /// <param name="numSignificant">unsigned int number of significant digits to round to</param>
    public static string RoundSignificantDigits(RatPak ratPak, Rational num, uint numSignificant)
    {
        return RatPakDecimal.FormatFixed(ratPak, num, (int)numSignificant);
    }

    /// <summary>
    ///  Convert a Number to Scientific Notation
    /// </summary>
    /// <param name="number">number to convert</param>
    public static string ToScientificNumber(RatPak ratPak, Rational number)
    {
        return RatPakDecimal.FormatScientific(ratPak, number, 6);
    }
}
