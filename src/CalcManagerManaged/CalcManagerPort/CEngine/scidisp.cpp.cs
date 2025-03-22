// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

/****************************Module*Header***********************************\
* Module Name: SCIDISP.C
*
* Module Description:
*
* Warnings:
*
* Created:
*
* Author:
\****************************************************************************/

using System.Text;
using System.Text.RegularExpressions;
using uint32_t = System.UInt32;
using wstring_view = string;
using wstring = string;


namespace CalcEngine;

public partial class CCalcEngine
{
    const int MAX_EXPONENT = 4;
    const uint32_t MAX_GROUPING_SIZE = 16;
    const wstring_view c_decPreSepStr = "[+-]?(\\d*)[";
    const wstring_view c_decPostSepStr = "]?(\\d*)(?:e[+-]?(\\d*))?$";

    /****************************************************************************\
    * void DisplayNum(void)
    *
    * Convert m_currentVal to a string in the current radix.
    *
    * Updates the following variables:
    *   m_currentVal, m_numberString
    \****************************************************************************/
    //
    // State of calc last time DisplayNum was called
    //
    struct LASTDISP
    {
        public Rational? value;
        public int precision;
        public uint radix;
        public int nFE;
        public NUM_WIDTH numwidth;
        public bool fIntMath;
        public bool bRecord;
        public bool bUseSep;
    }

    private static LASTDISP gldPrevious = new LASTDISP
    {
        value = null,
        precision = -1,
        radix = 0,
        nFE = -1,
        numwidth = (NUM_WIDTH)(-1),
        fIntMath = false,
        bRecord = false,
        bUseSep = false
    };


// Truncates if too big, makes it a non negative - the number in rat. Doesn't do anything if not in INT mode
    Rational TruncateNumForIntMath(Rational rat)
    {
        if (!m_fIntegerMode)
        {
            return rat;
        }

        // Truncate to an integer. Do not round here.
        var result = RationalMath.Integer(m_ratPak, rat);

        // Can be converting a dec negative number to Hex/Oct/Bin rep. Use 2's complement form
        // Check the range.
        if (result < new Rational(m_ratPak, 0))
        {
            // if negative make positive by doing a twos complement
            result = -(result) - new Rational(m_ratPak, 1);
            result ^= GetChopNumber();
        }

        result &= GetChopNumber();

        return result;
    }

    void DisplayNum()
    {
        if (gldPrevious.value is null)
            gldPrevious.value = new Rational(m_ratPak, 0);

        //
        // Only change the display if
        //  we are in record mode                               -OR-
        //  this is the first time DisplayNum has been called,  -OR-
        //  something important has changed since the last time DisplayNum was
        //  called.
        //
        if (m_bRecord || gldPrevious.value != m_currentVal || gldPrevious.precision != m_precision ||
            gldPrevious.radix != m_radix || gldPrevious.nFE != (int)m_nFE
            || !gldPrevious.bUseSep || gldPrevious.numwidth != m_numwidth || gldPrevious.fIntMath != m_fIntegerMode ||
            gldPrevious.bRecord != m_bRecord)
        {
            gldPrevious.precision = m_precision;
            gldPrevious.radix = m_radix;
            gldPrevious.nFE = (int)m_nFE;
            gldPrevious.numwidth = m_numwidth;

            gldPrevious.fIntMath = m_fIntegerMode;
            gldPrevious.bRecord = m_bRecord;
            gldPrevious.bUseSep = true;

            if (m_bRecord)
            {
                // Display the string and return.
                m_numberString = m_input.ToString(m_radix);
            }
            else
            {
                // If we're in Programmer mode, perform integer truncation so e.g. 5 / 2 * 2 results in 4, not 5.
                if (m_fIntegerMode)
                {
                    m_currentVal = TruncateNumForIntMath(m_currentVal);
                }

                m_numberString = GetStringForDisplay(m_currentVal, m_radix);
            }

            // Displayed number can go through transformation. So copy it after transformation
            gldPrevious.value = m_currentVal;

            if ((m_radix == 10) && IsNumberInvalid(m_numberString, MAX_EXPONENT, m_precision, m_radix) != 0)
            {
                DisplayError(CalcErr.CALC_E_OVERFLOW);
            }
            else
            {
                // Display the string and return.
                SetPrimaryDisplay(GroupDigitsPerRadix(m_numberString, m_radix));
            }
        }
    }

    public int IsNumberInvalid(wstring numberString, int iMaxExp, int iMaxMantissa, uint32_t radix)
    {
        int iError = 0;

        if (radix == 10)
        {
            // C# Port: Added a ^ at the start so that it matches the whole string.
            // without it, the unit tests fails.

            // start with an optional + or -
            // followed by zero or more digits
            // followed by an optional decimal point
            // followed by zero or more digits
            // followed by an optional exponent
            // in case there's an exponent:
            //      its optionally followed by a + or -
            //      which is followed by zero or more digits
            var rx = new Regex(@"^" + c_decPreSepStr + m_decimalSeparator + c_decPostSepStr);

            var matches = rx.Match(numberString);

            // Check if it fully matches the string, otherwise bail out.
            // TODO: Not sure how to optimize this.
            if (rx.IsMatch(numberString))
            {
                // Check that exponent isn't too long
                if (matches.Groups[3].Length > iMaxExp)
                {
                    iError = EngineStrings.IDS_ERR_INPUT_OVERFLOW;
                }
                else
                {
                    wstring exp = matches.Groups[1].Value;
                    int intItr = 0;

                    // Count leading zeros
                    foreach (char c in exp)
                    {
                        if (c == '0')
                            intItr++;
                        else
                            break;
                    }

                    int iMantissa = exp.Length - intItr + matches.Groups[2].Length;
                    if (iMantissa > iMaxMantissa)
                    {
                        iError = EngineStrings.IDS_ERR_INPUT_OVERFLOW;
                    }
                }
            }
            else
            {
                iError = EngineStrings.IDS_ERR_UNK_CH;
            }
        }
        else
        {
            foreach (var c in numberString)
            {
                if (radix == 16)
                {
                    if (!(char.IsDigit(c) || (c >= 'A' && c <= 'F')))
                    {
                        iError = EngineStrings.IDS_ERR_UNK_CH;
                    }
                }
                else if (c < '0' || c >= '0' + radix)
                {
                    iError = EngineStrings.IDS_ERR_UNK_CH;
                }
            }
        }

        return iError;
    }

    /****************************************************************************\
    *
    * DigitGroupingStringToGroupingVector
    *
    * Description:
    *   This will take the digit grouping string found in the regional applet and
    *   represent this string as a vector.
    *
    *   groupingString
    *   0;0      - no grouping
    *   3;0      - group every 3 digits
    *   3        - group 1st 3, then no grouping after
    *   3;0;0    - group 1st 3, then no grouping after
    *   3;2;0    - group 1st 3 and then every 2 digits
    *   4;0      - group every 4 digits
    *   5;3;2;0  - group 5, then 3, then every 2
    *   5;3;2    - group 5, then 3, then 2, then no grouping after
    *
    * Returns: the groupings as a vector
    *
    \****************************************************************************/
    public List<uint32_t> DigitGroupingStringToGroupingVector(wstring_view groupingString)
    {
        var grouping = new List<uint32_t>();

        var str = groupingString;
        var index = 0;

        while (index < str.Length)
        {
            // Find the end of the current number
            var endIndex = index;
            while (endIndex < str.Length && char.IsDigit(str[endIndex]))
            {
                endIndex++;
            }

            // If we found digits
            if (endIndex > index)
            {
                // Parse the number
                if (uint32_t.TryParse(str.AsSpan(index, endIndex - index), out var currentGroup))
                {
                    // If we successfully parsed a group, add it to the grouping
                    if (currentGroup < MAX_GROUPING_SIZE)
                    {
                        grouping.Add(currentGroup);
                    }
                }

                // Move past the number
                index = endIndex;
            }

            // Skip any non-digit characters (like separators)
            while (index < str.Length && !char.IsDigit(str[index]))
            {
                index++;
            }
        }

        return grouping;
    }

    public wstring GroupDigitsPerRadix(wstring_view numberString, uint32_t radix)
    {
        if (string.IsNullOrEmpty(numberString))
        {
            return string.Empty;
        }

        switch (radix)
        {
            case 10:
                return GroupDigits(m_groupSeparator.ToString(), m_decGrouping, numberString, ('-' == numberString[0]));
            case 8:
                return GroupDigits(" ", [3, 0], numberString);
            case 2:
            case 16:
                return GroupDigits(" ", [4, 0], numberString);
            default:
                return numberString;
        }
    }

    /****************************************************************************\
   *
   * GroupDigits
   *
   * Description:
   *   This routine will take a grouping vector and the display string and
   *   add the separator according to the pattern indicated by the separator.
   *
   *   Grouping
   *   0,0      - no grouping
   *   3,0      - group every 3 digits
   *   3        - group 1st 3, then no grouping after
   *   3,0,0    - group 1st 3, then no grouping after
   *   3,2,0    - group 1st 3 and then every 2 digits
   *   4,0      - group every 4 digits
   *   5,3,2,0  - group 5, then 3, then every 2
   *   5,3,2    - group 5, then 3, then 2, then no grouping after
   *
   \***************************************************************************/
    public string GroupDigits(string delimiter, List<uint> grouping, string displayString, bool isNumNegative = false)
    {
        // if there's nothing to do, bail
        if (string.IsNullOrEmpty(delimiter) || grouping.Count == 0)
        {
            return displayString;
        }

        // Find the position of exponential 'e' in the string
        var exp = displayString.IndexOf('e');
        var hasExponent = (exp != -1);

        // Find the position of decimal point in the string
        var dec = displayString.IndexOf(m_decimalSeparator);
        var hasDecimal = (dec != -1);

        // Determine the end position of the portion subject to grouping
        int integerPartEnd;
        if (hasDecimal)
        {
            integerPartEnd = dec;
        }
        else if (hasExponent)
        {
            integerPartEnd = exp;
        }
        else
        {
            integerPartEnd = displayString.Length;
        }

        var result = new StringBuilder();
        var groupingSize = 0;

        // Initialize with the first grouping value
        var groupIdx = 0;
        var currGrouping = grouping[groupIdx];

        // Mark the 'start' of the string as either 0 or 1 if there is a negative sign
        // We exclude the sign here because we don't want to end up with e.g. "-,123,456"
        var startIdx = isNumNegative ? 1 : 0;

        // Process the integer part from right to left
        for (var i = integerPartEnd - 1; i >= startIdx; i--)
        {
            result.Append(displayString[i]);
            groupingSize++;

            // If a group is complete, add a separator
            // Do not add a separator if:
            // - grouping size is 0
            // - we are at the end of the digit string
            if (currGrouping != 0 && (groupingSize % currGrouping) == 0 && i > startIdx)
            {
                result.Append(delimiter);
                groupingSize = 0; // reset for a new group

                // Shift the grouping to next values if they exist

                // IMPORTANT: The original only checks if it's not equal the grouping.count,
                // so if groupingIdx is still zero then it passes this check and continues inside the if block.
                if (groupIdx < grouping.Count)
                {
                    groupIdx++;

                    // Loop through grouping vector until we find a non-zero value.
                    // "0" values may appear in a form of either e.g. "3;0" or "3;0;0".
                    // A 0 in the last position means repeat the previous grouping.
                    // A 0 in another position is a group. So, "3;0;0" means "group 3, then group 0 repeatedly"
                    // This could be expressed as just "3" but GetLocaleInfo is returning 3;0;0 in some cases instead.
                    currGrouping = 0;
                    for (; groupIdx < grouping.Count; groupIdx++)
                    {
                        // If it's a non-zero value, that's our new group
                        if (grouping[groupIdx] != 0)
                        {
                            currGrouping = grouping[groupIdx];
                            break;
                        }

                        // Otherwise, save the previous grouping in case we need to repeat it
                        currGrouping = grouping[groupIdx - 1];
                    }
                }
            }
        }

        // now copy the negative sign if it is there
        if (isNumNegative)
        {
            result.Append(displayString[0]);
        }

        // Reverse the string we've built (equivalent to C++'s reverse function)
        var formattedInteger = new string(result.ToString().Reverse().ToArray());

        // Add the right (fractional or exponential) part of the number
        // C# Substring is equivalent to C++ substr
        if (hasDecimal)
        {
            formattedInteger += displayString[dec..];
        }
        else if (hasExponent)
        {
            formattedInteger += displayString[exp..];
        }

        return formattedInteger;
    }
}
