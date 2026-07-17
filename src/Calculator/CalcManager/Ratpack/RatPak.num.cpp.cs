// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

//-----------------------------------------------------------------------------
//  Package Title  ratpak
//  File           num.c
//  Copyright      (C) 1995-97 Microsoft
//  Date           01-16-95
//
//
//  Description
//
//     Contains number routines for add, mul, div, rem and other support
//  and longs.
//
//  Special Information
//
//
//-----------------------------------------------------------------------------

// #include <list>
// #include <cstring> // for memmove
// #include "ratpak.h"

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using uint8_t = System.Byte;
using uint32_t = System.UInt32;
using uint64_t = System.UInt64;
using int32_t = System.Int32;
using wchar_t = System.Char;
using wstring_view = string;
using WString = string;
using MANTTYPE = System.UInt32;
using TWO_MANTTYPE = System.UInt64;
using PNUMBER = CalcEngine.RatPakNUMBER;
using PPNUMBER = CalcEngine.RatPakNUMBER;
using PRAT = CalcEngine.RatPakRAT;

namespace CalcEngine;

internal sealed partial class RatPak
{
    //----------------------------------------------------------------------------
    //
    //    FUNCTION: addnum
    //
    //    ARGUMENTS: pointer to a number a second number, and the
    //               radix.
    //
    //    RETURN: None, changes first pointer.
    //
    //    DESCRIPTION: Does the number equivalent of *pa += b.
    //    Assumes radix is the base of both numbers.
    //
    //    ALGORITHM: Adds each digit from least significant to most
    //    significant.
    //
    //
    //----------------------------------------------------------------------------
    // void _addnum(PNUMBER* pa, PNUMBER b, uint32_t radix);
    public static void addnum(ref PNUMBER pa, PNUMBER b, uint32_t radix)
    {
        if (b.cdigit > 1 || b.mant[0] != 0)
        {
            // If b is zero we are done.
            if (pa.cdigit > 1 || (pa).mant[0] != 0)
            {
                // pa and b are both nonzero.
                _addnum(ref pa, b, radix);
            }
            else
            {
                // if pa is zero and b isn't just copy b.
                dupnum(ref pa, b);
            }
        }
    }

    static
    void _addnum(ref PNUMBER pa, PNUMBER b, uint32_t radix)
    {
        PNUMBER? c = null; // c will contain the result.
        PNUMBER? a = null; // a is the dereferenced number pointer from *pa
        MANTTYPE[] pcha; // pcha is a pointer to the mantissa of a.
        MANTTYPE[] pchb; // pchb is a pointer to the mantissa of b.
        MANTTYPE[] pchc; // pchc is a pointer to the mantissa of c.

        int32_t pchaCnt = 0, pchbCnt = 0, pchcCnt = 0;

        int32_t cdigits; // cdigits is the max count of the digits results used as a counter.
        int32_t mexp; // mexp is the exponent of the result.
        MANTTYPE da; // da is a single 'digit' after possible padding.
        MANTTYPE db; // db is a single 'digit' after possible padding.
        MANTTYPE cy = 0; // cy is the value of a carry after adding two 'digits'
        int32_t fcompla = 0; // fcompla is a flag to signal a is negative.
        int32_t fcomplb = 0; // fcomplb is a flag to signal b is negative.

        a = pa;

        // Calculate the overlap of the numbers after alignment, this includes
        // necessary padding 0's
        cdigits = Math.Max(a.cdigit + a.exp, b.cdigit + b.exp) - Math.Min(a.exp, b.exp);

        createnum(ref c, (uint32_t)(cdigits + 1));
        c.exp = Math.Min(a.exp, b.exp);
        mexp = c.exp;
        c.cdigit = cdigits;
        pcha = a.mant;
        pchb = b.mant;
        pchc = c.mant;

        // Figure out the sign of the numbers
        if (a.sign != b.sign)
        {
            cy = 1;
            fcompla = (a.sign == -1) ? 1 : 0;
            fcomplb = (b.sign == -1) ? 1 : 0;
        }

        // Loop over all the digits, real and 0 padded. Here we know a and b are
        // aligned
        for (; cdigits > 0; cdigits--, mexp++)
        {
            // Get digit from a, taking padding into account.
            da = (((mexp >= a.exp) && (cdigits + a.exp - c.exp > (c.cdigit - a.cdigit))) ? pcha[pchaCnt++] : 0);
            // Get digit from b, taking padding into account.
            db = (((mexp >= b.exp) && (cdigits + b.exp - c.exp > (c.cdigit - b.cdigit))) ? pchb[pchbCnt++] : 0);

            // Handle complementing for a and b digit. Might be a better way, but
            // haven't found it yet.
            if (fcompla != 0)
            {
                da = (MANTTYPE)(radix) - 1 - da;
            }

            if (fcomplb != 0)
            {
                db = (MANTTYPE)(radix) - 1 - db;
            }

            // Update carry as necessary
            cy = da + db + cy;
            pchc[pchcCnt++] = (MANTTYPE)(cy % (MANTTYPE)radix);
            cy /= (MANTTYPE)radix;
        }

        // Handle carry from last sum as extra digit
        if (cy != 0 && !(fcompla != 0 || fcomplb != 0))
        {
            pchc[pchcCnt++] = cy; // FIXED: Changed from pcha to pchc
            c.cdigit++;
        }

        // Compute sign of result
        if (!(fcompla != 0 || fcomplb != 0))
        {
            c.sign = a.sign;
        }
        else
        {
            if (cy != 0)
            {
                c.sign = 1;
            }
            else
            {
                // In this particular case an overflow or underflow has occurred
                // and all the digits need to be complemented, at one time an
                // attempt to handle this above was made, it turned out to be much
                // slower on average.
                c.sign = -1;
                cy = 1;
                pchcCnt = 0; // Reset counter to start of mantissa

                // FIXED: Properly initialize cdigits to c.cdigit
                for (cdigits = c.cdigit; cdigits > 0; cdigits--)
                {
                    cy = (MANTTYPE)radix - (MANTTYPE)1 - pchc[pchcCnt] + cy;
                    pchc[pchcCnt++] = (MANTTYPE)(cy % (MANTTYPE)radix);
                    cy /= (MANTTYPE)radix;
                }
            }
        }

        // Reset pchcCnt to point to the last digit
        pchcCnt = c.cdigit - 1;

        // Remove leading zeros, remember digits are in order of
        // increasing significance. i.e. 100 would be 0,0,1
        while (c.cdigit > 1 && pchc[pchcCnt--] == 0)
        {
            c.cdigit--;
        }

        pa = c;
    }


    //----------------------------------------------------------------------------
    //
    //    FUNCTION: mulnum
    //
    //    ARGUMENTS: pointer to a number a second number, and the
    //               radix.
    //
    //    RETURN: None, changes first pointer.
    //
    //    DESCRIPTION: Does the number equivalent of *pa *= b.
    //    Assumes radix is the radix of both numbers.  This algorithm is the
    //    same one you learned in grade school.
    //
    //----------------------------------------------------------------------------

    // void _mulnum(PNUMBER* pa, PNUMBER b, uint32_t radix);

    public static void mulnum(ref PNUMBER pa, PNUMBER b, uint32_t radix)
    {
        if (b.cdigit > 1 || b.mant[0] != 1 || b.exp != 0)
        {
            // If b is one we don't multiply exactly.
            if ((pa).cdigit > 1 || (pa).mant[0] != 1 || (pa).exp != 0)
            {
                // pa and b are both non-one.
                _mulnum(ref pa, b, radix);
            }
            else
            {
                // if pa is one and b isn't just copy b, and adjust the sign.
                int32_t sign = (pa).sign;
                dupnum(ref pa, b);
                (pa).sign *= sign;
            }
        }
        else
        {
            // But we do have to set the sign.
            (pa).sign *= b.sign;
        }
    }

    static
    void _mulnum(ref PNUMBER pa, PNUMBER b, uint32_t radix)
    {
        PNUMBER? c = null; // c will contain the result.
        PNUMBER? a = null; // a is the dereferenced number pointer from *pa

        MANTTYPE[] pcha; // pcha is a pointer to the mantissa of a.
        MANTTYPE[] pchb; // pchb is a pointer to the mantissa of b.
        // REMOVED: We don't need separate pchc and pchcoffset arrays

        // ADDED: Counters to track position in arrays
        int pchaCnt = 0; // Counter for pcha array position
        int pchbCnt = 0; // Counter for pchb array position
        int pchcCnt = 0; // Counter for current position in result array c.mant
        int pchcoffsetCnt = 0; // Counter for row offset in result array

        int32_t iadigit = 0; // Index of digit being used in the first number.
        int32_t ibdigit = 0; // Index of digit being used in the second number.
        MANTTYPE da = 0; // da is the digit from the fist number.
        TWO_MANTTYPE cy = 0; // cy is the carry resulting from the addition of
        // a multiplied row into the result.
        TWO_MANTTYPE mcy = 0; // mcy is the resultant from a single
        // multiply, AND the carry of that multiply.
        int32_t icdigit = 0; // Index of digit being calculated in final result.

        a = pa;
        ibdigit = a.cdigit + b.cdigit - 1;
        createnum(ref c, (uint32_t)(ibdigit + 1));
        c.cdigit = ibdigit;
        c.sign = a.sign * b.sign;

        c.exp = a.exp + b.exp;
        pcha = a.mant;
        // REMOVED: pchcoffset = c.mant; - We'll use index tracking instead

        for (iadigit = a.cdigit; iadigit > 0; iadigit--)
        {
            da = pcha[pchaCnt++]; // CHANGED: *pcha++ becomes array indexing with counter
            pchb = b.mant;
            pchbCnt = 0; // ADDED: Reset b pointer counter for each digit of a

            // Shift pchc, and pchcoffset, one for each digit
            pchcCnt = pchcoffsetCnt++; // CHANGED: pchc = pchcoffset++ using counters

            for (ibdigit = b.cdigit; ibdigit > 0; ibdigit--)
            {
                cy = 0;
                mcy = (TWO_MANTTYPE)da * pchb[pchbCnt]; // CHANGED: *pchb becomes array indexing
                if (mcy != 0)
                {
                    icdigit = 0;
                    if (ibdigit == 1 && iadigit == 1)
                    {
                        c.cdigit++;
                    }
                }

                // If result is nonzero, or while result of carry is nonzero...
                while (mcy != 0 || cy != 0)
                {
                    // ADDED: Calculate absolute position in c.mant
                    int absPos = pchcCnt + icdigit;

                    // update carry from addition(s) and multiply.
                    cy += (TWO_MANTTYPE)c.mant[absPos] + (mcy % (TWO_MANTTYPE)radix);
                    // CHANGED: pchc[icdigit] becomes c.mant[absPos]

                    // update result digit from
                    c.mant[absPos] = (MANTTYPE)(cy % (TWO_MANTTYPE)radix);
                    // CHANGED: pchc[icdigit++] becomes c.mant[absPos] with separate icdigit++
                    icdigit++;

                    // update carries from
                    mcy /= (TWO_MANTTYPE)radix;
                    cy /= (TWO_MANTTYPE)radix;
                }

                pchbCnt++;
                pchcCnt++; // CHANGED: pchc++ becomes counter increment
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

    //----------------------------------------------------------------------------
    //
    //    FUNCTION: remnum
    //
    //    ARGUMENTS: pointer to a number a second number, and the
    //               radix.
    //
    //    RETURN: None, changes first pointer.
    //
    //    DESCRIPTION: Does the number equivalent of *pa %= b.
    //            Repeatedly subtracts off powers of 2 of b until *pa < b.
    //
    //
    //----------------------------------------------------------------------------
    static
    //----------------------------------------------------------------------------
    //
    //    FUNCTION: remnum
    //
    //    ARGUMENTS: pointer to a number a second number, and the
    //               radix.
    //
    //    RETURN: None, changes first pointer.
    //
    //    DESCRIPTION: Does the number equivalent of *pa %= b.
    //            Repeatedly subtracts off powers of 2 of b until *pa < b.
    //
    //
    //----------------------------------------------------------------------------
    void remnum(ref PNUMBER pa, PNUMBER b, uint32_t radix)
    {
        PNUMBER? tmp = null; // tmp is the working remainder.
        PNUMBER? lasttmp = null; // lasttmp is the last remainder which worked.

        // Once *pa is less than b, *pa is the remainder.
        while (!lessnum(pa, b))
        {
            dupnum(ref tmp, b);
            if (lessnum(tmp, pa))
            {
                // Start off close to the right answer for subtraction.
                tmp.exp = (pa).cdigit + (pa).exp - tmp.cdigit;
                if (MSD(pa) <= MSD(tmp))
                {
                    // Don't take the chance that the numbers are equal.
                    tmp.exp--;
                }
            }

            destroynum(ref lasttmp);
            lasttmp = i32tonum(0, radix);

            while (lessnum(tmp, pa))
            {
                dupnum(ref lasttmp, tmp);
                addnum(ref tmp, tmp, radix);
            }

            if (lessnum(pa, tmp))
            {
                // too far, back up...
                destroynum(ref tmp);
                tmp = lasttmp;
                lasttmp = null;
            }

            // Subtract the working remainder from the remainder holder.
            tmp.sign = -1 * (pa).sign;
            addnum(ref pa, tmp, radix);
            destroynum(ref tmp);
            destroynum(ref lasttmp);
        }
    }

    //---------------------------------------------------------------------------
    //
    //    FUNCTION: divnum
    //
    //    ARGUMENTS: pointer to a number a second number, and the
    //               radix.
    //
    //    RETURN: None, changes first pointer.
    //
    //    DESCRIPTION: Does the number equivalent of *pa /= b.
    //    Assumes radix is the radix of both numbers.
    //
    //---------------------------------------------------------------------------

    // void _divnum(PNUMBER* pa, PNUMBER b, uint32_t radix, int32_t precision);

    public static void divnum(ref PNUMBER pa, PNUMBER b, uint32_t radix, int32_t precision)
    {
        if (b.cdigit > 1 || b.mant[0] != 1 || b.exp != 0)
        {
            // b is not one
            _divnum(ref pa, b, radix, precision);
        }
        else
        {
            // But we do have to set the sign.
            pa.sign *= b.sign;
        }
    }

    static
    void _divnum(ref PNUMBER pa, PNUMBER b, uint32_t radix, int32_t precision)
    {
        PNUMBER a = pa;
        int32_t thismax = precision + 2;
        if (thismax < a.cdigit)
        {
            thismax = a.cdigit;
        }

        if (thismax < b.cdigit)
        {
            thismax = b.cdigit;
        }

        PNUMBER? c = null;
        createnum(ref c, (uint32_t)(thismax + 1));
        c.exp = (a.cdigit + a.exp) - (b.cdigit + b.exp) + 1;
        c.sign = a.sign * b.sign;

        MANTTYPE[] ptrc = c.mant;
        int32_t ptrcCnt = thismax;

        PNUMBER? rem = null;
        PNUMBER? tmp = null;
        dupnum(ref rem, a);
        dupnum(ref tmp, b);
        tmp.sign = a.sign;
        rem.exp = b.cdigit + b.exp - rem.cdigit;

        // Build a table of multiplications of the divisor, this is quicker for
        // more than radix 'digits'
        LinkedList<PNUMBER> numberList = new LinkedList<PNUMBER>();
        numberList.AddFirst(i32tonum(0, radix));

        for (uint32_t i = 1; i < radix; i++)
        {
            PNUMBER? newValue = null;
            dupnum(ref newValue, numberList.First!.Value);
            addnum(ref newValue, tmp, radix);

            numberList.AddFirst(newValue);
        }

        destroynum(ref tmp);

        int32_t digit;
        int32_t cdigits = 0;
        while (cdigits++ < thismax && !zernum(rem))
        {
            digit = (int32_t)(radix - 1);
            PNUMBER? multiple = null;
            foreach (var num in numberList)
            {
                if (!lessnum(rem, num) || --digit == 0)
                {
                    multiple = num;
                    break;
                }
            }

            if (multiple is null)
            {
                throw new InvalidOperationException("Division lookup did not produce a multiplier.");
            }

            if (digit != 0)
            {
                multiple.sign *= -1;
                addnum(ref rem, multiple, radix);
                multiple.sign *= -1;
            }

            rem.exp++;
            ptrc[ptrcCnt--] = (MANTTYPE)digit;
        }

        cdigits--;

        // This is equivalent to "if (c->mant != ++ptrc)" in C++
        if (ptrcCnt + 1 > 0) // If we need to shift the data
        {
            // Move the digits to the beginning of the array
            Array.Copy(ptrc, ptrcCnt + 1, ptrc, 0, cdigits);
        }

        // Cleanup table structure
        numberList.Clear();

        if (cdigits == 0)
        {
            c.cdigit = 1;
            c.exp = 0;
        }
        else
        {
            c.cdigit = cdigits;
            c.exp -= cdigits;
            while (c.cdigit > 1 && c.mant[c.cdigit - 1] == 0)
            {
                c.cdigit--;
            }
        }

        destroynum(ref rem);
        pa = c;
    }

    //---------------------------------------------------------------------------
    //
    //    FUNCTION: equnum
    //
    //    ARGUMENTS: two numbers.
    //
    //    RETURN: Boolean
    //
    //    DESCRIPTION: Does the number equivalent of ( a == b )
    //    Only assumes that a and b are the same radix.
    //
    //---------------------------------------------------------------------------

    public static bool equnum(PNUMBER a, PNUMBER b)
    {
        int32_t diff;
        MANTTYPE[] pa;
        MANTTYPE[] pb;
        int32_t cdigits;
        int32_t ccdigits;
        MANTTYPE da;
        MANTTYPE db;

        diff = (a.cdigit + a.exp) - (b.cdigit + b.exp);
        if (diff < 0)
        {
            // If the exponents are different, these are different numbers.
            return false;
        }
        else
        {
            if (diff > 0)
            {
                // If the exponents are different, these are different numbers.
                return false;
            }
            else
            {
                // OK the exponents match.
                pa = a.mant;
                pb = b.mant;
                var paCnt = a.cdigit - 1;
                var pbCnt = b.cdigit - 1;
                cdigits = Math.Max(a.cdigit, b.cdigit);
                ccdigits = cdigits;

                // Loop over all digits until we run out of digits or there is a
                // difference in the digits.
                for (; cdigits > 0; cdigits--)
                {
                    da = ((cdigits > (ccdigits - a.cdigit)) ? pa[paCnt--] : 0);
                    db = ((cdigits > (ccdigits - b.cdigit)) ? pb[pbCnt--] : 0);
                    if (da != db)
                    {
                        return false;
                    }
                }

                // In this case, they are equal.
                return true;
            }
        }
    }

    //---------------------------------------------------------------------------
    //
    //    FUNCTION: lessnum
    //
    //    ARGUMENTS: two numbers.
    //
    //    RETURN: Boolean
    //
    //    DESCRIPTION: Does the number equivalent of ( abs(a) < abs(b) )
    //    Only assumes that a and b are the same radix, WARNING THIS IS AN.
    //    UNSIGNED COMPARE!
    //
    //---------------------------------------------------------------------------
    static bool lessnum(PNUMBER a, PNUMBER b)
    {
        int32_t diff = (a.cdigit + a.exp) - (b.cdigit + b.exp);
        if (diff < 0)
        {
            // The exponent of a is less than b
            return true;
        }

        if (diff > 0)
        {
            return false;
        }

        MANTTYPE[] pa = a.mant;
        MANTTYPE[] pb = b.mant;

        int paCnt = a.cdigit - 1;
        int pbCnt = b.cdigit - 1;

        int32_t cdigits = Math.Max(a.cdigit, b.cdigit);
        int32_t ccdigits = cdigits;
        for (; cdigits > 0; cdigits--)
        {
            // PORT: FIX: Apply post-decrement to the array index, not the value
            MANTTYPE da = ((cdigits > (ccdigits - a.cdigit)) ? pa[paCnt--] : 0);
            MANTTYPE db = ((cdigits > (ccdigits - b.cdigit)) ? pb[pbCnt--] : 0);
            diff = (int32_t)(da - db);
            if (diff != 0)
            {
                return (diff < 0);
            }
        }

        // In this case, they are equal.
        return false;
    }

    //----------------------------------------------------------------------------
    //
    //    FUNCTION: zernum
    //
    //    ARGUMENTS: number
    //
    //    RETURN: Boolean
    //
    //    DESCRIPTION: Does the number equivalent of ( !a )
    //
    //----------------------------------------------------------------------------
    public static bool zernum(PNUMBER a)
    {
        int32_t length;
        MANTTYPE[] pcha;
        int32_t pchaCnt = 0;
        length = a.cdigit;
        pcha = a.mant;

        // loop over all the digits until you find a nonzero or until you run
        // out of digits
        while (length-- > 0)
        {
            if (pcha[pchaCnt++] != 0)
            {
                // One of the digits isn't zero, therefore the number isn't zero
                return false;
            }
        }

        // All of the digits are zero, therefore the number is zero
        return true;
    }
}
