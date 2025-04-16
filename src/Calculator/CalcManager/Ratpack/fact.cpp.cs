// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

//-----------------------------------------------------------------------------
//  Package Title  ratpak
//  File           fact.c
//  Copyright      (C) 1995-96 Microsoft
//  Date           01-16-95
//
//
//  Description
//
//     Contains fact(orial) and supporting _gamma functions.
//
//-----------------------------------------------------------------------------

using uint32_t = System.UInt32;
using int32_t = System.Int32;
using PNUMBER = CalcEngine.RatPak.NUMBER;
using PRAT = CalcEngine.RatPak.RAT;

namespace CalcEngine;

public partial class RatPak
{
    // Methods to replace the C++ macros
    private void ABSRAT(PRAT x)
    {
        x.pp.sign = 1;
        x.pq.sign = 1;
    }

    private void NEGATE(PRAT x)
    {
        x.pp.sign *= -1;
    }

    //-----------------------------------------------------------------------------
    //
    //  FUNCTION: factrat, _gamma, gamma
    //
    //  ARGUMENTS:  x PRAT representation of number to take the sine of
    //
    //  RETURN: factorial of x in PRAT form.
    //
    //  EXPLANATION: This uses Taylor series
    //
    //      n
    //     ___    2j
    //   n \  ]  A       1          A
    //  A   \   -----[ ---- - ---------------]
    //      /   (2j)!  n+2j   (n+2j+1)(2j+1)
    //     /__]
    //     j=0
    //
    //                        / oo
    //                        |    n-1 -x     __
    //  This was derived from |   x   e  dx = |
    //                        |               | (n) { = (n-1)! for +integers}
    //                        / 0
    //
    //  It can be shown that the above series is within precision if A is chosen
    //  big enough.
    //                          A    n  precision
    //  Based on the relation ne  = A 10            A was chosen as
    //
    //             precision
    //  A = ln(Base         /n)+1
    //  A += n*ln(A)  This is close enough for precision > base and n < 1.5
    //
    //
    //-----------------------------------------------------------------------------
    void _gamma(ref PRAT pn, uint32_t radix, int32_t precision)
    {
        PRAT factorial = null;
        PNUMBER count = null;
        PRAT tmp = null;
        PRAT one_pt_five = null;
        PRAT a = null;
        PRAT a2 = null;
        PRAT term = null;
        PRAT sum = null;
        PRAT err = null;
        PRAT mpy = null;

        // Set up constants and initial conditions
        PRAT ratprec = i32torat(precision);

        // Find the best 'A' for convergence to the required precision.
        a = i32torat((int32_t)radix);
        lograt(ref a, precision);
        mulrat(ref a, ratprec, precision);

        // Really is -ln(n)+1, but -ln(n) will be < 1
        // if we scale n between 0.5 and 1.5
        addrat(ref a, rat_two, precision);
        duprat(ref tmp, a);
        lograt(ref tmp, precision);
        mulrat(ref tmp, pn, precision);
        addrat(ref a, tmp, precision);
        addrat(ref a, rat_one, precision);

        // Calculate the necessary bump in precision and up the precision.
        // The following code is equivalent to
        // precision += ln(exp(a)*pow(a,n+1.5))-ln(radix));
        duprat(ref tmp, pn);
        one_pt_five = i32torat(3);
        divrat(ref one_pt_five, rat_two, precision);
        addrat(ref tmp, one_pt_five, precision);
        duprat(ref term, a);
        powratcomp(ref term, tmp, radix, precision);
        duprat(ref tmp, a);
        exprat(ref tmp, radix, precision);
        mulrat(ref term, tmp, precision);
        lograt(ref term, precision);
        PRAT ratRadix = i32torat((int32_t)radix);
        duprat(ref tmp, ratRadix);
        lograt(ref tmp, precision);
        subrat(ref term, tmp, precision);
        precision += rattoi32(term, radix, precision);

        // Set up initial terms for series, refer to series in above comment block.
        duprat(ref factorial, rat_one); // Start factorial out with one
        count = i32tonum(0, BASEX);

        duprat(ref mpy, a);
        powratcomp(ref mpy, pn, radix, precision);
        // a2=a^2
        duprat(ref a2, a);
        mulrat(ref a2, a, precision);

        // sum=(1/n)-(a/(n+1))
        duprat(ref sum, rat_one);
        divrat(ref sum, pn, precision);
        duprat(ref tmp, pn);
        addrat(ref tmp, rat_one, precision);
        duprat(ref term, a);
        divrat(ref term, tmp, precision);
        subrat(ref sum, term, precision);

        duprat(ref err, ratRadix);
        NEGATE(ratprec);
        powratcomp(ref err, ratprec, radix, precision);
        divrat(ref err, ratRadix, precision);

        // Just get something not tiny in term
        duprat(ref term, rat_two);

        // Loop until precision is reached, or asked to halt.
        while (!zerrat(term) && rat_gt(term, err, precision))
        {
            addrat(ref pn, rat_two, precision);

            // WARNING: mixing numbers and rationals here.
            // for speed and efficiency.
            INC(count);
            mulnumx(ref (factorial.pp), count);
            INC(count);
            mulnumx(ref (factorial.pp), count);

            divrat(ref factorial, a2, precision);

            duprat(ref tmp, pn);
            addrat(ref tmp, rat_one, precision);
            destroyrat(ref term);
            createrat(ref term);
            dupnum(ref term.pp, count);
            dupnum(ref term.pq, num_one);
            addrat(ref term, rat_one, precision);
            mulrat(ref term, tmp, precision);
            duprat(ref tmp, a);
            divrat(ref tmp, term, precision);

            duprat(ref term, rat_one);
            divrat(ref term, pn, precision);
            subrat(ref term, tmp, precision);

            divrat(ref term, factorial, precision);
            addrat(ref sum, term, precision);
            ABSRAT(term);
        }

        // Multiply by factor.
        mulrat(ref sum, mpy, precision);

        // And cleanup
        destroyrat(ref ratprec);
        destroyrat(ref err);
        destroyrat(ref term);
        destroyrat(ref a);
        destroyrat(ref a2);
        destroyrat(ref tmp);
        destroyrat(ref one_pt_five);

        destroynum(ref count);

        destroyrat(ref factorial);
        destroyrat(ref pn);
        duprat(ref pn, sum);
        destroyrat(ref sum);
    }

    public void factrat(ref PRAT px, uint32_t radix, int32_t precision)
    {
        PRAT fact = null;
        PRAT frac = null;
        PRAT neg_rat_one = null;

        if (rat_gt(px, rat_max_fact, precision) || rat_lt(px, rat_min_fact, precision))
        {
            // Don't attempt factorial of anything too large or small.
            throw new CalcErrException(CalcErr.CALC_E_OVERFLOW);
        }

        duprat(ref fact, rat_one);

        duprat(ref neg_rat_one, rat_one);
        neg_rat_one.pp.sign *= -1;

        duprat(ref frac, px);
        fracrat(ref frac, radix, precision);

        // Check for negative integers and throw an error.
        if ((zerrat(frac) || (LOGRATRADIX(frac) <= -precision)) && (SIGN(px) == -1))
        {
            throw new CalcErrException(CalcErr.CALC_E_DOMAIN);
        }
        while (rat_gt(px, rat_zero, precision) && (LOGRATRADIX(px) > -precision))
        {
            mulrat(ref fact, px, precision);
            subrat(ref px, rat_one, precision);
        }

        // Added to make numbers 'close enough' to integers use integer factorial.
        if (LOGRATRADIX(px) <= -precision)
        {
            duprat(ref px, rat_zero);
            intrat(ref fact, radix, precision);
        }

        while (rat_lt(px, neg_rat_one, precision))
        {
            addrat(ref px, rat_one, precision);
            divrat(ref fact, px, precision);
        }

        if (rat_neq(px, rat_zero, precision))
        {
            addrat(ref px, rat_one, precision);
            _gamma(ref px, radix, precision);
            mulrat(ref px, fact, precision);
        }
        else
        {
            duprat(ref px, fact);
        }

        destroyrat(ref fact);
        destroyrat(ref frac);
        destroyrat(ref neg_rat_one);
    }
}
