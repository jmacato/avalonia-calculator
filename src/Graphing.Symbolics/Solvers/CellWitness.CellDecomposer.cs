using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record CellWitness(CellKind Kind, int Index, BigRational? Sample, ImmutableArray<int> AtomSigns, bool Included)
{
    public string Canonical => $"cell[{(int)Kind},{Index},{Sample?.ToString() ?? "point"},{string.Join(',', AtomSigns)},{(Included ? 1 : 0)}]";
}
