// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

//-----------------------------------------------------------------------------
//  Package Title  ratpak
//  File           itransh.c
//  Copyright      (C) 1995-97 Microsoft
//  Date           01-16-95
//
//
//  Description
//
//    Contains inverse hyperbolic sin, cos, and tan functions.
//
//  Special Information
//
//
//-----------------------------------------------------------------------------

using uint32_t = System.UInt32;
using int32_t = System.Int32;
using PRAT = CalcEngine.RatPak.RAT;

namespace CalcEngine;

public partial class RatPak
{
    // Helper function for asinh Taylor series term - implements the NEXTTERM macro for asinh
    void ASINH_RAT_NEXTTERM(ref RAT thisterm, ref RAT xx, ref NUMBER n2, int32_t precision, ref RAT pret)
    {
        // First multiply by xx (the -x² value)
        mulrat(ref thisterm, xx, precision);

        // Execute the operations from the d parameter: MULNUM(n2) MULNUM(n2) INC(n2) DIVNUM(n2) INC(n2) DIVNUM(n2)
        MULNUM(ref n2, thisterm);
        MULNUM(ref n2, thisterm);
        INC(n2);
        DIVNUM(ref n2, thisterm);
        INC(n2);
        DIVNUM(ref n2, thisterm);

        // Add the result to pret
        addrat(ref pret, thisterm, precision);
    }

    //-----------------------------------------------------------------------------
    //
    //  FUNCTION: asinhrat
    //
    //  ARGUMENTS:  x PRAT representation of number to take the inverse
    //    hyperbolic sine of
    //  RETURN: asinh of x in PRAT form.
    //
    //  EXPLANATION: This uses Taylor series
    //
    //    n
    //   ___                                                   2 2
    //   \  ]                                           -(2j+1) X
    //    \   thisterm  ; where thisterm   = thisterm  * ---------
    //    /           j                 j+1          j   (2j+2)*(2j+3)
    //   /__]
    //   j=0
    //
    //   thisterm  = X ;  and stop when thisterm < precision used.
    //           0                              n
    //
    //   For abs(x) < .85, and
    //
    //   asinh(x) = log(x+sqrt(x^2+1))
    //
    //   For abs(x) >= .85
    //
    //-----------------------------------------------------------------------------
    public void asinhrat(ref PRAT px, uint32_t radix, int32_t precision)
    {
        PRAT neg_pt_eight_five = null;

        duprat(ref neg_pt_eight_five, pt_eight_five);
        neg_pt_eight_five.pp.sign *= -1;
        if (rat_gt(px, pt_eight_five, precision) || rat_lt(px, neg_pt_eight_five, precision))
        {
            PRAT ptmp = null;
            duprat(ref ptmp, px);
            mulrat(ref ptmp, px, precision);
            addrat(ref ptmp, rat_one, precision);
            rootrat(ref ptmp, rat_two, radix, precision);
            addrat(ref px, ptmp, precision);
            lograt(ref px, precision);
            destroyrat(ref ptmp);
        }
        else
        {
            CREATETAYLOR(ref px, precision, out var xx, out var n2, out var pret, out var thisterm);
            xx.pp.sign *= -1;

            duprat(ref pret, px);
            duprat(ref thisterm, px);

            dupnum(ref n2, num_one);

            do
            {
                ASINH_RAT_NEXTTERM(ref thisterm, ref xx, ref n2, precision, ref pret);
            } while (!SMALL_ENOUGH_RAT(thisterm, precision));

            DESTROYTAYLOR(ref px, ref n2, ref xx, ref thisterm, pret, precision);
        }
        destroyrat(ref neg_pt_eight_five);
    }

    //-----------------------------------------------------------------------------
    //
    //  FUNCTION: acoshrat
    //
    //  ARGUMENTS:  x PRAT representation of number to take the inverse
    //    hyperbolic cose of
    //  RETURN: acosh of x in PRAT form.
    //
    //  EXPLANATION: This uses
    //
    //   acosh(x)=ln(x+sqrt(x^2-1))
    //
    //   For x >= 1
    //
    //-----------------------------------------------------------------------------
    public void acoshrat(ref PRAT px, uint32_t radix, int32_t precision)
    {
        if (rat_lt(px, rat_one, precision))
        {
            throw new CalcErrException(CalcErr.CALC_E_DOMAIN);
        }
        else
        {
            PRAT ptmp = null;
            duprat(ref ptmp, px);
            mulrat(ref ptmp, px, precision);
            subrat(ref ptmp, rat_one, precision);
            rootrat(ref ptmp, rat_two, radix, precision);
            addrat(ref px, ptmp, precision);
            lograt(ref px, precision);
            destroyrat(ref ptmp);
        }
    }

    //-----------------------------------------------------------------------------
    //
    //  FUNCTION: atanhrat
    //
    //  ARGUMENTS:  x PRAT representation of number to take the inverse
    //              hyperbolic tangent of
    //
    //  RETURN: atanh of x in PRAT form.
    //
    //  EXPLANATION: This uses
    //
    //             1     x+1
    //  atanh(x) = -*ln(----)
    //             2     x-1
    //
    //-----------------------------------------------------------------------------
    public void atanhrat(ref PRAT px, int32_t precision)
    {
        PRAT ptmp = null;
        duprat(ref ptmp, px);
        subrat(ref ptmp, rat_one, precision);
        addrat(ref px, rat_one, precision);
        divrat(ref px, ptmp, precision);
        px.pp.sign *= -1;
        lograt(ref px, precision);
        divrat(ref px, rat_two, precision);
        destroyrat(ref ptmp);
    }
}
