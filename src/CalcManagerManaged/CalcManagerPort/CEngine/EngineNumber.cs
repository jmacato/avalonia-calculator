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
            p?._sign ?? throw new ArgumentNullException(nameof(p)),
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

        if (cDigits < 1 || mantissa.Count < cDigits)
        {
            throw new ArgumentOutOfRangeException(nameof(cDigits));
        }

        CDigits = cDigits;

        Sign = sign;
        Exp = exp;

        // C++ Number(PNUMBER) stores only the logical digits. Native RatPak
        // work storage belongs to the temporary PNUMBER allocation instead.
        _mantissa = mantissa.Take(CDigits).ToArray();
    }

    public PNUMBER ToPNUMBER()
    {
        PNUMBER? ret = null;

        RatPak.createnum(ref ret, (uint32_t)(_mantissa.Length + 1));
        ret._sign = Sign;
        ret._exp = Exp;
        ret._cdigit = CDigits;
        Array.Copy(_mantissa, ret._mant, _mantissa.Length);

        return ret;
    }

    public int32_t Sign { get; }

    public int32_t Exp { get; }

    public int32_t CDigits { get; }

    public IReadOnlyList<uint32_t> Mantissa => _mantissa;

    public bool IsZero()
    {
        return _mantissa.All(x => x == 0);
    }
}
