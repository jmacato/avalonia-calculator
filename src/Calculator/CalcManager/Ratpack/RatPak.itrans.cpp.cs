// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

//-----------------------------------------------------------------------------
//  Package Title  ratpak
//  File           itrans.c
//  Copyright      (C) 1995-96 Microsoft
//  Date           01-16-95
//
//
//  Description
//
//     Contains inverse sin, cos, tan functions for rationals
//
//  Special Information
//
//-----------------------------------------------------------------------------

using uint32_t = System.UInt32;
using int32_t = System.Int32;
using PRAT = CalcEngine.RatPakRAT;

namespace CalcEngine;

internal sealed partial class RatPak
{
    // Scales the angle based on the angle type (radians, degrees, gradians)
    void ascalerat(ref PRAT pa, RatPakAngleType angletype, int32_t precision)
    {
        switch (angletype)
        {
            case RatPakAngleType.Radians:
                break;
            case RatPakAngleType.Degrees:
                divrat(ref pa, two_pi, precision);
                mulrat(ref pa, rat_360, precision);
                break;
            case RatPakAngleType.Gradians:
                divrat(ref pa, two_pi, precision);
                mulrat(ref pa, rat_400, precision);
                break;
        }
    }

    //-----------------------------------------------------------------------------
    //
    //  FUNCTION: asinrat, _asinrat
    //
    //  ARGUMENTS: x PRAT representation of number to take the inverse
    //    sine of
    //  RETURN: asin of x in PRAT form.
    //
    //  EXPLANATION: This uses Taylor series
    //
    //    n
    //   ___
    //   \  ]                                                   2 2
    //    \   thisterm  ; where thisterm   = thisterm  * (2j+1) X
    //    /           j                 j+1          j   (2j+2)*(2j+3)
    //   /__]
    //   j=0
    //
    //   thisterm  = X ;  and stop when thisterm < precision used.
    //           0                              n
    //
    //   If abs(x) > 0.85 then an alternate form is used
    //      pi/2-sgn(x)*asin(sqrt(1-x^2)
    //
    //-----------------------------------------------------------------------------
    // Helper function for asin Taylor series term
    void ASIN_RAT_NEXTTERM(ref RatPakRAT thisterm, ref RatPakRAT xx, ref RatPakNUMBER n2, int32_t precision, ref RatPakRAT pret)
    {
        mulrat(ref thisterm, xx, precision);
        MULNUM(ref n2, thisterm);
        MULNUM(ref n2, thisterm);
        INC(n2);
        DIVNUM(ref n2, thisterm);
        INC(n2);
        DIVNUM(ref n2, thisterm);
        addrat(ref pret, thisterm, precision);
    }

    void _asinrat(ref PRAT px, int32_t precision)
    {
        CREATETAYLOR(ref px, precision, out var xx, out var n2, out var pret, out var thisterm);

        duprat(ref pret, px);
        duprat(ref thisterm, px);
        dupnum(ref n2, num_one);

        do
        {
            ASIN_RAT_NEXTTERM(ref thisterm, ref xx, ref n2, precision, ref pret);
        } while (!SMALL_ENOUGH_RAT(thisterm, precision));

        DESTROYTAYLOR(ref px, ref n2, ref xx, ref thisterm, pret, precision);
    }

    public void asinanglerat(ref PRAT pa, RatPakAngleType angletype, uint32_t radix, int32_t precision)
    {
        asinrat(ref pa, radix, precision);
        ascalerat(ref pa, angletype, precision);
    }

    public void asinrat(ref PRAT px, uint32_t radix, int32_t precision)
    {
        PRAT? pret = null;
        PRAT? phack = null;
        int32_t sgn = SIGN(px);

        px.pp.sign = 1;
        px.pq.sign = 1;

        // Avoid the really bad part of the asin curve near +/-1.
        duprat(ref phack, px);
        subrat(ref phack, rat_one, precision);
        // Since px might be epsilon near zero we must set it to zero.
        if (rat_le(phack, rat_smallest, precision) && rat_ge(phack, rat_negsmallest, precision))
        {
            destroyrat(ref phack);
            duprat(ref px, pi_over_two);
        }
        else
        {
            destroyrat(ref phack);
            if (rat_gt(px, pt_eight_five, precision))
            {
                if (rat_gt(px, rat_one, precision))
                {
                    subrat(ref px, rat_one, precision);
                    if (rat_gt(px, rat_smallest, precision))
                    {
                        throw new CalcErrException(CalcErr.CALC_E_DOMAIN);
                    }
                    else
                    {
                        duprat(ref px, rat_one);
                    }
                }
                duprat(ref pret, px);
                mulrat(ref px, pret, precision);
                px.pp.sign *= -1;
                addrat(ref px, rat_one, precision);
                rootrat(ref px, rat_two, radix, precision);
                _asinrat(ref px, precision);
                px.pp.sign *= -1;
                addrat(ref px, pi_over_two, precision);
                destroyrat(ref pret);
            }
            else
            {
                _asinrat(ref px, precision);
            }
        }
        px.pp.sign = sgn;
        px.pq.sign = 1;
    }

    //-----------------------------------------------------------------------------
    //
    //  FUNCTION: acosrat, _acosrat
    //
    //  ARGUMENTS: x PRAT representation of number to take the inverse
    //    cosine of
    //  RETURN: acos of x in PRAT form.
    //
    //  EXPLANATION: This uses Taylor series
    //
    //    n
    //   ___
    //   \  ]                                                   2 2
    //    \   thisterm  ; where thisterm   = thisterm  * (2j+1) X
    //    /           j                 j+1          j   (2j+2)*(2j+3)
    //   /__]
    //   j=0
    //
    //   thisterm  = 1 ;  and stop when thisterm < precision used.
    //           0                              n
    //
    //   In this case pi/2-asin(x) is used.  At least for now _acosrat isn't
    //   called.
    //
    //-----------------------------------------------------------------------------
    void _acosrat(ref PRAT px, int32_t precision)
    {
        CREATETAYLOR(ref px, precision, out var xx, out var n2, out var pret, out var thisterm);

        createrat(ref thisterm);
        thisterm.pp = i32tonum(1, BASEX);
        thisterm.pq = i32tonum(1, BASEX);

        dupnum(ref n2, num_one);

        do
        {
            ASIN_RAT_NEXTTERM(ref thisterm, ref xx, ref n2, precision, ref pret);
        } while (!SMALL_ENOUGH_RAT(thisterm, precision));

        DESTROYTAYLOR(ref px, ref n2, ref xx, ref thisterm, pret, precision);
    }

    public void acosanglerat(ref PRAT pa, RatPakAngleType angletype, uint32_t radix, int32_t precision)
    {
        acosrat(ref pa, radix, precision);
        ascalerat(ref pa, angletype, precision);
    }

    public void acosrat(ref PRAT px, uint32_t radix, int32_t precision)
    {
        int32_t sgn = SIGN(px);

        px.pp.sign = 1;
        px.pq.sign = 1;

        if (rat_equ(px, rat_one, precision))
        {
            if (sgn == -1)
            {
                duprat(ref px, pi);
            }
            else
            {
                duprat(ref px, rat_zero);
            }
        }
        else
        {
            px.pp.sign = sgn;
            asinrat(ref px, radix, precision);
            px.pp.sign *= -1;
            addrat(ref px, pi_over_two, precision);
        }
    }

    //-----------------------------------------------------------------------------
    //
    //  FUNCTION: atanrat, _atanrat
    //
    //  ARGUMENTS: x PRAT representation of number to take the inverse
    //             tangent of
    //
    //  RETURN: atan of x in PRAT form.
    //
    //  EXPLANATION: This uses Taylor series
    //
    //    n
    //   ___
    //   \  ]                                                   2
    //    \   thisterm  ; where thisterm   = thisterm  * (2j)*X (-1^j)
    //    /           j                 j+1          j   (2j+2)
    //   /__]
    //   j=0
    //
    //   thisterm  = X ;  and stop when thisterm < precision used.
    //           0                              n
    //
    //   If abs(x) > 0.85 then an alternate form is used
    //      asin(x/sqrt(1+x^2))
    //
    //   And if abs(x) > 2.0 then this form is used:
    //
    //   pi/2 - atan(1/x)
    //
    //-----------------------------------------------------------------------------

    // Helper function for atan Taylor series term
    void ATAN_RAT_NEXTTERM(ref RatPakRAT thisterm, ref RatPakRAT xx, ref RatPakNUMBER n2, int32_t precision, ref RatPakRAT pret)
    {
        mulrat(ref thisterm, xx, precision);
        MULNUM(ref n2, thisterm);
        INC(n2);
        INC(n2);
        DIVNUM(ref n2, thisterm);
        addrat(ref pret, thisterm, precision);
    }

    void _atanrat(ref PRAT px, int32_t precision)
    {
        CREATETAYLOR(ref px, precision, out var xx, out var n2, out var pret, out var thisterm);

        duprat(ref pret, px);
        duprat(ref thisterm, px);

        dupnum(ref n2, num_one);

        xx.pp.sign *= -1;

        do
        {
            ATAN_RAT_NEXTTERM(ref thisterm, ref xx, ref n2, precision, ref pret);
        } while (!SMALL_ENOUGH_RAT(thisterm, precision));

        DESTROYTAYLOR(ref px, ref n2, ref xx, ref thisterm, pret, precision);
    }

    public void atananglerat(ref PRAT pa, RatPakAngleType angletype, uint32_t radix, int32_t precision)
    {
        atanrat(ref pa, radix, precision);
        ascalerat(ref pa, angletype, precision);
    }

    public void atanrat(ref PRAT px, uint32_t radix, int32_t precision)
    {
        PRAT? tmpx = null;
        int32_t sgn = SIGN(px);

        px.pp.sign = 1;
        px.pq.sign = 1;

        if (rat_gt(px, pt_eight_five, precision))
        {
            if (rat_gt(px, rat_two, precision))
            {
                px.pp.sign = sgn;
                px.pq.sign = 1;
                duprat(ref tmpx, rat_one);
                divrat(ref tmpx, px, precision);
                _atanrat(ref tmpx, precision);
                tmpx.pp.sign = sgn;
                tmpx.pq.sign = 1;
                duprat(ref px, pi_over_two);
                subrat(ref px, tmpx, precision);
                destroyrat(ref tmpx);
            }
            else
            {
                px.pp.sign = sgn;
                duprat(ref tmpx, px);
                mulrat(ref tmpx, px, precision);
                addrat(ref tmpx, rat_one, precision);
                rootrat(ref tmpx, rat_two, radix, precision);
                divrat(ref px, tmpx, precision);
                destroyrat(ref tmpx);
                asinrat(ref px, radix, precision);
                px.pp.sign = sgn;
                px.pq.sign = 1;
            }
        }
        else
        {
            px.pp.sign = sgn;
            px.pq.sign = 1;
            _atanrat(ref px, precision);
        }
        if (rat_gt(px, pi_over_two, precision))
        {
            subrat(ref px, pi, precision);
        }
    }
}
