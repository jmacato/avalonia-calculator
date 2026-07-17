// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using CalcEngine;
using CalculationManager;
using uint32_t = System.UInt32;
using int32_t = System.Int32;
using PNUMBER = CalcEngine.RatPakNUMBER;
using PRAT = CalcEngine.RatPakRAT;
using wchar_t = char;
using wstring_view = string;
using WString = string;
using size_t = int;
using System.Linq;

namespace CalcEngine;

internal sealed partial class CalcNumSec
{
    public void Clear()
    {
        value = string.Empty;
        m_isNegative = false;
    }
}
