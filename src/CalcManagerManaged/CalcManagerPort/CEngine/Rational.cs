// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalcEngine;

public class Rational
{
    // Default Base/Radix to use for Rational calculations
    // RatPack calculations currently support up to Base64.
    const UInt32 RATIONAL_BASE = 10;

    // Default Precision to use for Rational calculations
    const Int32 RATIONAL_PRECISION = 128;

    public EngineNumber P { get; }

    public EngineNumber Q { get; }

    private readonly RatPak _ratPak;

    public Rational(RatPak ratPak)
    {
        _ratPak = ratPak;
        P = new EngineNumber();
        Q = new EngineNumber(1, 0, 1, [1]);
    }

    public Rational(RatPak ratPak, EngineNumber n)
    {
        if (n is null)
        {
            throw new ArgumentNullException(nameof(n));
        }

        _ratPak = ratPak;

        int32_t qExp = 0;
        if (n.Exp < 0)
        {
            qExp -= n.Exp;
        }

        P = new EngineNumber(n.Sign, 0, n.CDigits, n.Mantissa);
        Q = new EngineNumber(1, qExp, 1, [1]);
    }

    public Rational(RatPak ratPak, EngineNumber p, EngineNumber q)

    {
        _ratPak = ratPak;
        P = p;
        Q = q;
    }

    public Rational(RatPak ratPak, int32_t i)
    {
        _ratPak = ratPak;

        PRAT? pr = RatPak.i32torat(i);

        P = new EngineNumber(pr._pp);
        Q = new EngineNumber(pr._pq);

        RatPak.destroyrat(ref pr);
    }

    public Rational(RatPak ratPak, uint32_t i)
    {
        _ratPak = ratPak;

        PRAT? pr = RatPak.Ui32torat(i);

        P = new EngineNumber(pr._pp);
        Q = new EngineNumber(pr._pq);

        RatPak.destroyrat(ref pr);
    }

    public Rational(RatPak ratPak, uint64_t ui)
    {
        _ratPak = ratPak;
        uint32_t hi = (uint32_t)((ui >> 32) & 0xffffffff);
        uint32_t lo = (uint32_t)ui;

        Rational temp = (new Rational(_ratPak, hi) <<
                         new Rational(_ratPak, 32)) |
                        new Rational(_ratPak, lo);

        P = new EngineNumber(temp.P.Sign, temp.P.Exp, temp.P.CDigits, temp.P.Mantissa);
        Q = new EngineNumber(temp.Q.Sign, temp.Q.Exp, temp.Q.CDigits, temp.Q.Mantissa);
    }

    public Rational(RatPak ratPak, PRAT prat)
    {
        if (prat is null)
        {
            throw new ArgumentNullException(nameof(prat));
        }

        _ratPak = ratPak;
        P = new EngineNumber(prat._pp);
        Q = new EngineNumber(prat._pq);
    }

    public PRAT ToPRAT()
    {
        PRAT ret = RatPak.createrat();

        ret._pp = P.ToPNUMBER();
        ret._pq = Q.ToPNUMBER();

        return ret;
    }

    public static Rational operator +(Rational lhs, Rational rhs)
    {
        if (lhs is null)
        {
            throw new ArgumentNullException(nameof(lhs));
        }

        if (rhs is null)
        {
            throw new ArgumentNullException(nameof(rhs));
        }

        PRAT? lhsRat = lhs.ToPRAT();
        PRAT? rhsRat = rhs.ToPRAT();
        RatPak pak = lhs._ratPak;

        pak.addrat(ref lhsRat, rhsRat, RATIONAL_PRECISION);
        RatPak.destroyrat(ref rhsRat);

        var ret = new Rational(pak, lhsRat);
        RatPak.destroyrat(ref lhsRat);
        return ret;
    }

    public static Rational operator -(Rational lhs)
    {
        if (lhs is null)
        {
            throw new ArgumentNullException(nameof(lhs));
        }

        PRAT? lhsRat = lhs.ToPRAT();
        RatPak pak = lhs._ratPak;

        pak.mulrat(ref lhsRat, pak.rat_neg_one, RATIONAL_PRECISION);

        var ret = new Rational(pak, lhsRat);
        RatPak.destroyrat(ref lhsRat);
        return ret;
    }

    public static Rational operator -(Rational lhs, Rational rhs)
    {
        if (lhs is null)
        {
            throw new ArgumentNullException(nameof(lhs));
        }

        if (rhs is null)
        {
            throw new ArgumentNullException(nameof(rhs));
        }

        PRAT? lhsRat = lhs.ToPRAT();
        PRAT? rhsRat = rhs.ToPRAT();
        RatPak pak = lhs._ratPak;

        pak.subrat(ref lhsRat, rhsRat, RATIONAL_PRECISION);
        RatPak.destroyrat(ref rhsRat);

        var ret = new Rational(pak, lhsRat);
        RatPak.destroyrat(ref lhsRat);
        return ret;
    }

    public static Rational operator *(Rational lhs, Rational rhs)
    {
        if (lhs is null)
        {
            throw new ArgumentNullException(nameof(lhs));
        }

        if (rhs is null)
        {
            throw new ArgumentNullException(nameof(rhs));
        }

        PRAT? lhsRat = lhs.ToPRAT();
        PRAT? rhsRat = rhs.ToPRAT();
        RatPak pak = lhs._ratPak;

        pak.mulrat(ref lhsRat, rhsRat, RATIONAL_PRECISION);
        RatPak.destroyrat(ref rhsRat);

        var ret = new Rational(pak, lhsRat);
        RatPak.destroyrat(ref lhsRat);
        return ret;
    }

    public static Rational operator /(Rational lhs, Rational rhs)
    {
        if (lhs is null)
        {
            throw new ArgumentNullException(nameof(lhs));
        }

        if (rhs is null)
        {
            throw new ArgumentNullException(nameof(rhs));
        }

        PRAT? lhsRat = lhs.ToPRAT();
        PRAT? rhsRat = rhs.ToPRAT();
        RatPak pak = lhs._ratPak;

        pak.divrat(ref lhsRat, rhsRat, RATIONAL_PRECISION);
        RatPak.destroyrat(ref rhsRat);

        var ret = new Rational(pak, lhsRat);
        RatPak.destroyrat(ref lhsRat);
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
        if (lhs is null)
        {
            throw new ArgumentNullException(nameof(lhs));
        }

        if (rhs is null)
        {
            throw new ArgumentNullException(nameof(rhs));
        }

        PRAT? lhsRat = lhs.ToPRAT();
        PRAT? rhsRat = rhs.ToPRAT();
        RatPak pak = lhs._ratPak;

        RatPak.remrat(ref lhsRat, rhsRat);
        RatPak.destroyrat(ref rhsRat);

        var ret = new Rational(pak, lhsRat);
        RatPak.destroyrat(ref lhsRat);
        return ret;
    }

    public static Rational operator <<(Rational lhs, Rational rhs)
    {
        if (lhs is null)
        {
            throw new ArgumentNullException(nameof(lhs));
        }

        if (rhs is null)
        {
            throw new ArgumentNullException(nameof(rhs));
        }

        PRAT? lhsRat = lhs.ToPRAT();
        PRAT? rhsRat = rhs.ToPRAT();
        RatPak pak = lhs._ratPak;

        pak.lshrat(ref lhsRat, rhsRat, RATIONAL_BASE, RATIONAL_PRECISION);
        RatPak.destroyrat(ref rhsRat);

        var ret = new Rational(pak, lhsRat);
        RatPak.destroyrat(ref lhsRat);
        return ret;
    }

    public static Rational operator >>(Rational lhs, Rational rhs)
    {
        if (lhs is null)
        {
            throw new ArgumentNullException(nameof(lhs));
        }

        if (rhs is null)
        {
            throw new ArgumentNullException(nameof(rhs));
        }

        PRAT? lhsRat = lhs.ToPRAT();
        PRAT? rhsRat = rhs.ToPRAT();
        RatPak pak = lhs._ratPak;

        pak.rshrat(ref lhsRat, rhsRat, RATIONAL_BASE, RATIONAL_PRECISION);
        RatPak.destroyrat(ref rhsRat);

        var ret = new Rational(pak, lhsRat);
        RatPak.destroyrat(ref lhsRat);
        return ret;
    }

    public static Rational operator &(Rational lhs, Rational rhs)
    {
        if (lhs is null)
        {
            throw new ArgumentNullException(nameof(lhs));
        }

        if (rhs is null)
        {
            throw new ArgumentNullException(nameof(rhs));
        }

        PRAT? lhsRat = lhs.ToPRAT();
        PRAT? rhsRat = rhs.ToPRAT();
        RatPak pak = lhs._ratPak;

        pak.andrat(ref lhsRat, rhsRat, RATIONAL_BASE, RATIONAL_PRECISION);
        RatPak.destroyrat(ref rhsRat);

        var ret = new Rational(pak, lhsRat);
        RatPak.destroyrat(ref lhsRat);
        return ret;
    }

    public static Rational operator |(Rational lhs, Rational rhs)
    {
        if (lhs is null)
        {
            throw new ArgumentNullException(nameof(lhs));
        }

        if (rhs is null)
        {
            throw new ArgumentNullException(nameof(rhs));
        }

        PRAT? lhsRat = lhs.ToPRAT();
        PRAT? rhsRat = rhs.ToPRAT();
        RatPak pak = lhs._ratPak;

        pak.orrat(ref lhsRat, rhsRat, RATIONAL_BASE, RATIONAL_PRECISION);
        RatPak.destroyrat(ref rhsRat);

        var ret = new Rational(pak, lhsRat);
        RatPak.destroyrat(ref lhsRat);
        return ret;
    }

    public static Rational operator ^(Rational lhs, Rational rhs)
    {
        if (lhs is null)
        {
            throw new ArgumentNullException(nameof(lhs));
        }

        if (rhs is null)
        {
            throw new ArgumentNullException(nameof(rhs));
        }

        PRAT? lhsRat = lhs.ToPRAT();
        PRAT? rhsRat = rhs.ToPRAT();
        RatPak pak = lhs._ratPak;

        pak.xorrat(ref lhsRat, rhsRat, RATIONAL_BASE, RATIONAL_PRECISION);
        RatPak.destroyrat(ref rhsRat);

        var ret = new Rational(pak, lhsRat);
        RatPak.destroyrat(ref lhsRat);

        return ret;
    }

    public static bool operator ==(Rational? lhs, Rational? rhs)
    {
        if (lhs is null || rhs is null)
        {
            return lhs is null && rhs is null;
        }

        PRAT? lhsRat = lhs.ToPRAT();
        PRAT? rhsRat = rhs.ToPRAT();
        RatPak pak = lhs._ratPak;

        var ret = pak.RatEqu(lhsRat, rhsRat, RATIONAL_PRECISION);
        RatPak.destroyrat(ref lhsRat);
        RatPak.destroyrat(ref rhsRat);
        return ret;
    }

    public static bool operator <(Rational lhs, Rational rhs)
    {
        if (lhs is null || rhs is null)
        {
            return false;
        }

        PRAT? lhsRat = lhs.ToPRAT();
        PRAT? rhsRat = rhs.ToPRAT();
        RatPak pak = lhs._ratPak;

        var ret = pak.RatLt(lhsRat, rhsRat, RATIONAL_PRECISION);
        RatPak.destroyrat(ref lhsRat);
        RatPak.destroyrat(ref rhsRat);

        return ret;
    }

    public static bool operator !=(Rational lhs, Rational rhs)
    {
        return !(lhs == rhs);
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

    public static Rational Add(Rational left, Rational right)
    {
        return left + right;
    }

    public static Rational Subtract(Rational left, Rational right)
    {
        return left - right;
    }

    public static Rational Multiply(Rational left, Rational right)
    {
        return left * right;
    }

    public static Rational Divide(Rational left, Rational right)
    {
        return left / right;
    }

    public static Rational Remainder(Rational left, Rational right)
    {
        return left % right;
    }

    public static Rational Negate(Rational item)
    {
        return -item;
    }

    public static Rational LeftShift(Rational left, Rational right)
    {
        return left << right;
    }

    public static Rational RightShift(Rational left, Rational right)
    {
        return left >> right;
    }

    public static Rational BitwiseAnd(Rational left, Rational right)
    {
        return left & right;
    }

    public static Rational BitwiseOr(Rational left, Rational right)
    {
        return left | right;
    }

    public static Rational Xor(Rational left, Rational right)
    {
        return left ^ right;
    }

    public int CompareTo(Rational other)
    {
        if (other is null)
        {
            return 1;
        }

        if (this == other)
        {
            return 0;
        }

        return this < other ? -1 : 1;
    }

    public override bool Equals(object obj)
    {
        return obj is Rational other && this == other;
    }

    public override int GetHashCode()
    {
        return 0;
    }

    public wstring ToString(uint32_t radix, NumberFormat fmt, int32_t precision)
    {
        PRAT? rat = ToPRAT();

        var result = _ratPak.RatToString(ref rat, fmt, radix, precision);
        RatPak.destroyrat(ref rat);

        return result;
    }

    public uint64_t ToUInt64T()
    {
        PRAT? rat = ToPRAT();

        var result = _ratPak.rattoUi64(rat, RATIONAL_BASE, RATIONAL_PRECISION);
        RatPak.destroyrat(ref rat);

        return result;
    }
}
