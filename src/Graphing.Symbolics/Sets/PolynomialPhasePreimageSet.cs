namespace Graphing.Symbolics;

internal sealed record PolynomialPhasePreimageSet(PolynomialPhasePreimage Preimage) : RealSet
{
    public override string Canonical => Preimage.Canonical;
}
