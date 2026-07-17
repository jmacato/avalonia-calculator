// Copyright (c) Microsoft Corporation. All rights reserved.

using uint32_t = System.UInt32;
using int32_t = System.Int32;
using PNUMBER = CalcEngine.RatPakNUMBER;
using System.Linq;
using System;

namespace CalcEngine;

internal sealed class Number
{
    private readonly RatPak _ratPak;

    public Number(RatPak ratPak)
        : this(ratPak, 1, 0, 1, [0])
    {
    }

    public Number(RatPak ratPak, PNUMBER p)
        : this(ratPak, p.sign, p.exp, p.cdigit, p.mant.ToArray())
    {
    }

    public Number(RatPak ratPak, int32_t sign, int32_t exp, int32_t cDigits, uint32_t[] mantissa)
    {
        _ratPak = ratPak;

        CDigits = cDigits;

        Sign = sign;
        Exp = exp;

        var tmp = mantissa.ToArray();

        if (tmp.Length <= CDigits)
        {
            // Let's buffer here as it's important for the algorithms to have some leeway for the mantissa digits.
            Array.Resize(ref tmp, Math.Min((Math.Max(tmp.Length, cDigits) + 8), 256));
        }

        Mantissa = tmp;
    }

    public PNUMBER ToPNUMBER()
    {
        PNUMBER? ret = null;

        RatPak.createnum(ref ret, (uint32_t)(Mantissa.Length + 1));
        ret.sign = Sign;
        ret.exp = Exp;
        ret.cdigit = CDigits;
        ret.mant = Mantissa.ToArray();

        return ret;
    }

    public int32_t Sign { get; }

    public int32_t Exp { get; }

    public int32_t CDigits { get; }

    public uint32_t[] Mantissa { get; }

    public bool IsZero() =>
        Mantissa.All(x =>
            x == 0);
}
