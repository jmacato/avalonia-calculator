using CalcEngine;

namespace UnitConversionManager;

internal sealed class RationalConversionData
{
    internal RationalConversionData(Rational ratio, Rational offset, bool offsetFirst)
    {
        Ratio = ratio;
        Offset = offset;
        OffsetFirst = offsetFirst;
    }

    internal Rational Ratio { get; }

    internal Rational Offset { get; }

    internal bool OffsetFirst { get; }
}
