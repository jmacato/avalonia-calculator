// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// #pragma once
// #ifdef TESTING
// #define PRIVATE public
// #else
// #define PRIVATE private
// #endif
/****************************ModuleHeader**********************************\
* Module Name: CalcEngine.h
*
* Module Description:
*       The class definition for the Calculator's engine class CCalcEngine
*
* Warnings:
*
* Created: 17-Jan-2008
*
\****************************************************************************/
//
// #include <random>
// #include "CCommand.h"
// #include "EngineStrings.h"
// #include "../Command.h"
// #include "../ExpressionCommand.h"
// #include "RadixType.h"
// #include "History.h" // for History Collector
// #include "CalcInput.h"
// #include "CalcUtils.h"
// #include "ICalcDisplay.h"
// #include "Rational.h"
// #include "RationalMath.h"
// The following are NOT real exports of CalcEngine, but for forward declarations
// The real exports follows later
using System;
using System.Collections.Generic;
using uint32_t = System.UInt32;
using int32_t = System.Int32;
using wchar_t = char;
using wstring_view = string;
using WString = string;
using size_t = ulong;
using uint64_t = ulong;

namespace CalcEngine;
// This is expected to be in same order as IDM_QWORD, IDM_DWORD etc.
internal enum CCalcEngineNUM_WIDTH
{
    QWORD_WIDTH, // Number width of 64 bits mode (default)
    DWORD_WIDTH, // Number width of 32 bits mode
    WORD_WIDTH, // Number width of 16 bits mode
    BYTE_WIDTH // Number width of 16 bits mode
};
