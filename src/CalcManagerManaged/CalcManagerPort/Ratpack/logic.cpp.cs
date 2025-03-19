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


using uint32_t = System.UInt32;
using int32_t = System.Int32;
using MANTTYPE = System.UInt32;
using PNUMBER = CalcManagerPort.RatPak.NUMBER;
using PRAT = CalcManagerPort.RatPak.RAT;

namespace CalcManagerPort;

public partial class RatPak
{
    public void lshrat(ref PRAT pa, PRAT b, uint32_t radix, int32_t precision)
    {
        PRAT pwr = null;

        intrat(ref pa, radix, precision);
        if (!zernum((pa).pp))
        {
            // If input is zero we're done.
            if (rat_gt(b, rat_max_exp, precision))
            {
                // Don't attempt lsh of anything big
                throw new CalcErrException(CalcErr.CALC_E_DOMAIN);
            }

            int32_t intb = rattoi32(b, radix, precision);
            duprat(ref pwr, rat_two);
            ratpowi32(ref pwr, intb, precision);
            mulrat(ref pa, pwr, precision);
            destroyrat(ref pwr);
        }
    }

    public void rshrat(ref PRAT pa, PRAT b, uint32_t radix, int32_t precision)
    {
        PRAT pwr = null;

        intrat(ref pa, radix, precision);
        if (!zernum((pa).pp))
        {
            // If input is zero we're done.
            if (rat_lt(b, rat_min_exp, precision))
            {
                // Don't attempt rsh of anything big and negative.
                throw new CalcErrException(CalcErr.CALC_E_DOMAIN);
            }

            int32_t intb = rattoi32(b, radix, precision);
            duprat(ref pwr, rat_two);
            ratpowi32(ref pwr, intb, precision);
            divrat(ref pa, pwr, precision);
            destroyrat(ref pwr);
        }
    }

// void boolrat(PRAT pa, PRAT b, int func, uint32_t radix, int32_t precision);
// void boolnum(PNUMBER* pa, PNUMBER b, int func);

    enum BOOL_FUNCS : int
    {
        FUNC_AND,
        FUNC_OR,
        FUNC_XOR
    }

    public void andrat(ref PRAT pa, PRAT b, uint32_t radix, int32_t precision)
    {
        boolrat(ref pa, b, (int)BOOL_FUNCS.FUNC_AND, radix, precision);
    }

    public void orrat(ref PRAT pa, PRAT b, uint32_t radix, int32_t precision)
    {
        boolrat(ref pa, b, (int)BOOL_FUNCS.FUNC_OR, radix, precision);
    }

    public void xorrat(ref PRAT pa, PRAT b, uint32_t radix, int32_t precision)
    {
        boolrat(ref pa, b, (int)BOOL_FUNCS.FUNC_XOR, radix, precision);
    }

    //---------------------------------------------------------------------------
    //
    //    FUNCTION: boolrat
    //
    //    ARGUMENTS: pointer to a rational a second rational.
    //
    //    RETURN: None, changes pointer.
    //
    //    DESCRIPTION: Does the rational equivalent of *pa op= b;
    //
    //---------------------------------------------------------------------------

    void boolrat(ref PRAT pa, PRAT b, int func, uint32_t radix, int32_t precision)
    {
        PRAT tmp = null;
        intrat(ref pa, radix, precision);
        duprat(ref tmp, b);
        intrat(ref tmp, radix, precision);

        boolnum(ref ((pa).pp), tmp.pp, func);
        destroyrat(ref tmp);
    }

    //---------------------------------------------------------------------------
    //
    //    FUNCTION: boolnum
    //
    //    ARGUMENTS: pointer to a number a second number
    //
    //    RETURN: None, changes first pointer.
    //
    //    DESCRIPTION: Does the number equivalent of *pa &= b.
    //    radix doesn't matter for logicals.
    //    WARNING: Assumes numbers are unsigned.
    //
    //---------------------------------------------------------------------------

    void boolnum(ref PNUMBER pa, PNUMBER b, int func)
    {
        PNUMBER c = null;
        PNUMBER a = null;
        MANTTYPE[] pcha;
        MANTTYPE[] pchb;
        MANTTYPE[] pchc;
        MANTTYPE pchaCnt = 0;
        MANTTYPE pchbCnt = 0;
        MANTTYPE pchcCnt = 0;
        int32_t cdigits;
        int32_t mexp;
        MANTTYPE da;
        MANTTYPE db;

        a = pa;
        cdigits = Math.Max(a.cdigit + a.exp, b.cdigit + b.exp) - Math.Min(a.exp, b.exp);
        createnum(ref c, (uint32_t)cdigits);

        c.exp = Math.Min(a.exp, b.exp);
        mexp = c.exp;
        c.cdigit = cdigits;
        pcha = a.mant;
        pchb = b.mant;
        pchc = c.mant;
        for (; cdigits > 0; cdigits--, mexp++)
        {
            da = (((mexp >= a.exp) && (cdigits + a.exp - c.exp > (c.cdigit - a.cdigit))) ? pcha[pchaCnt++] : 0);
            db = (((mexp >= b.exp) && (cdigits + b.exp - c.exp > (c.cdigit - b.cdigit))) ? pchb[pchbCnt++] : 0);
            switch (func)
            {
                case (int)BOOL_FUNCS.FUNC_AND:
                    pchc[pchcCnt++] = da & db;
                    break;
                case (int)BOOL_FUNCS.FUNC_OR:
                    pchc[pchcCnt++] = da | db;
                    break;
                case (int)BOOL_FUNCS.FUNC_XOR:
                    pchc[pchcCnt++] = da ^ db;
                    break;
            }
        }

        c.sign = a.sign;
        while (c.cdigit > 1 && pchc[--pchcCnt] == 0)
        {
            c.cdigit--;
        }

        destroynum(ref pa);
        pa = c;
    }

    //-----------------------------------------------------------------------------
    //
    //    FUNCTION: remrat
    //
    //    ARGUMENTS: pointer to a rational a second rational.
    //
    //    RETURN: None, changes pointer.
    //
    //    DESCRIPTION: Calculate the remainder of *pa / b,
    //                 equivalent of 'pa % b' in C/C++ and produces a result
    //                 that is either zero or has the same sign as the dividend.
    //
    //-----------------------------------------------------------------------------

    public void remrat(ref PRAT pa, PRAT b)
    {
        if (zerrat(b))
        {
            throw new CalcErrException(CalcErr.CALC_E_INDEFINITE);
        }

        PRAT tmp = null;
        duprat(ref tmp, b);

        mulnumx(ref ((pa).pp), tmp.pq);
        mulnumx(ref (tmp.pp), (pa).pq);
        remnum(ref ((pa).pp), tmp.pp, BASEX);
        mulnumx(ref ((pa).pq), tmp.pq);

        // Get *pa back in the integer over integer form.
        RENORMALIZE(pa);

        destroyrat(ref tmp);
    }

    //-----------------------------------------------------------------------------
    //
    //    FUNCTION: modrat
    //
    //    ARGUMENTS: pointer to a rational a second rational.
    //
    //    RETURN: None, changes pointer.
    //
    //    DESCRIPTION: Calculate the remainder of *pa / b, with the sign of the result
    //                 either zero or has the same sign as the divisor.
    //    NOTE: When *pa or b are negative, the result won't be the same as
    //          the C/C++ operator %, use remrat if it's the behavior you expect.
    //
    //-----------------------------------------------------------------------------

    public void modrat(ref PRAT pa, PRAT b)
    {
        // contrary to remrat(X, 0) returning 0, modrat(X, 0) must return X
        if (zerrat(b))
        {
            return;
        }

        PRAT tmp = null;
        duprat(ref tmp, b);

        var needAdjust = (SIGN(pa) == -1 ? (SIGN(b) == 1) : (SIGN(b) == -1));

        mulnumx(ref ((pa).pp), tmp.pq);
        mulnumx(ref (tmp.pp), (pa).pq);
        remnum(ref ((pa).pp), tmp.pp, BASEX);
        mulnumx(ref ((pa).pq), tmp.pq);

        if (needAdjust && !zerrat(pa))
        {
            addrat(ref pa, b, unchecked((int32_t)BASEX));
        }

        // Get *pa back in the integer over integer form.
        RENORMALIZE(pa);

        destroyrat(ref tmp);
    }
}
