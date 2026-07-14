// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CalcEngine;
using CalculationManager;

namespace CalcEngine;

public class CalcNumSec
{
    // public:
    public CalcNumSec()
    {
        Value = "";
        m_isNegative = (false);
    }

    public bool IsEmpty()
    {
        return string.IsNullOrEmpty(Value);
    }

    public bool IsNegative()
    {
        return m_isNegative;
    }

    public void IsNegative(bool isNegative)
    {
        m_isNegative = isNegative;
    }

    public wstring Value { get; set; }

    bool m_isNegative;

    public void Clear()
    {
        Value = string.Empty;
        m_isNegative = false;
    }
}
