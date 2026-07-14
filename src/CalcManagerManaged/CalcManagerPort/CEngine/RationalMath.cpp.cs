// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// ReSharper disable once CheckNamespace
namespace CalcEngine;

public static class RationalMath
{
    // Default Base/Radix to use for Rational calculations
    // RatPack calculations currently support up to Base64.
    const uint32_t RATIONAL_BASE = 10;

    // Default Precision to use for Rational calculations
    const int32_t RATIONAL_PRECISION = 128;

    public static Rational Frac(RatPak ratPak, Rational rat)
    {
        if (ratPak is null)
        {
            throw new ArgumentNullException(nameof(ratPak));
        }

        if (rat is null)
        {
            throw new ArgumentNullException(nameof(rat));
        }

        var prat = rat.ToPRAT();

        try
        {
            ratPak.fracrat(ref prat, RATIONAL_BASE, RATIONAL_PRECISION);
        }
        catch
        {
            RatPak.destroyrat(ref prat);
            throw;
        }

        Rational result = new(ratPak, prat);
        RatPak.destroyrat(ref prat);

        return result;
    }

    public static Rational Integral(RatPak ratPak, Rational rat)
    {
        if (ratPak is null)
        {
            throw new ArgumentNullException(nameof(ratPak));
        }

        if (rat is null)
        {
            throw new ArgumentNullException(nameof(rat));
        }

        var prat = rat.ToPRAT();
        try
        {
            ratPak.intrat(ref prat, RATIONAL_BASE, RATIONAL_PRECISION);
        }
        catch
        {
            RatPak.destroyrat(ref prat);
            throw;
        }

        Rational result = new(ratPak, prat);
        RatPak.destroyrat(ref prat);

        return result;
    }

    public static Rational Pow(RatPak ratPak, Rational @base, Rational pow)
    {
        if (ratPak is null)
        {
            throw new ArgumentNullException(nameof(ratPak));
        }

        if (@base is null)
        {
            throw new ArgumentNullException(nameof(@base));
        }

        if (pow is null)
        {
            throw new ArgumentNullException(nameof(pow));
        }

        var baseRat = @base.ToPRAT();
        var powRat = pow.ToPRAT();

        try
        {
            ratPak.powrat(ref baseRat, powRat, RATIONAL_BASE, RATIONAL_PRECISION);
            RatPak.destroyrat(ref powRat);
        }
        catch
        {
            RatPak.destroyrat(ref baseRat);
            RatPak.destroyrat(ref powRat);
            throw;
        }

        Rational result = new(ratPak, baseRat);
        RatPak.destroyrat(ref baseRat);

        return result;
    }

    public static Rational Root(RatPak ratPak, Rational @base, Rational root)
    {
        return Pow(ratPak, @base, Invert(ratPak, root));
    }

    public static Rational Fact(RatPak ratPak, Rational rat)
    {
        if (ratPak is null)
        {
            throw new ArgumentNullException(nameof(ratPak));
        }

        if (rat is null)
        {
            throw new ArgumentNullException(nameof(rat));
        }

        var prat = rat.ToPRAT();

        try
        {
            ratPak.factrat(ref prat, RATIONAL_BASE, RATIONAL_PRECISION);
        }
        catch
        {
            RatPak.destroyrat(ref prat);
            throw;
        }

        Rational result = new(ratPak, prat);
        RatPak.destroyrat(ref prat);

        return result;
    }

    public static Rational Exp(RatPak ratPak, Rational rat)
    {
        if (ratPak is null)
        {
            throw new ArgumentNullException(nameof(ratPak));
        }

        if (rat is null)
        {
            throw new ArgumentNullException(nameof(rat));
        }

        var prat = rat.ToPRAT();

        try
        {
            ratPak.exprat(ref prat, RATIONAL_BASE, RATIONAL_PRECISION);
        }
        catch
        {
            RatPak.destroyrat(ref prat);
            throw;
        }

        Rational result = new(ratPak, prat);
        RatPak.destroyrat(ref prat);

        return result;
    }

    public static Rational Log(RatPak ratPak, Rational rat)
    {
        if (ratPak is null)
        {
            throw new ArgumentNullException(nameof(ratPak));
        }

        if (rat is null)
        {
            throw new ArgumentNullException(nameof(rat));
        }

        var prat = rat.ToPRAT();

        try
        {
            ratPak.lograt(ref prat, RATIONAL_PRECISION);
        }
        catch
        {
            RatPak.destroyrat(ref prat);
            throw;
        }

        Rational result = new(ratPak, prat);
        RatPak.destroyrat(ref prat);

        return result;
    }

    public static Rational Log10(RatPak ratPak, Rational rat)
    {
        if (ratPak is null)
        {
            throw new ArgumentNullException(nameof(ratPak));
        }

        return RationalMath.Log(ratPak, rat) / new Rational(ratPak, ratPak.ln_ten);
    }

    public static Rational Invert(RatPak ratPak, Rational rat)
    {
        return new Rational(ratPak, 1) / rat;
    }

    public static Rational Abs(RatPak ratPak, Rational rat)
    {
        if (rat is null)
        {
            throw new ArgumentNullException(nameof(rat));
        }

        return new Rational(ratPak
            , new EngineNumber(1, rat.P.Exp, rat.P.CDigits, rat.P.Mantissa)
            , new EngineNumber(1, rat.Q.Exp, rat.Q.CDigits, rat.Q.Mantissa)
        );

        //Number{ 1, rat.P().Exp(), rat.P().Mantissa() }, Number{ 1, rat.Q().Exp(), rat.Q().Mantissa() } };
    }

    public static Rational Sin(RatPak ratPak, Rational rat, AngleType angletype)
    {
        if (ratPak is null)
        {
            throw new ArgumentNullException(nameof(ratPak));
        }

        if (rat is null)
        {
            throw new ArgumentNullException(nameof(rat));
        }

        var prat = rat.ToPRAT();

        try
        {
            ratPak.sinanglerat(ref prat, angletype, RATIONAL_BASE, RATIONAL_PRECISION);
        }
        catch
        {
            RatPak.destroyrat(ref prat);
            throw;
        }

        Rational result = new(ratPak, prat);
        RatPak.destroyrat(ref prat);

        return result;
    }

    public static Rational Cos(RatPak ratPak, Rational rat, AngleType angletype)
    {
        if (ratPak is null)
        {
            throw new ArgumentNullException(nameof(ratPak));
        }

        if (rat is null)
        {
            throw new ArgumentNullException(nameof(rat));
        }

        var prat = rat.ToPRAT();

        try
        {
            ratPak.cosanglerat(ref prat, angletype, RATIONAL_BASE, RATIONAL_PRECISION);
        }
        catch
        {
            RatPak.destroyrat(ref prat);
            throw;
        }

        Rational result = new(ratPak, prat);
        RatPak.destroyrat(ref prat);

        return result;
    }

    public static Rational Tan(RatPak ratPak, Rational rat, AngleType angletype)
    {
        if (ratPak is null)
        {
            throw new ArgumentNullException(nameof(ratPak));
        }

        if (rat is null)
        {
            throw new ArgumentNullException(nameof(rat));
        }

        var prat = rat.ToPRAT();

        try
        {
            ratPak.tananglerat(ref prat, angletype, RATIONAL_BASE, RATIONAL_PRECISION);
        }
        catch
        {
            RatPak.destroyrat(ref prat);
            throw;
        }

        Rational result = new(ratPak, prat);
        RatPak.destroyrat(ref prat);

        return result;
    }

    public static Rational ASin(RatPak ratPak, Rational rat, AngleType angletype)
    {
        if (ratPak is null)
        {
            throw new ArgumentNullException(nameof(ratPak));
        }

        if (rat is null)
        {
            throw new ArgumentNullException(nameof(rat));
        }

        var prat = rat.ToPRAT();

        try
        {
            ratPak.asinanglerat(ref prat, angletype, RATIONAL_BASE, RATIONAL_PRECISION);
        }
        catch
        {
            RatPak.destroyrat(ref prat);
            throw;
        }

        Rational result = new(ratPak, prat);
        RatPak.destroyrat(ref prat);

        return result;
    }

    public static Rational ACos(RatPak ratPak, Rational rat, AngleType angletype)
    {
        if (ratPak is null)
        {
            throw new ArgumentNullException(nameof(ratPak));
        }

        if (rat is null)
        {
            throw new ArgumentNullException(nameof(rat));
        }

        var prat = rat.ToPRAT();

        try
        {
            ratPak.acosanglerat(ref prat, angletype, RATIONAL_BASE, RATIONAL_PRECISION);
        }
        catch
        {
            RatPak.destroyrat(ref prat);
            throw;
        }

        Rational result = new(ratPak, prat);
        RatPak.destroyrat(ref prat);

        return result;
    }

    public static Rational ATan(RatPak ratPak, Rational rat, AngleType angletype)
    {
        if (ratPak is null)
        {
            throw new ArgumentNullException(nameof(ratPak));
        }

        if (rat is null)
        {
            throw new ArgumentNullException(nameof(rat));
        }

        var prat = rat.ToPRAT();

        try
        {
            ratPak.atananglerat(ref prat, angletype, RATIONAL_BASE, RATIONAL_PRECISION);
        }
        catch
        {
            RatPak.destroyrat(ref prat);
            throw;
        }

        Rational result = new(ratPak, prat);
        RatPak.destroyrat(ref prat);

        return result;
    }

    public static Rational Sinh(RatPak ratPak, Rational rat)
    {
        if (ratPak is null)
        {
            throw new ArgumentNullException(nameof(ratPak));
        }

        if (rat is null)
        {
            throw new ArgumentNullException(nameof(rat));
        }

        var prat = rat.ToPRAT();

        try
        {
            ratPak.sinhrat(ref prat, RATIONAL_BASE, RATIONAL_PRECISION);
        }
        catch
        {
            RatPak.destroyrat(ref prat);
            throw;
        }

        Rational result = new(ratPak, prat);
        RatPak.destroyrat(ref prat);

        return result;
    }

    public static Rational Cosh(RatPak ratPak, Rational rat)
    {
        if (ratPak is null)
        {
            throw new ArgumentNullException(nameof(ratPak));
        }

        if (rat is null)
        {
            throw new ArgumentNullException(nameof(rat));
        }

        var prat = rat.ToPRAT();

        try
        {
            ratPak.coshrat(ref prat, RATIONAL_BASE, RATIONAL_PRECISION);
        }
        catch
        {
            RatPak.destroyrat(ref prat);
            throw;
        }

        Rational result = new(ratPak, prat);
        RatPak.destroyrat(ref prat);

        return result;
    }

    public static Rational Tanh(RatPak ratPak, Rational rat)
    {
        if (ratPak is null)
        {
            throw new ArgumentNullException(nameof(ratPak));
        }

        if (rat is null)
        {
            throw new ArgumentNullException(nameof(rat));
        }

        var prat = rat.ToPRAT();

        try
        {
            ratPak.tanhrat(ref prat, RATIONAL_BASE, RATIONAL_PRECISION);
        }
        catch
        {
            RatPak.destroyrat(ref prat);
            throw;
        }

        Rational result = new(ratPak, prat);
        RatPak.destroyrat(ref prat);

        return result;
    }

    public static Rational ASinh(RatPak ratPak, Rational rat)
    {
        if (ratPak is null)
        {
            throw new ArgumentNullException(nameof(ratPak));
        }

        if (rat is null)
        {
            throw new ArgumentNullException(nameof(rat));
        }

        var prat = rat.ToPRAT();

        try
        {
            ratPak.asinhrat(ref prat, RATIONAL_BASE, RATIONAL_PRECISION);
        }
        catch
        {
            RatPak.destroyrat(ref prat);
            throw;
        }

        Rational result = new(ratPak, prat);
        RatPak.destroyrat(ref prat);

        return result;
    }

    public static Rational ACosh(RatPak ratPak, Rational rat)
    {
        if (ratPak is null)
        {
            throw new ArgumentNullException(nameof(ratPak));
        }

        if (rat is null)
        {
            throw new ArgumentNullException(nameof(rat));
        }

        var prat = rat.ToPRAT();

        try
        {
            ratPak.acoshrat(ref prat, RATIONAL_BASE, RATIONAL_PRECISION);
        }
        catch
        {
            RatPak.destroyrat(ref prat);
            throw;
        }

        Rational result = new(ratPak, prat);
        RatPak.destroyrat(ref prat);

        return result;
    }

    public static Rational ATanh(RatPak ratPak, Rational rat)
    {
        if (ratPak is null)
        {
            throw new ArgumentNullException(nameof(ratPak));
        }

        if (rat is null)
        {
            throw new ArgumentNullException(nameof(rat));
        }

        var prat = rat.ToPRAT();

        try
        {
            ratPak.atanhrat(ref prat, RATIONAL_PRECISION);
        }
        catch
        {
            RatPak.destroyrat(ref prat);
            throw;
        }

        Rational result = new(ratPak, prat);
        RatPak.destroyrat(ref prat);

        return result;
    }

    /// <summary>
    /// Calculate the modulus after division, the sign of the result will match the sign of b.
    /// </summary>
    /// <remarks>
    /// When one of the operand is negative
    /// the result will differ from the C/C++ operator '%'
    /// use <see cref="Rational.operator %(Rational, Rational)"/> instead to calculate the remainder after division.
    /// </remarks>
    public static Rational Mod(RatPak ratPak, Rational a, Rational b)
    {
        if (ratPak is null)
        {
            throw new ArgumentNullException(nameof(ratPak));
        }

        if (a is null)
        {
            throw new ArgumentNullException(nameof(a));
        }

        if (b is null)
        {
            throw new ArgumentNullException(nameof(b));
        }

        var prat = a.ToPRAT();
        var pn = b.ToPRAT();

        try
        {
            ratPak.modrat(ref prat, pn);
            RatPak.destroyrat(ref pn);
        }
        catch
        {
            RatPak.destroyrat(ref prat);
            RatPak.destroyrat(ref pn);
            throw;
        }

        var res = new Rational(ratPak, prat);
        RatPak.destroyrat(ref prat);
        return res;
    }
}
