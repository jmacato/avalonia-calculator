namespace Graphing.Symbolics;

internal sealed record DifferenceSet(RealSet Source, RealSet Removed) : RealSet
{
    public override string Canonical => $"difference[{Source.Canonical},{Removed.Canonical}]";
}
