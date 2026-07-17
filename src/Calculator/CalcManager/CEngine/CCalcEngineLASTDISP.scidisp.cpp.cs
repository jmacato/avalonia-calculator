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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using uint32_t = System.UInt32;
using wstring_view = string;
using WString = string;

namespace CalcEngine;

internal /****************************************************************************\
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
struct CCalcEngineLASTDISP
{
    public Rational? value;
    public int precision;
    public uint radix;
    public int nFE;
    public CCalcEngineNUM_WIDTH numwidth;
    public bool fIntMath;
    public bool bRecord;
    public bool bUseSep;
}
