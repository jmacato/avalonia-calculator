// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
/****************************Module*Header***********************************\
* Module Name: SCICOMM.C
*
* Module Description:
*
* Warnings:
*
* Created:
*
* Author:
\****************************************************************************/
using OpCode = uint;
using uint64_t = ulong;
using System.Diagnostics;
using CalcEngine;
using CalculationManager;
using uint32_t = System.UInt32;
using int32_t = System.Int32;
using PNUMBER = CalcEngine.RatPakNUMBER;
using PRAT = CalcEngine.RatPakRAT;
using size_t = ulong;
using wchar_t = char;
using wstring_view = string;
using WString = string;
using System.Collections.Generic;
using System;

namespace CalcEngine;
// Unary operator Function Name table Element
// since unary operators button names aren't exactly friendly for history purpose,
// we have this separate table to get its localized name and for its Inv function if it exists.
// Unary operator Function Name table Element
// since unary operators button names aren't exactly friendly for history purpose,
// we have this separate table to get its localized name and for its Inv function if it exists.
internal struct CCalcEngineFunctionNameElement
{
    public string degreeString; // Used by default if there are no rad or grad specific strings.
    public string inverseDegreeString; // Will fall back to degreeString if empty
    public string radString;
    public string inverseRadString; // Will fall back to radString if empty
    public string gradString;
    public string inverseGradString; // Will fall back to gradString if empty
    public string programmerModeString;
    public bool hasAngleStrings => (!string.IsNullOrEmpty(radString) || !string.IsNullOrEmpty(inverseRadString) || !string.IsNullOrEmpty(gradString) || !string.IsNullOrEmpty(inverseGradString));
}
