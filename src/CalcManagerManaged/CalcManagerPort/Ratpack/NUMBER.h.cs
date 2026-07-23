// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalcEngine;

//-----------------------------------------------------------------------------
//
//  NUMBER type is a representation of a generic sized generic radix number
//
//-----------------------------------------------------------------------------
public class NUMBER
{
    public NUMBER() : this(8)
    {
    }

    internal NUMBER(int mantissaLength)
    {
        _mant = new MANTTYPE[mantissaLength];
    }

    internal int32_t _sign; // The sign of the mantissa, +1, or -1

    // The number of digits, or what passes for digits in the
    // radix being used.
    internal int32_t _cdigit;

    internal int32_t _exp; // The offset of digits from the radix point

    // (decimal point in radix 10)
    // This is actually allocated as a continuation of the
    // NUMBER structure.
    internal MANTTYPE[] _mant; /*[8]*/

    // Used only while the instance is in RatPak's thread-local recycler.
    internal NUMBER? _poolNext;

    public int32_t Sign
    {
        get => _sign;
        set => _sign = value;
    }

    public int32_t Cdigit
    {
        get => _cdigit;
        set => _cdigit = value;
    }

    public int32_t Exp
    {
        get => _exp;
        set => _exp = value;
    }

    public IReadOnlyList<MANTTYPE> Mant => _mant;

    internal const int32_t ConstSizeOf = 44;
}
