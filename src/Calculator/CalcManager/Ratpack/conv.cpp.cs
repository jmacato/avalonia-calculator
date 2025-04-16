// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

//---------------------------------------------------------------------------
//  Package Title  ratpak
//  File           conv.c
//  Copyright      (C) 1995-97 Microsoft
//  Date           01-16-95
//
//
//  Description
//
//     Contains conversion, input and output routines for numbers rationals
//  and i32s.
//
//
//
//---------------------------------------------------------------------------

using System.Text;
using uint8_t = System.Byte;
using uint32_t = System.UInt32;
using uint64_t = System.UInt64;
using int32_t = System.Int32;
using wchar_t = System.Char;
using wstring_view = string;
using wstring = string;
using MANTTYPE = System.UInt32;
using PNUMBER = CalcEngine.RatPak.NUMBER;
using PRAT = CalcEngine.RatPak.RAT;
using System.Linq;
using System;

namespace CalcEngine;

public partial class RatPak
{
    private const int MAX_ZEROS_AFTER_DECIMAL = 2;
    private const uint32_t UINT32_MAX = uint.MaxValue;

    // digits 0..64 used by bases 2 .. 64
    private const wstring_view DIGITS = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_@";

    // ratio of internal 'digits' to output 'digits'
    // Calculated elsewhere as part of initialization and when base is changed
    //    public int32_t g_ratio; // int(log(2L^BASEXPWR)/log(radix))

    // Default decimal separator
    private wchar_t g_decimalSeparator = '.';

    private uint64_t Calc_UInt32x32To64(UInt32 a, UInt32 b) => (a * (uint64_t)b);

    public const int32_t
        CALC_INTSAFE_E_ARITHMETIC_OVERFLOW = unchecked((int32_t)0x80070216U); // 0x216 = 534 = ERROR_ARITHMETIC_OVERFLOW

    public const uint32_t CALC_ULONG_ERROR = 0xffffffffU;

    int32_t Calc_ULongAdd(uint32_t ulAugend, uint32_t ulAddend, out uint32_t pulResult)
    {
        int32_t hr = CALC_INTSAFE_E_ARITHMETIC_OVERFLOW;
        pulResult = CALC_ULONG_ERROR;

        if ((ulAugend + ulAddend) >= ulAugend)
        {
            pulResult = (ulAugend + ulAddend);
            hr = (int32_t)S_OK;
        }

        return hr;
    }

    int32_t Calc_ULongLongToULong(uint64_t ullOperand, out uint32_t pulResult)
    {
        int32_t hr = CALC_INTSAFE_E_ARITHMETIC_OVERFLOW;
        pulResult = CALC_ULONG_ERROR;

        if (ullOperand <= UINT32_MAX)
        {
            pulResult = (uint32_t)ullOperand;
            hr = (int32_t)S_OK;
        }

        return hr;
    }

    int32_t Calc_ULongMult(uint32_t ulMultiplicand, uint32_t ulMultiplier, out uint32_t pulResult)
    {
        uint64_t ull64Result = Calc_UInt32x32To64(ulMultiplicand, ulMultiplier);

        return Calc_ULongLongToULong(ull64Result, out pulResult);
    }

    public void SetDecimalSeparator(wchar_t decimalSeparator)
    {
        g_decimalSeparator = decimalSeparator;
    }

    //-----------------------------------------------------------------------------
    //
    //    FUNCTION: _dupnum
    //
    //    ARGUMENTS: pointer to a number, pointer to a number
    //
    //    RETURN: None
    //
    //    DESCRIPTION: Copies the source to the destination
    //
    //-----------------------------------------------------------------------------
    private  void _dupnum(ref PNUMBER? dest, NUMBER src)
    {
        dest = new PNUMBER
        {
            cdigit = src.cdigit,
            exp = src.exp,
            sign = src.sign,
        };

        dest.mant = src.mant.ToArray();

        // memcpy(dest, src, (int)(sizeof(NUMBER) + ((src).cdigit) * (sizeof(MANTTYPE))));
    }

    //-----------------------------------------------------------------------------
    //
    //    FUNCTION: _destroynum
    //
    //    ARGUMENTS: pointer to a number
    //
    //    RETURN: None
    //
    //    DESCRIPTION: Deletes the number and associated allocation
    //
    //-----------------------------------------------------------------------------
    private   void _destroynum(ref PNUMBER? pnum)
    {
        if (pnum != null)
        {
            pnum = null;
        }
    }

    //-----------------------------------------------------------------------------
    //
    //    FUNCTION: _destroyrat
    //
    //    ARGUMENTS: pointer to a rational
    //
    //    RETURN: None
    //
    //    DESCRIPTION: Deletes the rational and associated
    //    allocations.
    //
    //-----------------------------------------------------------------------------
    private   void _destroyrat(ref PRAT? prat)

    {
        if (prat != null)
        {
            destroynum(ref prat.pp);
            destroynum(ref prat.pq);
            prat = null;
        }
    }

    //-----------------------------------------------------------------------------
    //
    //    FUNCTION: _createnum
    //
    //    ARGUMENTS: size of number in 'digits'
    //
    //    RETURN: pointer to a number
    //
    //    DESCRIPTION: allocates and zeros out number type.
    //
    //-----------------------------------------------------------------------------
    private   PNUMBER _createnum(uint32_t size)
    {
        PNUMBER pnumret = null;
        uint32_t cbAlloc;

        // sizeof( MANTTYPE ) is the size of a 'digit'
        if (SUCCEEDED(Calc_ULongAdd(size, 1, out cbAlloc)) &&
            SUCCEEDED(Calc_ULongMult(cbAlloc, (uint32_t)sizeof(MANTTYPE), out cbAlloc))
            && SUCCEEDED(Calc_ULongAdd(cbAlloc, NUMBER.ConstSizeOf, out cbAlloc)))
        {
            // pnumret = (PNUMBER)zmalloc(cbAlloc);
            // if (pnumret == null)
            // {
            // throw(CALC_E_OUTOFMEMORY);
            // }
            pnumret = new PNUMBER
            {
                mant = new uint32_t[size]
            };
        }
        else
        {
            throw new CalcErrException(CalcErr.CALC_E_INVALIDRANGE);
        }

        return (pnumret);
    }

    //-----------------------------------------------------------------------------
    //
    //    FUNCTION: _createrat
    //
    //    ARGUMENTS: none
    //
    //    RETURN: pointer to a rational
    //
    //    DESCRIPTION: allocates a rational structure but does not
    //    allocate the numbers that make up the rational p over q
    //    form.  These number pointers are left pointing to null.
    //
    //-----------------------------------------------------------------------------
    private PRAT _createrat()
    {
        PRAT? prat = null;

        prat = new PRAT(); //(PRAT)zmalloc(sizeof(RAT));

        // Probably not gonna happen ever? I hope.
        if (prat == null)
        {
            throw new CalcErrException(CalcErr.CALC_E_OUTOFMEMORY);
        }

        prat.pp = null;
        prat.pq = null;
        return prat;
    }

    //-----------------------------------------------------------------------------
    //
    //    FUNCTION: numtorat
    //
    //    ARGUMENTS: pointer to a number, radix number is in.
    //
    //    RETURN: Rational representation of number.
    //
    //    DESCRIPTION: The rational representation of the number
    //    is guaranteed to be in the form p (number with internal
    //    base   representation) over q (number with internal base
    //    representation)  Where p and q are integers.
    //
    //-----------------------------------------------------------------------------
    public PRAT numtorat(PNUMBER pin, uint32_t radix)

    {
        PNUMBER? pnRadixn = null;
        dupnum(ref pnRadixn, pin);

        PNUMBER? qnRadixn = i32tonum(1, radix);

        // Ensure p and q start out as integers.
        if (pnRadixn.exp < 0)
        {
            qnRadixn.exp -= pnRadixn.exp;
            pnRadixn.exp = 0;
        }

        PRAT pout = null;
        createrat(ref pout);

        // There is probably a better way to do this.
        pout.pp = numtonRadixx(pnRadixn, radix);
        pout.pq = numtonRadixx(qnRadixn, radix);

        destroynum(ref pnRadixn);
        destroynum(ref qnRadixn);

        return (pout);
    }

    //----------------------------------------------------------------------------
    //
    //    FUNCTION: nRadixxtonum
    //
    //    ARGUMENTS: pointer to a number, base requested.
    //
    //    RETURN: number representation in radix requested.
    //
    //    DESCRIPTION: Does a base conversion on a number from
    //    internal to requested base. Assumes number being passed
    //    in is really in internal base form.
    //
    //----------------------------------------------------------------------------
    public PNUMBER nRadixxtonum(PNUMBER a, uint32_t radix, int32_t precision)
    {
        PNUMBER sum = i32tonum(0, radix);

        // Fix the precision problem - use unchecked to avoid overflow exception
        // and cast to long before negating to properly handle INT_MIN
        PNUMBER powofnRadix = i32tonum(unchecked((int32_t)BASEX), radix);

        // A large penalty is paid for conversion of digits no one will see anyway.
        // limit the digits to the minimum of the existing precision or the
        // requested precision.
        uint32_t cdigits = (uint32_t)(precision + 1);
        if (cdigits > (uint32_t)a.cdigit)
        {
            cdigits = (uint32_t)a.cdigit;
        }

        // scale by the internal base to the internal exponent offset of the LSD
        numpowi32(ref powofnRadix, (int32_t)(a.exp + (a.cdigit - cdigits)), radix, precision);

        // Loop over all the relative digits from MSD to LSD
        // In C#, we'll use an index to simulate pointer arithmetic
        for (int digitIndex = a.cdigit - 1; cdigits > 0; digitIndex--, cdigits--)
        {
            MANTTYPE digit = a.mant[digitIndex];

            // Loop over all the bits from MSB to LSB
            for (uint32_t bitmask = BASEX / 2; bitmask > 0; bitmask /= 2)
            {
                addnum(ref sum, sum, radix);
                if ((digit & bitmask) != 0)
                {
                    sum.mant[0] |= 1;
                }
            }
        }

        // Scale answer by power of internal exponent.
        mulnum(ref sum, powofnRadix, radix);

        destroynum(ref powofnRadix);
        sum.sign = a.sign;
        return sum;
    }

    //-----------------------------------------------------------------------------
    //
    //    FUNCTION: numtonRadixx
    //
    //    ARGUMENTS: pointer to a number, radix of that number.
    //
    //    RETURN: number representation in internal radix.
    //
    //    DESCRIPTION: Does a radix conversion on a number from
    //    specified radix to requested radix.  Assumes the radix
    //    specified is the radix of the number passed in.
    //
    //-----------------------------------------------------------------------------
    public PNUMBER numtonRadixx(PNUMBER a, uint32_t radix)
    {
        PNUMBER pnumret = i32tonum(0, BASEX); // pnumret is the number in internal form.
        PNUMBER num_radix = i32tonum((int32_t)radix, BASEX);

        // Digits are in reverse order, back over them LSD first.
        int mantIndex = a.cdigit - 1;

        PNUMBER thisdigit = null; // thisdigit holds the current digit of a
        for (int32_t idigit = 0; idigit < a.cdigit; idigit++)
        {
            mulnumx(ref pnumret, num_radix);
            // WARNING:
            // This should just smack in each digit into a 'special' thisdigit.
            // and not do the overhead of recreating the number type each time.
            thisdigit = i32tonum((int32_t)a.mant[mantIndex--], BASEX);
            addnum(ref pnumret, thisdigit, BASEX);
            destroynum(ref thisdigit);
        }

        // Calculate the exponent of the external base for scaling.
        numpowi32x(ref num_radix, a.exp);

        // ... and scale the result.
        mulnumx(ref pnumret, num_radix);

        destroynum(ref num_radix);

        // And propagate the sign.
        pnumret.sign = a.sign;

        return pnumret;
    }

    //-----------------------------------------------------------------------------
    //
    //  FUNCTION: StringToRat
    //
    //  ARGUMENTS:
    //              mantissaIsNegative true if mantissa is less than zero
    //              mantissa a string representation of a number
    //              exponentIsNegative  true if exponent is less than zero
    //              exponent a string representation of a number
    //              radix is the number base used in the source string
    //
    //  RETURN: PRAT representation of string input.
    //          Or null if no number scanned.
    //
    //  EXPLANATION: This is for calc.
    //
    //
    //-----------------------------------------------------------------------------
    public PRAT StringToRat(bool mantissaIsNegative, wstring_view mantissa, bool exponentIsNegative,
        wstring_view exponent,
        uint32_t radix, int32_t precision)
    {
        PRAT resultRat = null; // holds exponent in rational form.

        // Deal with mantissa
        if (string.IsNullOrEmpty(mantissa))
        {
            // Preset value if no mantissa
            if (string.IsNullOrEmpty(exponent))
            {
                // Exponent not specified, preset value to zero
                duprat(ref resultRat, rat_zero);
            }
            else
            {
                // Exponent specified, preset value to one
                duprat(ref resultRat, rat_one);
            }
        }
        else
        {
            // Mantissa specified, convert to number form.
            PNUMBER pnummant = StringToNumber(mantissa, radix, precision);
            if (pnummant == null)
            {
                return null;
            }

            resultRat = numtorat(pnummant, radix);
            // convert to rational form, and cleanup.
            destroynum(ref pnummant);
        }

        // Deal with exponent
        int32_t expt = 0;
        if (!string.IsNullOrEmpty(exponent))
        {
            // Exponent specified, convert to number form.
            // Don't use native stuff, as it is restricted in the bases it can
            // handle.
            PNUMBER numExp = StringToNumber(exponent, radix, precision);
            if (numExp == null)
            {
                return null;
            }

            // Convert exponent number form to native integral form,  and cleanup.
            expt = numtoi32(numExp, radix);
            destroynum(ref numExp);
        }

        // Convert native integral exponent form to rational multiplier form.
        PNUMBER pnumexp = i32tonum((int32_t)radix, BASEX);
        numpowi32x(ref pnumexp, Math.Abs(expt));

        PRAT pratexp = null;
        createrat(ref pratexp);
        dupnum(ref pratexp.pp, pnumexp);
        pratexp.pq = i32tonum(1, BASEX);
        destroynum(ref pnumexp);

        if (exponentIsNegative)
        {
            // multiplier is less than 1, this means divide.
            divrat(ref resultRat, pratexp, precision);
        }
        else if (expt > 0)
        {
            // multiplier is greater than 1, this means multiply.
            mulrat(ref resultRat, pratexp, precision);
        }
        // multiplier can be 1, in which case it'd be a waste of time to multiply.

        destroyrat(ref pratexp);

        if (mantissaIsNegative)
        {
            // A negative number was used, adjust the sign.
            resultRat.pp.sign *= -1;
        }

        return resultRat;
    }

    //-----------------------------------------------------------------------------
    //
    //  FUNCTION: StringToNumber
    //
    //  ARGUMENTS:
    //              wstring_view numberString
    //              int radix
    //              int32_t precision
    //
    //  RETURN: pnumber representation of string input.
    //          Or null if no number scanned.
    //
    //  EXPLANATION: This is a state machine,
    //
    //    State      Description            Example, ^shows just read position.
    //                                                which caused the transition
    //
    //    START      Start state            ^1.0
    //    MANTS      Mantissa sign          -^1.0
    //    LZ         Leading Zero           0^1.0
    //    LZDP       Post LZ dec. pt.       000.^1
    //    LD         Leading digit          1^.0
    //    DZ         Post LZDP Zero         000.0^1
    //    DD         Post Decimal digit     .01^2
    //    DDP        Leading Digit dec. pt. 1.^2
    //    EXPB       Exponent Begins        1.0e^2
    //    EXPS       Exponent sign          1.0e+^5
    //    EXPD       Exponent digit         1.0e1^2 or  even 1.0e0^1
    //    EXPBZ      Exponent begin post 0  0.000e^+1
    //    EXPSZ      Exponent sign post 0   0.000e+^1
    //    EXPDZ      Exponent digit post 0  0.000e+1^2
    //    ERR        Error case             0.0.^
    //
    //    Terminal   Description
    //
    //    DP         '.'
    //    ZR         '0'
    //    NZ         '1'..'9' 'A'..'Z' 'a'..'z' '@' '_'
    //    SG         '+' '-'
    //    EX         'e' '^' e is used for radix 10, ^ for all other radixes.
    //
    //-----------------------------------------------------------------------------
    const uint8_t DP = 0;
    const uint8_t ZR = 1;
    const uint8_t NZ = 2;
    const uint8_t SG = 3;
    const uint8_t EX = 4;

    const uint8_t START = 0;
    const uint8_t MANTS = 1;
    const uint8_t LZ = 2;
    const uint8_t LZDP = 3;
    const uint8_t LD = 4;
    const uint8_t DZ = 5;
    const uint8_t DD = 6;
    const uint8_t DDP = 7;
    const uint8_t EXPB = 8;
    const uint8_t EXPS = 9;
    const uint8_t EXPD = 10;
    const uint8_t EXPBZ = 11;
    const uint8_t EXPSZ = 12;
    const uint8_t EXPDZ = 13;
    const uint8_t ERR = 14;

#if DEBUG
    string[] statestr =
    [
        "START", "MANTS", "LZ", "LZDP", "LD", "DZ", "DD", "DDP", "EXPB", "EXPS", "EXPD", "EXPBZ", "EXPSZ", "EXPDZ",
        "ERR",
    ];
#endif

    // New state is machine[state][terminal]

    static readonly uint8_t[,] machine = new uint8_t[ERR + 1, EX + 1]
    {
        //        DP,     ZR,      NZ,      SG,     EX
        // START
        { LZDP, LZ, LD, MANTS, ERR },
        // MANTS
        { LZDP, LZ, LD, ERR, ERR },
        // LZ
        { LZDP, LZ, LD, ERR, EXPBZ },
        // LZDP
        { ERR, DZ, DD, ERR, EXPB },
        // LD
        { DDP, LD, LD, ERR, EXPB },
        // DZ
        { ERR, DZ, DD, ERR, EXPBZ },
        // DD
        { ERR, DD, DD, ERR, EXPB },
        // DDP
        { ERR, DD, DD, ERR, EXPB },
        // EXPB
        { ERR, EXPD, EXPD, EXPS, ERR },
        // EXPS
        { ERR, EXPD, EXPD, ERR, ERR },
        // EXPD
        { ERR, EXPD, EXPD, ERR, ERR },
        // EXPBZ
        { ERR, EXPDZ, EXPDZ, EXPSZ, ERR },
        // EXPSZ
        { ERR, EXPDZ, EXPDZ, ERR, ERR },
        // EXPDZ
        { ERR, EXPDZ, EXPDZ, ERR, ERR },
        // ERR
        { ERR, ERR, ERR, ERR, ERR }
    };

    wchar_t NormalizeCharDigit(wchar_t c, uint32_t radix)
    {
        // Allow upper and lower case letters as equivalent, base
        // is in the range where this is not ambiguous.
        if (radix >= DIGITS.IndexOf('A') && radix <= DIGITS.IndexOf('Z'))
        {
            // Convert to uppercase if it's a letter
            return wchar_t.ToUpperInvariant(c);
        }

        return c;
    }

    public PNUMBER StringToNumber(wstring_view numberString, uint32_t radix, int32_t precision)
    {
        int32_t expSign = 1; // expSign is exponent sign ( +/- 1 )
        int32_t expValue = 0; // expValue is exponent mantissa, should be unsigned

        PNUMBER pnumret = null;
        createnum(ref pnumret, (uint32_t)(numberString.Length));
        pnumret.sign = 1;
        pnumret.cdigit = 0;
        pnumret.exp = 0;
        MANTTYPE[] pmant = pnumret.mant;
        var pmantCnt = numberString.Length - 1;

        uint8_t state = START; // state is the state of the input state machine.
        foreach (var c in numberString)
        {
            // If the character is the decimal separator, use '.' for the purposes of the state machine.
            wchar_t curChar = (c == g_decimalSeparator ? '.' : c);

            // Switch states based on the character we encountered
            switch (curChar)
            {
                case '-':
                case '+':
                    state = machine[state, SG];
                    break;
                case '.':
                    state = machine[state, DP];
                    break;
                case '0':
                    state = machine[state, ZR];
                    break;
                case '^':
                case 'e':
                    if (curChar == '^' || radix == 10)
                    {
                        state = machine[state, EX];
                        break;
                    }

                    // Drop through in the 'e'-as-a-digit case
                    // [[fallthrough]];
                    goto default;
                default:
                    state = machine[state, NZ];
                    break;
            }

            // Now update our result value based on the state we are in
            switch (state)
            {
                case MANTS:
                    pnumret.sign = (curChar == '-') ? -1 : 1;
                    break;
                case EXPSZ:
                case EXPS:
                    expSign = (curChar == '-') ? -1 : 1;
                    break;
                case EXPDZ:
                case EXPD:
                {
                    curChar = NormalizeCharDigit(curChar, radix);

                    var pos = DIGITS.IndexOf(curChar);
                    if (pos != -1) //wstring_view.npos)
                    {
                        expValue *= (int32_t)radix;
                        expValue += (int32_t)(pos);
                    }
                    else
                    {
                        state = ERR;
                    }
                }
                    break;
                case LD:
                    pnumret.exp++;
                    goto case DD;
                case DD:
                {
                    curChar = NormalizeCharDigit(curChar, radix);

                    var pos = DIGITS.IndexOf(curChar);
                    if (pos != -1 /*wstring_view.npos*/ && pos < /*static_cast<size_t>*/(radix))
                    {
                        pmant[pmantCnt--] = (MANTTYPE)(pos);
                        pnumret.exp--;
                        pnumret.cdigit++;
                    }
                    else
                    {
                        state = ERR;
                    }
                }
                    break;
                case DZ:
                    pnumret.exp--;
                    break;
                case LZ:
                case LZDP:
                case DDP:
                    break;
            }
        }

        if (state == DZ || state == EXPDZ)
        {
            pnumret.cdigit = 1;
            pnumret.exp = 0;
            pnumret.sign = 1;
        }
        else
        {
            while (pnumret.cdigit < (int32_t)(numberString.Length))
            {
                pnumret.cdigit++;
                pnumret.exp--;
            }

            pnumret.exp += expSign * expValue;
        }

        // If we don't have a number, clear our result.
        if (pnumret.cdigit == 0)
        {
            destroynum(ref pnumret);
            pnumret = null;
        }
        else
        {
            stripzeroesnum(ref pnumret, precision);
        }

        return pnumret;
    }

    //-----------------------------------------------------------------------------
    //
    //    FUNCTION: i32torat
    //
    //    ARGUMENTS: int32_t
    //
    //    RETURN: Rational representation of int32_t input.
    //
    //    DESCRIPTION: Converts int32_t input to rational (p over q)
    //    form, where q is 1 and p is the int32_t.
    //
    //-----------------------------------------------------------------------------
    public PRAT i32torat(int32_t ini32)
    {
        PRAT pratret = null;
        createrat(ref pratret);
        pratret.pp = i32tonum(ini32, BASEX);
        pratret.pq = i32tonum(1, BASEX);
        return (pratret);
    }

    //-----------------------------------------------------------------------------
    //
    //    FUNCTION: Ui32torat
    //
    //    ARGUMENTS: ui32
    //
    //    RETURN: Rational representation of uint32_t input.
    //
    //    DESCRIPTION: Converts uint32_t input to rational (p over q)
    //    form, where q is 1 and p is the uint32_t. Being unsigned cant take negative
    //    numbers, but the full range of unsigned numbers
    //
    //-----------------------------------------------------------------------------
    public PRAT Ui32torat(uint32_t inui32)
    {
        PRAT pratret = null;
        createrat(ref pratret);
        pratret.pp = Ui32tonum(inui32, BASEX);
        pratret.pq = i32tonum(1, BASEX);
        return (pratret);
    }

    //-----------------------------------------------------------------------------
    //
    //    FUNCTION: i32tonum
    //
    //    ARGUMENTS: int32_t input and radix requested.
    //
    //    RETURN: number
    //
    //    DESCRIPTION: Returns a number representation in the
    //    base   requested of the int32_t value passed in.
    //
    //-----------------------------------------------------------------------------
    public PNUMBER i32tonum(int32_t ini32, uint32_t radix)
    {
        MANTTYPE[] pmant;
        uint32_t pmantCnt = 0;
        PNUMBER pnumret = null;
        createnum(ref pnumret, MAX_LONG_SIZE);
        pmant = pnumret.mant;
        pnumret.cdigit = 0;
        pnumret.exp = 0;
        if (ini32 < 0)
        {
            pnumret.sign = -1;
//        ini32 *= -1;  // PORTFIX1 ISSUE: This will fail for INT_MIN
        }
        else
        {
            pnumret.sign = 1;
        }

        // PORTFIX1: Get absolute value safely using long to avoid overflow.
        // Gotta watch out for differences in overflow handling in C# and C++.
        // C# just wraps around to max while C++ discards the overflown bits.
        uint32_t value = (uint32_t)(ini32 < 0 ? -((long)ini32) : ini32);
        do
        {
            pmant[pmantCnt++] = (value % radix);
            value /= radix;
            pnumret.cdigit++;
        } while (value != 0);

        // Resize the mantissa array according to the digits.
        Array.Resize(ref pnumret.mant, pnumret.cdigit);

        return (pnumret);
    }

    //-----------------------------------------------------------------------------
    //
    //    FUNCTION: Ui32tonum
    //
    //    ARGUMENTS: uint32_t input and radix requested.
    //
    //    RETURN: number
    //
    //    DESCRIPTION: Returns a number representation in the
    //    base   requested of the uint32_t value passed in. Being unsigned number it has no
    //    negative number and takes the full range of unsigned number
    //
    //-----------------------------------------------------------------------------
    public PNUMBER Ui32tonum(uint32_t ini32, uint32_t radix)
    {
        MANTTYPE[] pmant;
        var pmantCnt = 0;
        PNUMBER pnumret = null;
        createnum(ref pnumret, MAX_LONG_SIZE);
        pmant = pnumret.mant;
        pnumret.cdigit = 0;
        pnumret.exp = 0;
        pnumret.sign = 1;
        do
        {
            pmant[pmantCnt++] = (MANTTYPE)(ini32 % radix);
            ini32 /= radix;
            pnumret.cdigit++;
        } while (ini32 != 0);

        return (pnumret);
    }

    //-----------------------------------------------------------------------------
    //
    //    FUNCTION: rattoi32
    //
    //    ARGUMENTS: rational number in internal base, integer radix and int32_t precision.
    //
    //    RETURN: int32_t
    //
    //    DESCRIPTION: returns the int32_t representation of the
    //    number input.  Assumes that the number is in the internal
    //    base.
    //
    //-----------------------------------------------------------------------------

    public int32_t rattoi32(PRAT prat, uint32_t radix, int32_t precision)
    {
        if (rat_gt(prat, rat_max_i32, precision) || rat_lt(prat, rat_min_i32, precision))
        {
            // Don't attempt rattoi32 of anything too big or small
            throw new CalcErrException(CalcErr.CALC_E_DOMAIN);
        }

        PRAT pint = null;
        duprat(ref pint, prat);

        intrat(ref pint, radix, precision);
        divnumx(ref (pint.pp), pint.pq, precision);
        dupnum(ref pint.pq, num_one);

        int32_t lret = numtoi32(pint.pp, BASEX);

        destroyrat(ref pint);

        return (lret);
    }

    //-----------------------------------------------------------------------------
    //
    //    FUNCTION: rattoUi32
    //
    //    ARGUMENTS: rational number in internal base, integer radix and int32_t precision.
    //
    //    RETURN: Ui32
    //
    //    DESCRIPTION: returns the Ui32 representation of the
    //    number input.  Assumes that the number is in the internal
    //    base.
    //
    //-----------------------------------------------------------------------------
    public uint32_t rattoUi32(PRAT prat, uint32_t radix, int32_t precision)
    {
        if (rat_gt(prat, rat_dword, precision) || rat_lt(prat, rat_zero, precision))
        {
            // Don't attempt rattoui32 of anything too big or small
            throw new CalcErrException(CalcErr.CALC_E_DOMAIN);
        }

        PRAT pint = null;
        duprat(ref pint, prat);

        intrat(ref pint, radix, precision);
        divnumx(ref (pint.pp), pint.pq, precision);
        dupnum(ref pint.pq, num_one);

        // uint32_t lret = (uint32_t)numtoi32(pint.pp, BASEX); // This happens to work even if it is only signed
        int32_t signedResult = numtoi32(pint.pp, BASEX);
        uint32_t lret = (uint32_t)signedResult;

        destroyrat(ref pint);

        return (lret);
    }

    //-----------------------------------------------------------------------------
    //
    //    FUNCTION: rattoUi64
    //
    //    ARGUMENTS: rational number in internal base, integer radix and int32_t precision
    //
    //    RETURN: Ui64
    //
    //    DESCRIPTION: returns the 64 bit (irrespective of which processor this is running in) representation of the
    //    number input.  Assumes that the number is in the internal
    //    base. Can throw exception if the number exceeds 2^64
    //    Implementation by getting the HI & LO 32 bit words and concatenating them, as the
    //    internal base chosen happens to be 2^32, this is easier.
    //-----------------------------------------------------------------------------
    public uint64_t rattoUi64(PRAT prat, uint32_t radix, int32_t precision)
    {
        PRAT pint = null;

        // first get the LO 32 bit word
        duprat(ref pint, prat);
        andrat(ref pint, rat_dword, radix, precision); // & 0xFFFFFFFF   (2 ^ 32 -1)
        uint32_t lo = rattoUi32(pint, radix, precision); // wont throw exception because already hi-dword chopped off

        duprat(ref pint, prat); // previous pint will get freed by this as well
        PRAT prat32 = i32torat(32);
        rshrat(ref pint, prat32, radix, precision);
        intrat(ref pint, radix, precision);
        andrat(ref pint, rat_dword, radix, precision); // & 0xFFFFFFFF   (2 ^ 32 -1)
        uint32_t hi = rattoUi32(pint, radix, precision);

        destroyrat(ref prat32);
        destroyrat(ref pint);

        return (((uint64_t)hi << 32) | lo);
    }

    //-----------------------------------------------------------------------------
    //
    //    FUNCTION: numtoi32
    //
    //    ARGUMENTS: number input and base of that number.
    //
    //    RETURN: int32_t
    //
    //    DESCRIPTION: returns the int32_t representation of the
    //    number input.  Assumes that the number is really in the
    //    base   claimed.
    //
    //-----------------------------------------------------------------------------
    public int32_t numtoi32(PNUMBER pnum, uint32_t radix)
    {
        long lret = 0;

        MANTTYPE[] pmant = pnum.mant;
        var pmantCnt = pnum.cdigit - 1;

        int32_t expt = pnum.exp;
        for (int32_t length = pnum.cdigit; length > 0 && length + expt > 0; length--)
        {
            lret = (int32_t)(lret * radix);
            lret = (int32_t)(lret + (pmant[pmantCnt--]));
        }

        while (expt-- > 0)
        {
            lret = (int32_t)(lret * radix);
        }

        lret *= pnum.sign;

        // if (lret >= Int32.MaxValue || lret <= Int32.MinValue)
        //     throw new CalcErrException(CalcErr.CALC_E_DOMAIN);

        return (int32_t)lret;
    }

    //-----------------------------------------------------------------------------
    //
    //    FUNCTION: bool stripzeroesnum
    //
    //    ARGUMENTS:            a number representation
    //
    //    RETURN: true if stripping done, modifies number in place.
    //
    //    DESCRIPTION: Strips off trailing zeros.
    //
    //-----------------------------------------------------------------------------
    public bool stripzeroesnum(ref PNUMBER pnum, int32_t starting)
    {
        bool fstrip = false;
        // point pmant to the LeastCalculatedDigit
        MANTTYPE[] pmant = pnum.mant;
        var pmantCnt = 0;

        int32_t cdigits = pnum.cdigit;
        // point pmant to the LSD
        if (cdigits > starting)
        {
            pmantCnt += cdigits - starting;
            cdigits = starting;
        }

        // Check we haven't gone too far, and we are still looking at zeros.
        while ((cdigits > 0) && pmant[pmantCnt] == 0)
        {
            // move to next significant digit and keep track of digits we can
            // ignore later.
            pmantCnt++;
            cdigits--;
            fstrip = true;
        }

        // If there are zeros to remove.
        if (fstrip)
        {
            // Remove them.
            Array.Copy(pnum.mant, pmantCnt, pnum.mant, 0, cdigits);

            // And adjust exponent and digit count accordingly.
            pnum.exp += (pnum.cdigit - cdigits);
            pnum.cdigit = cdigits;
        }

        return (fstrip);
    }

    //-----------------------------------------------------------------------------
    //
    //    FUNCTION: NumberToString
    //
    //    ARGUMENTS: number representation
    //          fmt, one of NumberFormat.Float, NumberFormat.Scientific or
    //          NumberFormat.Engineering
    //          integer radix and int32_t precision value
    //
    //    RETURN: String representation of number.
    //
    //    DESCRIPTION: Converts a number to its string
    //    representation.
    //
    //-----------------------------------------------------------------------------
    public wstring NumberToString(ref PNUMBER pnum, NumberFormat format, uint32_t radix, int32_t precision)
    {
        stripzeroesnum(ref pnum, precision + 2);
        int32_t length = pnum.cdigit;
        int32_t exponent = pnum.exp + length; // Actual number of digits to the left of decimal

        NumberFormat oldFormat = format;
        if (exponent > precision && format == NumberFormat.Float)
        {
            // Force scientific mode to prevent user from assuming 33rd digit is exact.
            format = NumberFormat.Scientific;
        }

        // Make length small enough to fit in pret.
        if (length > precision)
        {
            length = precision;
        }

        // If there is a chance a round has to occur, round.
        // - if number is zero no rounding
        // - if number of digits is less than the maximum output no rounding
        PNUMBER round = null;
        if (!zernum(pnum) && (pnum.cdigit >= precision ||
                              (length - exponent > precision && exponent >= -MAX_ZEROS_AFTER_DECIMAL)))
        {
            // Otherwise round.
            round = i32tonum((int32_t)radix, radix);
            divnum(ref round, num_two, radix, precision);

            // Make round number exponent one below the LSD for the number.
            if (exponent > 0 || format == NumberFormat.Float)
            {
                round.exp = pnum.exp + pnum.cdigit - round.cdigit - precision;
            }
            else
            {
                round.exp = pnum.exp + pnum.cdigit - round.cdigit - precision - exponent;
                length = precision + exponent;
            }

            round.sign = pnum.sign;
        }

        if (format == NumberFormat.Float)
        {
            // Figure out if the exponent will fill more space than the non-exponent field.
            if ((length - exponent > precision) || (exponent > precision + 3))
            {
                if (exponent >= -MAX_ZEROS_AFTER_DECIMAL)
                {
                    round.exp -= exponent;
                    length = precision + exponent;
                }
                else
                {
                    // Case where too many zeros are to the right or left of the
                    // decimal pt. And we are forced to switch to scientific form.
                    format = NumberFormat.Scientific;
                }
            }
            else if (length + Math.Abs(exponent) < precision && round != null)
            {
                // Minimum loss of precision occurs with listing leading zeros
                // if we need to make room for zeros sacrifice some digits.
                round.exp -= exponent;
            }
        }

        if (round != null)
        {
            addnum(ref pnum, round, radix);
            int32_t offset = (pnum.cdigit + pnum.exp) - (round.cdigit + round.exp);
            destroynum(ref round);
            if (stripzeroesnum(ref pnum, offset))
            {
                // WARNING: nesting/recursion, too much has been changed, need to
                // re-figure format.
                return NumberToString(ref pnum, oldFormat, radix, precision);
            }
        }
        else
        {
            stripzeroesnum(ref pnum, precision);
        }

        // Set up all the post rounding stuff.
        bool useSciForm = false;
        int32_t eout = exponent - 1; // Displayed exponent.
        MANTTYPE[] pmant = pnum.mant;
        var pmantCnt = pnum.cdigit - 1;

        // Case where too many digits are to the left of the decimal or
        // NumberFormat.Scientific or NumberFormat.Engineering was specified.
        if ((format == NumberFormat.Scientific) || (format == NumberFormat.Engineering))
        {
            useSciForm = true;
            if (eout != 0)
            {
                if (format == NumberFormat.Engineering)
                {
                    exponent = (eout % 3);
                    eout -= exponent;
                    exponent++;

                    // Fix the case where 0.02e-3 should really be 2.e-6 etc.
                    if (exponent < 0)
                    {
                        exponent += 3;
                        eout -= 3;
                    }
                }
                else
                {
                    exponent = 1;
                }
            }
        }
        else
        {
            eout = 0;
        }

        // Begin building the result string
        StringBuilder result = new();

        // Make sure negative zeros aren't allowed.
        if ((pnum.sign == -1) && (length > 0))
        {
            result.Append("-");
        }

        if (exponent <= 0 && !useSciForm)
        {
            result.Append('0');
            result.Append(g_decimalSeparator);
            // Used up a digit unaccounted for.
        }

        while (exponent < 0)
        {
            result.Append('0');
            exponent++;
        }

        while (length > 0)
        {
            exponent--;
            result.Append(DIGITS[(int)pmant[pmantCnt--]]);
            length--;

            // Be more regular in using a decimal point.
            if (exponent == 0)
            {
                result.Append(g_decimalSeparator);
            }
        }

        while (exponent > 0)
        {
            result.Append('0');
            exponent--;
            // Be more regular in using a decimal point.
            if (exponent == 0)
            {
                result.Append(g_decimalSeparator);
            }
        }

        if (useSciForm)
        {
            result.Append(radix == 10 ? 'e' : '^');
            result.Append(eout < 0 ? '-' : '+');
            eout = Math.Abs(eout);
            StringBuilder expString = new();
            do
            {
                // This is to emulate the reversed insert that expString.crbegin() does.
                // reference: https://en.cppreference.com/w/cpp/string/basic_string/rbegin
                expString.Insert(0, DIGITS[(int)(eout % radix)]);
                eout = (int32_t)(eout / radix);
            } while (eout > 0);

            result.Append(expString);
            // This got emulated on the above do-while.
            //result.insert(result.end(), expString.crbegin(), expString.crend());
        }

        // Remove trailing decimal
        if (result.Length != 0 && result[result.Length - 1] == g_decimalSeparator)
        {
            result.Remove(result.Length - 1, 1);
        }

        return result.ToString();
    }

    //-----------------------------------------------------------------------------
    //
    //  FUNCTION: RatToString
    //
    //  ARGUMENTS:
    //              PRAT *representation of a number.
    //              i32 representation of base  to  dump to screen.
    //              fmt, one of NumberFormat.Float, NumberFormat.Scientific, or NumberFormat.Engineering
    //              precision uint32_t
    //
    //  RETURN: string
    //
    //  DESCRIPTION: returns a string representation of rational number passed
    //  in, at least to the precision digits.
    //
    //  NOTE: It may be that doing a GCD() could shorten the rational form
    //       And it may eventually be worthwhile to keep the result.  That is
    //       why a pointer to the rational is passed in.
    //
    //-----------------------------------------------------------------------------
    public wstring RatToString(ref PRAT prat, NumberFormat format, uint32_t radix, int32_t precision)
    {
        PNUMBER p = RatToNumber(prat, radix, precision);

        wstring result = NumberToString(ref p, format, radix, precision);
        destroynum(ref p);

        return result;
    }

    public PNUMBER RatToNumber(PRAT prat, uint32_t radix, int32_t precision)
    {
        PRAT temprat = null;
        duprat(ref temprat, prat);
        // Convert p and q of rational form from internal base to requested base.
        // Scale by largest power of BASEX possible.
        int32_t scaleby = Math.Min(temprat.pp.exp, temprat.pq.exp);
        scaleby = Math.Max(scaleby, 0);

        temprat.pp.exp -= scaleby;
        temprat.pq.exp -= scaleby;

        PNUMBER p = nRadixxtonum(temprat.pp, radix, precision);
        PNUMBER q = nRadixxtonum(temprat.pq, radix, precision);

        destroyrat(ref temprat);

        // finally take the time hit to actually divide.
        divnum(ref p, q, radix, precision);
        destroynum(ref q);

        return p;
    }

    // Converts a PRAT to a PNUMBER and back to a PRAT, flattening/simplifying the rational in the process
    public void flatrat(ref PRAT prat, uint32_t radix, int32_t precision)
    {
        PNUMBER pnum = RatToNumber(prat, radix, precision);
        destroyrat(ref prat);
        prat = numtorat(pnum, radix);
        destroynum(ref pnum);
    }

    //-----------------------------------------------------------------------------
    //
    //  FUNCTION: gcd
    //
    //  ARGUMENTS:
    //              PNUMBER representation of a number.
    //              PNUMBER representation of a number.
    //              int for Radix
    //
    //  RETURN: Greatest common divisor in internal BASEX PNUMBER form.
    //
    //  DESCRIPTION: gcd uses remainders to find the greatest common divisor.
    //
    //  ASSUMPTIONS: gcd assumes inputs are integers.
    //
    //  NOTE: Before it was found that the TRIM macro actually kept the
    //        size down cheaper than GCD, this routine was used extensively.
    //        now it is not used but might be later.
    //
    //-----------------------------------------------------------------------------

    public PNUMBER gcd(PNUMBER a, PNUMBER b)
    {
        PNUMBER r = null;
        PNUMBER larger = null;
        PNUMBER smaller = null;

        if (zernum(a))
        {
            return b;
        }
        else if (zernum(b))
        {
            return a;
        }

        if (lessnum(a, b))
        {
            dupnum(ref larger, b);
            dupnum(ref smaller, a);
        }
        else
        {
            dupnum(ref larger, a);
            dupnum(ref smaller, b);
        }

        while (!zernum(smaller))
        {
            remnum(ref larger, smaller, BASEX);
            // swap larger and smaller
            r = larger;
            larger = smaller;
            smaller = r;
        }

        destroynum(ref smaller);
        return larger;
    }

    //-----------------------------------------------------------------------------
    //
    //  FUNCTION: i32factnum
    //
    //  ARGUMENTS:
    //              int32_t integer to factorialize.
    //              int32_t integer representing base   of answer.
    //              uint32_t integer for radix
    //
    //  RETURN: Factorial of input in radix PNUMBER form.
    //
    //  NOTE:  Not currently used.
    //
    //-----------------------------------------------------------------------------
    public PNUMBER i32factnum(int32_t ini32, uint32_t radix)
    {
        PNUMBER? lret = null;
        PNUMBER? tmp = null;

        lret = i32tonum(1, radix);

        while (ini32 > 0)
        {
            tmp = i32tonum(ini32--, radix);
            mulnum(ref lret, tmp, radix);
            destroynum(ref tmp);
        }

        return (lret);
    }

    //-----------------------------------------------------------------------------
    //
    //  FUNCTION: i32prodnum
    //
    //  ARGUMENTS:
    //              int32_t integer to factorialize.
    //              int32_t integer representing base of answer.
    //              uint32_t integer for radix
    //
    //  RETURN: Factorial of input in base PNUMBER form.
    //
    //-----------------------------------------------------------------------------
    public PNUMBER i32prodnum(int32_t start, int32_t stop, uint32_t radix)
    {
        PNUMBER lret = null;
        PNUMBER tmp = null;

        lret = i32tonum(1, radix);

        while (start <= stop)
        {
            if (start != 0)
            {
                tmp = i32tonum(start, radix);
                mulnum(ref lret, tmp, radix);
                destroynum(ref tmp);
            }

            start++;
        }

        return (lret);
    }

    //-----------------------------------------------------------------------------
    //
    //    FUNCTION: numpowi32
    //
    //    ARGUMENTS: root as number power as int32_t and radix of
    //               number along with the precision value in int32_t.
    //
    //    RETURN: None root is changed.
    //
    //    DESCRIPTION: changes numeric representation of root to
    //    root ** power. Assumes radix is the radix of root.
    //
    //-----------------------------------------------------------------------------
    public void numpowi32(ref PNUMBER proot, int32_t power, uint32_t radix, int32_t precision)
    {
        PNUMBER lret = i32tonum(1, radix);

        while (power > 0)
        {
            if ((power & 1) != 0)
            {
                mulnum(ref lret, proot, radix);
            }

            mulnum(ref proot, proot, radix);
            TRIMNUM(proot, precision);
            power >>= 1;
        }

        destroynum(ref proot);
        proot = lret;
    }

    //-----------------------------------------------------------------------------
    //
    //    FUNCTION: ratpowi32
    //
    //    ARGUMENTS: root as rational, power as int32_t and precision as int32_t.
    //
    //    RETURN: None root is changed.
    //
    //    DESCRIPTION: changes rational representation of root to
    //    root ** power.
    //
    //-----------------------------------------------------------------------------
    public void ratpowi32(ref PRAT proot, int32_t power, int32_t precision)
    {
        if (power < 0)
        {
            // Take the positive power and invert answer.
            PNUMBER pnumtemp = null;
            ratpowi32(ref proot, -power, precision);
            pnumtemp = (proot).pp;
            (proot).pp = (proot).pq;
            (proot).pq = pnumtemp;
        }
        else
        {
            PRAT lret = null;

            lret = i32torat(1);

            while (power > 0)
            {
                if ((power & 1) != 0)
                {
                    mulnumx(ref (lret.pp), (proot).pp);
                    mulnumx(ref (lret.pq), (proot).pq);
                }

                mulrat(ref proot, proot, precision);
                trimit(ref lret, precision);
                trimit(ref proot, precision);
                power >>= 1;
            }

            destroyrat(ref proot);
            proot = lret;
        }
    }
}
