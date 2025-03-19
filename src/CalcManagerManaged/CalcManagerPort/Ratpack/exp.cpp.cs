// // Copyright (c) Microsoft Corporation. All rights reserved.
// // Licensed under the MIT License.
//
// //-----------------------------------------------------------------------------
// //  Package Title  ratpak
// //  File           exp.c
// //  Copyright      (C) 1995-96 Microsoft
// //  Date           01-16-95
// //
// //
// //  Description
// //
// //     Contains exp, and log functions for rationals
// //
// //
// //-----------------------------------------------------------------------------
// #include "ratpak.h"

using uint32_t = System.UInt32;
using int32_t = System.Int32;
using PNUMBER = CalcManagerPort.RatPak.NUMBER;
using PRAT = CalcManagerPort.RatPak.RAT;

namespace CalcManagerPort;

public partial class RatPak
{
    //-----------------------------------------------------------------------------
    //
    //  FUNCTION: exprat
    //
    //  ARGUMENTS: x PRAT representation of number to exponentiate
    //
    //  RETURN: exp  of x in PRAT form.
    //
    //  EXPLANATION: This uses Taylor series
    //
    //    n
    //   ___
    //   \  ]                                               X
    //    \   thisterm  ; where thisterm   = thisterm  * ---------
    //    /           j                 j+1          j      j+1
    //   /__]
    //   j=0
    //
    //   thisterm  = X ;  and stop when thisterm < precision used.
    //           0                              n
    //
    //-----------------------------------------------------------------------------

    void _exprat(ref PRAT px, int32_t precision)
    {
        CREATETAYLOR(ref px, precision, out var xx, out var n2, out var pret, out var thisterm);

        addnum(ref (pret.pp), num_one, BASEX);
        addnum(ref (pret.pq), num_one, BASEX);
        duprat(ref thisterm, pret);

        n2 = i32tonum(0, BASEX);

        do
        {
            EXP_RAT_NEXTTERM(ref thisterm, ref px, n2, precision, ref pret);
        } while (!SMALL_ENOUGH_RAT(thisterm, precision));

        DESTROYTAYLOR(ref px, ref n2, ref xx, ref thisterm, pret, precision);
    }

    public void exprat(ref PRAT px, uint32_t radix, int32_t precision)
    {
        PRAT pwr = null;
        PRAT pint = null;

        if (rat_gt(px, rat_max_exp, precision) || rat_lt(px, rat_min_exp, precision))
        {
            // Don't attempt exp of anything large.
            throw new CalcErrException(CalcErr.CALC_E_DOMAIN);
        }

        duprat(ref pwr, rat_exp);
        duprat(ref pint, px);

        intrat(ref pint, radix, precision);

        int32_t intpwr = rattoi32(pint, radix, precision);
        ratpowi32(ref pwr, intpwr, precision);

        subrat(ref px, pint, precision);

        // It just so happens to be an integral power of e.
        if (rat_gt(px, rat_negsmallest, precision) && rat_lt(px, rat_smallest, precision))
        {
            duprat(ref px, pwr);
        }
        else
        {
            _exprat(ref px, precision);
            mulrat(ref px, pwr, precision);
        }

        destroyrat(ref pwr);
        destroyrat(ref pint);
    }

    //-----------------------------------------------------------------------------
    //
    //  FUNCTION: lograt, _lograt
    //
    //  ARGUMENTS: x PRAT representation of number to logarithim
    //
    //  RETURN: log  of x in PRAT form.
    //
    //  EXPLANATION: This uses Taylor series
    //
    //    n
    //   ___
    //   \  ]                                             j*(1-X)
    //    \   thisterm  ; where thisterm   = thisterm  * ---------
    //    /           j                 j+1          j      j+1
    //   /__]
    //   j=0
    //
    //   thisterm  = X ;  and stop when thisterm < precision used.
    //           0                              n
    //
    //   Number is scaled between one and e_to_one_half prior to taking the
    //   log. This is to keep execution time from exploding.
    //
    //
    //-----------------------------------------------------------------------------

    void _lograt(ref PRAT px, int32_t precision)
    {
        CREATETAYLOR(ref px, precision, out var xx, out var n2, out var pret, out var thisterm);

        createrat(ref thisterm);

        // sub one from x
        (px).pq.sign *= -1;
        addnum(ref ((px).pp), (px).pq, BASEX);
        (px).pq.sign *= -1;

        duprat(ref pret, px);
        duprat(ref thisterm, px);

        n2 = i32tonum(1, BASEX);
        (px).pp.sign *= -1;

        do
        {
            LOG_RAT_NEXTTERM(ref thisterm, ref px, n2, precision, ref pret);
            TRIMTOP(px, precision);
        } while (!SMALL_ENOUGH_RAT(thisterm, precision));

        DESTROYTAYLOR(ref px, ref n2, ref xx, ref thisterm, pret, precision);
    }

    public void lograt(ref PRAT px, int32_t precision)
    {
        PRAT pwr = null; // pwr is the large scaling factor.
        PRAT offset = null; // offset is the incremental scaling factor.

        // Check for someone taking the log of zero or a negative number.
        if (rat_le(px, rat_zero, precision))
        {
            throw new CalcErrException(CalcErr.CALC_E_DOMAIN);
        }

        // Get number > 1, for scaling
        bool fneglog = rat_lt(px, rat_one, precision);
        if (fneglog)
        {
            PNUMBER pnumtemp = (px).pp;
            (px).pp = (px).pq;
            (px).pq = pnumtemp;
        }

        // Scale the number within BASEX factor of 1, for the large scale.
        // log(x*2^(BASEXPWR*k)) = BASEXPWR*k*log(2)+log(x)
        if (LOGRAT2(px) > 1)
        {
            int32_t intpwr = LOGRAT2(px) - 1;
            (px).pq.exp += intpwr;
            pwr = i32torat(unchecked((int32_t)(intpwr * BASEXPWR))); // TODO: Check this for overflowhandling
            mulrat(ref pwr, ln_two, precision);
            // ln(x+e)-ln(x) looks close to e when x is close to one using some
            // expansions.  This means we can trim past precision digits+1.
            TRIMTOP(px, precision);
        }
        else
        {
            duprat(ref pwr, rat_zero);
        }

        duprat(ref offset, rat_zero);
        // Scale the number between 1 and e_to_one_half, for the small scale.
        while (rat_gt(px, e_to_one_half, precision))
        {
            divrat(ref px, e_to_one_half, precision);
            addrat(ref offset, rat_one, precision);
        }

        _lograt(ref px, precision);

        // Add the large and small scaling factors, take into account
        // small scaling was done in e_to_one_half chunks.
        divrat(ref offset, rat_two, precision);
        addrat(ref pwr, offset, precision);

        // And add the resulting scaling factor to the answer.
        addrat(ref px, pwr, precision);

        trimit(ref px, precision);

        // If number started out < 1 rescale answer to negative.
        if (fneglog)
        {
            (px).pp.sign *= -1;
        }

        destroyrat(ref offset);
        destroyrat(ref pwr);
    }

    public void log10rat(ref PRAT px, int32_t precision)
    {
        lograt(ref px, precision);
        divrat(ref px, ln_ten, precision);
    }

    //
    // return if the given x is even number. The assumption here is its denominator is 1 and we are testing the numerator is
    // even or not
    bool IsEven(PRAT x, uint32_t radix, int32_t precision)
    {
        PRAT tmp = null;
        bool bRet = false;

        duprat(ref tmp, x);
        divrat(ref tmp, rat_two, precision);
        fracrat(ref tmp, radix, precision);
        addrat(ref tmp, tmp, precision);
        subrat(ref tmp, rat_one, precision);
        if (rat_lt(tmp, rat_zero, precision))
        {
            bRet = true;
        }

        destroyrat(ref tmp);
        return bRet;
    }

    //---------------------------------------------------------------------------
    //
    //  FUNCTION: powrat
    //
    //  ARGUMENTS: PRAT *px, PRAT y, uint32_t radix, int32_t precision
    //
    //  RETURN: none, sets *px to *px to the y.
    //
    //  EXPLANATION: Calculates the power of both px and
    //  handles special cases where px is a perfect root.
    //  Assumes, all checking has been done on validity of numbers.
    //
    //
    //---------------------------------------------------------------------------
    public void powrat(ref PRAT px, PRAT y, uint32_t radix, int32_t precision)
    {
        // Handle cases where px or y is 0 by calling powratcomp directly
        if (zerrat(px) || zerrat(y))
        {
            powratcomp(ref px, y, radix, precision);
            return;
        }

        // When y is 1, return px
        if (rat_equ(y, rat_one, precision))
        {
            return;
        }

        try
        {
            powratNumeratorDenominator(ref px, y, radix, precision);
        }
        catch (Exception)
        {
            // If calculating the power using numerator/denominator
            // failed, fall back to the less accurate method of
            // passing in the original y
            powratcomp(ref px, y, radix, precision);
        }
    }

    void powratNumeratorDenominator(ref PRAT px, PRAT y, uint32_t radix, int32_t precision)
    {
        // Prepare rationals
        PRAT yNumerator = null;
        PRAT yDenominator = null;
        duprat(ref yNumerator, rat_zero); // yNumerator.pq is 1 one
        duprat(ref yDenominator, rat_zero); // yDenominator.pq is 1 one
        dupnum(ref yNumerator.pp, y.pp);
        dupnum(ref yDenominator.pp, y.pq);

        // Calculate the following use the Powers of Powers rule:
        // px ^ (yNum/yDenom) == px ^ yNum ^ (1/yDenom)
        // 1. For px ^ yNum, we call powratcomp directly which will call ratpowi32
        //    and store the result in pxPowNum
        // 2. For pxPowNum ^ (1/yDenom), we call powratcomp
        // 3. Validate the result of 2 by adding/subtracting 0.5, flooring and call powratcomp with yDenom
        //    on the floored result.

        // 1. Initialize result.
        PRAT pxPow = null;
        duprat(ref pxPow, px);

        // 2. Calculate pxPow = px ^ yNumerator
        // if yNumerator is not 1
        if (!rat_equ(yNumerator, rat_one, precision))
        {
            powratcomp(ref pxPow, yNumerator, radix, precision);
        }

        // 2. Calculate pxPowNumDenom = pxPowNum ^ (1/yDenominator),
        // if yDenominator is not 1
        if (!rat_equ(yDenominator, rat_one, precision))
        {
            // Calculate 1 over y
            PRAT oneoveryDenom = null;
            duprat(ref oneoveryDenom, rat_one);
            divrat(ref oneoveryDenom, yDenominator, precision);

            // ##################################
            // Take the oneoveryDenom power
            // ##################################
            PRAT originalResult = null;
            duprat(ref originalResult, pxPow);
            powratcomp(ref originalResult, oneoveryDenom, radix, precision);

            // ##################################
            // Round the originalResult to roundedResult
            // ##################################
            PRAT roundedResult = null;
            duprat(ref roundedResult, originalResult);
            if (roundedResult.pp.sign == -1)
            {
                subrat(ref roundedResult, rat_half, precision);
            }
            else
            {
                addrat(ref roundedResult, rat_half, precision);
            }

            intrat(ref roundedResult, radix, precision);

            // ##################################
            // Take the yDenom power of the roundedResult.
            // ##################################
            PRAT roundedPower = null;
            duprat(ref roundedPower, roundedResult);
            powratcomp(ref roundedPower, yDenominator, radix, precision);

            // ##################################
            // if roundedPower == px,
            // we found an exact power in roundedResult
            // ##################################
            if (rat_equ(roundedPower, pxPow, precision))
            {
                duprat(ref px, roundedResult);
            }
            else
            {
                duprat(ref px, originalResult);
            }

            destroyrat(ref oneoveryDenom);
            destroyrat(ref originalResult);
            destroyrat(ref roundedResult);
            destroyrat(ref roundedPower);
        }
        else
        {
            duprat(ref px, pxPow);
        }

        destroyrat(ref yNumerator);
        destroyrat(ref yDenominator);
        destroyrat(ref pxPow);
    }

    //---------------------------------------------------------------------------
    //
    //  FUNCTION: powratcomp
    //
    //  ARGUMENTS: PRAT *px, and PRAT y
    //
    //  RETURN: none, sets *px to *px to the y.
    //
    //  EXPLANATION: This uses x^y=e(y*ln(x)), or a more exact calculation where
    //  y is an integer.
    //  Assumes, all checking has been done on validity of numbers.
    //
    //
    //---------------------------------------------------------------------------
    void powratcomp(ref PRAT px, PRAT y, uint32_t radix, int32_t precision)
    {
        int32_t sign = SIGN(px);

        // Take the absolute value
        (px).pp.sign = 1;
        (px).pq.sign = 1;

        if (zerrat(px))
        {
            // *px is zero.
            if (rat_lt(y, rat_zero, precision))
            {
                throw new CalcErrException(CalcErr.CALC_E_DOMAIN);
            }
            else if (zerrat(y))
            {
                // *px and y are both zero, special case a 1 return.
                duprat(ref px, rat_one);
                // Ensure sign is positive.
                sign = 1;
            }
        }
        else
        {
            PRAT pxint = null;
            duprat(ref pxint, px);
            subrat(ref pxint, rat_one, precision);
            if (rat_gt(pxint, rat_negsmallest, precision) && rat_lt(pxint, rat_smallest, precision) && (sign == 1))
            {
                // *px is one, special case a 1 return.
                duprat(ref px, rat_one);
                // Ensure sign is positive.
                sign = 1;
            }
            else
            {
                // Only do the exp if the number isn't zero or one
                PRAT podd = null;
                duprat(ref podd, y);
                fracrat(ref podd, radix, precision);
                if (rat_gt(podd, rat_negsmallest, precision) && rat_lt(podd, rat_smallest, precision))
                {
                    // If power is an integer let ratpowi32 deal with it.
                    PRAT iy = null;
                    duprat(ref iy, y);
                    subrat(ref iy, podd, precision);
                    int32_t inty = rattoi32(iy, radix, precision);

                    PRAT plnx = null;
                    duprat(ref plnx, px);
                    lograt(ref plnx, precision);
                    mulrat(ref plnx, iy, precision);
                    if (rat_gt(plnx, rat_max_exp, precision) || rat_lt(plnx, rat_min_exp, precision))
                    {
                        // Don't attempt exp of anything large or small.A
                        destroyrat(ref plnx);
                        destroyrat(ref iy);
                        destroyrat(ref pxint);
                        destroyrat(ref podd);
                        throw new CalcErrException(CalcErr.CALC_E_DOMAIN);
                    }

                    destroyrat(ref plnx);
                    ratpowi32(ref px, inty, precision);
                    if ((inty & 1) == 0)
                    {
                        sign = 1;
                    }

                    destroyrat(ref iy);
                }
                else
                {
                    // power is a fraction
                    if (sign == -1)
                    {
                        // Need to throw an error if the exponent has an even denominator.
                        // As a first step, the numerator and denominator must be divided by 2 as many times as
                        //     possible, so that 2/6 is allowed.
                        // If the final numerator is still even, the end result should be positive.
                        PRAT pNumerator = null;
                        PRAT pDenominator = null;
                        bool fBadExponent = false;

                        // Get the numbers in arbitrary precision rational number format
                        duprat(ref pNumerator, rat_zero); // pNumerator.pq is 1 one
                        duprat(ref pDenominator, rat_zero); // pDenominator.pq is 1 one

                        dupnum(ref pNumerator.pp, y.pp);
                        pNumerator.pp.sign = 1;
                        dupnum(ref pDenominator.pp, y.pq);
                        pDenominator.pp.sign = 1;

                        while (IsEven(pNumerator, radix, precision) &&
                               IsEven(pDenominator, radix, precision)) // both Numerator & denominator is even
                        {
                            divrat(ref pNumerator, rat_two, precision);
                            divrat(ref pDenominator, rat_two, precision);
                        }

                        if (IsEven(pDenominator, radix, precision)) // denominator is still even
                        {
                            fBadExponent = true;
                        }

                        if (IsEven(pNumerator, radix, precision)) // numerator is still even
                        {
                            sign = 1;
                        }

                        destroyrat(ref pNumerator);
                        destroyrat(ref pDenominator);

                        if (fBadExponent)
                        {
                            throw new CalcErrException(CalcErr.CALC_E_DOMAIN);
                        }
                    }
                    else
                    {
                        // If the exponent is not odd disregard the sign.
                        sign = 1;
                    }

                    lograt(ref px, precision);
                    mulrat(ref px, y, precision);
                    exprat(ref px, radix, precision);
                }

                destroyrat(ref podd);
            }

            destroyrat(ref pxint);
        }

        (px).pp.sign *= sign;
    }
}
