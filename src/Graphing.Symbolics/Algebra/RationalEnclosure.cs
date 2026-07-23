namespace Graphing.Symbolics;

internal readonly record struct RationalEnclosure(BigRational Lower, BigRational Upper)
{
    public bool IsExact => Lower == Upper;
}
