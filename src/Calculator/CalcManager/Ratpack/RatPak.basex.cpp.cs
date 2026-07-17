// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

//-----------------------------------------------------------------------------
//  Package Title  ratpak
//  File           basex.c
//  Copyright      (C) 1995-97 Microsoft
//  Date           03-14-97
//
//
//  Description
//
//     Contains number routines for internal base computations, these assume
//  internal base is a power of 2.
//
//-----------------------------------------------------------------------------

using System;
using uint32_t = System.UInt32;
using int32_t = System.Int32;
using MANTTYPE = System.UInt32;
using TWO_MANTTYPE = System.UInt64;
using PNUMBER = CalcEngine.RatPakNUMBER;

namespace CalcEngine;

internal sealed partial class RatPak
{
    //----------------------------------------------------------------------------
    //
    //    FUNCTION: mulnumx
    //
    //    ARGUMENTS: pointer to a number and a second number, the
    //               base is always BASEX.
    //
    //    RETURN: None, changes first pointer.
    //
    //    DESCRIPTION: Does the number equivalent of *pa *= b.
    //    This is a stub which prevents multiplication by 1, this is a big speed
    //    improvement.
    //
    //----------------------------------------------------------------------------
    public static void mulnumx(ref PNUMBER pa, PNUMBER b)
    {
        if (b.cdigit > 1 || b.mant[0] != 1 || b.exp != 0)
        {
            // If b is not one we multiply
            if ((pa).cdigit > 1 || (pa).mant[0] != 1 || (pa).exp != 0)
            {
                // pa and b are both non-one.
                _mulnumx(ref pa, b);
            }
            else
            {
                // if pa is one and b isn't just copy b. and adjust the sign.
                int32_t sign = pa.sign;
                dupnum(ref pa, b);
                pa.sign *= sign;
            }
        }
        else
        {
            // B is +/- 1, But we do have to set the sign.
            pa.sign *= b.sign;
        }
    }

    //----------------------------------------------------------------------------
    //
    //    FUNCTION: _mulnumx
    //
    //    ARGUMENTS: pointer to a number and a second number, the
    //               base is always BASEX.
    //
    //    RETURN: None, changes first pointer.
    //
    //    DESCRIPTION: Does the number equivalent of *pa *= b.
    //    Assumes the base is BASEX of both numbers.  This algorithm is the
    //    same one you learned in grade school, except the base isn't 10 it's
    //    BASEX.
    //
    //----------------------------------------------------------------------------
    static
    //----------------------------------------------------------------------------
    //
    //    FUNCTION: _mulnumx
    //
    //    ARGUMENTS: pointer to a number and a second number, the
    //               base is always BASEX.
    //
    //    RETURN: None, changes first pointer.
    //
    //    DESCRIPTION: Does the number equivalent of *pa *= b.
    //    Assumes the base is BASEX of both numbers.  This algorithm is the
    //    same one you learned in grade school, except the base isn't 10 it's
    //    BASEX.
    //
    //----------------------------------------------------------------------------
    void _mulnumx(ref PNUMBER pa, PNUMBER b)
    {
        PNUMBER? c = null; // c will contain the result.
        PNUMBER a = pa; // a is the dereferenced number pointer from *pa

        MANTTYPE[] ptra; // ptra is a pointer to the mantissa of a.
        MANTTYPE[] ptrb; // ptrb is a pointer to the mantissa of b.

        int ptraCnt = 0; // Index in a's mantissa
        int ptrbCnt = 0; // Index in b's mantissa
        int ptrcCnt = 0; // Current position in c's mantissa
        int ptrcoffsetCnt = 0; // Base position in c for current digit of a

        int32_t iadigit = 0; // Index of digit being used in the first number.
        int32_t ibdigit = 0; // Index of digit being used in the second number.
        MANTTYPE da = 0; // da is the digit from the first number.
        TWO_MANTTYPE cy = 0; // cy is the carry resulting from the addition of a multiplied row.
        TWO_MANTTYPE mcy = 0; // mcy is the resultant from a single multiply and carry.
        int32_t icdigit = 0; // Index of digit being calculated in final result.

        // Calculate size for result and create it
        ibdigit = a.cdigit + b.cdigit - 1;
        createnum(ref c, (uint32_t)(ibdigit + 1));
        c.cdigit = ibdigit;
        c.sign = a.sign * b.sign;
        c.exp = a.exp + b.exp;

        ptra = a.mant;

        // Process each digit of a
        for (iadigit = a.cdigit; iadigit > 0; iadigit--)
        {
            da = ptra[ptraCnt++];
            ptrb = b.mant;
            ptrbCnt = 0; // Reset b index for each digit of a

            // Set position in result for this digit of a
            ptrcCnt = ptrcoffsetCnt++;

            // Multiply this digit of a by each digit of b
            for (ibdigit = b.cdigit; ibdigit > 0; ibdigit--)
            {
                cy = 0;
                mcy = (TWO_MANTTYPE)da * (ptrb[ptrbCnt]);

                if (mcy != 0)
                {
                    icdigit = 0;
                    if (ibdigit == 1 && iadigit == 1)
                    {
                        c.cdigit++;
                    }
                }

                // Process all carries and additions for this multiply
                while (mcy != 0 || cy != 0)
                {
                    // Calculate absolute position in c.mant
                    var cMantCnt = ptrcCnt + icdigit;

                    // Update carry from addition(s) and multiply
                    cy += (TWO_MANTTYPE)c.mant[cMantCnt] + ((uint32_t)mcy & ((uint32_t)~BASEX));

                    // Update result digit
                    c.mant[cMantCnt] = (MANTTYPE)((uint32_t)cy & ((uint32_t)~BASEX));

                    icdigit++;

                    // Update carries
                    mcy = mcy >> (int)BASEXPWR;
                    cy = cy >> (int)BASEXPWR;
                }

                ptrbCnt++;
                ptrcCnt++;
            }
        }

        // prevent different kinds of zeros, by stripping leading duplicate zeros.
        // digits are in order of increasing significance.
        while (c.cdigit > 1 && c.mant[c.cdigit - 1] == 0)
        {
            c.cdigit--;
        }

        pa = c;
    }

    //-----------------------------------------------------------------------------
    //
    //    FUNCTION: numpowi32x
    //
    //    ARGUMENTS: root as number power as int32_t
    //               number.
    //
    //    RETURN: None root is changed.
    //
    //    DESCRIPTION: changes numeric representation of root to
    //    root ** power. Assumes base BASEX
    //    decomposes the exponent into it's sums of powers of 2, so on average
    //    it will take n+n/2 multiplies where n is the highest on bit.
    //
    //-----------------------------------------------------------------------------
    public static void numpowi32x(ref PNUMBER proot, int32_t power)
    {
        PNUMBER lret = i32tonum(1, BASEX);

        // Once the power remaining is zero we are done.
        while (power > 0)
        {
            // If this bit in the power decomposition is on, multiply the result
            // by the root number.
            if ((power & 1) != 0)
            {
                mulnumx(ref lret, proot);
            }

            // multiply the root number by itself to scale for the next bit (i.e.
            // square it.
            mulnumx(ref proot, proot);

            // move the next bit of the power into place.
            power >>= 1;
        }

        proot = lret;
    }

    //----------------------------------------------------------------------------
    //
    //    FUNCTION: divnumx
    //
    //    ARGUMENTS: pointer to a number, a second number and precision.
    //
    //    RETURN: None, changes first pointer.
    //
    //    DESCRIPTION: Does the number equivalent of *pa /= b.
    //    Assumes radix is the internal radix representation.
    //    This is a stub which prevents division by 1, this is a big speed
    //    improvement.
    //
    //----------------------------------------------------------------------------
    public void divnumx(ref PNUMBER pa, PNUMBER b, int32_t precision)
    {
        if (b.cdigit > 1 || b.mant[0] != 1 || b.exp != 0)
        {
            // b is not one.
            if (pa.cdigit > 1 || pa.mant[0] != 1 || pa.exp != 0)
            {
                // pa and b are both not one.
                _divnumx(ref pa, b, precision);
            }
            else
            {
                // if pa is one and b is not one, just copy b, and adjust the sign.
                int32_t sign = pa.sign;
                dupnum(ref pa, b);
                pa.sign *= sign;
            }
        }
        else
        {
            // b is one so don't divide, but set the sign.
            pa.sign *= b.sign;
        }
    }

    //----------------------------------------------------------------------------
    //
    //    FUNCTION: _divnumx
    //
    //    ARGUMENTS: pointer to a number, a second number and precision.
    //
    //    RETURN: None, changes first pointer.
    //
    //    DESCRIPTION: Does the number equivalent of *pa /= b.
    //    Assumes radix is the internal radix representation.
    //
    //----------------------------------------------------------------------------
    void _divnumx(ref PNUMBER pa, PNUMBER b, int32_t precision)
    {
        PNUMBER a = pa; // a is the dereferenced number pointer from *pa
        PNUMBER? c = null; // c will contain the result.
        PNUMBER? lasttmp = null; // lasttmp allows a backup when the algorithm
        // guesses one bit too far.
        PNUMBER? tmp = null; // current guess being worked on for divide.
        PNUMBER? rem = null; // remainder after applying guess.
        int32_t cdigits; // count of digits for answer.
        int32_t ptrcIndex; // index into the mantissa array (replacing pointer arithmetic)

        int32_t thismax = precision + g_ratio; // set a maximum number of internal digits
        // to shoot for in the divide.

        if (thismax < a.cdigit)
        {
            // a has more digits than precision specified, bump up digits to shoot
            // for.
            thismax = a.cdigit;
        }

        if (thismax < b.cdigit)
        {
            // b has more digits than precision specified, bump up digits to shoot
            // for.
            thismax = b.cdigit;
        }

        // Create c (the divide answer) and set up exponent and sign.
        createnum(ref c, (uint32_t)(thismax + 1));

        c.exp = (a.cdigit + a.exp) - (b.cdigit + b.exp) + 1;
        c.sign = a.sign * b.sign;

        // In C++: ptrc = c.mant + thismax; - convert to array index
        ptrcIndex = thismax;
        cdigits = 0;

        dupnum(ref rem, a);
        rem.sign = b.sign;
        rem.exp = b.cdigit + b.exp - rem.cdigit;

        while (cdigits++ < thismax && !zernum(rem))
        {
            int32_t digit = 0;
            c.mant[ptrcIndex] = 0; // *ptrc = 0; in C++
            while (!lessnum(rem, b))
            {
                digit = 1;
                dupnum(ref tmp, b);
                destroynum(ref lasttmp);
                lasttmp = i32tonum(0, BASEX);
                while (lessnum(tmp, rem))
                {
                    destroynum(ref lasttmp);
                    dupnum(ref lasttmp, tmp);
                    addnum(ref tmp, tmp, BASEX);
                    digit *= 2;
                }

                if (lessnum(rem, tmp))
                {
                    // too far, back up...
                    destroynum(ref tmp);
                    digit /= 2;
                    tmp = lasttmp;
                    lasttmp = null;
                }

                tmp.sign *= -1;
                addnum(ref rem, tmp, BASEX);
                destroynum(ref tmp);
                destroynum(ref lasttmp);
                c.mant[ptrcIndex] |= (MANTTYPE)digit; // *ptrc |= digit; in C++
            }

            rem.exp++;
            ptrcIndex--; // ptrc--; in C++
        }

        cdigits--;
        ptrcIndex++; // ++ptrc; in C++

        if (ptrcIndex != 0) // if (c.mant != ptrc) in C++
        {
            // Replace memmove with Array.Copy
            Array.Copy(c.mant, ptrcIndex, c.mant, 0, cdigits);
        }

        if (cdigits == 0)
        {
            // A zero, make sure no weird exponents creep in
            c.exp = 0;
            c.cdigit = 1;
        }
        else
        {
            c.cdigit = cdigits;
            c.exp -= cdigits;
            // prevent different kinds of zeros, by stripping leading duplicate
            // zeros. digits are in order of increasing significance.
            while (c.cdigit > 1 && c.mant[c.cdigit - 1] == 0)
            {
                c.cdigit--;
            }
        }

        destroynum(ref rem);
        pa = c;
    }
}
