// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalcEngine;

internal
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
    public NumWidth numwidth;
    public bool fIntMath;
    public bool bRecord;
    public bool bUseSep;
}
