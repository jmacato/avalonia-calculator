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
//  RatPakRAT type is a representation radix  on 2 RatPakNUMBER types.
//  pp/pq, where pp and pq are pointers to integral RatPakNUMBER types.
//
//-----------------------------------------------------------------------------
internal sealed class RatPakRAT
{
    public PNUMBER pp = new();
    public PNUMBER pq = new();
}
