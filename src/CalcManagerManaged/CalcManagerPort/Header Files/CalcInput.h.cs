// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#pragma once

// #include "Rational.h"
using size_t = int;
using wchar_t = char;
using wstring = string;

// Space to hold enough digits for a quadword binary number (64) plus digit separator strings for that number (20)

namespace CalcEngine
{
    partial class CalcNumSec
    {
        // public:
        public CalcNumSec()
        {
            value = "";
            m_isNegative = (false);
        }

        // void Clear();
        public bool IsEmpty()
        {
            return string.IsNullOrEmpty(value);
        }

        public  bool IsNegative()
        {
            return m_isNegative;
        }

        public   void IsNegative(bool isNegative)
        {
            m_isNegative = isNegative;
        }

        public  wstring value ;

        bool m_isNegative;
    };

    public partial class CalcInput
    {
        // public:
        public CalcInput()
            : this('.')
        {
        }

        public CalcInput(wchar_t decSymbol)

        {
            m_base = new CalcNumSec();
            m_exponent = new CalcNumSec();
            m_decSymbol = (decSymbol);
        }
        //
        // void Clear();
        // bool TryToggleSign(bool isIntegerMode, std::wstring_view maxNumStr);
        // bool TryAddDigit(unsigned int value, uint32_t radix, bool isIntegerMode, std::wstring_view maxNumStr, int32_t wordBitWidth, int maxDigits);
        // bool TryAddDecimalPt();
        // bool HasDecimalPt();
        // bool TryBeginExponent();
        // void Backspace();
        // void SetDecimalSymbol(wchar_t decSymbol);
        // bool IsEmpty();
        // std::wstring ToString(uint32_t radix);
        // Rational ToRational(uint32_t radix, int32_t precision);

        // private:
        bool m_hasExponent;
        bool m_hasDecimal;
        size_t m_decPtIndex;
        wchar_t m_decSymbol;
        CalcNumSec m_base;
        CalcNumSec m_exponent;
    };
}
