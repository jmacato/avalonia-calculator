// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// #include "Rational.h"
using size_t = int;
using wchar_t = char;
using WString = string;

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

        public bool IsNegative()
        {
            return m_isNegative;
        }

        public void IsNegative(bool isNegative)
        {
            m_isNegative = isNegative;
        }

        public WString value;
        bool m_isNegative;
    };
}
