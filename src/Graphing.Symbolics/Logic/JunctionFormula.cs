using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record JunctionFormula(bool IsConjunction, ImmutableArray<Formula> Operands) : Formula
{
    public override string Canonical => $"{(IsConjunction ? "and" : "or")}({string.Join(',', Operands.Select(static value => value.Canonical))})";
}
