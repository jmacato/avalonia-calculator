// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//-----------------------------------------------------------------------------
//  Package Title  ratpak
//  File           ratpak.h
//  Copyright      (C) 1995-99 Microsoft
//  Date           01-16-95
//
//
//  Description
//
//     Infinite precision math package header file, if you use ratpak.lib you
//  need to include this header.
//
//-----------------------------------------------------------------------------
using System;
using uint32_t = System.UInt32;
using int32_t = System.Int32;
using MANTTYPE = System.UInt32;
using PNUMBER = CalcEngine.RatPakNUMBER;
using PRAT = CalcEngine.RatPakRAT;

namespace CalcEngine;
//-----------------------------------------------------------------------------
//
//  RatPakNUMBER type is a representation of a generic sized generic radix number
//
//-----------------------------------------------------------------------------
internal sealed class RatPakNUMBER
{
    public int32_t sign; // The sign of the mantissa, +1, or -1
    // The number of digits, or what passes for digits in the
    // radix being used.
    public int32_t cdigit;
    public int32_t exp; // The offset of digits from the radix point
    // (decimal point in radix 10)
    // This is actually allocated as a continuation of the
    // RatPakNUMBER structure.
    public MANTTYPE[] mant = new MANTTYPE[8]; /*[8]*/
    internal const int32_t ConstSizeOf = 44;
}
