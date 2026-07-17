// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//---------------------------------------------------------------------------
//  Package Title  ratpak
//  File           num.c
//  Copyright      (C) 1995-99 Microsoft
//  Date           01-16-95
//
//
//  Description
//
//     Contains routines for and, or, xor, not and other support
//
//---------------------------------------------------------------------------
// #include "ratpak.h"
using System;
using uint32_t = System.UInt32;
using int32_t = System.Int32;
using MANTTYPE = System.UInt32;
using PNUMBER = CalcEngine.RatPakNUMBER;
using PRAT = CalcEngine.RatPakRAT;

namespace CalcEngine;

internal // void boolrat(PRAT pa, PRAT b, int func, uint32_t radix, int32_t precision);
// void boolnum(PNUMBER* pa, PNUMBER b, int func);
enum RatPakBOOL_FUNCS : int
{
    FUNC_AND,
    FUNC_OR,
    FUNC_XOR
}
