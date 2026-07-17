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
// this to 2^32 after solving scaling problems with
// overflow detection esp. in mul
internal enum RatPakNumberFormat
{
    Float, // returns floating point, or exponential if number is too big
    Scientific, // always returns scientific notation
    Engineering // always returns engineering notation such that exponent is a multiple of 3
};
