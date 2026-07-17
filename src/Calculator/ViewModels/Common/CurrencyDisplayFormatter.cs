// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using CalcEngine;
using CalculatorApp.ViewModel.DataLoaders;

namespace CalculatorApp.ViewModel.Common;

/// <summary>
/// Portable counterpart of the WinUI CurrencyFormatter configuration used by
/// UnitConverterViewModel. RatPak owns parsing and half-down rounding; the
/// platform currency format contributes only grouping, separators, and signs.
/// </summary>
internal sealed class CurrencyDisplayFormatter
{
    private readonly RatPak _ratPak = new(RatPakDecimal.Precision);

    internal string Format(
        string invariantValue,
        string isoCode,
        NumberFormatInfo numberFormat)
    {
        int exponentPosition = invariantValue.IndexOfAny(['e', 'E']);
        if (exponentPosition >= 0 && exponentPosition < invariantValue.Length - 1)
        {
            string exponent = invariantValue[(exponentPosition + 1)..];
            string exponentSign = string.Empty;
            if (exponent.StartsWith('+') || exponent.StartsWith('-'))
            {
                exponentSign = exponent[..1];
                exponent = exponent[1..];
            }

            return Format(invariantValue[..exponentPosition], isoCode, numberFormat)
                   + "e"
                   + exponentSign
                   + Format(exponent, isoCode, numberFormat);
        }

        // The WinUI CurrencyFormatter is reset to zero fraction digits for
        // every value, then restores the selected currency's scale only when
        // the engine string contains a decimal point. This keeps an untouched
        // zero as "0", while partial input such as "2." displays "2.00" for
        // a two-fraction-digit currency.
        int fractionDigits = invariantValue.Contains('.', StringComparison.Ordinal)
            ? CldrCurrencyNameProvider.GetFractionDigits(isoCode)
            : 0;
        Rational value;
        try
        {
            value = RatPakDecimal.Parse(_ratPak, invariantValue);
        }
        catch (FormatException)
        {
            return LocalizeDecimalSeparator(invariantValue, numberFormat);
        }

        string rounded = RatPakDecimal.FormatFixed(
            _ratPak,
            value,
            fractionDigits,
            RatPakRoundingMode.HalfDown);
        bool isNegative = rounded.StartsWith('-') ||
                          invariantValue.StartsWith('-');
        if (rounded.StartsWith('-'))
        {
            rounded = rounded.Substring(1);
        }

        int decimalSeparator = rounded.IndexOf('.', StringComparison.Ordinal);
        string whole = decimalSeparator < 0 ? rounded : rounded.Substring(0, decimalSeparator);
        string fraction = decimalSeparator < 0 ? string.Empty : rounded.Substring(decimalSeparator + 1);
        string grouped = ApplyGrouping(
            whole,
            numberFormat.CurrencyGroupSeparator,
            numberFormat.CurrencyGroupSizes);
        string localized = fractionDigits == 0
            ? grouped
            : grouped + numberFormat.CurrencyDecimalSeparator + fraction;
        localized = LocalizeDigits(localized, numberFormat);

        return isNegative ? ApplyCurrencyNegativePattern(localized, numberFormat) : localized;
    }

    internal static string LocalizeDigits(string value, NumberFormatInfo numberFormat)
    {
        string[] nativeDigits = numberFormat.NativeDigits;
        System.Text.StringBuilder localized = new(value.Length);
        foreach (char character in value)
        {
            if (character is >= '0' and <= '9')
            {
                localized.Append(nativeDigits[character - '0']);
            }
            else
            {
                localized.Append(character);
            }
        }

        return localized.ToString();
    }

    internal static string ApplyGrouping(string whole, string separator, int[] groupSizes)
    {
        if (string.IsNullOrEmpty(separator) || groupSizes.Length == 0 || groupSizes[0] == 0)
        {
            return whole;
        }

        List<string> groups = new();
        int remaining = whole.Length;
        int groupIndex = 0;
        int groupSize = groupSizes[groupIndex];
        while (remaining > groupSize && groupSize > 0)
        {
            groups.Add(whole.Substring(remaining - groupSize, groupSize));
            remaining -= groupSize;
            if (groupIndex < groupSizes.Length - 1)
            {
                groupIndex++;
                groupSize = groupSizes[groupIndex];
            }
        }

        groups.Add(whole.Substring(0, remaining));
        groups.Reverse();
        return string.Join(separator, groups);
    }

    private static string ApplyCurrencyNegativePattern(
        string value,
        NumberFormatInfo numberFormat)
    {
        string sign = numberFormat.NegativeSign;
        const string currencyToken = "\uFFF0";
        string formatted = numberFormat.CurrencyNegativePattern switch
        {
            0 => "(" + currencyToken + value + ")",
            1 => sign + currencyToken + value,
            2 => currencyToken + sign + value,
            3 => currencyToken + value + sign,
            4 => "(" + value + currencyToken + ")",
            5 => sign + value + currencyToken,
            6 => value + sign + currencyToken,
            7 => value + currencyToken + sign,
            8 => sign + value + " " + currencyToken,
            9 => sign + currencyToken + " " + value,
            10 => value + " " + currencyToken + sign,
            11 => currencyToken + " " + value + sign,
            12 => currencyToken + " " + sign + value,
            13 => value + sign + " " + currencyToken,
            14 => "(" + currencyToken + " " + value + ")",
            15 => "(" + value + " " + currencyToken + ")",
            _ => sign + currencyToken + value
        };

        // WinUI's CurrencyFormatter is configured to emit the ISO code. The
        // original view model removes that code and only trims whitespace at
        // the outer edges, so preserve any pattern-significant inner space.
        formatted = formatted.Replace(currencyToken, string.Empty, StringComparison.Ordinal).Trim();

        // The original normalizes cultures that place the minus at the end so
        // input and display retain Calculator's leading-sign convention.
        if (formatted.EndsWith(sign, StringComparison.Ordinal))
        {
            formatted = string.Concat(sign, formatted.AsSpan(0, formatted.Length - sign.Length));
        }

        return formatted;
    }

    private static string LocalizeDecimalSeparator(
        string invariantValue,
        NumberFormatInfo numberFormat) =>
        numberFormat.CurrencyDecimalSeparator == "."
            ? invariantValue
            : invariantValue.Replace(
                ".",
                numberFormat.CurrencyDecimalSeparator,
                StringComparison.Ordinal);
}
