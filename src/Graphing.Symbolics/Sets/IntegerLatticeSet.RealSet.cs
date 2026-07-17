using System.Collections.Immutable;
using System.Text;

namespace Graphing.Symbolics;

internal sealed record IntegerLatticeSet(string Expression, ImmutableArray<string> Parameters, ImmutableArray<string> Predicates) : RealSet
{
    public override string Canonical => $"integer-lattice[{Expression};{string.Join(',', Parameters)};{string.Join(',', Predicates)}]";
}
