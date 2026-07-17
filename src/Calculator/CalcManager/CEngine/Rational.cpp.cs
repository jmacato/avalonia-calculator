// Copyright (c) Microsoft Corporation. All rights reserved.

using uint32_t = System.UInt32;
using uint64_t = System.UInt64;
using int32_t = System.Int32;
using WString = string;
using PRAT = CalcEngine.RatPakRAT;

namespace CalcEngine;

internal sealed partial class Rational
{
    public Number P { get; }
    public Number Q { get; }

    private readonly RatPak _ratPak;

    public Rational(RatPak ratPak)
        : this(ratPak, 0)
    {
    }

    public Rational(RatPak ratPak, Number n)
    {
        _ratPak = ratPak;

        int32_t qExp = 0;
        if (n.Exp < 0)
        {
            qExp -= n.Exp;
        }

        P = new Number(_ratPak, n.Sign, 0, n.CDigits, n.Mantissa);
        Q = new Number(_ratPak, 1, qExp, n.CDigits, [1]);
    }

    public Rational(RatPak ratPak, Number p, Number q)

    {
        _ratPak = ratPak;
        P = p;
        Q = q;
    }

    public Rational(RatPak ratPak, int32_t i)
    {
        _ratPak = ratPak;

        PRAT pr = RatPak.i32torat(i);

        P = new Number(ratPak, pr.pp);
        Q = new Number(ratPak, pr.pq);

    }


    public Rational(RatPak ratPak, uint32_t i)
    {
        _ratPak = ratPak;

        PRAT pr = RatPak.Ui32torat(i);

        P = new Number(ratPak, pr.pp);
        Q = new Number(ratPak, pr.pq);

    }

    public Rational(RatPak ratPak, uint64_t ui)
    {
        _ratPak = ratPak;
        uint32_t hi = (uint32_t)(((ui) >> 32) & 0xffffffff);
        uint32_t lo = (uint32_t)ui;

        Rational temp = (new Rational(_ratPak, hi) <<
                         new Rational(_ratPak, 32)) |
                        new Rational(_ratPak, lo);

        P = new Number(_ratPak, temp.P.Sign, 0, temp.P.CDigits, temp.P.Mantissa);
        Q = new Number(_ratPak, temp.Q.Sign, 0, temp.Q.CDigits, temp.Q.Mantissa);
    }

    public Rational(RatPak ratPak, PRAT prat)
    {
        _ratPak = ratPak;
        P = new Number(_ratPak, prat.pp.sign, 0, prat.pp.cdigit, prat.pp.mant);
        Q = new Number(_ratPak, prat.pq.sign, 0, prat.pp.cdigit, prat.pq.mant);
    }

    public PRAT ToPRAT()
    {
        PRAT ret = RatPak.createrat();

        ret.pp = P.ToPNUMBER();
        ret.pq = Q.ToPNUMBER();

        return ret;
    }

    public static Rational operator +(Rational lhs, Rational rhs)
    {
        PRAT lhsRat = lhs.ToPRAT();
        PRAT rhsRat = rhs.ToPRAT();
        RatPak pak = lhs._ratPak;

        pak.addrat(ref lhsRat, rhsRat, RATIONAL_PRECISION);

        var ret = new Rational(pak, lhsRat);
        return ret;
    }


    public static Rational operator -(Rational lhs)
    {
        PRAT lhsRat = lhs.ToPRAT();
        RatPak pak = lhs._ratPak;

        pak.mulrat(ref lhsRat, pak.rat_neg_one, RATIONAL_PRECISION);

        var ret = new Rational(pak, lhsRat);
        return ret;
    }


    public static Rational operator -(Rational lhs, Rational rhs)
    {
        PRAT lhsRat = lhs.ToPRAT();
        PRAT rhsRat = rhs.ToPRAT();
        RatPak pak = lhs._ratPak;

        pak.subrat(ref lhsRat, rhsRat, RATIONAL_PRECISION);

        var ret = new Rational(pak, lhsRat);
        return ret;
    }


    public static Rational operator *(Rational lhs, Rational rhs)
    {
        PRAT lhsRat = lhs.ToPRAT();
        PRAT rhsRat = rhs.ToPRAT();
        RatPak pak = lhs._ratPak;

        pak.mulrat(ref lhsRat, rhsRat, RATIONAL_PRECISION);

        var ret = new Rational(pak, lhsRat);
        return ret;
    }


    public static Rational operator /(Rational lhs, Rational rhs)
    {
        PRAT lhsRat = lhs.ToPRAT();
        PRAT rhsRat = rhs.ToPRAT();
        RatPak pak = lhs._ratPak;

        pak.divrat(ref lhsRat, rhsRat, RATIONAL_PRECISION);

        var ret = new Rational(pak, lhsRat);
        return ret;
    }

    /// <summary>
    /// Calculate the remainder after division, the sign of a result will match the sign of the current object.
    /// </summary>
    /// <remarks>
    /// This function has the same behavior as the standard C/C++ operator '%'
    /// to calculate the modulus after division instead, use <see cref="RationalMath::Mod"/> instead.
    /// </remarks>
    public static Rational operator %(Rational lhs, Rational rhs)
    {
        PRAT lhsRat = lhs.ToPRAT();
        PRAT rhsRat = rhs.ToPRAT();
        RatPak pak = lhs._ratPak;

        RatPak.remrat(ref lhsRat, rhsRat);

        var ret = new Rational(pak, lhsRat);
        return ret;
    }


    public static Rational operator <<(Rational lhs, Rational rhs)
    {
        PRAT lhsRat = lhs.ToPRAT();
        PRAT rhsRat = rhs.ToPRAT();
        RatPak pak = lhs._ratPak;

        pak.lshrat(ref lhsRat, rhsRat, RATIONAL_BASE, RATIONAL_PRECISION);

        var ret = new Rational(pak, lhsRat);
        return ret;
    }

    public static Rational operator >>(Rational lhs, Rational rhs)
    {
        PRAT lhsRat = lhs.ToPRAT();
        PRAT rhsRat = rhs.ToPRAT();
        RatPak pak = lhs._ratPak;

        pak.rshrat(ref lhsRat, rhsRat, RATIONAL_BASE, RATIONAL_PRECISION);

        var ret = new Rational(pak, lhsRat);
        return ret;
    }


    public static Rational operator &(Rational lhs, Rational rhs)
    {
        PRAT lhsRat = lhs.ToPRAT();
        PRAT rhsRat = rhs.ToPRAT();
        RatPak pak = lhs._ratPak;

        pak.andrat(ref lhsRat, rhsRat, RATIONAL_BASE, RATIONAL_PRECISION);

        var ret = new Rational(pak, lhsRat);
        return ret;
    }

    public static Rational operator |(Rational lhs, Rational rhs)
    {
        PRAT lhsRat = lhs.ToPRAT();
        PRAT rhsRat = rhs.ToPRAT();
        RatPak pak = lhs._ratPak;

        pak.orrat(ref lhsRat, rhsRat, RATIONAL_BASE, RATIONAL_PRECISION);

        var ret = new Rational(pak, lhsRat);
        return ret;
    }


    public static Rational operator ^(Rational lhs, Rational rhs)
    {
        PRAT lhsRat = lhs.ToPRAT();
        PRAT rhsRat = rhs.ToPRAT();
        RatPak pak = lhs._ratPak;

        pak.xorrat(ref lhsRat, rhsRat, RATIONAL_BASE, RATIONAL_PRECISION);

        var ret = new Rational(pak, lhsRat);

        return ret;
    }

    public static bool operator ==(Rational? lhs, Rational? rhs)
    {
        if (ReferenceEquals(lhs, rhs))
        {
            return true;
        }

        if (lhs is null || rhs is null)
        {
            return false;
        }

        PRAT lhsRat = lhs.ToPRAT();
        PRAT rhsRat = rhs.ToPRAT();
        RatPak pak = lhs._ratPak;

        var ret = pak.rat_equ(lhsRat, rhsRat, RATIONAL_PRECISION);
        return ret;
    }

    public static bool operator <(Rational lhs, Rational rhs)
    {
        PRAT lhsRat = lhs.ToPRAT();
        PRAT rhsRat = rhs.ToPRAT();
        RatPak pak = lhs._ratPak;

        var ret = pak.rat_lt(lhsRat, rhsRat, RATIONAL_PRECISION);

        return ret;
    }

    public static bool operator !=(Rational? lhs, Rational? rhs)
    {
        return !(lhs == rhs);
    }

    public override bool Equals(object? obj)
    {
        return obj is Rational other && this == other;
    }

    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(ToString(10, RatPakNumberFormat.Scientific, RATIONAL_PRECISION));
    }

    public static bool operator >(Rational lhs, Rational rhs)
    {
        return rhs < lhs;
    }

    public static bool operator <=(Rational lhs, Rational rhs)
    {
        return !(lhs > rhs);
    }

    public static bool operator >=(Rational lhs, Rational rhs)
    {
        return !(lhs < rhs);
    }

    public WString ToString(uint32_t radix, RatPakNumberFormat fmt, int32_t precision)
    {
        var rat = ToPRAT();

        var result = _ratPak.RatToString(ref rat, fmt, radix, precision);

        return result;
    }

    public uint64_t ToUInt64_t()
    {
        var rat = ToPRAT();

        var result = _ratPak.rattoUi64(rat, RATIONAL_BASE, RATIONAL_PRECISION);

        return result;
    }
}
