// Copyright (c) Microsoft Corporation. All rights reserved.

namespace CalcEngine;

public class EngineNumber
{
    private readonly uint32_t[] _mantissa;

    public EngineNumber()
        : this(1, 0, 1, [0])
    {
    }

    public EngineNumber(PNUMBER p)
        : this(
            p is null ? throw new ArgumentNullException(nameof(p)) : p._sign,
            p._exp,
            p._cdigit,
            p._mant)
    {
    }

    public EngineNumber(int32_t sign, int32_t exp, int32_t cDigits, IReadOnlyList<uint32_t> mantissa)
    {
        if (mantissa is null)
        {
            throw new ArgumentNullException(nameof(mantissa));
        }

        CDigits = cDigits;

        Sign = sign;
        Exp = exp;

        var tmp = mantissa.ToArray();

        if (tmp.Length <= CDigits)
        {
            // Let's buffer here as it's important for the algorithms to have some leeway for the mantissa digits.
            Array.Resize(ref tmp, Math.Min((Math.Max(tmp.Length, cDigits) + 8), 256));
        }

        _mantissa = tmp;
    }

    public PNUMBER ToPNUMBER()
    {
        PNUMBER? ret = null;

        RatPak.createnum(ref ret, (uint32_t)(_mantissa.Length + 1));
        ret._sign = Sign;
        ret._exp = Exp;
        ret._cdigit = CDigits;
        ret._mant = _mantissa.ToArray();

        return ret;
    }

    public int32_t Sign { get; }

    public int32_t Exp { get; }

    public int32_t CDigits { get; }

    public IReadOnlyList<uint32_t> Mantissa => _mantissa;

    public bool IsZero() =>
        _mantissa.All(x =>
            x == 0);
}
