using System;

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
        // If no decimal point exists, return the original string
        if (!number.Contains('.'))
        {
            return;
        }

        // Trim trailing zeros
        string result = number.TrimEnd('0');

        // If the result ends with a decimal point, remove it
        if (result.EndsWith("."))
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
    public static uint GetNumberDigitsWholeNumberPart(double value)
    {
        return value == 0 ? 1u : (uint)(1 + Math.Max(0.0, Math.Log10(Math.Abs(value))));
    }

    /// <summary>
    /// Rounds the given double to the given number of significant digits
    /// </summary>
    /// <param name="num">input double</param>
    /// <param name="numSignificant">unsigned int number of significant digits to round to</param>
    public static string RoundSignificantDigits(double num, uint numSignificant)
    {
        return num.ToString($"F{numSignificant}");
        // stringstream out(stringstream::out);
        // out << fixed;
        // out.precision(numSignificant);
        // out << num;
        // return out.str();
    }

    /// <summary>
    ///  Convert a Number to Scientific Notation
    /// </summary>
    /// <param name="number">number to convert</param>
    public static string ToScientificNumber(double number)
    {
        // First format with standard scientific notation
        var formatted = number.ToString("e6", System.Globalization.CultureInfo.InvariantCulture);

        // The following junk is to preserve C++ style formatting for double scientific notation strings.

        // Remove trailing zeros in the exponent part
        // Find the 'e' character
        var ePosition = formatted.IndexOf('e');
        if (ePosition < 0) return formatted; // Fallback to original if 'e' not found

        // Get the part before 'e'
        var mantissa = formatted[..(ePosition + 2)]; // Include 'e' and sign

        // Get the exponent part and remove leading zeros
        var exponent = formatted[(ePosition + 2)..].TrimStart('0');

        // If exponent is empty, it was just zeros, so use "0"
        if (string.IsNullOrEmpty(exponent))
            exponent = "0";

        return mantissa + exponent;
    }
}
