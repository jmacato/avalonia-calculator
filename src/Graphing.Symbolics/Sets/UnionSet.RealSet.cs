using System.Collections.Immutable;
using System.Text;

namespace Graphing.Symbolics;

internal sealed record UnionSet(ImmutableArray<RealSet> Operands) : RealSet
{
    public override string Canonical => $"union[{string.Join(',', Operands.Select(static operand => operand.Canonical))}]";
}
