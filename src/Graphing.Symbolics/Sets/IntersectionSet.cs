using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record IntersectionSet(ImmutableArray<RealSet> Operands) : RealSet
{
    public override string Canonical => $"intersection[{string.Join(',', Operands.Select(static operand => operand.Canonical))}]";
}
