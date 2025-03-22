// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

//-----------------------------------------------------------------------------
//  Package Title  ratpak
//  File           transh.c
//  Copyright      (C) 1995-96 Microsoft
//  Date           01-16-95
//
//
//  Description
//
//     Contains hyperbolic sin, cos, and tan for rationals.
//
//
//-----------------------------------------------------------------------------

using uint32_t = System.UInt32;
using int32_t = System.Int32;
using PNUMBER = CalcEngine.RatPak.NUMBER;
using PRAT = CalcEngine.RatPak.RAT;

namespace CalcEngine;

public partial class RatPak
{
    // Helper function for hyperbolic Taylor series term - implements the NEXTTERM macro for hyperbolic functions
    void HYP_RAT_NEXTTERM(ref RAT thisterm, ref RAT xx, ref NUMBER n2, int32_t precision, ref RAT pret)
    {
        // First multiply thisterm by xx
        mulrat(ref thisterm, xx, precision);

        // Execute the operations from the d parameter: INC(n2) DIVNUM(n2) INC(n2) DIVNUM(n2)
        INC(n2);
        DIVNUM(ref n2, thisterm);
        INC(n2);
        DIVNUM(ref n2, thisterm);

        // Add the result to pret
        addrat(ref pret, thisterm, precision);
    }

    bool IsValidForHypFunc(PRAT px, int32_t precision)
    {
        PRAT ptmp = null;
        bool bRet = true;

        duprat(ref ptmp, rat_min_exp);
        divrat(ref ptmp, rat_ten, precision);
        if (rat_lt(px, ptmp, precision))
        {
            bRet = false;
        }
        destroyrat(ref ptmp);
        return bRet;
    }

    //-----------------------------------------------------------------------------
    //
    //  FUNCTION: sinhrat, _sinhrat
    //
    //  ARGUMENTS:  x PRAT representation of number to take the sine hyperbolic
    //    of
    //  RETURN: sinh of x in PRAT form.
    //
    //  EXPLANATION: This uses Taylor series
    //
    //    n
    //   ___    2j+1
    //   \  ]  X
    //    \   ---------
    //    /    (2j+1)!
    //   /__]
    //   j=0
    //          or,
    //    n
    //   ___                                                 2
    //   \  ]                                               X
    //    \   thisterm  ; where thisterm   = thisterm  * ---------
    //    /           j                 j+1          j   (2j)*(2j+1)
    //   /__]
    //   j=0
    //
    //   thisterm  = X ;  and stop when thisterm < precision used.
    //           0                              n
    //
    //   if x is bigger than 1.0 (e^x-e^-x)/2 is used.
    //
    //-----------------------------------------------------------------------------

    void _sinhrat(ref PRAT px, int32_t precision)
    {
        if (!IsValidForHypFunc(px, precision))
        {
            // Don't attempt exp of anything large or small
            throw new CalcErrException(CalcErr.CALC_E_DOMAIN);
        }

        CREATETAYLOR(ref px, precision, out var xx, out var n2, out var pret, out var thisterm);

        duprat(ref pret, px);
        duprat(ref thisterm, pret);

        dupnum(ref n2, num_one);

        do
        {
            HYP_RAT_NEXTTERM(ref thisterm, ref xx, ref n2, precision, ref pret);
        } while (!SMALL_ENOUGH_RAT(thisterm, precision));

        DESTROYTAYLOR(ref px, ref n2, ref xx, ref thisterm, pret, precision);
    }

    public void sinhrat(ref PRAT px, uint32_t radix, int32_t precision)
    {
        PRAT tmpx = null;

        if (rat_ge(px, rat_one, precision))
        {
            duprat(ref tmpx, px);
            exprat(ref px, radix, precision);
            tmpx.pp.sign *= -1;
            exprat(ref tmpx, radix, precision);
            subrat(ref px, tmpx, precision);
            divrat(ref px, rat_two, precision);
            destroyrat(ref tmpx);
        }
        else
        {
            _sinhrat(ref px, precision);
        }
    }

    //-----------------------------------------------------------------------------
    //
    //  FUNCTION: coshrat
    //
    //  ARGUMENTS:  x PRAT representation of number to take the cosine
    //              hyperbolic of
    //
    //  RETURN: cosh  of x in PRAT form.
    //
    //  EXPLANATION: This uses Taylor series
    //
    //    n
    //   ___    2j
    //   \  ]  X
    //    \   ---------
    //    /    (2j)!
    //   /__]
    //   j=0
    //          or,
    //    n
    //   ___                                                 2
    //   \  ]                                               X
    //    \   thisterm  ; where thisterm   = thisterm  * ---------
    //    /           j                 j+1          j   (2j)*(2j+1)
    //   /__]
    //   j=0
    //
    //   thisterm  = 1 ;  and stop when thisterm < precision used.
    //           0                              n
    //
    //   if x is bigger than 1.0 (e^x+e^-x)/2 is used.
    //
    //-----------------------------------------------------------------------------

    void _coshrat(ref PRAT px, uint32_t radix, int32_t precision)
    {
        if (!IsValidForHypFunc(px, precision))
        {
            // Don't attempt exp of anything large or small
            throw new CalcErrException(CalcErr.CALC_E_DOMAIN);
        }

        CREATETAYLOR(ref px, precision, out var xx, out var n2, out var pret, out var thisterm);

        pret.pp = i32tonum(1, radix);
        pret.pq = i32tonum(1, radix);

        duprat(ref thisterm, pret);

        n2 = i32tonum(0, radix);

        do
        {
            HYP_RAT_NEXTTERM(ref thisterm, ref xx, ref n2, precision, ref pret);
        } while (!SMALL_ENOUGH_RAT(thisterm, precision));

        DESTROYTAYLOR(ref px, ref n2, ref xx, ref thisterm, pret, precision);
    }

    public void coshrat(ref PRAT px, uint32_t radix, int32_t precision)
    {
        PRAT tmpx = null;

        px.pp.sign = 1;
        px.pq.sign = 1;
        if (rat_ge(px, rat_one, precision))
        {
            duprat(ref tmpx, px);
            exprat(ref px, radix, precision);
            tmpx.pp.sign *= -1;
            exprat(ref tmpx, radix, precision);
            addrat(ref px, tmpx, precision);
            divrat(ref px, rat_two, precision);
            destroyrat(ref tmpx);
        }
        else
        {
            _coshrat(ref px, radix, precision);
        }
        // Since px might be epsilon below 1 due to TRIMIT
        // we need this trick here.
        if (rat_lt(px, rat_one, precision))
        {
            duprat(ref px, rat_one);
        }
    }

    //-----------------------------------------------------------------------------
    //
    //  FUNCTION: tanhrat
    //
    //  ARGUMENTS:  x PRAT representation of number to take the tangent
    //              hyperbolic of
    //
    //  RETURN: tanh of x in PRAT form.
    //
    //  EXPLANATION: This uses sinhrat and coshrat
    //
    //-----------------------------------------------------------------------------

    public void tanhrat(ref PRAT px, uint32_t radix, int32_t precision)
    {
        PRAT ptmp = null;

        duprat(ref ptmp, px);
        sinhrat(ref px, radix, precision);
        coshrat(ref ptmp, radix, precision);
        mulnumx(ref (px.pp), ptmp.pq);
        mulnumx(ref (px.pq), ptmp.pp);

        destroyrat(ref ptmp);
    }
}
