// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CalcEngine;
using CalculationManager;
using uint32_t = System.UInt32;
using int32_t = System.Int32;
using PNUMBER = CalcEngine.RatPak.NUMBER;
using PRAT = CalcEngine.RatPak.RAT;
using wchar_t = char;
using wstring_view = string;
using wstring = string;
using size_t = int;

namespace CalcEngine;

public partial class CalcNumSec
{
    public void Clear()
    {
        value = string.Empty;
        m_isNegative = false;
    }
}

public partial class CalcInput
{
    const int MAX_STRLEN = 84;

    const int C_NUM_MAX_DIGITS = MAX_STRLEN;
    const int C_EXP_MAX_DIGITS = 4;

    public void Clear()
    {
        m_base.Clear();
        m_exponent.Clear();
        m_hasExponent = false;
        m_hasDecimal = false;
        m_decPtIndex = 0;
    }

    public bool TryToggleSign(bool isIntegerMode, wstring_view maxNumStr)
    {
        // Zero is always positive
        if (m_base.IsEmpty())
        {
            m_base.IsNegative(false);
            m_exponent.IsNegative(false);
        }
        else if (m_hasExponent)
        {
            m_exponent.IsNegative(!m_exponent.IsNegative());
        }
        else
        {
            // When in integer only mode, it isn't always allowed to toggle, as toggling can cause the num to be out of
            // bounds. For eg. in byte -128 is valid, but when it toggled it becomes 128, which is more than 127.
            if (isIntegerMode && m_base.IsNegative())
            {
                // Decide if this additional digit will fit for the given bit width
                if (m_base.value.Length >= maxNumStr.Length && m_base.value.Last() > maxNumStr.Last())
                {
                    // Last digit is more than the allowed positive number. Fail
                    return false;
                }
            }

            m_base.IsNegative(!m_base.IsNegative());
        }

        return true;
    }

    public bool TryAddDigit(uint value, uint32_t radix, bool isIntegerMode, wstring_view maxNumStr, int32_t
        wordBitWidth, int maxDigits)
    {
        // Convert from an integer into a character
        // This includes both normal digits and alpha 'digits' for radixes > 10
        var chDigit = (wchar_t)((value < 10) ? ('0' + value) : ('A' + value - 10));

        CalcNumSec pNumSec;
        size_t maxCount;
        if (m_hasExponent)
        {
            pNumSec = m_exponent;
            maxCount = C_EXP_MAX_DIGITS;
        }
        else
        {
            pNumSec = m_base;
            maxCount = maxDigits;
            // Don't include the decimal point in the count. In that way you can enter the maximum allowed precision.
            // Precision doesn't include decimal point.
            if (HasDecimalPt())
            {
                maxCount++;
            }

            // First leading 0 is not counted in input restriction as the output can be of that form
            // See NumberToString algorithm. REVIEW: We don't have such input restriction mimicking based on output of NumberToString for exponent
            // NumberToString can give 10 digit exponent, but we still restrict the exponent here to be only 4 digits.
            if (!pNumSec.IsEmpty() && pNumSec.value.First() == '0')
            {
                maxCount++;
            }
        }

        // Ignore leading zeros
        if (pNumSec.IsEmpty() && (value == 0))
        {
            return true;
        }

        if (pNumSec.value.Length < maxCount)
        {
            pNumSec.value += chDigit;
            return true;
        }

        // if we are in integer mode, within the base, and we're on the last digit then
        // there are special cases where we can actually add one more digit.
        if (isIntegerMode && pNumSec.value.Length == maxCount && !m_hasExponent)
        {
            bool allowExtraDigit = false;

            if (radix == 8)
            {
                switch (wordBitWidth % 3)
                {
                    case 1:
                        // in 16 or 64bit word size, if the first digit is a 1 we can enter 6 (16bit) or 22 (64bit) digits
                        allowExtraDigit = (pNumSec.value.First() == '1');
                        break;

                    case 2:
                        // in 8 or 32bit word size, if the first digit is a 3 or less we can enter 3 (8bit) or 11 (32bit) digits
                        allowExtraDigit = (pNumSec.value.First() <= '3');
                        break;
                }
            }
            else if (radix == 10)
            {
                // If value length is at least the max, we know we can't add another digit.
                if (pNumSec.value.Length < maxNumStr.Length)
                {
                    // Compare value to substring of maxNumStr of value.Length length.
                    // If cmpResult > 0:
                    // eg. max is "127", and the current number is "20". first digit itself says we are out.
                    // Additional digit is not possible

                    // If cmpResult < 0:
                    // Success case. eg. max is "127", and current number is say "11". The second digit '1' being <
                    // corresponding digit '2', means all digits are possible to append, like 119 will still be < 127

                    // If cmpResult == 0:
                    // Undecided still. The case when max is "127", and current number is "12". Look for the new number being 7 or less to allow
                    // var cmpResult = pNumSec.value.compare(0, wstring::npos, maxNumStr, 0, pNumSec.value.Length);
                    var cmpResult = string.Compare(pNumSec.value, 0, maxNumStr, 0, pNumSec.value.Length);
                    if (cmpResult < 0)
                    {
                        allowExtraDigit = true;
                    }
                    else if (cmpResult == 0)
                    {
                        var lastChar = maxNumStr[pNumSec.value.Length];
                        if (chDigit <= lastChar)
                        {
                            allowExtraDigit = true;
                        }
                        else if (pNumSec.IsNegative() && chDigit <= lastChar + 1)
                        {
                            // Negative value case, eg. max is "127", and current number is "-12". Then 8 is also valid, as the range
                            // is always from -(max+1)...max in signed mode
                            allowExtraDigit = true;
                        }
                    }
                }
            }

            if (allowExtraDigit)
            {
                pNumSec.value += chDigit;
                return true;
            }
        }

        return false;
    }

    public bool TryAddDecimalPt()
    {
        // Already have a decimal pt or we're in the exponent
        if (m_hasDecimal || m_hasExponent)
        {
            return false;
        }

        if (m_base.IsEmpty())
        {
            m_base.value += '0'; // Add a leading zero
        }

        m_decPtIndex = m_base.value.Length;
        m_base.value += m_decSymbol;
        m_hasDecimal = true;

        return true;
    }

    public bool HasDecimalPt()
    {
        return m_hasDecimal;
    }

    public bool TryBeginExponent()
    {
        // For compatibility, add a trailing dec point to base num if it doesn't have one
        TryAddDecimalPt();

        if (m_hasExponent) // Already entering exponent
        {
            return false;
        }

        m_hasExponent = true; // Entering exponent
        return true;
    }

    public void Backspace()
    {
        if (m_hasExponent)
        {
            if (!m_exponent.IsEmpty())
            {
                m_exponent.value = m_exponent.value[..^1];
                if (m_exponent.IsEmpty())
                {
                    m_exponent.Clear();
                }
            }
            else
            {
                m_hasExponent = false;
            }
        }
        else
        {
            if (!m_base.IsEmpty())
            {
                m_base.value = m_base.value[..^1];

                if (m_base.value == "0")
                {
                    m_base.value = m_base.value[..^1];
                }
            }

            if (m_base.value.Length <= m_decPtIndex)
            {
                // Backed up over decimal point
                m_hasDecimal = false;
                m_decPtIndex = 0;
            }

            if (m_base.IsEmpty())
            {
                m_base.Clear();
            }
        }
    }

    public void SetDecimalSymbol(wchar_t decSymbol)
    {
        if (m_decSymbol != decSymbol)
        {
            m_decSymbol = decSymbol;

            if (m_hasDecimal)
            {
                // Change to new decimal pt
                // TODO: There must be a better way of doing this in C#.
                char[] chars = m_base.value.ToCharArray();
                chars[m_decPtIndex] = m_decSymbol;
                m_base.value = new string(chars);
            }
        }
    }

    public bool IsEmpty()
    {
        return m_base.IsEmpty() && !m_hasExponent && m_exponent.IsEmpty() && !m_hasDecimal;
    }

    public wstring ToString(uint32_t radix)
    {
        wstring result = string.Empty;
        ;

        // In theory both the base and exponent could be C_NUM_MAX_DIGITS long.
        if ((m_base.value.Length > MAX_STRLEN) || (m_hasExponent && m_exponent.value.Length > MAX_STRLEN))
        {
            return result;
        }


        if (m_base.IsNegative())
        {
            result += '-';
        }

        if (m_base.IsEmpty())
        {
            result += '0';
        }
        else
        {
            result += m_base.value;
        }

        if (m_hasExponent)
        {
            // Add a decimal point if it is not already there
            if (!m_hasDecimal)
            {
                result += m_decSymbol;
            }

            result += ((radix == 10) ? 'e' : '^');
            result += (m_exponent.IsNegative() ? '-' : '+');

            if (m_exponent.IsEmpty())
            {
                result += '0';
            }
            else
            {
                result += m_exponent.value;
            }
        }

        // Base and Exp can each be up to C_NUM_MAX_DIGITS in length, plus 4 characters for sign, dec, exp, and expSign.
        if (result.Length > C_NUM_MAX_DIGITS * 2 + 4)
        {
            return string.Empty;
        }

        return result;
    }

    public Rational ToRational(RatPak ratPak, uint32_t radix, int32_t precision)
    {
        PRAT rat = ratPak.StringToRat(m_base.IsNegative(), m_base.value, m_exponent.IsNegative(), m_exponent.value,
            radix,
            precision);
        if (rat == null)
        {
            return new Rational(ratPak, 0);
        }

        var ret = new Rational(ratPak, rat);


        ratPak.destroyrat(ref rat);

        return ret;
    }
}
