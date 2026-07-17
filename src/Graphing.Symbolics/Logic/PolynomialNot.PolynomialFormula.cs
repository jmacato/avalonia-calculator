using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record PolynomialNot(PolynomialFormula Operand) : PolynomialFormula
{
    public override string Canonical => $"not[{Operand.Canonical}]";
}
