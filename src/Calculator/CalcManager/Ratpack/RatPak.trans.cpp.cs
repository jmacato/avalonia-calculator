// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

//----------------------------------------------------------------------------
//  File           trans.c
//  Copyright      (C) 1995-96 Microsoft
//  Date           01-16-95
//
//
//  Description
//
//     Contains sin, cos and tan for rationals
//
//
//----------------------------------------------------------------------------

using uint32_t = System.UInt32;
using int32_t = System.Int32;
using PNUMBER = CalcEngine.RatPakNUMBER;
using PRAT = CalcEngine.RatPakRAT;

namespace CalcEngine;

internal sealed partial class RatPak
{
    // Helper function for sin/cos Taylor series term - implements the NEXTTERM macro for trigonometric functions
    void SIN_COS_RAT_NEXTTERM(ref RatPakRAT thisterm, ref RatPakRAT xx, ref RatPakNUMBER n2, int32_t precision, ref RatPakRAT pret)
    {
        // First multiply thisterm by xx (the -x² value)
        mulrat(ref thisterm, xx, precision);

        // Execute the operations from the d parameter: INC(n2) DIVNUM(n2) INC(n2) DIVNUM(n2)
        INC(n2);
        DIVNUM(ref n2, thisterm);
        INC(n2);
        DIVNUM(ref n2, thisterm);

        // Add the result to pret
        addrat(ref pret, thisterm, precision);
    }

    public void scalerat(ref PRAT pa, RatPakAngleType angletype, uint32_t radix, int32_t precision)
    {
        switch (angletype)
        {
            case RatPakAngleType.Radians:
                scale2pi(ref pa, radix, precision);
                break;
            case RatPakAngleType.Degrees:
                scale(ref pa, rat_360, radix, precision);
                break;
            case RatPakAngleType.Gradians:
                scale(ref pa, rat_400, radix, precision);
                break;
        }
    }

    //-----------------------------------------------------------------------------
    //
    //  FUNCTION: sinrat, _sinrat
    //
    //  ARGUMENTS:  x PRAT representation of number to take the sine of
    //
    //  RETURN: sin of x in PRAT form.
    //
    //  EXPLANATION: This uses Taylor series
    //
    //    n
    //   ___          2j+1
    //   \  ]   j    X
    //    \   -1  * ---------
    //    /          (2j+1)!
    //   /__]
    //   j=0
    //          or,
    //    n
    //   ___                                                 2
    //   \  ]                                              -X
    //    \   thisterm  ; where thisterm   = thisterm  * ---------
    //    /           j                 j+1          j   (2j)*(2j+1)
    //   /__]
    //   j=0
    //
    //   thisterm  = X ;  and stop when thisterm < precision used.
    //           0                              n
    //
    //-----------------------------------------------------------------------------

    void _sinrat(ref PRAT px, int32_t precision)
    {
        CREATETAYLOR(ref px, precision, out var xx, out var n2, out var pret, out var thisterm);

        duprat(ref pret, px);
        duprat(ref thisterm, px);

        dupnum(ref n2, num_one);
        xx.pp.sign *= -1;

        do
        {
            SIN_COS_RAT_NEXTTERM(ref thisterm, ref xx, ref n2, precision, ref pret);
        } while (!SMALL_ENOUGH_RAT(thisterm, precision));

        DESTROYTAYLOR(ref px, ref n2, ref xx, ref thisterm, pret, precision);

        // Since px might be epsilon above 1 or below -1, due to TRIMIT we need
        // this trick here.
        inbetween(ref px, rat_one, precision);

        // Since px might be epsilon near zero we must set it to zero.
        if (rat_le(px, rat_smallest, precision) && rat_ge(px, rat_negsmallest, precision))
        {
            duprat(ref px, rat_zero);
        }
    }

    public void sinrat(ref PRAT px, uint32_t radix, int32_t precision)
    {
        scale2pi(ref px, radix, precision);
        _sinrat(ref px, precision);
    }

    public void sinanglerat(ref PRAT pa, RatPakAngleType angletype, uint32_t radix, int32_t precision)
    {
        scalerat(ref pa, angletype, radix, precision);
        switch (angletype)
        {
            case RatPakAngleType.Degrees:
                if (rat_gt(pa, rat_180, precision))
                {
                    subrat(ref pa, rat_360, precision);
                }
                divrat(ref pa, rat_180, precision);
                mulrat(ref pa, pi, precision);
                break;
            case RatPakAngleType.Gradians:
                if (rat_gt(pa, rat_200, precision))
                {
                    subrat(ref pa, rat_400, precision);
                }
                divrat(ref pa, rat_200, precision);
                mulrat(ref pa, pi, precision);
                break;
        }
        _sinrat(ref pa, precision);
    }

    //-----------------------------------------------------------------------------
    //
    //  FUNCTION: cosrat, _cosrat
    //
    //  ARGUMENTS:  x PRAT representation of number to take the cosine of
    //
    //  RETURN: cosine of x in PRAT form.
    //
    //  EXPLANATION: This uses Taylor series
    //
    //    n
    //   ___    2j   j
    //   \  ]  X   -1
    //    \   ---------
    //    /    (2j)!
    //   /__]
    //   j=0
    //          or,
    //    n
    //   ___                                                 2
    //   \  ]                                              -X
    //    \   thisterm  ; where thisterm   = thisterm  * ---------
    //    /           j                 j+1          j   (2j)*(2j+1)
    //   /__]
    //   j=0
    //
    //   thisterm  = 1 ;  and stop when thisterm < precision used.
    //           0                              n
    //
    //-----------------------------------------------------------------------------

    void _cosrat(ref PRAT px, uint32_t radix, int32_t precision)
    {
        CREATETAYLOR(ref px, precision, out var xx, out var n2, out var pret, out var thisterm);

        pret.pp = i32tonum(1, radix);
        pret.pq = i32tonum(1, radix);

        duprat(ref thisterm, pret);

        n2 = i32tonum(0, radix);
        xx.pp.sign *= -1;

        do
        {
            SIN_COS_RAT_NEXTTERM(ref thisterm, ref xx, ref n2, precision, ref pret);
        } while (!SMALL_ENOUGH_RAT(thisterm, precision));

        DESTROYTAYLOR(ref px, ref n2, ref xx, ref thisterm, pret, precision);

        // Since px might be epsilon above 1 or below -1, due to TRIMIT we need
        // this trick here.
        inbetween(ref px, rat_one, precision);

        // Since px might be epsilon near zero we must set it to zero.
        if (rat_le(px, rat_smallest, precision) && rat_ge(px, rat_negsmallest, precision))
        {
            duprat(ref px, rat_zero);
        }
    }

    public void cosrat(ref PRAT px, uint32_t radix, int32_t precision)
    {
        scale2pi(ref px, radix, precision);
        _cosrat(ref px, radix, precision);
    }

    public void cosanglerat(ref PRAT pa, RatPakAngleType angletype, uint32_t radix, int32_t precision)
    {
        scalerat(ref pa, angletype, radix, precision);
        switch (angletype)
        {
            case RatPakAngleType.Degrees:
                if (rat_gt(pa, rat_180, precision))
                {
                    PRAT? ptmp = null;
                    duprat(ref ptmp, rat_360);
                    subrat(ref ptmp, pa, precision);
                    pa = ptmp;
                }
                divrat(ref pa, rat_180, precision);
                mulrat(ref pa, pi, precision);
                break;
            case RatPakAngleType.Gradians:
                if (rat_gt(pa, rat_200, precision))
                {
                    PRAT? ptmp = null;
                    duprat(ref ptmp, rat_400);
                    subrat(ref ptmp, pa, precision);
                    pa = ptmp;
                }
                divrat(ref pa, rat_200, precision);
                mulrat(ref pa, pi, precision);
                break;
        }
        _cosrat(ref pa, radix, precision);
    }

    //-----------------------------------------------------------------------------
    //
    //  FUNCTION: tanrat, _tanrat
    //
    //  ARGUMENTS:  x PRAT representation of number to take the tangent of
    //
    //  RETURN: tan of x in PRAT form.
    //
    //  EXPLANATION: This uses sinrat and cosrat
    //
    //-----------------------------------------------------------------------------

    void _tanrat(ref PRAT px, uint32_t radix, int32_t precision)
    {
        PRAT? ptmp = null;

        duprat(ref ptmp, px);
        _sinrat(ref px, precision);
        _cosrat(ref ptmp, radix, precision);
        if (zerrat(ptmp))
        {
            destroyrat(ref ptmp);
            throw new CalcErrException(CalcErr.CALC_E_DOMAIN);
        }
        divrat(ref px, ptmp, precision);

        destroyrat(ref ptmp);
    }

    public void tanrat(ref PRAT px, uint32_t radix, int32_t precision)
    {
        scale2pi(ref px, radix, precision);
        _tanrat(ref px, radix, precision);
    }

    public void tananglerat(ref PRAT pa, RatPakAngleType angletype, uint32_t radix, int32_t precision)
    {
        scalerat(ref pa, angletype, radix, precision);
        switch (angletype)
        {
            case RatPakAngleType.Degrees:
                if (rat_gt(pa, rat_180, precision))
                {
                    subrat(ref pa, rat_180, precision);
                }
                divrat(ref pa, rat_180, precision);
                mulrat(ref pa, pi, precision);
                break;
            case RatPakAngleType.Gradians:
                if (rat_gt(pa, rat_200, precision))
                {
                    subrat(ref pa, rat_200, precision);
                }
                divrat(ref pa, rat_200, precision);
                mulrat(ref pa, pi, precision);
                break;
        }
        _tanrat(ref pa, radix, precision);
    }
}
