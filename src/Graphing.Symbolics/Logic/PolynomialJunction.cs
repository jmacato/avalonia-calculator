using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record PolynomialJunction(bool IsConjunction, ImmutableArray<PolynomialFormula> Operands) : PolynomialFormula
{
    public override string Canonical => $"{(IsConjunction ? "and" : "or")}[{string.Join(',', Operands.Select(static operand => operand.Canonical))}]";
}
