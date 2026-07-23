// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalcEngine;

//-----------------------------------------------------------------------------
//
//  RAT type is a representation radix  on 2 NUMBER types.
//  pp/pq, where pp and pq are pointers to integral NUMBER types.
//
//-----------------------------------------------------------------------------
public class RAT
{
    internal PNUMBER _pp = null!;
    internal PNUMBER _pq = null!;
    internal RAT? _poolNext;

    public PNUMBER Pp
    {
        get => _pp;
        set => _pp = value;
    }

    public PNUMBER Pq
    {
        get => _pq;
        set => _pq = value;
    }
}
