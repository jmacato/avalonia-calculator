// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

//-----------------------------------------------------------------------------
//  Package Title  ratpak
//  File           ratpak.h
//  Copyright      (C) 1995-99 Microsoft
//  Date           01-16-95
//
//
//  Description
//
//     Infinite precision math package header file, if you use ratpak.lib you
//  need to include this header.
//
//-----------------------------------------------------------------------------

using uint32_t = System.UInt32;
using int32_t = System.Int32;
using MANTTYPE = System.UInt32;
using PNUMBER = CalcEngine.RatPak.NUMBER;
using PRAT = CalcEngine.RatPak.RAT;

namespace CalcEngine;

public partial class RatPak
{
    public RatPak(int precision = CALC_DECIMAL_DIGITS_DEFAULT, uint radix = 10)
    {
        ChangeConstants(10, precision);
    }

    const uint32_t BASEXPWR = 31; // Internal log2(BASEX)

    const uint32_t BASEX = 0x80000000; // Internal radix used in calculations, hope to raise
    // this to 2^32 after solving scaling problems with
    // overflow detection esp. in mul


    public enum NumberFormat
    {
        Float, // returns floating point, or exponential if number is too big
        Scientific, // always returns scientific notation
        Engineering // always returns engineering notation such that exponent is a multiple of 3
    };

    public enum AngleType
    {
        Degrees, // Calculate trig using 360 degrees per revolution
        Radians, // Calculate trig using 2 pi radians per revolution
        Gradians // Calculate trig using 400 gradians per revolution
    };

    //-----------------------------------------------------------------------------
    //
    //  NUMBER type is a representation of a generic sized generic radix number
    //
    //-----------------------------------------------------------------------------
    public class NUMBER
    {
        public int32_t sign; // The sign of the mantissa, +1, or -1

        // The number of digits, or what passes for digits in the
        // radix being used.
        public int32_t cdigit;

        public int32_t exp; // The offset of digits from the radix point

        // (decimal point in radix 10)
        // This is actually allocated as a continuation of the
        // NUMBER structure.
        public MANTTYPE[] mant = new MANTTYPE[8]; /*[8]*/

        internal const int32_t ConstSizeOf = 44;
    }


    //-----------------------------------------------------------------------------
    //
    //  RAT type is a representation radix  on 2 NUMBER types.
    //  pp/pq, where pp and pq are pointers to integral NUMBER types.
    //
    //-----------------------------------------------------------------------------
    public class RAT
    {
        public PNUMBER? pp;
        public PNUMBER? pq;
    }

    const uint32_t MAX_LONG_SIZE = 33; // Base 2 requires 32 'digits'


    //-----------------------------------------------------------------------------
    //
    // List of useful constants for evaluation, note this list needs to be
    // initialized.
    //
    //-----------------------------------------------------------------------------
    public PNUMBER num_one;
    public PNUMBER num_two;
    public PNUMBER num_five;
    public PNUMBER num_six;
    public PNUMBER num_ten;

    public PRAT ln_ten;
    public PRAT ln_two;
    public PRAT rat_zero;
    public PRAT rat_neg_one;
    public PRAT rat_one;
    public PRAT rat_two;
    public PRAT rat_six;
    public PRAT rat_half;
    public PRAT rat_ten;
    public PRAT pt_eight_five;
    public PRAT pi;
    public PRAT pi_over_two;
    public PRAT two_pi;
    public PRAT one_pt_five_pi;
    public PRAT e_to_one_half;
    public PRAT rat_exp;
    public PRAT rad_to_deg;
    public PRAT rad_to_grad;
    public PRAT rat_qword;
    public PRAT rat_dword;
    public PRAT rat_word;
    public PRAT rat_byte;
    public PRAT rat_360;
    public PRAT rat_400;
    public PRAT rat_180;
    public PRAT rat_200;
    public PRAT rat_nRadix;
    public PRAT rat_smallest;
    public PRAT rat_negsmallest;
    public PRAT rat_max_exp;
    public PRAT rat_min_exp;
    public PRAT rat_max_fact;
    public PRAT rat_min_fact;
    public PRAT rat_max_i32;
    public PRAT rat_min_i32;

    // DUPNUM Duplicates a number taking care of allocation and internals
    public void dupnum(ref NUMBER a, NUMBER b)
    {
        destroynum(ref a);
        createnum(ref a, (uint32_t)b.cdigit);
        _dupnum(ref a, b);
    }

    // DUPRAT Duplicates a rational taking care of allocation and internals
    public void duprat(ref PRAT a, PRAT b)
    {
        destroyrat(ref a);
        createrat(ref a);
        dupnum(ref a.pp, b.pp);
        dupnum(ref a.pq, b.pq);
    }

    // LOG*RADIX calculates the integral portion of the log of a number in
    // the base currently being used, only accurate to within g_ratio
    int LOGNUMRADIX(NUMBER pnum) => (pnum.cdigit + pnum.exp) * g_ratio;
    int LOGRATRADIX(PRAT prat) => LOGNUMRADIX(prat.pp) - LOGNUMRADIX(prat.pq);

    // LOG*2 calculates the integral portion of the log of a number in
    // the internal base being used, only accurate to within g_ratio
    int LOGNUM2(NUMBER pnum) => pnum.cdigit + pnum.exp;
    int LOGRAT2(PRAT prat) => LOGNUM2(prat.pp) - LOGNUM2(prat.pq);

    // SIGN returns the sign of the rational
    int32_t SIGN(PRAT prat) => prat.pp.sign * prat.pq.sign;

    // ReSharper disable once RedundantAssignment
     void createrat(ref PRAT? y) => y = _createrat();
    public RAT createrat() =>  _createrat();

    public void destroyrat(ref PRAT x)
    {
        _destroyrat(ref x);
        x = null!;
    }

    public void createnum(ref NUMBER y, uint32_t size) => y = _createnum(size);

    void destroynum(ref NUMBER x)
    {
        _destroynum(ref x!);
        x = null!;
    }

    //-----------------------------------------------------------------------------
    //
    //   Defines for checking when to stop taylor series expansions due to
    //   precision satisfaction.
    //
    //-----------------------------------------------------------------------------

    // RENORMALIZE, gets the exponents non-negative.
    private void RENORMALIZE(PRAT x)
    {
        if (x.pp.exp < 0)
        {
            x.pq.exp -= x.pp.exp;
            x.pp.exp = 0;
        }

        if (x.pq.exp < 0)
        {
            x.pp.exp -= x.pq.exp;
            x.pq.exp = 0;
        }
    }

    // TRIMNUM ASSUMES the number is in radix form NOT INTERNAL BASEX!!!
    private void TRIMNUM(NUMBER x, int32_t precision)
    {
        if (!g_ftrueinfinite)
        {
            int32_t trim = x.cdigit - precision - g_ratio;
            if (trim > 1)
            {
                // memmove((x).mant, &((x).mant[trim]), sizeof(MANTTYPE) * ((x).cdigit - trim));

                // Replace memmove with Array.Copy to shift elements within the array
                // This moves elements starting at index 'trim' to the beginning of the array
                Array.Copy(x.mant, trim, x.mant, 0, x.cdigit - trim);

                x.cdigit -= trim;
                x.exp += trim;
            }
        }
    }

    // TRIMTOP ASSUMES the number is in INTERNAL BASEX!!!
    private void TRIMTOP(PRAT x, int32_t precision)
    {
        if (!g_ftrueinfinite)
        {
            int32_t trim = x.pp.cdigit - precision / g_ratio - 2;
            if (trim > 1)
            {
                Array.Copy(x.pp.mant, trim, x.pp.mant, 0, x.pp.cdigit - trim);
                x.pp.cdigit -= trim;
                x.pp.exp += trim;
            }

            trim = Math.Min(x.pp.exp, x.pq.exp);
            x.pp.exp -= trim;
            x.pq.exp -= trim;
        }
    }

    private bool SMALL_ENOUGH_RAT(PRAT a, int32_t precision)
    {
        return zernum(a.pp) ||
               (a.pq.cdigit + a.pq.exp - (a.pp.cdigit + a.pp.exp) - 1) * g_ratio > precision;
    }

    //-----------------------------------------------------------------------------
    //
    //   Defines for setting up taylor series expansions for infinite precision
    //   functions.
    //
    //-----------------------------------------------------------------------------
    private void CREATETAYLOR(ref PRAT px, int32_t precision, out PRAT xx, out NUMBER n2, out PRAT pret,
        out PRAT thisterm)
    {
        xx = null;
        n2 = null;
        pret = null;
        thisterm = null;

        duprat(ref xx, px);
        mulrat(ref xx, px, precision);
        createrat(ref pret);
        pret.pp = i32tonum(0, BASEX);
        pret.pq = i32tonum(0, BASEX);
    }

    private void DESTROYTAYLOR(ref PRAT px, ref NUMBER n2, ref PRAT xx, ref PRAT thisterm, PRAT pret, int32_t precision)
    {
        destroynum(ref n2);
        destroyrat(ref xx);
        destroyrat(ref thisterm);
        destroyrat(ref px);
        trimit(ref pret, precision);
        px = pret;
    }

    // INC(a) is the rational equivalent of a++
    // Check to see if we can avoid doing this the hard way.
    private void INC(NUMBER a)
    {
        if (a.mant[0] < BASEX - 1)
        {
            a.mant[0]++;
        }
        else
        {
            addnum(ref a, num_one, BASEX);
        }
    }

    uint32_t MSD(NUMBER x) => x.mant[x.cdigit - 1];

    // MULNUM(b) is the rational equivalent of thisterm *= b where thisterm is
    // a rational and b is a number, NOTE this is a mixed type operation for
    // efficiency reasons.
    private void MULNUM(ref NUMBER b, PRAT thisterm) => mulnumx(ref thisterm.pp, b);

    // DIVNUM(b) is the rational equivalent of thisterm /= b where thisterm is
    // a rational and b is a number, NOTE this is a mixed type operation for
    // efficiency reasons.
    private void DIVNUM(ref NUMBER b, PRAT thisterm) => mulnumx(ref thisterm.pq, b);

    // NEXTTERM(p,d) is the rational equivalent of
    // thisterm *= p
    // d    <d is usually an expansion of operations to get thisterm updated.>
    // pret += thisterm
    // #define NEXTTERM(p, d, precision)
    //     mulrat(&thisterm, p, precision);
    //     d addrat(&pret, thisterm, precision)
    //

    // Macros Suck.

    private void LOG_RAT_NEXTTERM(ref PRAT thisterm, ref PRAT px, NUMBER n2, int precision, ref PRAT pret)
    {
        mulrat(ref thisterm, px, precision);
        MULNUM(ref n2, thisterm);
        INC(n2);
        DIVNUM(ref n2, thisterm);
        addrat(ref pret, thisterm, precision);
    }

    private void EXP_RAT_NEXTTERM(ref PRAT thisterm, ref PRAT px, NUMBER n2, int precision, ref PRAT pret)
    {
        mulrat(ref thisterm, px, precision);
        INC(n2);
        DIVNUM(ref n2, thisterm);
        addrat(ref pret, thisterm, precision);
    }

    //-----------------------------------------------------------------------------
    //
    //   External variables used in the math package.
    //
    //-----------------------------------------------------------------------------

    // don't use unless you know what you are doing
    // used to help decide when to stop calculating.

    public int32_t g_ratio; // Internally calculated ratio of internal radix

    // //-----------------------------------------------------------------------------
    // //
    // //   External functions defined in the math package.
    // //
    // //-----------------------------------------------------------------------------
    //
    // // Call whenever decimal separator character changes.
    // extern void SetDecimalSeparator(wchar_t decimalSeparator);
    //
    // // Call whenever either radix or precision changes, is smarter about recalculating constants.
    // extern void ChangeConstants(uint32_t radix, int32_t precision);
    //
    // extern bool equnum(_In_ PNUMBER a, _In_ PNUMBER b);  // returns true of a == b
    // extern bool lessnum(_In_ PNUMBER a, _In_ PNUMBER b); // returns true of a < b
    // extern bool zernum(_In_ PNUMBER a);                  // returns true of a == 0
    // extern bool zerrat(_In_ PRAT a);                     // returns true if a == 0/q
    // extern std::wstring NumberToString(_Inout_ PNUMBER& pnum, NumberFormat format, uint32_t radix, int32_t precision);
    //
    // // returns a text representation of a PRAT
    // extern std::wstring RatToString(_Inout_ PRAT& prat, NumberFormat format, uint32_t radix, int32_t precision);
    // // converts a PRAT into a PNUMBER
    // extern PNUMBER RatToNumber(_In_ PRAT prat, uint32_t radix, int32_t precision);
    // // flattens a PRAT by converting it to a PNUMBER and back to a PRAT
    // extern void flatrat(_Inout_ PRAT& prat, uint32_t radix, int32_t precision);
    //
    // extern int32_t numtoi32(_In_ PNUMBER pnum, uint32_t radix);
    // extern int32_t rattoi32(_In_ PRAT prat, uint32_t radix, int32_t precision);
    // uint64_t rattoUi64(_In_ PRAT prat, uint32_t radix, int32_t precision);
    // extern PNUMBER _createnum(_In_ uint32_t size); // returns an empty number structure with size digits
    // extern PNUMBER nRadixxtonum(_In_ PNUMBER a, uint32_t radix, int32_t precision);
    // extern PNUMBER gcd(_In_ PNUMBER a, _In_ PNUMBER b);
    // extern PNUMBER StringToNumber(
    //     std::wstring_view numberString,
    //     uint32_t radix,
    //     int32_t precision); // takes a text representation of a number and returns a number.
    //
    // // takes a text representation of a number as a mantissa with sign and an exponent with sign.
    // extern PRAT
    // StringToRat(bool mantissaIsNegative, std::wstring_view mantissa, bool exponentIsNegative, std::wstring_view exponent, uint32_t radix, int32_t precision);
    //
    // extern PNUMBER i32factnum(int32_t ini32, uint32_t radix);
    // extern PNUMBER i32prodnum(int32_t start, int32_t stop, uint32_t radix);
    // extern PNUMBER i32tonum(int32_t ini32, uint32_t radix);
    // extern PNUMBER Ui32tonum(uint32_t ini32, uint32_t radix);
    // extern PNUMBER numtonRadixx(_In_ PNUMBER a, uint32_t radix);
    //
    // // creates a empty/undefined rational representation (p/q)
    // extern PRAT _createrat(void);
    //
    // // returns a new rat structure with the acos of x.p/x.q taking into account
    // // angle type
    // extern void acosanglerat(_Inout_ PRAT* px, AngleType angletype, uint32_t radix, int32_t precision);
    //
    // // returns a new rat structure with the acosh of x.p/x.q
    // extern void acoshrat(_Inout_ PRAT* px, uint32_t radix, int32_t precision);
    //
    // // returns a new rat structure with the acos of x.p/x.q
    // extern void acosrat(_Inout_ PRAT* px, uint32_t radix, int32_t precision);
    //
    // // returns a new rat structure with the asin of x.p/x.q taking into account
    // // angle type
    // extern void asinanglerat(_Inout_ PRAT* px, AngleType angletype, uint32_t radix, int32_t precision);
    //
    // extern void asinhrat(_Inout_ PRAT* px, uint32_t radix, int32_t precision);
    // // returns a new rat structure with the asinh of x.p/x.q
    //
    // // returns a new rat structure with the asin of x.p/x.q
    // extern void asinrat(_Inout_ PRAT* px, uint32_t radix, int32_t precision);
    //
    // // returns a new rat structure with the atan of x.p/x.q taking into account
    // // angle type
    // extern void atananglerat(_Inout_ PRAT* px, AngleType angletype, uint32_t radix, int32_t precision);
    //
    // // returns a new rat structure with the atanh of x.p/x.q
    // extern void atanhrat(_Inout_ PRAT* px, int32_t precision);
    //
    // // returns a new rat structure with the atan of x.p/x.q
    // extern void atanrat(_Inout_ PRAT* px, uint32_t radix, int32_t precision);
    //
    // // returns a new rat structure with the cosh of x.p/x.q
    // extern void coshrat(_Inout_ PRAT* px, uint32_t radix, int32_t precision);
    //
    // // returns a new rat structure with the cos of x.p/x.q
    // extern void cosrat(_Inout_ PRAT* px, uint32_t radix, int32_t precision);
    //
    // // returns a new rat structure with the cos of x.p/x.q taking into account
    // // angle type
    // extern void cosanglerat(_Inout_ PRAT* px, AngleType angletype, uint32_t radix, int32_t precision);
    //
    // // returns a new rat structure with the exp of x.p/x.q this should not be called explicitly.
    // extern void _exprat(_Inout_ PRAT* px, int32_t precision);
    //
    // // returns a new rat structure with the exp of x.p/x.q
    // extern void exprat(_Inout_ PRAT* px, uint32_t radix, int32_t precision);
    //
    // // returns a new rat structure with the log base 10 of x.p/x.q
    // extern void log10rat(_Inout_ PRAT* px, int32_t precision);
    //
    // // returns a new rat structure with the natural log of x.p/x.q
    // extern void lograt(_Inout_ PRAT* px, int32_t precision);
    //
    // extern PRAT i32torat(int32_t ini32);
    // extern PRAT Ui32torat(uint32_t inui32);
    // extern PRAT numtorat(_In_ PNUMBER pin, uint32_t radix);
    //
    // extern void sinhrat(_Inout_ PRAT* px, uint32_t radix, int32_t precision);
    // extern void sinrat(_Inout_ PRAT* px);
    //
    // // returns a new rat structure with the sin of x.p/x.q taking into account
    // // angle type
    // extern void sinanglerat(_Inout_ PRAT* px, AngleType angletype, uint32_t radix, int32_t precision);
    //
    // extern void tanhrat(_Inout_ PRAT* px, uint32_t radix, int32_t precision);
    // extern void tanrat(_Inout_ PRAT* px, uint32_t radix, int32_t precision);
    //
    // // returns a new rat structure with the tan of x.p/x.q taking into account
    // // angle type
    // extern void tananglerat(_Inout_ PRAT* px, AngleType angletype, uint32_t radix, int32_t precision);
    //
    // extern void _dupnum(_In_ PNUMBER dest, _In_ const NUMBER* const src);
    //
    // extern void _destroynum(_Frees_ptr_opt_ PNUMBER pnum);
    // extern void _destroyrat(_Frees_ptr_opt_ PRAT prat);
    // extern void addnum(_Inout_ PNUMBER* pa, _In_ PNUMBER b, uint32_t radix);
    // extern void addrat(_Inout_ PRAT* pa, _In_ PRAT b, int32_t precision);
    // extern void andrat(_Inout_ PRAT* pa, _In_ PRAT b, uint32_t radix, int32_t precision);
    // extern void divnum(_Inout_ PNUMBER* pa, _In_ PNUMBER b, uint32_t radix, int32_t precision);
    // extern void divnumx(_Inout_ PNUMBER* pa, _In_ PNUMBER b, int32_t precision);
    // extern void divrat(_Inout_ PRAT* pa, _In_ PRAT b, int32_t precision);
    // extern void fracrat(_Inout_ PRAT* pa, uint32_t radix, int32_t precision);
    // extern void factrat(_Inout_ PRAT* pa, uint32_t radix, int32_t precision);
    // extern void remrat(_Inout_ PRAT* pa, _In_ PRAT b);
    // extern void modrat(_Inout_ PRAT* pa, _In_ PRAT b);
    // extern void gcdrat(_Inout_ PRAT* pa, int32_t precision);
    // extern void intrat(_Inout_ PRAT* px, uint32_t radix, int32_t precision);
    // extern void mulnum(_Inout_ PNUMBER* pa, _In_ PNUMBER b, uint32_t radix);
    // extern void mulnumx(_Inout_ PNUMBER* pa, _In_ PNUMBER b);
    // extern void mulrat(_Inout_ PRAT* pa, _In_ PRAT b, int32_t precision);
    // extern void numpowi32(_Inout_ PNUMBER* proot, int32_t power, uint32_t radix, int32_t precision);
    // extern void numpowi32x(_Inout_ PNUMBER* proot, int32_t power);
    // extern void orrat(_Inout_ PRAT* pa, _In_ PRAT b, uint32_t radix, int32_t precision);
    // extern void powrat(_Inout_ PRAT* pa, _In_ PRAT b, uint32_t radix, int32_t precision);
    // extern void powratNumeratorDenominator(_Inout_ PRAT* pa, _In_ PRAT b, uint32_t radix, int32_t precision);
    // extern void powratcomp(_Inout_ PRAT* pa, _In_ PRAT b, uint32_t radix, int32_t precision);
    // extern void ratpowi32(_Inout_ PRAT* proot, int32_t power, int32_t precision);
    // extern void remnum(_Inout_ PNUMBER* pa, _In_ PNUMBER b, uint32_t radix);
    // extern void rootrat(_Inout_ PRAT* pa, _In_ PRAT b, uint32_t radix, int32_t precision);
    // extern void scale2pi(_Inout_ PRAT* px, uint32_t radix, int32_t precision);
    // extern void scale(_Inout_ PRAT* px, _In_ PRAT scalefact, uint32_t radix, int32_t precision);
    // extern void subrat(_Inout_ PRAT* pa, _In_ PRAT b, int32_t precision);
    // extern void xorrat(_Inout_ PRAT* pa, _In_ PRAT b, uint32_t radix, int32_t precision);
    // extern void lshrat(_Inout_ PRAT* pa, _In_ PRAT b, uint32_t radix, int32_t precision);
    // extern void rshrat(_Inout_ PRAT* pa, _In_ PRAT b, uint32_t radix, int32_t precision);
    // extern bool rat_equ(_In_ PRAT a, _In_ PRAT b, int32_t precision);
    // extern bool rat_neq(_In_ PRAT a, _In_ PRAT b, int32_t precision);
    // extern bool rat_gt(_In_ PRAT a, _In_ PRAT b, int32_t precision);
    // extern bool rat_ge(_In_ PRAT a, _In_ PRAT b, int32_t precision);
    // extern bool rat_lt(_In_ PRAT a, _In_ PRAT b, int32_t precision);
    // extern bool rat_le(_In_ PRAT a, _In_ PRAT b, int32_t precision);
    // extern void inbetween(_In_ PRAT* px, _In_ PRAT range, int32_t precision);
    // extern void trimit(_Inout_ PRAT* px, int32_t precision);
    // extern void _dumprawrat(_In_ const wchar_t* varname, _In_ PRAT rat, std::wostream& out);
    // extern void _dumprawnum(_In_ const wchar_t* varname, _In_ PNUMBER num, std::wostream& out);
}
