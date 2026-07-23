namespace Graphing.Symbolics;

internal readonly record struct RationalInterval(BigRational Lower, BigRational Upper)
{
    public BigRational Midpoint => (Lower + Upper) / 2;
    public BigRational Width => Upper - Lower;
}
